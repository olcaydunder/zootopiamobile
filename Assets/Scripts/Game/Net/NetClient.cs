using System.Collections;
using System.Collections.Generic;
using System.Net;
using UnityEngine;

/// <summary>
/// Phone side of an online match: joins the game server's waiting room, then plays the match —
/// sends the local player's movement 20 times a second plus its shots, hits, doors and pickups,
/// and draws everyone else (players and server bots) as <see cref="NetPuppet"/>s.
/// </summary>
public sealed class NetClient : MonoBehaviour
{
    public static NetClient Instance;

    public enum Phase { Idle, Connecting, Lobby, Playing }

    public Phase State { get; private set; }
    public bool InMatch { get { return State == Phase.Playing; } }

    // Waiting room (read by NetLobbyScreen)
    public string Status = "";
    public string Error = "";
    public string RoomCode = "";
    public MatchMode Mode;
    public bool PrivateRoom;
    public int MyId = -1;
    public int LeaderId;
    public int LobbyPhase;
    public double CountdownEnds;
    public readonly List<int> LobbyIds = new List<int>();
    public readonly List<string> LobbyNames = new List<string>();
    public readonly List<string> LobbyAccounts = new List<string>();

    /// <summary>A human player met online (for the friends screen: add, report, block).</summary>
    public struct MetPlayer
    {
        public string name, account, match;
        public bool teammate;
    }

    /// <summary>Players of the last online match (and the current one), newest first.</summary>
    public static readonly List<MetPlayer> RecentPlayers = new List<MetPlayer>();
    /// <summary>Changes whenever something the waiting room shows changes.</summary>
    public int Revision;

    // Match
    public int AliveCount;
    public int AliveTeams;
    public bool IsLeader { get { return State == Phase.Lobby && LeaderId == MyId; } }

    private const double ConnectTimeout = 45.0;
    private const double LinkTimeout = 12.0;
    private const double InterpolationDelay = 0.15;   // three snapshots: one lost packet does not show

    private sealed class EntityInfo
    {
        public int id, team;
        public bool bot;
        public string name, skin, para, account, mask;
    }

    private NetSocket socket;
    private IPEndPoint server;
    private NetConnection conn;
    private uint nonce;
    private double connectStarted, nextHello;
    private System.Action<byte[], int> output;
    private System.Action<byte[], int, int, bool> deliver;
    private readonly NetWriter w = new NetWriter(256);
    private readonly NetReader reader = new NetReader();

    private readonly Dictionary<int, EntityInfo> infos = new Dictionary<int, EntityInfo>();
    private readonly Dictionary<int, NetPuppet> puppets = new Dictionary<int, NetPuppet>();
    private int myServerTeam;
    private int teamCount = 1;
    private bool doorsMatch;
    private double nextState;
    private ushort stateSeq;
    private double serverOffset;
    private bool haveOffset;
    private ushort lastSnap;
    private bool haveSnap;
    private int lastAttacker = NetProtocol.NoEntity;
    private float lastAttackTime = -100f;
    private int placement = -1;
    private int placementTeams;
    private bool deathSent;
    private bool finished;
    private bool resultShown;
    private int lastAttackHow;
    /// <summary>Bumped by every Connect/Leave: result coroutines of an old match must not touch a new one.</summary>
    private int session;

    private static double Now { get { return Time.realtimeSinceStartupAsDouble; } }

    public static NetClient Ensure()
    {
        if (Instance == null)
        {
            var go = new GameObject("NetClient");
            Instance = go.AddComponent<NetClient>();
        }
        return Instance;
    }

    private void Awake()
    {
        output = (buf, len) => { if (socket != null) socket.Send(buf, len, server); };
        deliver = (buf, off, len, reliable) => Dispatch(buf, off, len);
    }

    // ----- Connecting -----

    /// <summary>Joins the match the matchmaker gave us (host:port, 6-digit code).</summary>
    public void Connect(string host, int port, string code, MatchMode mode, bool privateRoom)
    {
        Leave();
        Error = "";
        RoomCode = code;
        Mode = mode;
        PrivateRoom = privateRoom;
        server = NetSocket.Resolve(host, port);
        if (server == null)
        {
            Fail("Sunucu adresi bulunamadı");
            return;
        }
        try
        {
            socket = new NetSocket();
            socket.Open(0, server.AddressFamily);
        }
        catch (System.Exception e)
        {
            Fail("Ağ açılamadı: " + e.Message);
            return;
        }
        nonce = (uint)Random.Range(1, int.MaxValue);
        VoiceChat.Ensure();
        State = Phase.Connecting;
        connectStarted = Now;
        nextHello = 0;
        Status = "Sunucuya bağlanılıyor...";
        Revision++;
    }

