using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// The dedicated game server (one process per match, started by Server/orchestrator.py).
/// Waiting room -> countdown -> match -> results, then the process quits.
/// Authority: the server runs the bots, the zone, the plane, loot and doors, checks every hit a phone
/// reports (range, damage, rate) and decides kills and the winner. Each phone moves its own player
/// and owns its health (damage reaches it from here); positions go out in snapshots 20 times a second.
/// </summary>
public sealed class NetServer : MonoBehaviour
{
    public static NetServer Instance;

    private enum Phase { Waiting, Countdown, Playing, Ended }

    private sealed class Peer
    {
        public NetConnection conn;
        public EndPoint ep;
        public uint nonce;
        public int id;
        public string name, skin, para;
        public string account = "", secret = "";
        public bool verified;
        public int voiceThisSecond;
        public double voiceSecond;
        public double nextChat, nextEmote;
        public Entity entity;
        public bool gone;
        public int lastSeq = -1;
        public float damageBudget = 600f;
        public double budgetTime;
        public int shotsThisSecond;
        public double shotSecond;
        public System.Action<byte[], int> output;
    }

    private sealed class Entity
    {
        public int id, team;
        public string name = "", skin = "", para = "";
        public BotAgent agent;
        public ServerHuman human;
        public Peer peer;
        public double respawnAt;   // 5v5: when this dead player comes back (0 = not waiting)
    }

    private bool Tdm { get { return args.mode == MatchMode.Team5; } }
    private int MaxPlayers { get { return Tdm ? TeamMatch.Size * 2 : NetProtocol.MaxHumans; } }
    private readonly int[] teamScore = new int[2];
    private double nextScoreSend;

    private const double PeerTimeout = 15.0;
    private const double LobbyTimeout = 10.0;
    private const float QuickCountdown = 30f;
    private const float FullCountdown = 5f;
    private const float PrivateCountdown = 4f;
    private const double EmptyQuitAfter = 180.0;
    private const double MaxMatchLength = 40 * 60;
    private const int EntitiesPerMessage = 7;   // worst case ~110 bytes each: stays under NetConnection.MaxMessage
    private const int CratesPerMessage = 90;

    private NetGame.ServerArgs args;
    private readonly NetSocket socket = new NetSocket();
    private Phase phase = Phase.Waiting;
    private double countdownEnds;
    private double matchStarted;
    private double lastPeerSeen;
    private double nextSnapshot;
    private double nextLobbyRefresh;
    private double nextEndCheck;
    private double quitAt = -1;
    private ushort snapSeq;
    private int teamCount;
    private string reportedState = "";
    private int reportedPlayers = -1;
    private double nextReport;

    private readonly List<Peer> peers = new List<Peer>();
    private readonly Dictionary<uint, Peer> byToken = new Dictionary<uint, Peer>();
    private readonly Dictionary<uint, Peer> byNonce = new Dictionary<uint, Peer>();
    private readonly List<Entity> entities = new List<Entity>();
    private readonly Dictionary<int, Entity> byId = new Dictionary<int, Entity>();
    private readonly Dictionary<Object, Entity> byObject = new Dictionary<Object, Entity>();
    private int nextHumanId = 1;

    private readonly NetWriter w = new NetWriter(1024);
    private readonly NetReader reader = new NetReader();
    private readonly System.Random rng = new System.Random();
    private Peer current;
    private System.Action<byte[], int, int, bool> deliver;
    private readonly List<LootSystem.CrateInfo> crateScratch = new List<LootSystem.CrateInfo>();

    private static double Now { get { return Time.realtimeSinceStartupAsDouble; } }

    public static NetServer Create(NetGame.ServerArgs a)
    {
        var go = new GameObject("NetServer");
        var s = go.AddComponent<NetServer>();
        s.args = a;
        s.Begin();
        return s;
    }

    private void Begin()
    {
        Instance = this;
        deliver = (buf, off, len, reliable) => Dispatch(current, buf, off, len);
        try
        {
            socket.Open(args.port);
        }
        catch (System.Exception e)
        {
            Debug.LogError("[Sunucu] port " + args.port + " açılamadı: " + e.Message);
            Application.Quit(2);
            return;
        }
        lastPeerSeen = Now;
        Debug.Log("[Sunucu] hazır: port " + args.port + ", kod " + args.code + ", " + NetProtocol.ModeName(args.mode) +
                  (args.privateRoom ? " özel oda" : " hızlı maç") + ", harita " + MapCatalog.Current + ", sürüm " + NetGame.BuildVersion);
        Report(true);
    }

    // ----- Frame -----

    private void Update()
    {
        double now = Now;
        Receive(now);
        Tick(now);

        for (int i = peers.Count - 1; i >= 0; i--)
        {
            var p = peers[i];
            double limit = phase == Phase.Playing ? PeerTimeout : LobbyTimeout;
            if (now - p.conn.LastReceive > limit)
                Drop(p, "bağlantı koptu");
        }
        for (int i = 0; i < peers.Count; i++)
            peers[i].conn.Flush(now, peers[i].output);

        if (now >= nextReport)
            Report(peers.Count > 0 && phase != Phase.Ended);   // keeps friends' "in a room / match" status fresh
        if (quitAt > 0 && now >= quitAt)
            Quit();
    }

    private void Receive(double now)
    {
        byte[] data;
        int len;
        EndPoint from;
        int budget = 4000;
        while (budget-- > 0 && socket.TryReceive(out data, out len, out from))
        {
            if (len < 3 || data[0] != NetConnection.Magic0 || data[1] != NetConnection.Magic1)
                continue;
            byte kind = data[2];
            if (kind == NetConnection.KindHello)
            {
                HandleHello(data, len, from, now);
            }
            else if (kind == NetConnection.KindData)
            {
                Peer p;
                if (!byToken.TryGetValue(NetConnection.PeekToken(data, len), out p) || p.gone)
                    continue;
                if (!from.Equals(p.ep))
                    p.ep = from;   // the phone's address changed (mobile network): the token proves it is the same player
                current = p;
                p.conn.Receive(data, len, now, deliver);
                current = null;
            }
            else if (kind == NetConnection.KindBye && len >= 7)
            {
                uint token = (uint)(data[3] | (data[4] << 8) | (data[5] << 16) | (data[6] << 24));
                Peer p;
                if (byToken.TryGetValue(token, out p))
                    Drop(p, "ayrıldı");
            }
        }
    }

