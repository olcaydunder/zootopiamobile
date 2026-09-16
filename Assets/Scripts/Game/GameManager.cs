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

    public GameState currentState = GameState.Lobby;
    public MatchMode currentMode = MatchMode.Solo;

    public List<PlayerController> players = new List<PlayerController>();
    public List<BotAgent> bots = new List<BotAgent>();

    public SafeZoneController safeZone;
    public LootSystem lootSystem;
    public UIManager uiManager;
    public Matchmaker matchmaker;
    public SettingsManager settingsManager;
    public ProfileData profile;

    public Vector2 touchMove;
    public bool touchSprint;
    public bool touchFire;
    public bool touchJump;
    public bool touchCrouch;
    public bool touchReload;
    public bool touchPause;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        SetupProfile();
        SetupManagers();
    }

    private void SetupProfile()
    {
        var profileObj = new GameObject("ProfileData");
        profileObj.transform.SetParent(transform);
        profile = profileObj.AddComponent<ProfileData>();
    }

    private void SetupManagers()
    {
        if (uiManager == null)
        {
            var uiObj = new GameObject("UIManager");
            uiObj.transform.SetParent(transform);
            uiManager = uiObj.AddComponent<UIManager>();
        }

        if (matchmaker == null)
        {
            var mmObj = new GameObject("Matchmaker");
            mmObj.transform.SetParent(transform);
            matchmaker = mmObj.AddComponent<Matchmaker>();
        }

        if (lootSystem == null)
        {
            var lootObj = new GameObject("LootSystem");
            lootObj.transform.SetParent(transform);
            lootSystem = lootObj.AddComponent<LootSystem>();
        }

        if (safeZone == null)
        {
            var zoneObj = new GameObject("SafeZone");
            zoneObj.transform.SetParent(transform);
            safeZone = zoneObj.AddComponent<SafeZoneController>();
        }

        if (settingsManager == null)
        {
            var settingsObj = new GameObject("SettingsManager");
            settingsObj.transform.SetParent(transform);
            settingsManager = settingsObj.AddComponent<SettingsManager>();
        }
    }

    public void RegisterPlayer(PlayerController player)
    {
        if (!players.Contains(player))
            players.Add(player);
    }

    public void RegisterBot(BotAgent bot)
    {
        if (!bots.Contains(bot))
            bots.Add(bot);
    }

    public void JoinLobby(MatchMode mode)
    {
        currentMode = mode;
        currentState = GameState.Lobby;
        if (uiManager != null)
            uiManager.ShowLobby();
    }

    public void StartMatch(MatchMode mode)
    {
        currentMode = mode;
        currentState = GameState.Matchmaking;
        if (matchmaker != null)
            matchmaker.BeginMatching(mode);
    }

    public void BeginRound()
    {
        currentState = GameState.InGame;

        if (safeZone != null)
            safeZone.Init(60f, 9f);

        if (lootSystem != null)
            lootSystem.SpawnLoot();

        if (uiManager != null)
            uiManager.ShowBattleHud();
    }

    public void OnPlayerEliminated(PlayerController player)
    {
        if (players.Contains(player))
            players.Remove(player);

        if (players.Count == 0)
        {
            currentState = GameState.EndGame;
            if (uiManager != null)
                uiManager.ShowResultPanel("You were eliminated.");
        }
    }

    public void RespawnPlayer(PlayerController player)
    {
        var spawn = new Vector3(Random.Range(-15f, 15f), 1.2f, Random.Range(-15f, 15f));
        player.Respawn(spawn);
    }

    public List<BotAgent> GetLivingBots()
    {
        var living = new List<BotAgent>();
        foreach (var bot in bots)
        {
            if (bot != null && !bot.isDead)
                living.Add(bot);
        }
        return living;
    }
}
