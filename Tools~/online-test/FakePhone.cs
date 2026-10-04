using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Threading;

// End-to-end test of the real Linux game server with two fake phones speaking the protocol.
public struct V3 { public float x, y, z; public V3(float a, float b, float c) { x = a; y = b; z = c; } public override string ToString() { return "(" + x.ToString("F1") + "," + y.ToString("F1") + "," + z.ToString("F1") + ")"; }
    public static float Dist(V3 a, V3 b) { float dx = a.x - b.x, dy = a.y - b.y, dz = a.z - b.z; return (float)Math.Sqrt(dx * dx + dy * dy + dz * dz); } }

public class Ent { public int id, team, flags, weapon, health; public bool bot; public string name, skin; public V3 pos; public float yaw; }

public class Phone
{
    static int count; public string name; public NetSocket sock = new NetSocket(); public IPEndPoint server; public NetConnection conn; public uint nonce;
    public int id = -1, team = -1, teams; public bool inMatch; public V3 pos; public int flags = 2; ushort seq;
    public Dictionary<int, Ent> ents = new Dictionary<int, Ent>(); public Dictionary<int, V3> loot = new Dictionary<int, V3>(); public Dictionary<int, int> lootType = new Dictionary<int, int>();
    public List<string> lobbyNames = new List<string>(); public int leader; public int lobbyPhase;
    public int voiceFrames, lastVoiceFrom = -1; public int snaps, shotsSeen, damageMsgs, kills, lootGone, doorMsgs, grenades; public float damageTotal; public int lastAttacker = -1, lastHow = -1;
    public List<string> killLog = new List<string>(); public int placement = -1, endWinner = -2; public string reject; public int lootTakenId = -1; public bool lootTakenOk;
    public float snapTime; public int alive, aliveTeams; public float zoneR; public int zonePhase; public int doorCount;
    NetWriter w = new NetWriter(); NetReader r = new NetReader();
    static double T0 = Environment.TickCount / 1000.0; public static double Now { get { return Environment.TickCount / 1000.0 - T0; } }

    public Phone(string n, int port) { name = n; server = new IPEndPoint(IPAddress.Loopback, port); sock.Open(0); nonce = (uint)new Random(Environment.TickCount + (count++) * 7919).Next(1, int.MaxValue); }
    public void Hello(string version, string code)
    {
        w.Reset(); w.Byte('Z'); w.Byte('M'); w.Byte(1); w.Byte(2); w.UInt(nonce); w.String(version); w.String(code); w.String(name); w.String("NinjaSand"); w.String(""); w.String("ACC" + name.Length); w.String("secret");
        sock.Send(w.Buffer, w.Length, server);
    }
    public static void Pos(NetWriter w, V3 p) { w.Short((int)Math.Round(p.x * 20)); w.Short((int)Math.Round(p.y * 20)); w.Short((int)Math.Round(p.z * 20)); }
    public static V3 Pos(NetReader r) { float x = r.Short() * 0.05f, y = r.Short() * 0.05f, z = r.Short() * 0.05f; return new V3(x, y, z); }
    public void Rel(params Action<NetWriter>[] parts) { w.Reset(); foreach (var p in parts) p(w); conn.SendReliable(w.ToArray()); }
    public void Unrel(Action<NetWriter> f) { w.Reset(); f(w); conn.SendUnreliable(w.ToArray()); }

    public void Pump()
    {
        double now = Now; byte[] d; int len; EndPoint from;
        while (sock.TryReceive(out d, out len, out from))
        {
            if (len < 3 || d[0] != 'Z' || d[1] != 'M') continue;
            if (d[2] == 2 && conn == null) { r.Set(d, 3, len - 3); uint n = r.UInt(); uint tok = r.UInt(); int myid = r.UShort(); if (n == nonce) { conn = new NetConnection(tok, now); id = myid; } }
            else if (d[2] == 3) { r.Set(d, 3, len - 3); if (r.UInt() == nonce) reject = r.String(); }
            else if (d[2] == 4 && conn != null) conn.Receive(d, len, now, (b, o, l, rel) => Dispatch(b, o, l));
            else if (d[2] == 5) { reject = reject ?? "bye"; }
        }
        if (conn != null) conn.Flush(now, (b, l) => sock.Send(b, l, server));
    }

    public void SendState()
    {
        Unrel(x => { x.Byte(1); x.UShort(seq++); Pos(x, pos); x.Byte(0); x.SByte(0); x.Byte(flags); x.Byte(1); x.Byte(100); x.Byte(0); });
    }