    private void SendHello()
    {
        var gm = GameManager.Instance;
        w.Reset();
        w.Byte(NetConnection.Magic0);
        w.Byte(NetConnection.Magic1);
        w.Byte(NetConnection.KindHello);
        w.Byte(NetProtocol.Version);
        w.UInt(nonce);
        w.String(NetGame.BuildVersion, 64);
        w.String(RoomCode, 16);
        w.String(gm != null ? gm.profile.playerName : "Oyuncu", 40);
        w.String(gm != null ? gm.profile.equippedSkin : ModelLibrary.PlayerSkin, 32);
        w.String(Cosmetics.EquippedParachuteCamo ?? "", 32);
        w.String(OnlineService.AccountId, 12);
        w.String(OnlineService.SecretForHello, 64);
        w.String(MapCatalog.Current, 16);
        w.String(Gear.MaskId ?? "", 16);
        socket.Send(w.Buffer, w.Length, server);
    }

    /// <summary>Private room leader: start the match now.</summary>
    public void RequestStart()
    {
        if (State != Phase.Lobby || conn == null)
            return;
        conn.SendReliable(new[] { NetProtocol.C_Start });
    }

    /// <summary>Leaves the room or match (tells the server) and forgets everything about it.</summary>
    public void Leave()
    {
        if (socket != null && conn != null)
        {
            w.Reset();
            w.Byte(NetConnection.Magic0);
            w.Byte(NetConnection.Magic1);
            w.Byte(NetConnection.KindBye);
            w.UInt(conn.Token);
            socket.Send(w.Buffer, w.Length, server);
            socket.Send(w.Buffer, w.Length, server);
        }
        CloseSocket();
        ClearMatch();
        session++;
        State = Phase.Idle;
        MyId = -1;
        LeaderId = 0;
        LobbyPhase = NetProtocol.LobbyWaiting;
        CountdownEnds = -1;
        LobbyIds.Clear();
        LobbyNames.Clear();
        LobbyAccounts.Clear();
        Chat.Clear();
        EmoteQueue.Clear();
        ChatRevision++;
        Revision++;
    }

    private void OnApplicationQuit()
    {
        Leave();   // tell the server at once instead of letting it time out
    }

    private void CloseSocket()
    {
        if (socket != null)
            socket.Close();
        socket = null;
        conn = null;
    }

    private void ClearMatch()
    {
        var gm = GameManager.Instance;
        foreach (var p in puppets.Values)
        {
            if (p == null)
                continue;
            if (gm != null)
                gm.Combatants.Remove(p);
            Destroy(p.gameObject);
        }
        puppets.Clear();
        infos.Clear();
        haveOffset = false;
        haveSnap = false;
        placement = -1;
        placementTeams = 0;
        deathSent = false;
        finished = false;
        lastAttacker = NetProtocol.NoEntity;
        lastAttackHow = 0;
        AliveCount = 0;
        AliveTeams = 0;
    }

    private void Fail(string message)
    {
        bool wasPlaying = State == Phase.Playing;
        Debug.LogWarning("[Çevrimiçi] " + message);
        CloseSocket();
        Error = message;
        Status = message;
        if (wasPlaying)
        {
            ConnectionLostInMatch(message);
            return;
        }
        ClearMatch();
        State = Phase.Idle;
        Revision++;
    }

    // ----- Frame -----

    private void Update()
    {
        if (socket == null)
            return;
        double now = Now;
        Receive(now);
        if (socket == null)
            return;   // closed while handling a message

        if (State == Phase.Connecting)
        {
            if (now >= nextHello)
            {
                nextHello = now + 0.5;
                SendHello();
            }
            if (now - connectStarted > 3.0 && Status != "Sunucu hazırlanıyor...")
            {
                Status = "Sunucu hazırlanıyor...";
                Revision++;
            }
            if (now - connectStarted > ConnectTimeout)
                Fail("Sunucuya bağlanılamadı. İnternet bağlantını kontrol et.");
            return;
        }
        if (conn == null)
            return;
        if (now - conn.LastReceive > LinkTimeout)
        {
            Fail("Sunucu bağlantısı koptu");
            return;
        }
        if (State == Phase.Playing)
        {
            if (now >= nextState)
            {
                nextState = now + NetProtocol.TickInterval;
                SendState();
            }
            double renderTime = now + serverOffset - InterpolationDelay;
            foreach (var p in puppets.Values)
                if (p != null)
                    p.Tick(renderTime);
        }
        conn.Flush(now, output);
    }

