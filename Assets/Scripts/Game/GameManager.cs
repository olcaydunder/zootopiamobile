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
    Squad,
    Team5    // 5v5 team deathmatch in a small arena
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
    private bool tankDropped;
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
        if (NetClient.Instance != null)
            NetClient.Instance.Leave();   // leaving an online match from the pause menu
        StopAllCoroutines();
        Time.timeScale = 1f;
        currentState = GameState.Lobby;
        ClearRound();
        if (player != null)
        {
            player.ApplySkin(profile.equippedSkin);
            player.ResetForRound(World.LobbySpot + Vector3.up * 0.95f);
            player.transform.rotation = Quaternion.Euler(0f, World.LobbyYaw, 0f);   // faces the street, clinic behind
            player.SetLobbyView(true);
            RefreshLobbyWeapon();
        }

        // One-time welcome gift so the gunsmith can be tried straight away.
        if (PlayerPrefs.GetInt("zm_gift1", 0) == 0)
        {
            PlayerPrefs.SetInt("zm_gift1", 1);
            profile.coins += 1500;
            profile.Save();
            uiManager.Toast("Hoş geldin hediyesi: 1500 Kredi!");
        }
        uiManager.ShowLobby();
    }

    /// <summary>The character in the lobby holds the player's customised rifle.</summary>
    public void RefreshLobbyWeapon()
    {
        if (player != null && currentState == GameState.Lobby)
            player.ShowcaseWeapon(Loadout.PrimaryWeapon());
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
            uiManager.ShowMatchmaking((IsTeamMatch ? "5v5 hazırlanıyor... " : "Maç hazırlanıyor... ") + i);
            yield return new WaitForSeconds(1f);
        }
        if (IsTeamMatch)
            BeginTeamRound();
        else
            BeginRound();
    }

    // ----- 5v5 -----

    public bool IsTeamMatch { get { return currentMode == MatchMode.Team5; } }

    /// <summary>Offline 5v5: you and 4 bots against 5 bots in the map's arena.</summary>
    public void BeginTeamRound()
    {
        ClearRound();
        Physics.SyncTransforms();
        TeamMatch.Setup();
        MatchTokens.DisableAll();
        safeZone.InitArena(TeamMatch.Center, TeamMatch.Radius, false);

        player.SetLobbyView(false);
        player.TeamSpawn(TeamMatch.SpawnPoint(0), TeamMatch.Center, true);
        Combatants.Add(player);
        int nameIndex = Random.Range(0, BotNames.Length);
        for (int i = 0; i < TeamMatch.Size * 2 - 1; i++)
        {
            int team = i < TeamMatch.Size - 1 ? 0 : 1;
            var bot = SpawnBot(team, BotNames[nameIndex++ % BotNames.Length], team == 0 ? AllyColor : EnemyColors[0]);
            bot.PlaceAt(TeamMatch.SpawnPoint(team));
        }
        currentState = GameState.InGame;
        uiManager.ShowBattleHud();
        uiManager.Toast("5v5  •  " + TeamMatch.ScoreToWin + " öldürmeye ulaşan takım kazanır");
    }

    /// <summary>Game server 5v5: the bots of each team (the players' stand-ins are added by NetServer).</summary>
    public void BeginServerTeamRound(int bots0, int bots1)
    {
        currentMode = MatchMode.Team5;
        ClearRound();
        Physics.SyncTransforms();
        TeamMatch.Setup();
        safeZone.InitArena(TeamMatch.Center, TeamMatch.Radius, false);
        int nameIndex = Random.Range(0, BotNames.Length);
        for (int i = 0; i < bots0 + bots1; i++)
        {
            int team = i < bots0 ? 0 : 1;
            var bot = SpawnBot(team, BotNames[nameIndex++ % BotNames.Length], EnemyColors[team]);
            bot.PlaceAt(TeamMatch.SpawnPoint(team));
        }
        currentState = GameState.InGame;
    }

    /// <summary>Phone, online 5v5: the arena; the server says where to spawn (TeamRespawnLocal).</summary>
    public void BeginOnlineTeamRound(Vector3 arenaCenter, float arenaRadius)
    {
        StopAllCoroutines();
        Time.timeScale = 1f;
        currentMode = MatchMode.Team5;
        onlineResultShown = false;
        ClearRound();
        Physics.SyncTransforms();
        TeamMatch.Setup();
        MatchTokens.DisableAll();
        safeZone.InitArena(arenaCenter, arenaRadius, true);
        player.SetLobbyView(false);
        player.TeamSpawn(new Vector3(arenaCenter.x, World.GroundHeight(arenaCenter.x, arenaCenter.z), arenaCenter.z), arenaCenter, true);
        Combatants.Add(player);
        currentState = GameState.InGame;
        uiManager.ShowBattleHud();
        uiManager.Toast("ÇEVRİMİÇİ 5v5  •  " + TeamMatch.ScoreToWin + " öldürmeye ulaşan kazanır");
    }

    /// <summary>Phone: the server put us (back) in the fight here.</summary>
    public void TeamRespawnLocal(Vector3 ground, bool first)
    {
        if (currentState != GameState.InGame || player == null)
            return;
        player.TeamSpawn(ground, TeamMatch.Center, first);
        if (!first)
            uiManager.Toast("Yeniden doğdun!");
    }

    private IEnumerator RespawnBotLater(BotAgent bot)
    {
        yield return new WaitForSeconds(TeamMatch.RespawnDelay);
        if (currentState != GameState.InGame || bot == null || !IsTeamMatch)
            yield break;
        bot.Respawn(TeamMatch.SpawnPoint(bot.team));
        if (NetGame.IsServer && NetServer.Instance != null)
            NetServer.Instance.OnBotRespawned(bot);
    }

    private IEnumerator RespawnPlayerLater()
    {
        for (int s = Mathf.RoundToInt(TeamMatch.RespawnDelay); s > 0; s--)
        {
            uiManager.Toast("Öldün  •  " + s + " sn sonra yeniden doğacaksın");
            yield return new WaitForSeconds(1f);
            if (currentState != GameState.InGame)
                yield break;
        }
        player.TeamSpawn(TeamMatch.SpawnPoint(0), TeamMatch.Center, false);
        uiManager.Toast("Yeniden doğdun!");
    }

    private void TeamScore(int team)
    {
        if (team < 0 || team > 1 || NetGame.InOnlineMatch)
            return;
        TeamMatch.Score[team]++;
    }

    private void EndTeamMatch()
    {
        currentState = GameState.EndGame;
        safeZone.Stop();
        int winner = TeamMatch.Winner;
        StartCoroutine(TeamResultLater(winner == 0, winner < 0));
    }

    private IEnumerator TeamResultLater(bool won, bool draw)
    {
        uiManager.Toast(won ? "TAKIMIN KAZANDI!  " : draw ? "BERABERE  " : "TAKIMIN KAYBETTİ  ");
        yield return new WaitForSeconds(2f);
        GiveResult(won, won ? 1 : 2, 2);
    }

    public void BeginRound()
    {
        ClearRound();
        Physics.SyncTransforms();

        plane = AirPlane.Launch();
        UpgradeStation.SpawnAll();
        MatchTokens.BeginMatch();

        player.SetLobbyView(false);
        player.ResetForRound(new Vector3(0f, World.HeightAt(0f, 0f) + 0.95f, 0f));
        player.GiveWeapon(Gunsmith.BaseWeapon(Loadout.PrimaryType));   // the primary chosen in HAZIRLIK
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

        var spots = DropSpots(plane);
        Vector3 teamSpot = Vector3.zero;
        for (int i = 0; i < enemies; i++)
        {
            int team = 1 + i / teamSize;
            var bot = SpawnBot(team, BotNames[nameIndex++ % BotNames.Length], EnemyColors[team % EnemyColors.Length]);
            PlanBotJump(bot, i, teamSize, spots, ref teamSpot);
        }

        VehicleSpawns.SpawnAll(vehicles);
        tankDropped = false;

        lootSystem.SpawnLoot(70);
        safeZone.Init(Vector3.zero, ZoneStartRadius);

        currentState = GameState.InGame;
        uiManager.ShowBattleHud();
        uiManager.Toast("Atlamak için ATLA'ya bas!");
        if (!profile.tutorialDone)
            StartCoroutine(TutorialTips());
    }

    private static float ZoneStartRadius
    {
        get { return MapData.Loaded ? MapData.PlayHalf * 1.42f + 10f : World.IslandRadius * 1.15f; }
    }

    /// <summary>
    /// Places bots can glide to (~230 m either side of the flight line), shuffled: each bot team picks a
    /// different part of the map (a building to loot, or open ground).
    /// </summary>
    private static List<Vector3> DropSpots(AirPlane dropPlane)
    {
        Vector3 lineDir = (dropPlane.end - dropPlane.start);
        lineDir.y = 0f;
        lineDir.Normalize();
        var spots = new List<Vector3>();
        foreach (var h in World.HouseCenters)
        {
            Vector3 rel = h - dropPlane.start;
            rel.y = 0f;
            if ((rel - lineDir * Vector3.Dot(rel, lineDir)).magnitude < 230f)
                spots.Add(h);
        }
        for (int i = spots.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            Vector3 tmp = spots[i]; spots[i] = spots[j]; spots[j] = tmp;
        }
        return spots;
    }

    /// <summary>
    /// Bot number <paramref name="index"/> (teams of <paramref name="teamSize"/>) boards the plane and jumps
    /// where the flight path passes closest to its team's landing spot.
    /// </summary>
    private void PlanBotJump(BotAgent bot, int index, int teamSize, List<Vector3> spots, ref Vector3 teamSpot)
    {
        Vector3 lineDir = plane.end - plane.start;
        lineDir.y = 0f;
        lineDir.Normalize();
        Vector3 flight = plane.end - plane.start;
        flight.y = 0f;
        if (index % teamSize == 0)
        {
            int pick = index / teamSize;
            if (pick < spots.Count && Random.value < 0.75f)
                teamSpot = spots[pick];
            else
            {
                // Open ground somewhere along the flight path, off to either side.
                Vector3 side = new Vector3(-lineDir.z, 0f, lineDir.x);
                Vector3 onPath = Vector3.Lerp(plane.start, plane.end, Random.Range(0.15f, 0.85f));
                onPath.y = 0f;
                teamSpot = World.RandomOpenPoint(onPath + side * Random.Range(-200f, 200f), 30f);
            }
        }
        Vector3 landing = World.RandomOpenPoint(teamSpot, 14f);
        Vector3 fromStart = landing - plane.start;
        fromStart.y = 0f;
        float along = flight.sqrMagnitude > 1f ? Vector3.Dot(fromStart, flight) / flight.sqrMagnitude : Random.value;
        float jumpAt = Mathf.Clamp(along - 0.05f + Random.Range(-0.03f, 0.03f), 0.06f, 0.94f);
        bot.BoardPlane(plane, jumpAt, landing, false);
    }

    // ----- Online -----

    /// <summary>
    /// Dedicated server: a new round. Players' teams are 0 .. humanTeams-1 (their stand-ins are added by
    /// NetServer); <paramref name="botCount"/> bots fill the teams after them.
    /// </summary>
    public void BeginServerRound(MatchMode mode, int humanTeams, int botCount)
    {
        currentMode = mode;
        ClearRound();
        Physics.SyncTransforms();
        plane = AirPlane.Launch();

        int teamSize = TeamSize();
        var spots = DropSpots(plane);
        Vector3 teamSpot = Vector3.zero;
        int nameIndex = Random.Range(0, BotNames.Length);
        for (int i = 0; i < botCount; i++)
        {
            int team = humanTeams + i / teamSize;
            string botName = BotNames[nameIndex++ % BotNames.Length];
            if (i >= BotNames.Length)
                botName += " " + (i / BotNames.Length + 1);
            var bot = SpawnBot(team, botName, EnemyColors[team % EnemyColors.Length]);
            PlanBotJump(bot, i, teamSize, spots, ref teamSpot);
        }

        lootSystem.SpawnLoot(70);
        safeZone.Init(Vector3.zero, ZoneStartRadius);
        currentState = GameState.InGame;
    }

    private bool onlineResultShown;

    /// <summary>Phone: the online match starts (everything else arrives from the server).</summary>
    public void BeginOnlineRound(MatchMode mode, Vector3 planeStart, Vector3 planeEnd, Vector3 zoneCenter, float zoneRadius)
    {
        StopAllCoroutines();
        Time.timeScale = 1f;
        currentMode = mode;
        onlineResultShown = false;
        ClearRound();
        Physics.SyncTransforms();

        plane = AirPlane.Launch(planeStart, planeEnd);
        MatchTokens.DisableAll();   // tokens, abilities and vehicles are offline-only for now

        player.SetLobbyView(false);
        player.ResetForRound(new Vector3(0f, World.HeightAt(0f, 0f) + 0.95f, 0f));
        player.GiveWeapon(Gunsmith.BaseWeapon(Loadout.PrimaryType));
        player.BoardPlane(plane);
        Combatants.Add(player);

        safeZone.InitRemote(zoneCenter, zoneRadius);
        currentState = GameState.InGame;
        uiManager.ShowBattleHud();
        uiManager.Toast("ÇEVRİMİÇİ MAÇ  •  Atlamak için ATLA'ya bas!");
    }

    /// <summary>Phone: results of an online match (place and team count come from the server).</summary>
    public void EndOnlineMatch(bool won, int place, int teams)
    {
        if (onlineResultShown)
            return;
        onlineResultShown = true;
        StopAllCoroutines();
        currentState = GameState.EndGame;
        safeZone.Stop();
        GiveResult(won, place, Mathf.Max(teams, place));
    }

    /// <summary>One-time hints during the very first match.</summary>
    private IEnumerator TutorialTips()
    {
        string[] tips =
        {
            "Sol başparmak: hareket  •  Sağ taraf: kaydırarak bakış",
            "Yere inince sandıklara yürü: silah, mermi, zırh",
            "NİŞAN ile yakınlaştır, ATEŞ basılıyken parmağını kaydırarak nişan al",
            "Mavi duvarın dışı hasar verir, bölgenin içinde kal",
            "Ciplerin yanında BİN butonu çıkar"
        };
        yield return new WaitForSeconds(4f);
        foreach (var tip in tips)
        {
            while (player != null && player.IsAirborne && tip != tips[0])
                yield return new WaitForSeconds(1f);
            if (currentState != GameState.InGame)
                yield break;
            uiManager.Toast(tip);
            if (!profile.tutorialDone)
            {
                profile.tutorialDone = true;   // show the tips once, even if the match ends early
                profile.Save();
            }
            yield return new WaitForSeconds(7f);
        }
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
        MatchStats.Reset();
        foreach (var bot in bots)
        {
            if (bot != null)
                Destroy(bot.gameObject);
        }
        bots.Clear();
        Door.ResetAll();
        AbilityFx.ClearAll();
        UpgradeStation.ClearAll();
        VehicleSpawns.ClearPads();
        Marks.Clear();

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
        if (IsTeamMatch && currentState == GameState.InGame)
            StartCoroutine(RespawnBotLater(bot));
        if (NetGame.IsServer)
        {
            if (NetServer.Instance != null)
                NetServer.Instance.OnBotEliminated(bot);
            return;
        }
        if (IsTeamMatch)
        {
            if (attackerTeam >= 0 && attackerTeam != bot.team)
                TeamScore(attackerTeam);
            uiManager.AddKillFeed(bot.botName + (attackerTeam < 0 ? " arenanın dışında öldü" : " vuruldu"));
            return;
        }
        string how = attackerTeam < 0 ? " bölgede elendi" : " elendi";
        uiManager.AddKillFeed(bot.botName + how);
        CheckForWin();
    }

    public void OnPlayerEliminated()
    {
        if (currentState != GameState.InGame)
            return;
        if (NetGame.InOnlineMatch)
        {
            NetClient.Instance.OnLocalDeath();   // the server sends the placement (5v5: a respawn), then the result shows
            return;
        }
        if (IsTeamMatch)
        {
            if (player.lastDamageTeam >= 0 && player.lastDamageTeam != 0)
                TeamScore(player.lastDamageTeam);
            StartCoroutine(RespawnPlayerLater());
            return;
        }
        // Dirilme Jetonu: once per match, before the late zone phases.
        if (safeZone != null && safeZone.Phase < MatchTokens.ReviveBeforePhase && MatchTokens.Available(TokenType.Revive))
        {
            StartCoroutine(ReviveRoutine());
            return;
        }
        StartCoroutine(EndAfterDelay(false, 2f));
    }

    private System.Collections.IEnumerator ReviveRoutine()
    {
        const int Seconds = 8;
        for (int s = Seconds; s > 0; s--)
        {
            uiManager.Toast("DİRİLME JETONU  •  " + s + " sn sonra yeniden doğacaksın");
            yield return new WaitForSeconds(1f);
            if (currentState != GameState.InGame)
                yield break;
        }
        if (!MatchTokens.Use(TokenType.Revive))
        {
            StartCoroutine(EndAfterDelay(false, 0.5f));
            yield break;
        }
        Vector3 c = safeZone != null && safeZone.active ? safeZone.center : Vector3.zero;
        float r = safeZone != null && safeZone.active ? safeZone.radius * 0.5f : 60f;
        Vector3 p = World.RandomOpenPoint(c, r);
        player.RespawnFromToken(new Vector3(p.x, World.GroundHeight(p.x, p.z), p.z));
        uiManager.Toast("Yeniden doğdun! Güvenli bölgeye iniyorsun");
        CheckForWin();   // in case the last enemies fell while you were down
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
        if (currentState != GameState.InGame || player == null || player.isDead || IsTeamMatch)
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
        GiveResult(won, place, teams);
    }

    private void GiveResult(bool won, int place, int teams)
    {
        int kills = player.kills;
        // XP: taking part + kills + placement (up to 300) + the win.
        float placeShare = teams > 1 ? (float)(teams - place) / (teams - 1) : 1f;
        int xp = 100 + kills * 60 + Mathf.RoundToInt(300f * placeShare) + (won ? 400 : 0);
        int coins = 20 + kills * 10 + Mathf.RoundToInt(60f * placeShare) + (won ? 100 : 0);

        profile.AddMatchResult(won, kills, xp, coins);
        Missions.OnMatchEnd(won, place, kills, IsTeamMatch);
        uiManager.ShowResult(won, place, teams, kills, xp, coins);
    }

    // ----- Queries -----

    public int TeamSize()
    {
        return NetProtocol.TeamSize(currentMode);
    }

    public int AliveCount()
    {
        if (NetGame.InOnlineMatch)
            return NetClient.Instance.AliveCount;
        int count = 0;
        foreach (var c in Combatants)
        {
            if (c != null && !c.IsDead)
                count++;
        }
        return count;
    }

    /// <summary>Teammate bots that are alive and on the ground.</summary>
    public int AliveAllies()
    {
        int count = 0;
        foreach (var bot in bots)
        {
            if (bot != null && bot.team == 0 && !bot.isDead)
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

    private void Update()
    {
        // Offline 5v5: first to 40 kills, or the one ahead when the time is up.
        if (IsTeamMatch && currentState == GameState.InGame && !NetGame.Online && TeamMatch.Over)
            EndTeamMatch();
        // The tank comes down once per match, when the zone starts its second phase.
        if (currentState == GameState.InGame && !tankDropped && !IsTeamMatch && !NetGame.Online && safeZone != null && safeZone.active && safeZone.Phase >= 2)
        {
            tankDropped = true;
            Vector3 p = World.RandomOpenPoint(safeZone.center, safeZone.radius * 0.5f);
            TankDrop.Launch(new Vector3(p.x, World.GroundHeight(p.x, p.z), p.z));
            if (uiManager != null)
                uiManager.Toast("TANK İKMALİ İNİYOR!  Haritada işaretli");
        }
    }

    public Vehicle NearestVehicle(Vector3 position, float maxDistance)
    {
        Vehicle best = null;
        float bestDist = maxDistance;
        foreach (var v in vehicles)
        {
            if (v == null || v.driver != null || v.Destroyed)
                continue;
            // Measured from the vehicle's edge, so long vehicles and helicopters are easy to board.
            float d = Vector3.Distance(position, v.transform.position) - (v.def != null ? v.def.ccRadius * 1.4f : 0f);
            if (d < bestDist)
            {
                best = v;
                bestDist = d;
            }
        }
        return best;
    }
}
