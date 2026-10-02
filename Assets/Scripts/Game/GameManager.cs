using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum GameState
{
    Lobby,
    Matchmaking,
    InGame,
    EndGame
}

public enum MatchMode
{
    Solo,
    Duo,
    Squad
}

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    public const int PlayersPerMatch = 25;

    public GameState currentState = GameState.Lobby;
    public MatchMode currentMode = MatchMode.Solo;

    public PlayerController player;
    public readonly List<BotAgent> bots = new List<BotAgent>();
    public readonly List<IDamageable> Combatants = new List<IDamageable>();

    public SafeZoneController safeZone;
    public LootSystem lootSystem;
    public UIManager uiManager;
    public ProfileData profile = new ProfileData();

    private static readonly string[] BotNames =
    {
        "Kurt", "Şahin", "Atlas", "Poyraz", "Tuna", "Alp", "Kaan", "Deniz", "Ece", "Ada",
        "Mert", "Bora", "Efe", "Yiğit", "Arda", "Selin", "Duru", "Toprak", "Kuzey", "Asya",
        "Rüzgar", "Çağan", "Ilgaz", "Barlas", "Mira", "Aras", "Tolga", "Nehir", "Ozan", "Demir"
    };

    private static readonly Color AllyColor = new Color(0.2f, 0.8f, 0.35f);
    private static readonly Color[] EnemyColors =
    {
        new Color(0.9f, 0.35f, 0.2f), new Color(0.85f, 0.2f, 0.45f), new Color(0.95f, 0.6f, 0.1f),
        new Color(0.6f, 0.3f, 0.85f), new Color(0.75f, 0.15f, 0.15f), new Color(0.55f, 0.45f, 0.3f)
    };

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        profile.Load();

        lootSystem = CreateChild<LootSystem>("LootSystem");
        safeZone = CreateChild<SafeZoneController>("SafeZone");
        uiManager = CreateChild<UIManager>("UIManager");
    }

    private T CreateChild<T>(string childName) where T : Component
    {
        var obj = new GameObject(childName);
        obj.transform.SetParent(transform, false);
        return obj.AddComponent<T>();
    }

    public void RegisterPlayer(PlayerController p)
    {
        player = p;
    }

    // ----- Flow -----

    public void JoinLobby()
    {
        StopAllCoroutines();
        currentState = GameState.Lobby;
        ClearBots();
        lootSystem.Clear();
        safeZone.Stop();
        if (player != null)
            player.ResetForRound(new Vector3(0f, 0.95f, 0f));
        uiManager.ShowLobby();
    }

    public void StartMatch(MatchMode mode)
    {
        if (currentState != GameState.Lobby)
            return;

        currentMode = mode;
        currentState = GameState.Matchmaking;
        StartCoroutine(PrepareRoutine());
    }

    private IEnumerator PrepareRoutine()
    {
        for (int i = 3; i > 0; i--)
        {
            uiManager.ShowMatchmaking("Maç hazırlanıyor... " + i);
            yield return new WaitForSeconds(1f);
        }
        BeginRound();
    }

    public void BeginRound()
    {
        ClearBots();
        Combatants.Clear();
        Physics.SyncTransforms();

        Vector3 playerSpawn = RandomIslandPoint(GameBootstrap.IslandRadius - 8f);
        player.ResetForRound(playerSpawn);
        Combatants.Add(player);

        int teamSize = currentMode == MatchMode.Solo ? 1 : currentMode == MatchMode.Duo ? 2 : 4;
        int allies = teamSize - 1;
        int enemies = PlayersPerMatch - teamSize;
        int nameIndex = Random.Range(0, BotNames.Length);

        for (int i = 0; i < allies; i++)
        {
            Vector2 offset = Random.insideUnitCircle.normalized * Random.Range(2f, 4f);
            Vector3 pos = playerSpawn + new Vector3(offset.x, 0.2f, offset.y);
            SpawnBot(0, BotNames[nameIndex++ % BotNames.Length], AllyColor, pos);
        }

        for (int i = 0; i < enemies; i++)
        {
            int team = 1 + i / teamSize;
            Vector3 pos = RandomIslandPointAwayFrom(playerSpawn, 15f);
            SpawnBot(team, BotNames[nameIndex++ % BotNames.Length], EnemyColors[team % EnemyColors.Length], pos);
        }

        lootSystem.SpawnLoot(60);
        safeZone.Init(Vector3.zero, GameBootstrap.IslandRadius * 1.05f);

        currentState = GameState.InGame;
        uiManager.ShowBattleHud();
        uiManager.Toast(currentMode == MatchMode.Solo ? "Tek başına hayatta kal!" : "Takımınla hayatta kal!");
    }

    private void SpawnBot(int team, string botName, Color color, Vector3 position)
    {
        var obj = new GameObject("Bot_" + botName);
        obj.transform.position = position;
        obj.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        var bot = obj.AddComponent<BotAgent>();
        bot.Setup(team, botName, color, WeaponData.CreateRandomBotWeapon());
        bots.Add(bot);
        Combatants.Add(bot);
    }

    private void ClearBots()
    {
        foreach (var bot in bots)
        {
            if (bot != null)
                Destroy(bot.gameObject);
        }
        bots.Clear();
        Combatants.Clear();
        if (player != null)
            Combatants.Add(player);
    }

    // ----- Events -----

    public void OnPlayerKill()
    {
        player.kills++;
        uiManager.Toast("Düşman elendi! (" + player.kills + ")");
    }

    public void OnBotEliminated(BotAgent bot, int attackerTeam)
    {
        string how = attackerTeam < 0 ? " bölgede elendi" : " elendi";
        uiManager.AddKillFeed(bot.botName + how);
        CheckForWin();
    }

    public void OnPlayerEliminated()
    {
        if (currentState != GameState.InGame)
            return;
        EndMatch(false);
    }

    private void CheckForWin()
    {
        if (currentState != GameState.InGame || player == null || player.isDead)
            return;
        if (AliveEnemyTeams() == 0)
            EndMatch(true);
    }

    private void EndMatch(bool won)
    {
        currentState = GameState.EndGame;
        safeZone.Stop();

        int place = won ? 1 : AliveEnemyTeams() + 1;
        int teams = (PlayersPerMatch - 1) / TeamSize() + 1;
        int kills = player.kills;
        int xp = kills * 40 + (won ? 200 : Mathf.Max(0, (teams - place) * 6));
        int coins = kills * 10 + (won ? 100 : 0);

        profile.AddMatchResult(won, kills, xp, coins);
        uiManager.ShowResult(won, place, teams, kills, xp, coins);
    }

    // ----- Queries -----

    public int TeamSize()
    {
        return currentMode == MatchMode.Solo ? 1 : currentMode == MatchMode.Duo ? 2 : 4;
    }

    public int AliveCount()
    {
        int count = 0;
        foreach (var c in Combatants)
        {
            if (c != null && !c.IsDead)
                count++;
        }
        return count;
    }

    public int AliveEnemyTeams()
    {
        var teams = new HashSet<int>();
        foreach (var bot in bots)
        {
            if (bot != null && !bot.isDead && bot.team != 0)
                teams.Add(bot.team);
        }
        return teams.Count;
    }

    /// <summary>Random free spot on the island (not inside a house, tree or rock).</summary>
    public static Vector3 RandomIslandPoint(float maxRadius)
    {
        Vector3 point = Vector3.zero;
        for (int tries = 0; tries < 20; tries++)
        {
            Vector2 p = Random.insideUnitCircle * maxRadius;
            point = new Vector3(p.x, 1.2f, p.y);
            if (!Physics.CheckSphere(point, 0.7f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                break;
        }
        return point;
    }

    private static Vector3 RandomIslandPointAwayFrom(Vector3 from, float minDistance)
    {
        Vector3 p = RandomIslandPoint(GameBootstrap.IslandRadius - 4f);
        for (int tries = 0; tries < 10; tries++)
        {
            Vector3 d = p - from;
            d.y = 0f;
            if (d.magnitude >= minDistance)
                break;
            p = RandomIslandPoint(GameBootstrap.IslandRadius - 4f);
        }
        return p;
    }
}