    private void Receive(double now)
    {
        byte[] data;
        int len;
        EndPoint from;
        int budget = 500;
        while (socket != null && budget-- > 0 && socket.TryReceive(out data, out len, out from))
        {
            if (len < 3 || data[0] != NetConnection.Magic0 || data[1] != NetConnection.Magic1 || !from.Equals(server))
                continue;
            byte kind = data[2];
            if (kind == NetConnection.KindWelcome && State == Phase.Connecting && len >= 13)
            {
                reader.Set(data, 3, len - 3);
                uint n = reader.UInt();
                uint token = reader.UInt();
                int id = reader.UShort();
                if (n != nonce || token == 0)
                    continue;
                conn = new NetConnection(token, now);
                MyId = id;
                State = Phase.Lobby;
                Status = "Bekleme odası";
                Revision++;
            }
            else if (kind == NetConnection.KindReject && State == Phase.Connecting)
            {
                reader.Set(data, 3, len - 3);
                try
                {
                    if (reader.UInt() == nonce)
                        Fail(reader.String());
                }
                catch (NetFormatException) { }
            }
            else if (kind == NetConnection.KindData && conn != null)
            {
                conn.Receive(data, len, now, deliver);
            }
            else if (kind == NetConnection.KindBye && conn != null && NetConnection.PeekToken(data, len) == 0 && len >= 7)
            {
                uint token = (uint)(data[3] | (data[4] << 8) | (data[5] << 16) | (data[6] << 24));
                if (token == conn.Token)
                {
                    if (finished)
                        Leave();
                    else
                        Fail(State != Phase.Playing && Error.Length > 0 ? Error : "Sunucu bağlantıyı kapattı");
                }
            }
        }
    }

    private void Dispatch(byte[] buf, int off, int len)
    {
        reader.Set(buf, off, len);
        try
        {
            int type = reader.Byte();
            switch (type)
            {
                case NetProtocol.S_Lobby: OnLobby(); break;
                case NetProtocol.S_Entities: OnEntities(); break;
                case NetProtocol.S_MatchStart: OnMatchStart(); break;
                case NetProtocol.S_Loot: OnLoot(); break;
                case NetProtocol.S_LootGone: OnLootGone(); break;
                case NetProtocol.S_LootTaken:
                {
                    int id = reader.UShort();
                    bool ok = reader.Bool();
                    var gm = GameManager.Instance;
                    gm.lootSystem.NetPickupResult(id, ok, gm.player);
                    break;
                }
                case NetProtocol.S_Snap: OnSnapshot(); break;
                case NetProtocol.S_Shot: OnShot(); break;
                case NetProtocol.S_Damage: OnDamage(); break;
                case NetProtocol.S_Kill: OnKill(); break;
                case NetProtocol.S_Door: OnDoor(); break;
                case NetProtocol.S_Grenade: OnGrenade(); break;
                case NetProtocol.S_Placement:
                    placement = reader.Byte();
                    placementTeams = reader.Byte();
                    break;
                case NetProtocol.S_MatchEnd: OnMatchEnd(); break;
                case NetProtocol.S_Toast:
                {
                    string text = reader.String();
                    Toast(text);
                    if (State != Phase.Playing)
                    {
                        Error = text;   // e.g. "Hesabın yasaklandı" before the server closes the link
                        Status = text;
                        Revision++;
                    }
                    break;
                }
                case NetProtocol.S_Chat:
                {
                    int from = reader.UShort();
                    string name = reader.String();
                    string account = reader.String();
                    string text = reader.String();
                    if (!VoiceChat.IsBlocked(account))
                    {
                        Chat.Add(new ChatLine { id = from, name = name, text = text, mine = from == MyId });
                        if (Chat.Count > 30)
                            Chat.RemoveAt(0);
                        ChatRevision++;
                    }
                    break;
                }
                case NetProtocol.S_Emote:
                {
                    int from = reader.UShort();
                    string name = reader.String();
                    int emote = reader.Byte();
                    if (!VoiceChat.IsBlocked(AccountOf(from)) && EmoteQueue.Count < 20)
                        EmoteQueue.Enqueue(new EmoteEvent { id = from, name = name, emote = emote });
                    break;
                }
                case NetProtocol.S_Respawn:
                {
                    int id = reader.UShort();
                    double time = reader.Float();
                    Vector3 at = reader.Pos();
                    if (State != Phase.Playing)
                        break;
                    if (id == MyId)
                    {
                        deathSent = false;
                        GameManager.Instance.TeamRespawnLocal(at, !spawnedOnce);
                        spawnedOnce = true;
                    }
                    else
                    {
                        NetPuppet pup;
                        if (puppets.TryGetValue(id, out pup) && pup != null)
                            pup.Revive(time, at + Vector3.up * 0.95f);
                    }
                    break;
                }
                case NetProtocol.S_Score:
                {
                    int n = reader.Byte();
                    for (int i = 0; i < n; i++)
                    {
                        int score = reader.UShort();
                        int local = LocalTeam(i);   // ours first (Herkes Tek: we are index 0 here)
                        if (local >= 0 && local < TeamMatch.Score.Length)
                            TeamMatch.Score[local] = score;
                    }
                    TeamMatch.TimeLeft = reader.Float();
                    int pts = reader.Byte();
                    var obj = ArenaObjectives.Instance;
                    for (int i = 0; i < pts; i++)
                    {
                        int owner = reader.Byte();
                        float progress = (sbyte)reader.Byte() / 100f;
                        if (obj != null)
                            obj.ApplyNet(i, owner == 255 ? -1 : owner, progress, myServerTeam == 1);
                    }
                    break;
                }
                case NetProtocol.S_Voice:
                {
                    int speaker = reader.UShort();
                    if (len > 3)
                        VoiceChat.Receive(speaker, AccountOf(speaker), buf, off + 3, len - 3);
                    break;
                }
            }
        }
        catch (NetFormatException) { }
        catch (System.Exception e)
        {
            Debug.LogException(e);
        }
    }