    // ----- Joining -----

    private void HandleHello(byte[] data, int len, EndPoint from, double now)
    {
        reader.Set(data, 3, len - 3);
        try
        {
            int proto = reader.Byte();
            uint nonce = reader.UInt();
            if (proto != NetProtocol.Version)
            {
                // Older / newer game: its hello may be laid out differently, so answer before reading the rest.
                SendReject(from, nonce, "Sürüm uyuşmuyor: oyunu güncelle");
                return;
            }
            string ver = reader.String();
            string code = reader.String();
            string name = CleanName(reader.String());
            string skin = reader.String();
            string para = reader.String();
            string account = reader.String().ToUpperInvariant();
            string secret = reader.String();
            string map = reader.String();

            Peer existing;
            if (byNonce.TryGetValue(nonce, out existing) && !existing.gone)
            {
                if (existing.ep.Equals(from))
                    SendWelcome(existing);   // our welcome was lost
                return;
            }

            string why = null;
            if (proto != NetProtocol.Version || !NetGame.VersionsMatch(ver, NetGame.BuildVersion))
                why = "Sürüm uyuşmuyor: oyunu güncelle";
            else if (code != args.code)
                why = "Oda bulunamadı";
            else if (map != MapCatalog.Current)
                why = "Bu oda başka bir haritada: " + MapCatalog.CurrentInfo.name;
            else if (phase == Phase.Playing || phase == Phase.Ended)
                why = "Bu maç başladı";
            else if (peers.Count >= MaxPlayers)
                why = "Oda dolu";
            if (why != null)
            {
                SendReject(from, nonce, why);
                return;
            }

            uint token;
            do
            {
                token = (uint)rng.Next(1, int.MaxValue) ^ ((uint)rng.Next(0, 2) << 31);
            } while (token == 0 || byToken.ContainsKey(token));

            var p = new Peer
            {
                conn = new NetConnection(token, now),
                ep = from,
                nonce = nonce,
                id = nextHumanId++,
                name = name,
                skin = ValidSkin(skin),
                para = para.Length <= 32 ? para : "",
                account = Token(account, 12),
                secret = Token(secret, 64),
                budgetTime = now
            };
            p.output = (buf, n) => socket.Send(buf, n, p.ep);
            foreach (var other in peers)
                if (other.name == p.name)
                    p.name = (p.name.Length > 12 ? p.name.Substring(0, 12) : p.name) + " " + p.id;   // two "Tilki42"s
            peers.Add(p);
            byToken[token] = p;
            byNonce[nonce] = p;
            lastPeerSeen = now;
            SendWelcome(p);
            Debug.Log("[Sunucu] katıldı: " + p.name + " (#" + p.id + ")  oyuncu " + peers.Count);

            if (!args.privateRoom)
            {
                if (phase == Phase.Waiting)
                    StartCountdown(QuickCountdown);
                else if (phase == Phase.Countdown && peers.Count >= MaxPlayers)
                    countdownEnds = System.Math.Min(countdownEnds, now + FullCountdown);
            }
            SendLobbyToAll(now);
            Report(true);
            if (!string.IsNullOrEmpty(args.api))
                StartCoroutine(Verify(p));
            else
                p.verified = true;   // local test without the matchmaker
        }
        catch (NetFormatException) { }
    }

    /// <summary>Asks the matchmaker whether the player's account is real and not banned; uses its name.</summary>
    private IEnumerator Verify(Peer p)
    {
        if (p.account.Length == 0)
        {
            Kick(p, "Hesap gerekli: oyunu yeniden başlat");
            yield break;
        }
        string json = "{\"id\":\"" + p.account + "\",\"secret\":\"" + p.secret + "\"}";
        var req = new UnityWebRequest(args.api.TrimEnd('/') + "/verify", "POST");
        req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        req.timeout = 5;
        yield return req.SendWebRequest();
        string text = req.result == UnityWebRequest.Result.Success ? req.downloadHandler.text : "";
        req.Dispose();
        if (p.gone)
            yield break;
        if (text.Length == 0)
        {
            p.verified = true;   // matchmaker unreachable: don't punish the player for it
            yield break;
        }
        var answer = JsonUtility.FromJson<VerifyAnswer>(text);
        if (answer == null || !answer.ok)
        {
            Kick(p, answer != null && !string.IsNullOrEmpty(answer.error) ? answer.error : "Hesap doğrulanamadı");
            yield break;
        }
        p.verified = true;
        string name = CleanName(answer.name);
        if (name != p.name && phase != Phase.Playing)
        {
            p.name = name;
            foreach (var other in peers)
                if (other != p && other.name == p.name)
                    p.name = (p.name.Length > 12 ? p.name.Substring(0, 12) : p.name) + " " + p.id;
            SendLobbyToAll(Now);
        }
    }

    [System.Serializable]
    private class VerifyAnswer
    {
        public bool ok;
        public string name = "";
        public string error = "";
    }

    private void Kick(Peer p, string reason)
    {
        w.Reset();
        w.Byte(NetProtocol.S_Toast);
        w.String(reason, 160);
        p.conn.SendReliable(w.ToArray());
        p.conn.Flush(Now, p.output);
        Drop(p, "atıldı: " + reason);
    }