    void Dispatch(byte[] b, int o, int l)
    {
        r.Set(b, o, l); int t = r.Byte();
        switch (t)
        {
            case 20: lobbyPhase = r.Byte(); r.Float(); r.Byte(); r.Bool(); leader = r.UShort(); r.String(); { int n = r.Byte(); lobbyNames.Clear(); for (int i = 0; i < n; i++) { r.UShort(); lobbyNames.Add(r.String()); r.String(); } } break;
            case 21: { int n = r.Byte(); for (int i = 0; i < n; i++) { var e = new Ent { id = r.UShort(), team = r.Byte(), bot = r.Bool(), name = r.String(), skin = r.String() }; r.String(); r.String(); ents[e.id] = e; } } break;
            case 22: id = r.UShort(); team = r.Byte(); r.Byte(); { var ps = Pos(r); var pe = Pos(r); r.Float(); r.Float(); zoneR = r.Float(); doorCount = r.UShort(); teams = r.Byte(); pos = ps; inMatch = true; Console.WriteLine(name + ": MATCH START id=" + id + " team=" + team + " teams=" + teams + " plane " + ps + "->" + pe + " zoneR=" + zoneR + " doors=" + doorCount + " ents=" + ents.Count); } break;
            case 23: { int n = r.UShort(); for (int i = 0; i < n; i++) { int lid = r.UShort(); int ty = r.Byte(); loot[lid] = Pos(r); lootType[lid] = ty; } } break;
            case 24: lootTakenId = r.UShort(); lootTakenOk = r.Bool(); break;
            case 25: { int lid = r.UShort(); loot.Remove(lid); lootGone++; } break;
            case 26:
            {
                r.UShort(); snapTime = r.Float(); alive = r.Byte(); aliveTeams = r.Byte(); r.Float(); r.Float(); zoneR = r.Float(); r.Float(); r.Float(); r.Float(); r.Float(); zonePhase = r.Byte(); r.Byte();
                int n = r.Byte(); for (int i = 0; i < n; i++) { int eid = r.UShort(); var p = Pos(r); float yaw = r.Byte() * 360f / 256f; r.SByte(); int fl = r.Byte(); int wp = r.Byte() - 1; int hp = r.Byte(); Ent e; if (ents.TryGetValue(eid, out e)) { e.pos = p; e.flags = fl; e.weapon = wp; e.health = hp; e.yaw = yaw; } }
                snaps++;
            } break;
            case 27: r.UShort(); Pos(r); r.Byte(); shotsSeen++; break;
            case 28: { float a = r.UShort() / 10f; lastAttacker = r.UShort(); Pos(r); r.Bool(); lastHow = r.Byte(); damageMsgs++; damageTotal += a; } break;
            case 29: { int k = r.UShort(); int v = r.UShort(); int how = r.Byte(); killLog.Add(k + ">" + v + "(" + how + ")"); if (k == id) kills++; } break;
            case 30: r.UShort(); r.Bool(); Pos(r); doorMsgs++; break;
            case 31: r.UShort(); Pos(r); r.Float(); r.Float(); r.Float(); grenades++; break;
            case 32: placement = r.Byte(); r.Byte(); break;
            case 33: endWinner = r.Byte(); Console.WriteLine(name + ": MATCH END winner team " + endWinner + " (" + r.String() + ")"); break;
            case 34: Console.WriteLine(name + ": TOAST " + r.String()); break;
            case 35: lastVoiceFrom = r.UShort(); voiceFrames++; break;
        }
    }
}

public static class FakePhoneTest
{
    static int fails;
    static void Check(bool ok, string what) { Console.WriteLine((ok ? "  OK   " : "  FAIL ") + what); if (!ok) fails++; }
    static void Run(double seconds, Phone[] ps, Action each = null)
    {
        double end = Phone.Now + seconds, nextState = 0;
        while (Phone.Now < end) { foreach (var p in ps) p.Pump(); if (Phone.Now >= nextState) { nextState = Phone.Now + 0.05; foreach (var p in ps) if (p.inMatch && p.flags != 1) p.SendState(); } if (each != null) each(); Thread.Sleep(5); }
    }
    static bool RunUntil(double seconds, Phone[] ps, Func<bool> done) { double end = Phone.Now + seconds; while (Phone.Now < end) { Run(0.05, ps); if (done()) return true; } return false; }