    // ----- Waiting room -----

    private void OnLobby()
    {
        LobbyPhase = reader.Byte();
        float left = reader.Float();
        Mode = (MatchMode)Mathf.Clamp(reader.Byte(), 0, (int)MatchMode.Heist);
        PrivateRoom = reader.Bool();
        LeaderId = reader.UShort();
        RoomCode = reader.String();
        int n = reader.Byte();
        LobbyIds.Clear();
        LobbyNames.Clear();
        LobbyAccounts.Clear();
        for (int i = 0; i < n; i++)
        {
            LobbyIds.Add(reader.UShort());
            LobbyNames.Add(reader.String());
            LobbyAccounts.Add(reader.String());
        }
        CountdownEnds = left >= 0f ? Now + left : -1;
        Revision++;
    }

    // ----- Match start -----

    private void OnEntities()
    {
        int n = reader.Byte();
        for (int i = 0; i < n; i++)
        {
            var e = new EntityInfo
            {
                id = reader.UShort(),
                team = reader.Byte(),
                bot = reader.Bool(),
                name = reader.String(),
                skin = reader.String(),
                para = reader.String(),
                account = reader.String(),
                mask = reader.String()
            };
            infos[e.id] = e;
        }
    }

    private int LocalTeam(int serverTeam)
    {
        if (serverTeam == myServerTeam)
            return 0;
        return serverTeam == 0 ? myServerTeam : serverTeam;
    }

    private void OnMatchStart()
    {
        MyId = reader.UShort();
        myServerTeam = reader.Byte();
        Mode = (MatchMode)Mathf.Clamp(reader.Byte(), 0, (int)MatchMode.Heist);
        Vector3 planeStart = reader.Pos();
        Vector3 planeEnd = reader.Pos();
        float zx = reader.Float();
        float zz = reader.Float();
        float zr = reader.Float();
        int doors = reader.UShort();
        teamCount = Mathf.Max(1, reader.Byte());
        doorsMatch = doors == Door.All.Count;
        if (!doorsMatch)
            Debug.LogWarning("[Çevrimiçi] kapı sayısı farklı: sunucu " + doors + ", telefon " + Door.All.Count);

        var gm = GameManager.Instance;
        State = Phase.Playing;
        nextState = 0;
        resultShown = false;
        Revision++;
        spawnedOnce = false;
        if (Modes.Arena(Mode))
            gm.BeginOnlineArenaRound(Mode, new Vector3(zx, 0f, zz), zr, Modes.TwoTeams(Mode) && myServerTeam == 1);
        else
            gm.BeginOnlineRound(Mode, planeStart, planeEnd, new Vector3(zx, 0f, zz), zr);

        foreach (var e in infos.Values)
        {
            if (e.id == MyId)
                continue;
            var p = NetPuppet.Create(e.id, LocalTeam(e.team), e.bot, e.name, e.skin, e.para);
            p.rig.SetMask(Gear.Find(e.mask) != null ? e.mask : "");
            puppets[e.id] = p;
            gm.Combatants.Add(p);
        }
        AliveCount = infos.Count;

        RecentPlayers.Clear();
        foreach (var e in infos.Values)
            if (!e.bot && e.id != MyId && !string.IsNullOrEmpty(e.account))
                RecentPlayers.Add(new MetPlayer { name = e.name, account = e.account, match = RoomCode, teammate = e.team == myServerTeam });
    }