    private static string CleanName(string s)
    {
        if (string.IsNullOrEmpty(s))
            return "Oyuncu";
        var sb = new StringBuilder();
        foreach (char c in s)
        {
            if (!char.IsControl(c) && c != '<' && c != '>')   // no rich-text tags in other players' screens
                sb.Append(c);
            if (sb.Length >= 16)
                break;
        }
        string r = sb.ToString().Trim();
        return r.Length > 0 ? r : "Oyuncu";
    }

    /// <summary>Only letters, digits, '-' and '_' (account ids and secrets go into JSON).</summary>
    private static string Token(string s, int max)
    {
        var sb = new StringBuilder();
        foreach (char c in s ?? "")
            if (((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '-' || c == '_') && sb.Length < max)
                sb.Append(c);
        return sb.ToString();
    }

    private static string ValidSkin(string s)
    {
        foreach (var k in ModelLibrary.ShopSkins)
            if (k == s)
                return s;
        return ModelLibrary.PlayerSkin;
    }

    private void SendWelcome(Peer p)
    {
        w.Reset();
        w.Byte(NetConnection.Magic0);
        w.Byte(NetConnection.Magic1);
        w.Byte(NetConnection.KindWelcome);
        w.UInt(p.nonce);
        w.UInt(p.conn.Token);
        w.UShort(p.id);
        socket.Send(w.Buffer, w.Length, p.ep);
    }

    private void SendReject(EndPoint to, uint nonce, string reason)
    {
        w.Reset();
        w.Byte(NetConnection.Magic0);
        w.Byte(NetConnection.Magic1);
        w.Byte(NetConnection.KindReject);
        w.UInt(nonce);
        w.String(reason, 120);
        socket.Send(w.Buffer, w.Length, to);
    }

    private void SendBye(Peer p)
    {
        w.Reset();
        w.Byte(NetConnection.Magic0);
        w.Byte(NetConnection.Magic1);
        w.Byte(NetConnection.KindBye);
        w.UInt(p.conn.Token);
        socket.Send(w.Buffer, w.Length, p.ep);
    }

    private void Drop(Peer p, string reason)
    {
        if (p.gone)
            return;
        p.gone = true;
        SendBye(p);
        peers.Remove(p);
        byToken.Remove(p.conn.Token);
        byNonce.Remove(p.nonce);
        lastPeerSeen = Now;
        Debug.Log("[Sunucu] " + p.name + " (#" + p.id + ") " + reason + "  oyuncu " + peers.Count);

        if (phase == Phase.Playing && p.entity != null && p.entity.human != null && !p.entity.human.dead)
            HumanDied(p.entity, null, NetProtocol.HowLeft);
        if (phase == Phase.Waiting || phase == Phase.Countdown)
        {
            if (peers.Count == 0)
                phase = Phase.Waiting;
            SendLobbyToAll(Now);
        }
        if (phase == Phase.Playing)
            CheckEnd();
        Report(true);
    }

    private bool IsLeader(Peer p)
    {
        return peers.Count > 0 && peers[0] == p;
    }

    // ----- Phases -----

    private void StartCountdown(float seconds)
    {
        phase = Phase.Countdown;
        countdownEnds = Now + seconds;
    }

    private void Tick(double now)
    {
        switch (phase)
        {
            case Phase.Waiting:
                if (peers.Count == 0 && now - lastPeerSeen > EmptyQuitAfter)
                {
                    Debug.Log("[Sunucu] kimse gelmedi, kapanıyor");
                    Quit();
                }
                break;
            case Phase.Countdown:
                if (peers.Count == 0)
                {
                    phase = Phase.Waiting;
                    break;
                }
                if (now >= countdownEnds)
                    BeginMatch(now);
                else
                {
                    if (now >= nextLobbyRefresh)
                        SendLobbyToAll(now);
                    if (countdownEnds - now < 6.0 && reportedState == "waiting")
                        Report(true);   // "starting": the matchmaker stops sending players here
                }
                break;
            case Phase.Playing:
                if (now >= nextSnapshot)
                {
                    // Keep the 20 Hz rhythm on a 30 fps server (frames of 33 ms), without bursts after a hitch.
                    nextSnapshot = System.Math.Max(nextSnapshot + NetProtocol.TickInterval, now - NetProtocol.TickInterval);
                    SendSnapshot(now);
                }
                if (Tdm)
                {
                    foreach (var e in entities)
                        if (e.respawnAt > 0 && now >= e.respawnAt && e.peer != null && !e.peer.gone)
                            RespawnHuman(e);
                    if (now >= nextScoreSend)
                        BroadcastScore();
                }
                if (now >= nextEndCheck)
                {
                    nextEndCheck = now + 0.5;
                    CheckEnd();
                    if (phase == Phase.Playing && now - matchStarted > MaxMatchLength)
                        EndMatch(-1);
                }
                break;
        }
    }

    private void SendLobbyToAll(double now)
    {
        nextLobbyRefresh = now + 5.0;
        w.Reset();
        w.Byte(NetProtocol.S_Lobby);
        w.Byte(phase == Phase.Countdown ? NetProtocol.LobbyCountdown : NetProtocol.LobbyWaiting);
        w.Float(phase == Phase.Countdown ? (float)System.Math.Max(0.0, countdownEnds - now) : -1f);
        w.Byte((int)args.mode);
        w.Bool(args.privateRoom);
        w.UShort(peers.Count > 0 ? peers[0].id : 0);
        w.String(args.code, 16);
        w.Byte(peers.Count);
        foreach (var p in peers)
        {
            w.UShort(p.id);
            w.String(p.name, 40);
            w.String(p.account, 12);
        }
        Broadcast(w.ToArray(), true, null);
    }

    private void BeginMatch(double now)
    {
        try
        {
            StartRound(now);
        }
        catch (System.Exception e)
        {
            Debug.LogException(e);
            Debug.LogError("[Sunucu] maç başlatılamadı, kapanıyor");
            w.Reset();
            w.Byte(NetProtocol.S_Toast);
            w.String("Sunucu hatası: maç başlatılamadı", 120);
            Broadcast(w.ToArray(), true, null);
            foreach (var p in peers)
                p.conn.Flush(Now, p.output);
            phase = Phase.Ended;
            quitAt = Now + 1.0;
            Report(true);
        }
    }

    private void StartRound(double now)
    {
        var gm = GameManager.Instance;
        phase = Phase.Playing;
        // Match ids: players 1..16 (join order), bots from 100.
        for (int i = 0; i < peers.Count; i++)
            peers[i].id = i + 1;
        matchStarted = now;
        nextSnapshot = now;
        nextEndCheck = now + 5.0;

        int teamSize = NetProtocol.TeamSize(args.mode);
        var humanTeam = new int[peers.Count];
        int botCount;
        if (Tdm)
        {
            // 5v5: a private room is a group — together on one side (more than five: the rest on the other);
            // a quick match spreads the players over both sides. Bots fill each side to five.
            var onTeam = new int[2];
            for (int i = 0; i < peers.Count; i++)
            {
                humanTeam[i] = args.privateRoom ? (i < TeamMatch.Size ? 0 : 1) : i % 2;
                onTeam[humanTeam[i]]++;
            }
            int bots0 = Mathf.Max(0, TeamMatch.Size - onTeam[0]), bots1 = Mathf.Max(0, TeamMatch.Size - onTeam[1]);
            botCount = bots0 + bots1;
            teamScore[0] = teamScore[1] = 0;
            gm.BeginServerTeamRound(bots0, bots1);
            teamCount = 2;
        }
        else
        {
            for (int i = 0; i < peers.Count; i++)
                humanTeam[i] = i / teamSize;
            int humanTeams = (peers.Count + teamSize - 1) / teamSize;
            botCount = Mathf.Max(0, GameManager.PlayersPerMatch - humanTeams * teamSize);
            gm.BeginServerRound(args.mode, humanTeams, botCount);
            teamCount = humanTeams + (botCount + teamSize - 1) / teamSize;
        }

        entities.Clear();
        byId.Clear();
        byObject.Clear();
        Vector3 start = gm.plane != null ? gm.plane.transform.position : Vector3.up * 170f;
        for (int i = 0; i < peers.Count; i++)
        {
            var p = peers[i];
            var human = ServerHuman.Create(p.id, humanTeam[i], p.name, start);
            gm.Combatants.Add(human);
            var e = new Entity { id = p.id, team = human.team, name = p.name, skin = p.skin, para = p.para, human = human, peer = p };
            p.entity = e;
            p.lastSeq = -1;
            AddEntity(e, human);
        }
        int botId = 100;
        foreach (var bot in gm.bots)
        {
            var e = new Entity { id = botId++, team = bot.team, name = bot.botName, skin = bot.skin ?? "", agent = bot };
            AddEntity(e, bot);
            if (bot.weapon != null)
                byObject[bot.weapon] = e;
        }

        // Everyone gets the same entity list and crates; the start message is per player.
        var entityMessages = new List<byte[]>();
        for (int i = 0; i < entities.Count; i += EntitiesPerMessage)
        {
            int n = Mathf.Min(EntitiesPerMessage, entities.Count - i);
            w.Reset();
            w.Byte(NetProtocol.S_Entities);
            w.Byte(n);
            for (int k = i; k < i + n; k++)
            {
                var e = entities[k];
                w.UShort(e.id);
                w.Byte(e.team);
                w.Bool(e.agent != null);
                w.String(e.name, 40);
                w.String(e.skin, 32);
                w.String(e.para, 32);
                w.String(e.peer != null ? e.peer.account : "", 12);
            }
            entityMessages.Add(w.ToArray());
        }
        gm.lootSystem.GetCrates(crateScratch);
        var lootMessages = new List<byte[]>();
        for (int i = 0; i < crateScratch.Count; i += CratesPerMessage)
        {
            int n = Mathf.Min(CratesPerMessage, crateScratch.Count - i);
            lootMessages.Add(LootMessage(crateScratch, i, n));
        }

        var z = gm.safeZone;
        foreach (var p in peers)
        {
            foreach (var m in entityMessages)
                p.conn.SendReliable(m);
            w.Reset();
            w.Byte(NetProtocol.S_MatchStart);
            w.UShort(p.id);
            w.Byte(p.entity.team);
            w.Byte((int)args.mode);
            w.Pos(gm.plane != null ? gm.plane.start : Vector3.zero);
            w.Pos(gm.plane != null ? gm.plane.end : Vector3.forward);
            w.Float(z.center.x);
            w.Float(z.center.z);
            w.Float(z.radius);
            w.UShort(Door.All.Count);
            w.Byte(teamCount);
            p.conn.SendReliable(w.ToArray());
            foreach (var m in lootMessages)
                p.conn.SendReliable(m);
        }
        if (Tdm)
        {
            // Everyone on the ground at their team's side.
            foreach (var p in peers)
                RespawnHuman(p.entity);
            BroadcastScore();
        }
        Debug.Log("[Sunucu] maç başladı: " + peers.Count + " oyuncu, " + botCount + " bot, " + teamCount + " takım, " + crateScratch.Count + " sandık");
        Report(true);
    }

    private void AddEntity(Entity e, Object key)
    {
        entities.Add(e);
        byId[e.id] = e;
        byObject[key] = e;
    }

    private byte[] LootMessage(List<LootSystem.CrateInfo> list, int start, int count)
    {
        w.Reset();
        w.Byte(NetProtocol.S_Loot);
        w.UShort(count);
        for (int k = start; k < start + count; k++)
        {
            w.UShort(list[k].id);
            w.Byte((int)list[k].type);
            w.Pos(list[k].position);
        }
        return w.ToArray();
    }

    private static bool IsAlive(Entity e)
    {
        if (e.agent != null)
            return !e.agent.isDead;
        return e.human != null && !e.human.dead && e.peer != null && !e.peer.gone;
    }

    private static bool InAir(Entity e)
    {
        return e.agent != null ? e.agent.IsAirborne : e.human != null && e.human.IsAirborne;
    }

    private static Vector3 PositionOf(Entity e)
    {
        return e.agent != null ? e.agent.transform.position : e.human.transform.position;
    }

    private int AliveTeams(out int winner, out int humansAlive)
    {
        var teams = new HashSet<int>();
        winner = -1;
        humansAlive = 0;
        foreach (var e in entities)
        {
            if (!IsAlive(e))
                continue;
            teams.Add(e.team);
            winner = e.team;
            if (e.human != null)
                humansAlive++;
        }
        return teams.Count;
    }

    private void CheckEnd()
    {
        if (phase != Phase.Playing)
            return;
        if (Tdm)
        {
            bool anyone = false;
            foreach (var p in peers)
                anyone |= !p.gone;
            if (!anyone)
                EndMatch(-1);
            else if (teamScore[0] >= TeamMatch.ScoreToWin || teamScore[1] >= TeamMatch.ScoreToWin || Now - matchStarted >= TeamMatch.Duration)
            {
                BroadcastScore();
                EndMatch(teamScore[0] > teamScore[1] ? 0 : teamScore[1] > teamScore[0] ? 1 : -1);
            }
            return;
        }
        int winner, humansAlive;
        int alive = AliveTeams(out winner, out humansAlive);
        if (alive <= 1)
            EndMatch(alive == 1 ? winner : -1);
        else if (humansAlive == 0)
            EndMatch(-1);   // only bots left: nobody is watching
    }

    private void EndMatch(int winnerTeam)
    {
        if (phase == Phase.Ended)
            return;
        phase = Phase.Ended;
        var gm = GameManager.Instance;
        gm.currentState = GameState.EndGame;
        gm.safeZone.Stop();

        var names = new List<string>();
        foreach (var e in entities)
            if (e.team == winnerTeam)
                names.Add(e.name);
        w.Reset();
        w.Byte(NetProtocol.S_MatchEnd);
        w.Byte(winnerTeam >= 0 ? winnerTeam : 255);
        w.String(string.Join(", ", names.ToArray()), 200);
        Broadcast(w.ToArray(), true, null);
        Debug.Log("[Sunucu] maç bitti, kazanan takım " + winnerTeam + ": " + string.Join(", ", names.ToArray()));
        quitAt = Now + 12.0;
        Report(true);
    }

    private void Quit()
    {
        foreach (var p in peers)
            SendBye(p);
        socket.Close();
        Debug.Log("[Sunucu] kapanıyor");
        Application.Quit(0);
        quitAt = -1;
        enabled = false;
    }

    // ----- Messages from phones -----

    private void Dispatch(Peer p, byte[] buf, int off, int len)
    {
        if (p == null || p.gone)
            return;
        reader.Set(buf, off, len);
        try
        {
            int type = reader.Byte();
            switch (type)
            {
                case NetProtocol.C_Start:
                    if (args.privateRoom && phase == Phase.Waiting && IsLeader(p))
                    {
                        StartCountdown(PrivateCountdown);
                        SendLobbyToAll(Now);
                    }
                    break;
                case NetProtocol.C_State: OnState(p); break;
                case NetProtocol.C_Shot: OnShot(p); break;
                case NetProtocol.C_Hit: OnHit(p); break;
                case NetProtocol.C_Died: OnDied(p); break;
                case NetProtocol.C_Pickup: OnPickup(p); break;
                case NetProtocol.C_Door: OnDoor(p); break;
                case NetProtocol.C_Grenade: OnGrenade(p); break;
                case NetProtocol.C_Voice: OnVoice(p, buf, off, len); break;
                case NetProtocol.C_Chat: OnChat(p); break;
                case NetProtocol.C_Emote: OnEmote(p); break;
            }
        }
        catch (NetFormatException) { }
        catch (System.Exception e)
        {
            Debug.LogException(e);
        }
    }

    private bool Playing(Peer p)
    {
        return phase == Phase.Playing && p.entity != null && p.entity.human != null && !p.entity.human.dead;
    }

    private void OnState(Peer p)
    {
        int seq = reader.UShort();
        Vector3 pos = reader.Pos();
        float yaw = reader.Yaw();
        float pitch = reader.SByte();
        int flags = reader.Byte();
        int weapon = reader.Byte() - 1;
        int hp = reader.Byte();
        int ap = reader.Byte();
        if (!Playing(p))
            return;
        if (p.lastSeq >= 0 && !NetConnection.SeqBefore((ushort)p.lastSeq, (ushort)seq))
            return;   // older than what we have
        p.lastSeq = seq;
        p.entity.human.ApplyState(pos, yaw, pitch, flags, weapon, hp, ap);
        p.entity.human.lastState = Now;
    }

    private void OnShot(Peer p)
    {
        Vector3 end = reader.Pos();
        int weapon = reader.Byte();
        if (!Playing(p))
            return;
        double now = Now;
        if (now - p.shotSecond >= 1.0)
        {
            p.shotSecond = now;
            p.shotsThisSecond = 0;
        }
        if (++p.shotsThisSecond > 30)
            return;
        w.Reset();
        w.Byte(NetProtocol.S_Shot);
        w.UShort(p.id);
        w.Pos(end);
        w.Byte(weapon);
        Broadcast(w.ToArray(), false, p);
    }

    private void OnHit(Peer p)
    {
        int targetId = reader.UShort();
        float damage = reader.UShort() / 10f;
        int weapon = reader.Byte();
        bool head = reader.Bool();
        if (!Playing(p))
            return;
        var me = p.entity;
        Entity target;
        if (!byId.TryGetValue(targetId, out target) || target == me || !IsAlive(target) || target.team == me.team || InAir(target))
            return;
        float max = NetProtocol.MaxHitDamage(weapon);
        if (max <= 0f || damage <= 0f)
            return;
        damage = Mathf.Min(damage, max);
        Vector3 from = me.human.transform.position;
        float dist = Vector3.Distance(from, PositionOf(target));
        if (dist > NetProtocol.MaxHitRange(weapon))
        {
            Debug.Log("[Sunucu] menzil dışı vuruş reddedildi: " + p.name + " " + Mathf.RoundToInt(dist) + " m");
            return;
        }
        double now = Now;
        p.damageBudget = Mathf.Min(900f, p.damageBudget + (float)(now - p.budgetTime) * 500f);
        p.budgetTime = now;
        if (damage > p.damageBudget)
            return;   // faster than any gun can shoot
        p.damageBudget -= damage;

        if (target.agent != null)
        {
            HitContext.Set(me.human, from, head, weapon);
            try
            {
                target.agent.TakeDamage(damage, me.team);
            }
            finally
            {
                HitContext.Clear();
            }
        }
        else if (target.peer != null && !target.peer.gone)
        {
            SendDamage(target.peer, damage, me.id, from, head, weapon);
        }
    }

    private void SendDamage(Peer to, float amount, int attackerId, Vector3 from, bool head, int weapon)
    {
        w.Reset();
        w.Byte(NetProtocol.S_Damage);
        w.UShort(Mathf.Clamp(Mathf.RoundToInt(amount * 10f), 1, 65535));
        w.UShort(attackerId);
        w.Pos(from);
        w.Bool(head);
        w.Byte(HowCode(weapon));
        to.conn.SendReliable(w.ToArray());
    }

    /// <summary>Kill-feed code: weapon type + 1, HitGrenade, or 0 (unknown).</summary>
    private static int HowCode(int weapon)
    {
        if (weapon == NetProtocol.HitGrenade)
            return NetProtocol.HitGrenade;
        return weapon >= 0 && weapon <= (int)WeaponType.Pistol ? weapon + 1 : 0;
    }

    private void OnDied(Peer p)
    {
        int killerId = reader.UShort();
        int how = reader.Byte();   // what the last damage we sent this phone was
        if (!Playing(p))
            return;
        Entity killer;
        if (!byId.TryGetValue(killerId, out killer) || killer == p.entity || killer.team == p.entity.team)
            killer = null;   // no credit for yourself or a teammate (they can't hurt you)
        if (killer == null)
            how = 0;
        else if (how != NetProtocol.HitGrenade && (how < 1 || how > (int)WeaponType.Pistol + 1))
            how = 0;
        HumanDied(p.entity, killer, how);
    }

    private void HumanDied(Entity e, Entity killer, int how)
    {
        var h = e.human;
        if (h == null || h.dead)
            return;
        bool wasInAir = h.IsAirborne;
        Vector3 at = h.transform.position;
        h.MarkDead();
        Kill(killer, e, how);
        if (Tdm)
        {
            e.respawnAt = Now + TeamMatch.RespawnDelay;
            CheckEnd();
            return;
        }
        var gm = GameManager.Instance;
        if (!wasInAir && how != NetProtocol.HowLeft && gm.lootSystem != null)
            gm.lootSystem.DropDeathCrate(at);

        if (e.peer != null && !e.peer.gone)
        {
            int winner, humansAlive;
            int alive = AliveTeams(out winner, out humansAlive);
            bool teamAlive = false;
            foreach (var o in entities)
                if (o.team == e.team && IsAlive(o))
                    teamAlive = true;
            w.Reset();
            w.Byte(NetProtocol.S_Placement);
            w.Byte(teamAlive ? alive : alive + 1);
            w.Byte(teamCount);
            e.peer.conn.SendReliable(w.ToArray());
        }
        CheckEnd();
    }

    private void Kill(Entity killer, Entity victim, int how)
    {
        if (Tdm && killer != null && killer.team != victim.team && killer.team >= 0 && killer.team < 2)
        {
            teamScore[killer.team]++;
            nextScoreSend = 0;   // send the new score with the next tick
        }
        w.Reset();
        w.Byte(NetProtocol.S_Kill);
        w.UShort(killer != null ? killer.id : NetProtocol.NoEntity);
        w.UShort(victim.id);
        w.Byte(how);
        Broadcast(w.ToArray(), true, null);
    }

    private void OnPickup(Peer p)
    {
        int id = reader.UShort();
        if (!Playing(p))
            return;
        var gm = GameManager.Instance;
        Vector3 at;
        bool ok = gm.lootSystem.CratePosition(id, out at) &&                       // still there (nobody was faster)
                  (at - p.entity.human.transform.position).magnitude <= 6f;       // and within reach
        w.Reset();
        w.Byte(NetProtocol.S_LootTaken);
        w.UShort(id);
        w.Bool(ok);
        p.conn.SendReliable(w.ToArray());
        if (!ok)
            return;
        gm.lootSystem.RemoveById(id);
        w.Reset();
        w.Byte(NetProtocol.S_LootGone);
        w.UShort(id);
        Broadcast(w.ToArray(), true, p);
    }

    private void OnDoor(Peer p)
    {
        int index = reader.UShort();
        bool open = reader.Bool();
        Vector3 from = reader.Pos();
        if (!Playing(p) || index < 0 || index >= Door.All.Count)
            return;
        var door = Door.All[index];
        Vector3 me = p.entity.human.transform.position;
        if (Vector3.Distance(me, door.Center) > 6f)
        {
            // Too far by our positions: put the requester's door back the way it really is.
            w.Reset();
            w.Byte(NetProtocol.S_Door);
            w.UShort(index);
            w.Bool(door.IsOpen);
            w.Pos(door.Center);
            p.conn.SendReliable(w.ToArray());
            return;
        }
        if (open)
            door.Open(from);
        else
            door.Close();
    }

    private void OnGrenade(Peer p)
    {
        Vector3 pos = reader.Pos();
        Vector3 vel = reader.Vec();
        if (!Playing(p) || vel.magnitude > 60f)
            return;
        WriteGrenade(p.id, pos, vel);
        Broadcast(w.ToArray(), true, p);
    }

    /// <summary>
    /// Voice: in a private room's waiting room everyone hears everyone (friends); in the match only
    /// living teammates hear each other. Each phone mutes the players it blocked.
    /// </summary>
    private void OnVoice(Peer p, byte[] buf, int off, int len)
    {
        int payload = len - 1;
        if (payload < 4 || payload > NetProtocol.MaxVoiceBytes || !p.verified)
            return;
        double now = Now;
        if (now - p.voiceSecond >= 1.0)
        {
            p.voiceSecond = now;
            p.voiceThisSecond = 0;
        }
        if (++p.voiceThisSecond > 40)
            return;
        bool lobby = phase == Phase.Waiting || phase == Phase.Countdown;
        if (lobby && !args.privateRoom)
            return;
        if (!lobby && (p.entity == null || p.entity.human == null || p.entity.human.dead || phase != Phase.Playing))
            return;
        w.Reset();
        w.Byte(NetProtocol.S_Voice);
        w.UShort(p.id);
        w.Bytes(buf, off + 1, payload);
        byte[] msg = null;
        foreach (var o in peers)
        {
            if (o == p || o.gone)
                continue;
            if (!lobby && (o.entity == null || o.entity.team != p.entity.team))
                continue;
            if (msg == null)
                msg = w.ToArray();
            o.conn.SendUnreliable(msg);
        }
    }

    /// <summary>Who hears a room message: everyone in the waiting room; in a match, the sender's team.</summary>
    private void RoomBroadcast(Peer from, byte[] msg)
    {
        bool lobby = phase == Phase.Waiting || phase == Phase.Countdown;
        foreach (var o in peers)
        {
            if (o.gone)
                continue;
            if (!lobby && (o.entity == null || from.entity == null || o.entity.team != from.entity.team))
                continue;
            o.conn.SendReliable(msg);
        }
    }

    private void OnChat(Peer p)
    {
        string text = NetChat.Clean(reader.String());
        double now = Now;
        if (text.Length == 0 || now < p.nextChat)
            return;
        p.nextChat = now + 0.8;
        w.Reset();
        w.Byte(NetProtocol.S_Chat);
        w.UShort(p.id);
        w.String(p.name, 40);
        w.String(p.account, 12);
        w.String(text, NetChat.MaxChat * 2);
        RoomBroadcast(p, w.ToArray());
    }

    private void OnEmote(Peer p)
    {
        int emote = reader.Byte();
        double now = Now;
        if (emote >= NetChat.Emotes.Length || now < p.nextEmote)
            return;
        p.nextEmote = now + 1.0;
        w.Reset();
        w.Byte(NetProtocol.S_Emote);
        w.UShort(p.id);
        w.String(p.name, 40);
        w.Byte(emote);
        RoomBroadcast(p, w.ToArray());
    }

    private void WriteGrenade(int thrower, Vector3 pos, Vector3 vel)
    {
        w.Reset();
        w.Byte(NetProtocol.S_Grenade);
        w.UShort(thrower);
        w.Pos(pos);
        w.Vec(vel);
    }

    // ----- Hooks from the game systems (server side) -----

    public void OnWeaponFired(WeaponController weapon, Vector3 end)
    {
        Entity e;
        if (phase != Phase.Playing || weapon == null || weapon.weaponData == null || !byObject.TryGetValue(weapon, out e))
            return;
        w.Reset();
        w.Byte(NetProtocol.S_Shot);
        w.UShort(e.id);
        w.Pos(end);
        w.Byte((int)weapon.weaponData.weaponType + 1);
        Broadcast(w.ToArray(), false, null);
    }

    public void OnGrenadeThrown(Vector3 position, Vector3 velocity, IDamageable owner)
    {
        Entity e;
        var key = owner as Object;
        if (phase != Phase.Playing || key == null || !byObject.TryGetValue(key, out e) || e.agent == null)
            return;
        WriteGrenade(e.id, position, velocity);
        Broadcast(w.ToArray(), true, null);
    }

    public void OnDoorChanged(Door door, Vector3 from)
    {
        if (phase != Phase.Playing)
            return;
        int index = Door.All.IndexOf(door);
        if (index < 0)
            return;
        w.Reset();
        w.Byte(NetProtocol.S_Door);
        w.UShort(index);
        w.Bool(door.IsOpen);
        w.Pos(from);
        Broadcast(w.ToArray(), true, null);
    }

    public void OnCrateAdded(int id, LootType type, Vector3 position)
    {
        if (phase != Phase.Playing)
            return;
        crateScratch.Clear();
        crateScratch.Add(new LootSystem.CrateInfo { id = id, type = type, position = position });
        Broadcast(LootMessage(crateScratch, 0, 1), true, null);
    }

    /// <summary>A bot or a player's stand-in took damage on the server; the attacker is in HitContext.</summary>
    public void OnHumanDamaged(ServerHuman human, float amount, bool zone)
    {
        Entity e;
        if (phase != Phase.Playing || zone || !byObject.TryGetValue(human, out e) || e.peer == null || e.peer.gone)
            return;
        Entity attacker = null;
        var key = HitContext.Attacker as Object;
        if (key != null)
            byObject.TryGetValue(key, out attacker);
        Vector3 from = HitContext.Attacker != null ? HitContext.From : human.transform.position;
        SendDamage(e.peer, amount, attacker != null ? attacker.id : NetProtocol.NoEntity, from, HitContext.Head, HitContext.Weapon);
    }

    public void OnBotEliminated(BotAgent bot)
    {
        Entity e;
        if (phase != Phase.Playing || !byObject.TryGetValue(bot, out e))
            return;
        Entity killer = null;
        var key = HitContext.Attacker as Object;
        if (key != null)
            byObject.TryGetValue(key, out killer);
        Kill(killer, e, HowCode(HitContext.Weapon));
        CheckEnd();
    }

    // ----- 5v5 -----

    private void BroadcastScore()
    {
        nextScoreSend = Now + 3.0;
        TeamMatch.Score[0] = teamScore[0];
        TeamMatch.Score[1] = teamScore[1];
        w.Reset();
        w.Byte(NetProtocol.S_Score);
        w.UShort(teamScore[0]);
        w.UShort(teamScore[1]);
        w.Float((float)System.Math.Max(0.0, TeamMatch.Duration - (Now - matchStarted)));
        Broadcast(w.ToArray(), true, null);
    }

    /// <summary>A player (back) on the ground at their team's side; every phone is told (that one spawns there).</summary>
    private void RespawnHuman(Entity e)
    {
        if (e == null || e.human == null)
            return;
        e.respawnAt = 0;
        Vector3 at = TeamMatch.SpawnPoint(e.team);
        e.human.Revive(at + Vector3.up * 0.95f);
        if (e.peer != null)
            e.peer.lastSeq = -1;
        SendRespawn(e.id, at);
    }

    private void SendRespawn(int id, Vector3 ground)
    {
        w.Reset();
        w.Byte(NetProtocol.S_Respawn);
        w.UShort(id);
        w.Float((float)(Now - matchStarted));
        w.Pos(ground);
        Broadcast(w.ToArray(), true, null);
    }

    public void OnBotRespawned(BotAgent bot)
    {
        Entity e;
        if (phase != Phase.Playing || !byObject.TryGetValue(bot, out e))
            return;
        SendRespawn(e.id, bot.transform.position - Vector3.up * 0.95f);
    }

    // ----- Snapshots -----

    private void SendSnapshot(double now)
    {
        var gm = GameManager.Instance;
        int winner, humansAlive;
        int teamsAlive = AliveTeams(out winner, out humansAlive);
        int alive = 0;
        foreach (var e in entities)
            if (IsAlive(e))
                alive++;

        w.Reset();
        w.Byte(NetProtocol.S_Snap);
        w.UShort(snapSeq++);
        w.Float((float)(now - matchStarted));
        w.Byte(alive);
        w.Byte(teamsAlive);
        var z = gm.safeZone;
        w.Float(z.center.x);
        w.Float(z.center.z);
        w.Float(z.radius);
        w.Float(z.NextCenter.x);
        w.Float(z.NextCenter.z);
        w.Float(z.NextRadius);
        w.Float(z.Timer);
        w.Byte(z.Phase);
        w.Byte((z.active ? 1 : 0) | (z.Shrinking ? 2 : 0) | (z.Finished ? 4 : 0));
        w.Byte(entities.Count);
        foreach (var e in entities)
        {
            Vector3 pos;
            float yaw, pitch, health;
            int flags, weapon;
            if (e.agent != null)
            {
                var b = e.agent;
                pos = b.transform.position;
                yaw = b.transform.eulerAngles.y;
                pitch = 0f;
                flags = (b.isDead ? NetProtocol.F_Dead : 0) |
                        (b.air == BotAir.Plane ? NetProtocol.F_Plane : 0) |
                        (b.air == BotAir.Freefall ? NetProtocol.F_Freefall : 0) |
                        (b.air == BotAir.Parachute ? NetProtocol.F_Parachute : 0) |
                        (b.rig != null && b.rig.aiming ? NetProtocol.F_Aim : 0);
                weapon = b.weapon != null && b.weapon.gameObject.activeSelf && b.weapon.weaponData != null ? (int)b.weapon.weaponData.weaponType : -1;
                health = b.health;
            }
            else
            {
                var h = e.human;
                pos = h.transform.position;
                yaw = h.transform.eulerAngles.y;
                pitch = h.pitch;
                flags = h.flags | (IsAlive(e) ? 0 : NetProtocol.F_Dead);
                weapon = h.weapon;
                health = h.health;
            }
            w.UShort(e.id);
            w.Pos(pos);
            w.Yaw(yaw);
            w.SByte(Mathf.RoundToInt(pitch));
            w.Byte(flags);
            w.Byte(weapon + 1);
            w.Byte(Mathf.Clamp(Mathf.CeilToInt(health), 0, 255));
        }
        Broadcast(w.ToArray(), false, null);
    }

    private void Broadcast(byte[] message, bool reliable, Peer except)
    {
        foreach (var p in peers)
        {
            if (p == except || p.gone)
                continue;
            if (reliable)
                p.conn.SendReliable(message);
            else
                p.conn.SendUnreliable(message);
        }
    }

    // ----- Orchestrator status -----

    private void Report(bool force)
    {
        nextReport = Now + 10.0;
        if (string.IsNullOrEmpty(args.api))
            return;
        string state = phase == Phase.Playing ? "playing" : phase == Phase.Ended ? "ended" :
                       phase == Phase.Countdown && countdownEnds - Now < 6.0 ? "starting" : "waiting";
        if (!force && state == reportedState && peers.Count == reportedPlayers)
            return;
        reportedState = state;
        reportedPlayers = peers.Count;
        var accounts = new StringBuilder();
        foreach (var p in peers)
        {
            if (p.account.Length == 0 || !p.verified)
                continue;
            if (accounts.Length > 0)
                accounts.Append(',');
            accounts.Append('"').Append(p.account).Append('"');
        }
        string json = "{\"code\":\"" + args.code + "\",\"state\":\"" + state + "\",\"players\":" + peers.Count +
                      ",\"accounts\":[" + accounts + "]}";
        StartCoroutine(Post(args.api.TrimEnd('/') + "/report", json));
    }

    private static IEnumerator Post(string url, string json)
    {
        var req = new UnityWebRequest(url, "POST");
        req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        req.timeout = 5;
        yield return req.SendWebRequest();
        if (req.result != UnityWebRequest.Result.Success)
            Debug.LogWarning("[Sunucu] durum bildirilemedi: " + req.error);
        req.Dispose();
    }
}
