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
    public readonly List<Vehicle> vehicles = new List<Vehicle>();
    public AirPlane plane;

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

    private static readonly Color AllyColor = new Color(0.2f, 0.7f, 0.35f);
    private static readonly Color[] EnemyColors =
    {
        new Color(0.85f, 0.35f, 0.2f), new Color(0.8f, 0.2f, 0.4f), new Color(0.9f, 0.6f, 0.15f),
        new Color(0.55f, 0.3f, 0.8f), new Color(0.7f, 0.15f, 0.15f), new Color(0.55f, 0.45f, 0.3f)
    };
    private static readonly Color[] JeepColors =
    {
        new Color(0.35f, 0.42f, 0.25f), new Color(0.75f, 0.68f, 0.5f), new Color(0.55f, 0.15f, 0.12f), new Color(0.2f, 0.3f, 0.45f)
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
        ClearRound();
        if (player != null)
            player.ResetForRound(new Vector3(0f, World.HeightAt(0f, 0f) + 0.95f, 0f));
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
        ClearRound();
        Physics.SyncTransforms();

        plane = AirPlane.Launch();

        player.ResetForRound(new Vector3(0f, World.HeightAt(0f, 0f) + 0.95f, 0f));
        player.BoardPlane(plane);
        Combatants.Add(player);

        int teamSize = TeamSize();
        int allies = teamSize - 1;
        int enemies = PlayersPerMatch - teamSize;
        int nameIndex = Random.Range(0, BotNames.Length);

        for (int i = 0; i < allies; i++)
        {
            var bot = SpawnBot(0, BotNames[nameIndex++ % BotNames.Length], AllyColor);
            bot.BoardPlane(plane, 1.1f, Vector3.zero, true);    // waits for the player to jump
        }

        for (int i = 0; i < enemies; i++)
        {
            int team = 1 + i / teamSize;
            var bot = SpawnBot(team, BotNames[nameIndex++ % BotNames.Length], EnemyColors[team % EnemyColors.Length]);
            Vector3 landing = World.RandomOpenPoint(Vector3.zero, World.IslandRadius - 10f);
            bot.BoardPlane(plane, Random.Range(0.12f, 0.88f), landing, false);
        }

        foreach (var spot in World.VehicleSpots)
            vehicles.Add(Vehicle.Spawn(spot, Random.Range(0f, 360f), JeepColors[Random.Range(0, JeepColors.Length)]));

        lootSystem.SpawnLoot(45);
        safeZone.Init(Vector3.zero, World.IslandRadius * 1.15f);

        currentState = GameState.InGame;
        uiManager.ShowBattleHud();
        uiManager.Toast("Atlamak için ATLA'ya bas!");
    }

    private BotAgent SpawnBot(int team, string botName, Color color)
    {
        var obj = new GameObject("Bot_" + botName);
        obj.transform.position = plane != null ? plane.transform.position : Vector3.up * 100f;
        var bot = obj.AddComponent<BotAgent>();
        bot.Setup(team, botName, color, WeaponData.CreateRandomBotWeapon());
        bots.Add(bot);
        Combatants.Add(bot);
        return bot;
    }

    private void ClearRound()
    {
        foreach (var bot in bots)
        {
            if (bot != null)
                Destroy(bot.gameObject);
        }
        bots.Clear();

        foreach (var v in vehicles)
        {
            if (v != null)
                Destroy(v.gameObject);
        }
        vehicles.Clear();

        if (plane != null)
            Destroy(plane.gameObject);
        plane = null;

        Combatants.Clear();
        lootSystem.Clear();
        safeZone.Stop();
    }

    // ----- Events -----

    public void OnPlayerJumped()
    {
        foreach (var bot in bots)
        {
            if (bot != null && bot.team == 0)
                bot.JumpNow();
        }
        uiManager.Toast("Paraşüt yere yaklaşınca kendiliğinden açılır");
    }

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
        StartCoroutine(EndAfterDelay(false, 2f));
    }

    private IEnumerator EndAfterDelay(bool won, float delay)
    {
        currentState = GameState.EndGame;
        safeZone.Stop();
        yield return new WaitForSeconds(delay);
        EndMatch(won);
    }

    private void CheckForWin()
    {
        if (currentState != GameState.InGame || player == null || player.isDead)
            return;
        if (AliveEnemyTeams() == 0)
            StartCoroutine(EndAfterDelay(true, 1.5f));
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

    public Vehicle NearestVehicle(Vector3 position, float maxDistance)
    {
        Vehicle best = null;
        float bestDist = maxDistance;
        foreach (var v in vehicles)
        {
            if (v == null || v.driver != null)
                continue;
            float d = Vector3.Distance(position, v.transform.position);
            if (d < bestDist)
            {
                best = v;
                bestDist = d;
            }
        }
        return best;
    }
}