    /// <summary>Account id of a player by their id in the room / match ("" for bots and unknown ids).</summary>
    public string AccountOf(int id)
    {
        if (State == Phase.Playing)
        {
            EntityInfo e;
            return infos.TryGetValue(id, out e) && !e.bot ? e.account ?? "" : "";
        }
        int i = LobbyIds.IndexOf(id);
        return i >= 0 && i < LobbyAccounts.Count ? LobbyAccounts[i] : "";
    }

    public string NameOfPlayer(int id)
    {
        if (State == Phase.Playing)
            return NameOf(id);
        int i = LobbyIds.IndexOf(id);
        return i >= 0 ? LobbyNames[i] : "?";
    }

    /// <summary>Whether anyone can hear us now (private room waiting room, or teammates in the match).</summary>
    public bool VoiceAvailable
    {
        get
        {
            if (State == Phase.Lobby)
                return PrivateRoom && LobbyIds.Count > 1;
            if (State != Phase.Playing)
                return false;
            foreach (var p in puppets.Values)
                if (p != null && p.team == 0 && !p.isBot)
                    return true;
            return false;
        }
    }

    /// <summary>Teammates (in the match) or room members (waiting room) for the players list: id, name, account.</summary>
    public void ListPlayers(List<MetPlayer> into)
    {
        into.Clear();
        if (State == Phase.Playing)
        {
            foreach (var e in infos.Values)
                if (!e.bot && e.id != MyId)
                    into.Add(new MetPlayer { name = e.name, account = e.account ?? "", match = RoomCode, teammate = e.team == myServerTeam });
        }
        else
        {
            for (int i = 0; i < LobbyIds.Count; i++)
                if (LobbyIds[i] != MyId)
                    into.Add(new MetPlayer { name = LobbyNames[i], account = i < LobbyAccounts.Count ? LobbyAccounts[i] : "", match = RoomCode, teammate = true });
        }
    }

    public struct ChatLine
    {
        public int id;
        public string name, text;
        public bool mine;
    }

    public struct EmoteEvent
    {
        public int id, emote;
        public string name;
    }

    /// <summary>Room (and team) chat of this session, newest last.</summary>
    public readonly List<ChatLine> Chat = new List<ChatLine>();
    public int ChatRevision;
    /// <summary>Emotes to show (the waiting room pops them up).</summary>
    public readonly Queue<EmoteEvent> EmoteQueue = new Queue<EmoteEvent>();

    public void SendChat(string text)
    {
        text = NetChat.Clean(text);
        if (conn == null || text.Length == 0 || (State != Phase.Lobby && State != Phase.Playing))
            return;
        w.Reset();
        w.Byte(NetProtocol.C_Chat);
        w.String(text, NetChat.MaxChat * 2);
        conn.SendReliable(w.ToArray());
    }

    public void SendEmote(int emote)
    {
        if (conn == null || emote < 0 || emote >= NetChat.Emotes.Length || (State != Phase.Lobby && State != Phase.Playing))
            return;
        w.Reset();
        w.Byte(NetProtocol.C_Emote);
        w.Byte(emote);
        conn.SendReliable(w.ToArray());
    }

    public void SendVoice(byte[] payload, int length)
    {
        if (conn == null || (State != Phase.Lobby && State != Phase.Playing) || length > NetProtocol.MaxVoiceBytes)
            return;
        w.Reset();
        w.Byte(NetProtocol.C_Voice);
        w.Bytes(payload, 0, length);
        conn.SendUnreliable(w.ToArray());
    }

    private void OnLoot()
    {
        int n = reader.UShort();
        var loot = GameManager.Instance.lootSystem;
        for (int i = 0; i < n; i++)
        {
            int id = reader.UShort();
            int type = reader.Byte();
            Vector3 pos = reader.Pos();
            if (type <= (int)LootType.Supply)
                loot.SpawnNet(id, (LootType)type, pos);
        }
    }

    private void OnLootGone()
    {
        GameManager.Instance.lootSystem.RemoveById(reader.UShort());
    }

    // ----- During the match -----