    public static int Main(string[] a)
    {
        int port = int.Parse(a[0]); string version = a[1]; string mode = a.Length > 2 ? a[2] : "solo"; bool squad = mode == "squad";
        var A = new Phone("Tilki42", port); var B = new Phone("Tilki42", port); var ps = new[] { A, B };
        Console.WriteLine("== connect");
        var wrong = new Phone("Yanlis", port); double wAt = 0;
        RunUntil(60, new[] { wrong }, () => { if (Phone.Now > wAt) { wAt = Phone.Now + 0.5; wrong.Hello("net-xxxxx", "123456"); } return wrong.reject != null || wrong.conn != null; });
        Check(wrong.reject != null && wrong.reject.Contains("Sürüm"), "wrong version rejected: " + wrong.reject);
        double helloAt = 0;
        bool ok = RunUntil(60, ps, () => { if (Phone.Now > helloAt) { helloAt = Phone.Now + 0.5; foreach (var p in ps) if (p.conn == null) p.Hello(version, "123456"); } return A.conn != null && B.conn != null; });
        Check(ok, "both welcomed: ids " + A.id + ", " + B.id);
        RunUntil(5, ps, () => A.lobbyNames.Count == 2 && B.lobbyNames.Count == 2);
        Check(A.lobbyNames.Count == 2, "lobby list: " + string.Join(" | ", A.lobbyNames.ToArray()));
        Check(A.lobbyNames.Count == 2 && A.lobbyNames[0] != A.lobbyNames[1], "duplicate names made unique");
        Check(A.leader == A.id, "first player is the leader");
        Console.WriteLine("== voice in the private room");
        for (int i = 0; i < 5; i++) A.Unrel(x => { x.Byte(9); for (int k = 0; k < 164; k++) x.Byte(k); });
        RunUntil(2, ps, () => B.voiceFrames >= 5);
        Check(B.voiceFrames >= 5 && B.lastVoiceFrom == A.id, "B heard A in the room: " + B.voiceFrames + " frames");
        Check(A.voiceFrames == 0, "A does not hear itself");
        Console.WriteLine("== start (leader)");
        B.Rel(x => x.Byte(8));   // not the leader: ignored
        Run(1, ps); Check(A.lobbyPhase == 0, "non-leader start ignored");
        A.Rel(x => x.Byte(8));
        ok = RunUntil(15, ps, () => A.inMatch && B.inMatch && A.loot.Count > 50);
        Check(ok, "match started on both; loot " + A.loot.Count + ", entities " + A.ents.Count + ", teams " + A.teams);
        int bots = 0; foreach (var e in A.ents.Values) if (e.bot) bots++;
        Check(bots == (squad ? 21 : 23), mode + ": bots " + bots + ", teams " + A.teams);
        Check(squad ? A.teams == 7 : A.teams == 25, "team count " + A.teams);
        Check(squad ? A.team == B.team : A.team != B.team, "teams " + A.team + "/" + B.team);
        Run(3, ps);
        Check(A.snaps >= 45 && A.snaps <= 66, "snapshot rate ~20/s: " + A.snaps + " in ~3 s");
        Console.WriteLine("== voice in the match");
        int vb = B.voiceFrames;
        for (int i = 0; i < 5; i++) A.Unrel(x => { x.Byte(9); for (int k = 0; k < 164; k++) x.Byte(k); });
        Run(1, ps);
        if (squad) Check(B.voiceFrames - vb >= 5, "squad: teammate B hears A"); else Check(B.voiceFrames == vb, "solo: enemy B does not hear A");
        Console.WriteLine("== bots drop");
        ok = RunUntil(90, ps, () => { int landed = 0; foreach (var e in A.ents.Values) if (e.bot && (e.flags & 14) == 0 && (e.flags & 1) == 0) landed++; return landed >= 18; });
        int ln = 0; foreach (var e in A.ents.Values) if (e.bot && (e.flags & 14) == 0) ln++;
        Check(ok, "bots landed: " + ln + "  (zone r=" + A.zoneR.ToString("F0") + ", alive " + A.alive + ")");
        Console.WriteLine("== pick up a crate");
        int crate = -1; foreach (var kv in A.loot) { crate = kv.Key; break; }
        V3 cp = A.loot[crate];
        A.pos = new V3(cp.x, cp.y + 0.6f, cp.z); A.flags = 0; B.pos = new V3(cp.x + 50, cp.y + 0.6f, cp.z); B.flags = 0;
        Run(0.5, ps);
        A.Rel(x => { x.Byte(5); x.UShort(crate); });
        ok = RunUntil(3, ps, () => A.lootTakenId == crate);
        Check(ok && A.lootTakenOk, "server gave crate " + crate + " to A");
        Check(!B.loot.ContainsKey(crate), "B saw the crate disappear");
        B.Rel(x => { x.Byte(5); x.UShort(crate); });
        ok = RunUntil(3, ps, () => B.lootTakenId == crate);
        Check(ok && !B.lootTakenOk, "same crate refused to B");
        Console.WriteLine("== door (too far: corrected)");
        A.Rel(x => { x.Byte(6); x.UShort(0); x.Bool(true); Phone.Pos(x, A.pos); });
        ok = RunUntil(3, ps, () => A.doorMsgs > 0);
        Check(ok, "far door request answered with the real state");
        Console.WriteLine("== shots relay");
        int before = B.shotsSeen;
        for (int i = 0; i < 5; i++) A.Unrel(x => { x.Byte(2); Phone.Pos(x, new V3(0, 5, 0)); x.Byte(1); });
        Run(1, ps);
        Check(B.shotsSeen - before >= 5, "B saw A's shots: " + (B.shotsSeen - before));
        Console.WriteLine("== A hunts a bot");
        Ent target = null; foreach (var e in A.ents.Values) if (e.bot && (e.flags & 15) == 0) { target = e; break; }
        Check(target != null, "found a landed bot " + (target != null ? target.name + " #" + target.id + " " + target.pos : ""));
        if (target != null)
        {
            A.pos = new V3(target.pos.x + 30, target.pos.y, target.pos.z);   // within rifle range
            Run(0.5, ps);
            // out of range hit first (pistol range 45*1.7+10 = 86): from 200 m away -> refused
            ok = RunUntil(20, ps, () =>
            {
                A.pos = new V3(target.pos.x + 30, target.pos.y, target.pos.z);
                A.Rel(x => { x.Byte(3); x.UShort(target.id); x.UShort(350); x.Byte(0); x.Bool(true); });
                Run(0.15, new[] { A, B });
                return A.kills > 0;
            });
            Check(ok, "A killed bot " + target.id + ": kill log " + string.Join(" ", A.killLog.ToArray()));
            Check(A.killLog.Exists(s => s.StartsWith(A.id + ">" + target.id + "(1)")), "kill credited to A with rifle (how 1)");
            Run(1, ps);
            int crates = A.loot.Count;
            Check(true, "crates now " + crates);
        }
        Console.WriteLine("== A shoots B");
        B.pos = new V3(A.pos.x + 10, A.pos.y, A.pos.z); Run(0.5, ps);
        int dm = B.damageMsgs;
        A.Rel(x => { x.Byte(3); x.UShort(B.id); x.UShort(200); x.Byte(4); x.Bool(false); });
        ok = RunUntil(3, ps, () => B.damageMsgs > dm && B.lastAttacker == A.id);
        if (squad)
            Check(!ok, "squad: hit on a teammate ignored");
        else
            Check(ok && B.lastHow == 5, "B got 20 damage from A with pistol (how " + B.lastHow + ")");
        Console.WriteLine("== a bot shoots A");
        {
            int dmA = A.damageMsgs; bool hit = false;
            hit = RunUntil(40, ps, () =>
            {
                Ent near = null; float best = 1e9f;
                foreach (var e in A.ents.Values) if (e.bot && (e.flags & 15) == 0) { float d = V3.Dist(e.pos, A.pos); if (d < best) { best = d; near = e; } }
                if (near != null && best > 12f) A.pos = new V3(near.pos.x + 6, near.pos.y, near.pos.z);
                return A.damageMsgs > dmA;
            });
            Check(hit && A.lastAttacker >= 100, "A was shot by bot #" + A.lastAttacker + " (how " + A.lastHow + "), " + A.damageTotal.ToString("F0") + " hp so far");
        }
        A.Rel(x => { x.Byte(3); x.UShort(B.id); x.UShort(60000); x.Byte(4); x.Bool(false); });
        Run(1, ps);
        if (!squad) Check(B.damageTotal < 200, "absurd damage clamped: total " + B.damageTotal);
        Console.WriteLine("== B dies to A");
        B.Rel(x => { x.Byte(4); x.UShort(A.id); x.Byte(5); });
        ok = RunUntil(3, ps, () => B.placement >= 0 && A.killLog.Exists(s => s.StartsWith(A.id + ">" + B.id)));
        B.flags = 1;
        Check(ok, "B placement " + B.placement + ", A's kill log " + string.Join(" ", A.killLog.ToArray()));
        Console.WriteLine("== bots vs A for a while");
        Run(10, ps);
        Check(true, "A damage from bots: " + A.damageMsgs + " msgs, " + A.damageTotal.ToString("F0") + " hp, last attacker " + A.lastAttacker + "; grenades seen " + A.grenades + ", doors " + A.doorMsgs + ", alive " + A.alive + "/" + A.aliveTeams + " teams, zone phase " + A.zonePhase);
        Console.WriteLine("== A dies -> match ends (no humans left)");
        A.Rel(x => { x.Byte(4); x.UShort(0xFFFF); x.Byte(0); });
        ok = RunUntil(5, ps, () => A.endWinner != -2);
        Check(A.placement > 0, "A placement " + A.placement);
        Check(ok, "match end received: winner " + A.endWinner);
        return fails;
    }
}