    private void OnSnapshot()
    {
        ushort seq = (ushort)reader.UShort();
        float time = reader.Float();
        int alive = reader.Byte();
        int teams = reader.Byte();
        float cx = reader.Float(), cz = reader.Float(), r = reader.Float();
        float nx = reader.Float(), nz = reader.Float(), nr = reader.Float();
        float timer = reader.Float();
        int zonePhase = reader.Byte();
        int zoneFlags = reader.Byte();
        if (State != Phase.Playing)
            return;
        if (haveSnap && !NetConnection.SeqBefore(lastSnap, seq))
            return;   // an older snapshot arriving late
        lastSnap = seq;
        haveSnap = true;
        AliveCount = alive;
        AliveTeams = teams;

        double offset = time - Now;
        if (!haveOffset || offset > serverOffset)
            serverOffset = offset;
        else
            serverOffset += (offset - serverOffset) * 0.02;
        haveOffset = true;

        var gm = GameManager.Instance;
        if (gm.safeZone != null)
            gm.safeZone.ApplyNet(new Vector3(cx, 0f, cz), r, new Vector3(nx, 0f, nz), nr, zonePhase,
                (zoneFlags & 1) != 0, (zoneFlags & 2) != 0, (zoneFlags & 4) != 0, timer);

        int n = reader.Byte();
        for (int i = 0; i < n; i++)
        {
            int id = reader.UShort();
            Vector3 pos = reader.Pos();
            float yaw = reader.Yaw();
            float pitch = reader.SByte();
            int flags = reader.Byte();
            int weapon = reader.Byte() - 1;
            int health = reader.Byte();
            NetPuppet p;
            if (id != MyId && puppets.TryGetValue(id, out p) && p != null)
                p.Push(time, pos, yaw, pitch, flags, weapon, health);
        }
    }

    private void OnShot()
    {
        int shooter = reader.UShort();
        Vector3 end = reader.Pos();
        int weapon = reader.Byte() - 1;
        NetPuppet p;
        if (puppets.TryGetValue(shooter, out p) && p != null)
            p.PlayShot(end, weapon);
    }

    private void OnDamage()
    {
        float amount = reader.UShort() / 10f;
        int attacker = reader.UShort();
        Vector3 from = reader.Pos();
        int hitFlags = reader.Byte();
        bool head = (hitFlags & 1) != 0;
        int how = reader.Byte();
        var player = GameManager.Instance.player;
        if (State != Phase.Playing || player == null || player.isDead)
            return;
        if (attacker != NetProtocol.NoEntity)
        {
            lastAttacker = attacker;
            lastAttackHow = how;
            lastAttackTime = Time.time;
        }
        player.MarkHitFrom(from);
        player.nextHitHead = head;
        HitContext.Pierce = (hitFlags & 2) != 0;
        try
        {
            player.TakeDamage(amount, 1);
        }
        finally
        {
            HitContext.Pierce = false;
        }
    }

    private static readonly string[] HowNames = { "", "TÜFEK", "SMG", "POMPALI", "KESKİN", "TABANCA" };

    private string NameOf(int id)
    {
        if (id == MyId)
            return GameManager.Instance.profile.playerName;
        EntityInfo e;
        return infos.TryGetValue(id, out e) ? e.name : "?";
    }

    private void OnKill()
    {
        int killer = reader.UShort();
        int victim = reader.UShort();
        int how = reader.Byte();
        var gm = GameManager.Instance;
        NetPuppet p;
        if (puppets.TryGetValue(victim, out p) && p != null)
            p.MarkDead();

        string line;
        if (how == NetProtocol.HowLeft)
            line = NameOf(victim) + " oyundan ayrıldı";
        else if (killer == NetProtocol.NoEntity)
            line = NameOf(victim) + " bölgede elendi";
        else
        {
            string weapon = how == NetProtocol.HitGrenade ? "BOMBA" : how > 0 && how < HowNames.Length ? HowNames[how] : "";
            line = NameOf(killer) + (weapon.Length > 0 ? "  [" + weapon + "]  " : "  >  ") + NameOf(victim);
        }
        if (gm.uiManager != null)
            gm.uiManager.AddKillFeed(line);

        if (killer == MyId && victim != MyId && gm.player != null)
        {
            gm.player.kills++;
            Toast("Düşman elendi! (" + gm.player.kills + ")");
            Sfx.Play(SoundBank.Kill, 0.6f);
        }
    }

    private void OnDoor()
    {
        int index = reader.UShort();
        bool open = reader.Bool();
        Vector3 from = reader.Pos();
        if (!doorsMatch || index >= Door.All.Count)
            return;
        var door = Door.All[index];
        if (open)
            door.Open(from);
        else
            door.Close();
    }

    private void OnGrenade()
    {
        reader.UShort();
        Vector3 pos = reader.Pos();
        Vector3 vel = reader.Vec();
        var kind = (ThrowKind)Mathf.Clamp(reader.Byte(), 0, (int)ThrowKind.Gas);
        if (State == Phase.Playing)
            Grenade.ThrowVisual(pos, vel, kind);
    }

    private void OnMatchEnd()
    {
        int winner = reader.Byte();
        reader.String();
        finished = true;
        var gm = GameManager.Instance;
        if (Mode == MatchMode.FreeForAll)
        {
            // Herkes Tek: our place by the scores.
            bool first = winner == myServerTeam;
            int place = first ? 1 : Mathf.Max(2, TeamMatch.Place(0));
            gm.uiManager.Toast(first ? "KAZANDIN!" : place + ". OLDUN");
            StartCoroutine(FinishRoutine(first, place, 2f, session));
            return;
        }
        if (Modes.Arena(Mode))
        {
            // 5v5 / Hakimiyet: the team result, whoever is alive right now.
            bool teamWon = winner == myServerTeam;
            gm.uiManager.Toast(teamWon ? "TAKIMIN KAZANDI!" : winner == 255 ? "BERABERE" : "TAKIMIN KAYBETTİ");
            StartCoroutine(FinishRoutine(teamWon, teamWon ? 1 : 2, 2f, session));
            return;
        }
        if (deathSent || gm.player == null || gm.player.isDead)
            return;   // our result is already on its way
        bool won = winner == myServerTeam;
        StartCoroutine(FinishRoutine(won, won ? 1 : Mathf.Max(2, AliveTeams), 1.5f, session));
    }

    /// <summary>Living teammates on the ground (minimap).</summary>
    public void TeammatePositions(List<Vector3> into)
    {
        into.Clear();
        foreach (var p in puppets.Values)
            if (p != null && p.team == 0 && !p.IsDead && !p.IsAirborne)
                into.Add(p.transform.position);
    }

    // ----- Local player -> server -----

    private void SendState()
    {
        var p = GameManager.Instance.player;
        if (p == null || conn == null || deathSent)
            return;
        int flags = 0;
        if (p.isDead) flags |= NetProtocol.F_Dead;
        if (p.state == PlayerState.Plane) flags |= NetProtocol.F_Plane;
        if (p.state == PlayerState.Freefall) flags |= NetProtocol.F_Freefall;
        if (p.state == PlayerState.Parachute) flags |= NetProtocol.F_Parachute;
        if (p.isCrouching) flags |= NetProtocol.F_Crouch;
        if (p.IsSwimming) flags |= NetProtocol.F_Swim;
        if (p.rig != null && p.rig.aiming) flags |= NetProtocol.F_Aim;
        var cw = p.currentWeapon;
        int weapon = cw != null && cw.gameObject.activeSelf && cw.weaponData != null ? (int)cw.weaponData.weaponType : -1;

        w.Reset();
        w.Byte(NetProtocol.C_State);
        w.UShort(stateSeq++);
        w.Pos(p.transform.position);
        w.Yaw(p.transform.eulerAngles.y);
        w.SByte(Mathf.RoundToInt(p.rig != null ? p.rig.aimPitch : 0f));
        w.Byte(flags);
        w.Byte(weapon + 1);
        w.Byte(Mathf.Clamp(Mathf.CeilToInt(p.health), 0, 255));
        w.Byte(Mathf.Clamp(Mathf.CeilToInt(p.armor), 0, 255));
        conn.SendUnreliable(w.ToArray());
    }

    public void SendShot(WeaponController weapon, Vector3 end)
    {
        if (!InMatch || conn == null || weapon.weaponData == null)
            return;
        w.Reset();
        w.Byte(NetProtocol.C_Shot);
        w.Pos(end);
        w.Byte((int)weapon.weaponData.weaponType + 1);
        conn.SendUnreliable(w.ToArray());
    }

    public void SendHit(NetPuppet target, float amount, bool head, int weapon)
    {
        if (!InMatch || conn == null || deathSent)
            return;
        if (weapon < 0)
        {
            var p = GameManager.Instance.player;
            weapon = p != null && p.currentWeapon.weaponData != null ? (int)p.currentWeapon.weaponData.weaponType : 0;
        }
        w.Reset();
        w.Byte(NetProtocol.C_Hit);
        w.UShort(target.id);
        w.UShort(Mathf.Clamp(Mathf.RoundToInt(amount * 10f), 1, 65535));
        w.Byte(weapon);
        w.Byte((head ? 1 : 0) | (HitContext.Pierce ? 2 : 0));
        conn.SendReliable(w.ToArray());
    }

    public void SendGrenade(Vector3 position, Vector3 velocity, ThrowKind kind)
    {
        if (!InMatch || conn == null)
            return;
        w.Reset();
        w.Byte(NetProtocol.C_Grenade);
        w.Pos(position);
        w.Vec(velocity);
        w.Byte((int)kind);
        conn.SendReliable(w.ToArray());
    }

    public void SendDoor(Door door, Vector3 from)
    {
        if (!InMatch || conn == null || !doorsMatch)
            return;
        int index = Door.All.IndexOf(door);
        if (index < 0)
            return;
        w.Reset();
        w.Byte(NetProtocol.C_Door);
        w.UShort(index);
        w.Bool(door.IsOpen);
        w.Pos(from);
        conn.SendReliable(w.ToArray());
    }

    public void SendPickup(int lootId)
    {
        if (!InMatch || conn == null)
            return;
        w.Reset();
        w.Byte(NetProtocol.C_Pickup);
        w.UShort(lootId);
        conn.SendReliable(w.ToArray());
    }

    /// <summary>GameManager: the local player was eliminated in an online match.</summary>
    public void OnLocalDeath()
    {
        if (!InMatch || deathSent)
            return;
        deathSent = true;
        bool recent = Time.time - lastAttackTime < 15f;
        bool team5 = Modes.Arena(Mode);
        int killer = recent ? lastAttacker : NetProtocol.NoEntity;
        if (conn != null)
        {
            w.Reset();
            w.Byte(NetProtocol.C_Died);
            w.UShort(killer);
            w.Byte(recent ? lastAttackHow : 0);
            conn.SendReliable(w.ToArray());
            conn.Flush(Now, output);
        }
        if (team5)
        {
            // 5v5: the server brings us back (S_Respawn); meanwhile a countdown.
            StartCoroutine(RespawnCountdown(session));
            return;
        }
        StartCoroutine(DeathResultRoutine(session));
    }

    private bool spawnedOnce;

    private IEnumerator RespawnCountdown(int forSession)
    {
        for (int s = Mathf.RoundToInt(TeamMatch.RespawnDelay); s > 0; s--)
        {
            if (forSession != session || !deathSent || State != Phase.Playing)
                yield break;
            Toast("Öldün  •  " + s + " sn sonra yeniden doğacaksın");
            yield return new WaitForSecondsRealtime(1f);
        }
    }

    private IEnumerator DeathResultRoutine(int forSession)
    {
        // The server answers with our placement; wait a moment for it.
        float until = Time.realtimeSinceStartup + 4f;
        while (placement < 0 && Time.realtimeSinceStartup < until && State == Phase.Playing && session == forSession)
            yield return null;
        yield return new WaitForSecondsRealtime(1f);
        int place = placement > 0 ? placement : Mathf.Max(2, AliveTeams + 1);
        int teams = placementTeams > 0 ? placementTeams : teamCount;
        ShowResult(false, place, teams, forSession);
    }

    private IEnumerator FinishRoutine(bool won, int place, float delay, int forSession)
    {
        yield return new WaitForSecondsRealtime(delay);
        ShowResult(won, place, teamCount, forSession);
    }

    private void ShowResult(bool won, int place, int teams, int forSession)
    {
        if (forSession != session)
            return;   // that match is over and we are elsewhere now
        var gm = GameManager.Instance;
        bool show = !resultShown && State == Phase.Playing;
        resultShown = true;
        Leave();
        if (!show)
            return;
        if (gm != null && (gm.currentState == GameState.InGame || gm.currentState == GameState.EndGame))
            gm.EndOnlineMatch(won, Mathf.Clamp(place, 1, Mathf.Max(teams, place)), Mathf.Max(teams, place));
    }

    private void ConnectionLostInMatch(string message)
    {
        bool show = !resultShown;
        resultShown = true;
        Toast(message);
        var gm = GameManager.Instance;
        int teams = teamCount;
        int place = Mathf.Max(2, AliveTeams + 1);
        ClearMatch();
        State = Phase.Idle;
        Revision++;
        if (show && gm != null && gm.currentState == GameState.InGame)
            gm.EndOnlineMatch(false, Mathf.Min(place, Mathf.Max(teams, place)), Mathf.Max(teams, place));
    }

    private static void Toast(string text)
    {
        var gm = GameManager.Instance;
        if (gm != null && gm.uiManager != null)
            gm.uiManager.Toast(text);
    }
}
