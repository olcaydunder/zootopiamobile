using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>One line of the in-match scoreboard.</summary>
public struct ScoreEntry
{
    public string name;
    public int team;      // 0 = the local player's team
    public int kills, deaths;
    public bool me, alive;
}

/// <summary>
/// In-match scoreboard, opened by tapping the score / players-left counter at the top: team modes show MAVİ TAKIM
/// against KIRMIZI TAKIM side by side (eliminations and deaths per player), Herkes Tek and Battle Royale one list
/// sorted by eliminations. Tap anywhere to close.
/// </summary>
public class ScoreboardPanel : MonoBehaviour
{
    private const int RowsPerColumn = 6, RowsSingle = 10;
    private RectTransform content;
    private Text leftScore, rightScore, leftName, rightName, single;
    private GameObject twoTeamHead, singleHead;
    private readonly List<ScoreEntry> entries = new List<ScoreEntry>();
    private float nextRefresh;

    public static ScoreboardPanel Create(Transform parent)
    {
        var rect = UIUtil.CreateStretch(parent, "Scoreboard");
        var veil = rect.gameObject.AddComponent<Image>();
        veil.color = new Color(0.01f, 0.02f, 0.05f, 0.62f);
        var b = rect.gameObject.AddComponent<Button>();
        b.transition = Selectable.Transition.None;
        var panel = rect.gameObject.AddComponent<ScoreboardPanel>();
        b.onClick.AddListener(() => rect.gameObject.SetActive(false));
        panel.Build(rect);
        rect.gameObject.SetActive(false);
        return panel;
    }

    public void Toggle()
    {
        bool on = !gameObject.activeSelf;
        gameObject.SetActive(on);
        if (on)
        {
            transform.SetAsLastSibling();
            nextRefresh = 0f;
        }
    }

    private void Build(RectTransform rect)
    {
        var top = new Vector2(0.5f, 1f);
        twoTeamHead = UIUtil.CreateRect(rect, "TeamHead", top, new Vector2(0f, -150f), new Vector2(1760f, 110f)).gameObject;
        var th = twoTeamHead.transform;
        var blue = UIUtil.CreateImage(th, "Blue", new Vector2(0.5f, 0.5f), new Vector2(-470f, 0f), new Vector2(780f, 100f), new Color(0.16f, 0.42f, 0.95f, 0.96f), false);
        blue.raycastTarget = false;
        leftName = UIUtil.CreateText(blue.transform, "MAVİ TAKIM", new Vector2(0f, 0.5f), new Vector2(260f, 0f), new Vector2(460f, 80f), 44, TextAnchor.MiddleLeft);
        leftName.fontStyle = FontStyle.Bold;
        leftScore = UIUtil.CreateText(blue.transform, "0", new Vector2(1f, 0.5f), new Vector2(-80f, 0f), new Vector2(140f, 90f), 64, TextAnchor.MiddleCenter);
        leftScore.fontStyle = FontStyle.Bold;
        var red = UIUtil.CreateImage(th, "Red", new Vector2(0.5f, 0.5f), new Vector2(470f, 0f), new Vector2(780f, 100f), new Color(0.9f, 0.2f, 0.18f, 0.96f), false);
        red.raycastTarget = false;
        rightName = UIUtil.CreateText(red.transform, "KIRMIZI TAKIM", new Vector2(1f, 0.5f), new Vector2(-260f, 0f), new Vector2(460f, 80f), 44, TextAnchor.MiddleRight);
        rightName.fontStyle = FontStyle.Bold;
        rightScore = UIUtil.CreateText(red.transform, "0", new Vector2(0f, 0.5f), new Vector2(80f, 0f), new Vector2(140f, 90f), 64, TextAnchor.MiddleCenter);
        rightScore.fontStyle = FontStyle.Bold;
        var vs = UIUtil.CreateText(th, "VS", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(160f, 100f), 70, TextAnchor.MiddleCenter);
        vs.fontStyle = FontStyle.Bold;

        singleHead = UIUtil.CreateImage(rect, "SingleHead", top, new Vector2(0f, -150f), new Vector2(1200f, 100f), new Color(0.1f, 0.12f, 0.18f, 0.96f), false).gameObject;
        singleHead.GetComponent<Image>().raycastTarget = false;
        single = UIUtil.CreateText(singleHead.transform, "", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1160f, 90f), 42, TextAnchor.MiddleCenter);
        single.fontStyle = FontStyle.Bold;

        content = UIUtil.CreateRect(rect, "Rows", top, new Vector2(0f, -560f), new Vector2(1760f, 700f));
        var hint = UIUtil.CreateText(rect, "Kapatmak için dokun", new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(600f, 36f), 22, TextAnchor.MiddleCenter);
        hint.color = Theme.TextDim;
    }

    private void Update()
    {
        if (Time.unscaledTime < nextRefresh)
            return;
        nextRefresh = Time.unscaledTime + 0.5f;
        Refresh();
    }

    private void Collect()
    {
        entries.Clear();
        var gm = GameManager.Instance;
        if (gm == null)
            return;
        if (NetGame.InOnlineMatch && NetClient.Instance != null)
        {
            NetClient.Instance.FillScoreboard(entries);
            return;
        }
        if (gm.player != null)
            entries.Add(new ScoreEntry { name = gm.profile.playerName, team = 0, kills = gm.player.kills, deaths = gm.player.deaths, me = true, alive = !gm.player.isDead });
        foreach (var b in gm.bots)
            if (b != null)
                entries.Add(new ScoreEntry { name = b.botName, team = b.team, kills = b.kills, deaths = b.deaths, alive = !b.isDead });
    }

    private void Refresh()
    {
        Collect();
        for (int i = content.childCount - 1; i >= 0; i--)
            Destroy(content.GetChild(i).gameObject);
        var gm = GameManager.Instance;
        bool arena = gm != null && gm.IsArena;
        bool twoTeams = arena && Modes.TwoTeams(gm.currentMode);
        entries.Sort((a, b) => b.kills != a.kills ? b.kills.CompareTo(a.kills) : a.deaths.CompareTo(b.deaths));
        twoTeamHead.SetActive(twoTeams);
        singleHead.SetActive(!twoTeams);

        if (twoTeams)
        {
            leftScore.text = TeamMatch.Score[0].ToString();
            rightScore.text = TeamMatch.Score[1].ToString();
            int l = 0, r = 0;
            foreach (var e in entries)
            {
                bool ours = e.team == 0;
                int row = ours ? l++ : r++;
                if (row >= RowsPerColumn)
                    continue;
                Row(e, new Vector2(ours ? -470f : 470f, 290f - row * 92f), 780f, ours ? new Color(0.1f, 0.24f, 0.62f, 0.9f) : new Color(0.55f, 0.1f, 0.1f, 0.9f), row + 1);
            }
            return;
        }

        if (arena)
            single.text = "HERKES TEK  •  HEDEF " + TeamMatch.ScoreToWin + " ELEME  •  " + TeamMatch.TimeText;
        else
            single.text = "KALAN OYUNCU: " + (gm != null ? gm.AliveCount() : 0) + "   •   EN ÇOK ELEYENLER";
        int shown = 0;
        bool meShown = false;
        for (int i = 0; i < entries.Count && shown < RowsSingle; i++)
        {
            var e = entries[i];
            if (shown == RowsSingle - 1 && !meShown && !e.me)
                continue;   // keep the last line for me
            if (e.me)
                meShown = true;
            Color c = e.team == 0 ? new Color(0.1f, 0.24f, 0.62f, 0.9f) : new Color(0.2f, 0.21f, 0.26f, 0.9f);
            Row(e, new Vector2(0f, 300f - shown * 66f), 1200f, c, i + 1, 60f);
            shown++;
        }
    }

    private void Row(ScoreEntry e, Vector2 pos, float width, Color color, int rank, float height = 82f)
    {
        Transform t;
        if (e.me)
        {
            // my line: a yellow edge around it
            var edge = UIUtil.CreateImage(content, "Me", new Vector2(0.5f, 0.5f), pos, new Vector2(width + 8f, height + 8f), Theme.Accent, false);
            edge.raycastTarget = false;
            var inner = UIUtil.CreateImage(edge.transform, "Row", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(width, height), color, false);
            inner.raycastTarget = false;
            t = inner.transform;
        }
        else
        {
            var img = UIUtil.CreateImage(content, "Row", new Vector2(0.5f, 0.5f), pos, new Vector2(width, height), color, false);
            img.raycastTarget = false;
            t = img.transform;
        }
        float fs = height > 70f ? 30f : 26f;
        var rk = UIUtil.CreateText(t, rank.ToString(), new Vector2(0f, 0.5f), new Vector2(40f, 0f), new Vector2(60f, height), (int)fs, TextAnchor.MiddleCenter);
        rk.color = Theme.TextDim;
        var name = UIUtil.CreateText(t, e.name, new Vector2(0f, 0.5f), new Vector2(80f + width * 0.22f, 0f), new Vector2(width * 0.44f, height), (int)fs, TextAnchor.MiddleLeft);
        name.fontStyle = FontStyle.Bold;
        if (!e.alive)
            name.color = new Color(1f, 1f, 1f, 0.45f);
        var kl = UIUtil.CreateText(t, "ELEME", new Vector2(1f, 0.5f), new Vector2(-300f, 0f), new Vector2(110f, height), 18, TextAnchor.MiddleRight);
        kl.color = Theme.TextDim;
        var k = UIUtil.CreateText(t, e.kills.ToString(), new Vector2(1f, 0.5f), new Vector2(-205f, 0f), new Vector2(70f, height), (int)fs + 4, TextAnchor.MiddleCenter);
        k.fontStyle = FontStyle.Bold;
        var dl = UIUtil.CreateText(t, "ÖLÜM", new Vector2(1f, 0.5f), new Vector2(-125f, 0f), new Vector2(100f, height), 18, TextAnchor.MiddleRight);
        dl.color = Theme.TextDim;
        var d = UIUtil.CreateText(t, e.deaths.ToString(), new Vector2(1f, 0.5f), new Vector2(-40f, 0f), new Vector2(70f, height), (int)fs + 4, TextAnchor.MiddleCenter);
        d.fontStyle = FontStyle.Bold;
    }
}

/// <summary>
/// The big map, opened by tapping the minimap: the whole map (arena modes: the arena) with where you are and which
/// way you face, your team, the safe zone and the next one, a grid to call out places and the plane's path.
/// Tap anywhere outside or the X to close; the match goes on underneath.
/// </summary>
public class BigMapPanel : MonoBehaviour
{
    private const float Px = 900f;
    private RectTransform map;
    private RawImage raw;
    private RectTransform me, zone, next, plane, grid;
    private readonly List<RectTransform> allies = new List<RectTransform>();
    private readonly List<Text> gridLabels = new List<Text>();
    private Text title;
    private Rect view = new Rect(0f, 0f, 1f, 1f);

    public static BigMapPanel Create(Transform parent)
    {
        var rect = UIUtil.CreateStretch(parent, "BigMap");
        var veil = rect.gameObject.AddComponent<Image>();
        veil.color = new Color(0f, 0f, 0f, 0.55f);
        var b = rect.gameObject.AddComponent<Button>();
        b.transition = Selectable.Transition.None;
        b.onClick.AddListener(() => rect.gameObject.SetActive(false));
        var panel = rect.gameObject.AddComponent<BigMapPanel>();
        panel.Build(rect);
        rect.gameObject.SetActive(false);
        return panel;
    }

    public void Toggle()
    {
        bool on = !gameObject.activeSelf;
        gameObject.SetActive(on);
        if (on)
        {
            transform.SetAsLastSibling();
            raw.texture = World.MinimapTexture;
            LateUpdate();
        }
    }

    private void Build(RectTransform rect)
    {
        var c = new Vector2(0.5f, 0.5f);
        var frame = UIUtil.CreateImage(rect, "Frame", c, new Vector2(0f, -10f), new Vector2(Px + 16f, Px + 16f), new Color(0.05f, 0.06f, 0.08f, 0.95f), false);
        frame.gameObject.AddComponent<Button>().transition = Selectable.Transition.None;   // taps on the map don't reach the backdrop
        map = UIUtil.CreateRect(frame.transform, "Map", c, Vector2.zero, new Vector2(Px, Px));
        raw = map.gameObject.AddComponent<RawImage>();
        raw.raycastTarget = true;
        map.gameObject.AddComponent<RectMask2D>();

        grid = UIUtil.CreateRect(map, "Grid", c, Vector2.zero, new Vector2(Px, Px));
        for (int i = 1; i < 8; i++)
        {
            float o = -Px * 0.5f + i * Px / 8f;
            var v = UIUtil.CreateImage(grid, "V", c, new Vector2(o, 0f), new Vector2(2f, Px), new Color(1f, 1f, 1f, 0.16f), false);
            v.raycastTarget = false;
            var h = UIUtil.CreateImage(grid, "H", c, new Vector2(0f, o), new Vector2(Px, 2f), new Color(1f, 1f, 1f, 0.16f), false);
            h.raycastTarget = false;
        }
        for (int i = 0; i < 8; i++)
        {
            float o = -Px * 0.5f + (i + 0.5f) * Px / 8f;
            var a = UIUtil.CreateText(grid, ((char)('A' + i)).ToString(), c, new Vector2(o, Px * 0.5f - 18f), new Vector2(40f, 30f), 22, TextAnchor.MiddleCenter);
            a.color = new Color(1f, 1f, 1f, 0.7f);
            var n = UIUtil.CreateText(grid, (i + 1).ToString(), c, new Vector2(-Px * 0.5f + 16f, Px * 0.5f - (i + 0.5f) * Px / 8f), new Vector2(30f, 30f), 22, TextAnchor.MiddleCenter);
            n.color = new Color(1f, 1f, 1f, 0.7f);
            gridLabels.Add(a);
            gridLabels.Add(n);
        }

        var pl = UIUtil.CreateImage(map, "PlanePath", c, Vector2.zero, new Vector2(10f, 4f), new Color(1f, 0.9f, 0.3f, 0.9f), false);
        pl.raycastTarget = false;
        plane = pl.rectTransform;
        var nx = UIUtil.CreateImage(map, "NextZone", c, Vector2.zero, new Vector2(10f, 10f), new Color(1f, 1f, 1f, 0.95f), false);
        nx.sprite = UIUtil.Ring;
        nx.raycastTarget = false;
        next = nx.rectTransform;
        var zn = UIUtil.CreateImage(map, "Zone", c, Vector2.zero, new Vector2(10f, 10f), new Color(0.35f, 0.65f, 1f, 1f), false);
        zn.sprite = UIUtil.Ring;
        zn.raycastTarget = false;
        zone = zn.rectTransform;
        for (int i = 0; i < 4; i++)
        {
            var d = UIUtil.CreateImage(map, "Ally", c, Vector2.zero, new Vector2(18f, 18f), new Color(0.3f, 1f, 0.45f), true);
            d.raycastTarget = false;
            allies.Add(d.rectTransform);
        }
        var m = UIUtil.CreateImage(map, "Me", c, Vector2.zero, new Vector2(24f, 24f), new Color(1f, 0.85f, 0.2f), true);
        m.raycastTarget = false;
        me = m.rectTransform;
        var nose = UIUtil.CreateImage(me, "Facing", c, new Vector2(0f, 19f), new Vector2(7f, 20f), new Color(1f, 0.85f, 0.2f), false);
        nose.raycastTarget = false;
        var ring = UIUtil.CreateImage(me, "Pulse", c, Vector2.zero, new Vector2(44f, 44f), new Color(1f, 0.85f, 0.2f, 0.5f), false);
        ring.sprite = UIUtil.Ring;
        ring.raycastTarget = false;

        title = UIUtil.CreateText(frame.transform, "", new Vector2(0.5f, 1f), new Vector2(0f, 34f), new Vector2(Px, 50f), 30, TextAnchor.MiddleCenter);
        title.fontStyle = FontStyle.Bold;
        var north = UIUtil.CreateText(frame.transform, "K", new Vector2(1f, 1f), new Vector2(-30f, -30f), new Vector2(40f, 40f), 30, TextAnchor.MiddleCenter);
        north.fontStyle = FontStyle.Bold;
        Text l;
        UIUtil.CreateButton(rect, "X", new Vector2(0.5f, 0.5f), new Vector2(Px * 0.5f + 70f, Px * 0.5f - 40f), new Vector2(90f, 90f), Theme.Red, false, 44, out l)
            .onClick.AddListener(() => gameObject.SetActive(false));
    }

    private Vector2 ToMap(Vector3 world)
    {
        Vector2 uv = World.ToMapUV(world);
        return new Vector2((uv.x - view.x) / view.width - 0.5f, (uv.y - view.y) / view.height - 0.5f) * Px;
    }

    private void LateUpdate()
    {
        var gm = GameManager.Instance;
        if (gm == null || gm.player == null)
            return;
        var player = gm.player;
        // Battle Royale: the whole map; arena modes: zoomed on the arena.
        if (gm.IsArena)
        {
            float span = Mathf.Clamp(TeamMatch.Radius * 2.6f / World.MapSize, 0.05f, 1f);
            Vector2 cuv = World.ToMapUV(TeamMatch.Center);
            view = new Rect(Mathf.Clamp(cuv.x - span * 0.5f, 0f, 1f - span), Mathf.Clamp(cuv.y - span * 0.5f, 0f, 1f - span), span, span);
        }
        else
            view = new Rect(0f, 0f, 1f, 1f);
        raw.uvRect = view;
        grid.gameObject.SetActive(!gm.IsArena);
        float metres = World.MapSize * view.width;

        Vector3 pp = player.transform.position;
        me.anchoredPosition = ToMap(pp);
        float yaw = player.state == PlayerState.Driving && player.vehicle != null ? player.vehicle.Yaw : player.transform.eulerAngles.y;
        me.localRotation = Quaternion.Euler(0f, 0f, -yaw);
        float pulse = 1f + Mathf.PingPong(Time.unscaledTime * 1.6f, 0.6f);
        me.Find("Pulse").localScale = Vector3.one * pulse;

        // grid square under the player ("D4")
        Vector2 puv = World.ToMapUV(pp);
        int gx = Mathf.Clamp(Mathf.FloorToInt(puv.x * 8f), 0, 7), gy = Mathf.Clamp(7 - Mathf.FloorToInt(puv.y * 8f), 0, 7);
        title.text = gm.IsArena ? "ARENA HARİTASI" : "HARİTA  •  BULUNDUĞUN KARE: " + (char)('A' + gx) + (gy + 1) + "   •   " + Mathf.RoundToInt(metres) + " m";

        var z = gm.safeZone;
        bool zoneOn = z != null && z.active;
        zone.gameObject.SetActive(zoneOn);
        if (zoneOn)
        {
            zone.anchoredPosition = ToMap(z.center);
            float s = z.radius * 2f / metres * Px;
            zone.sizeDelta = new Vector2(s, s);
        }
        bool nextOn = zoneOn && z.HasNext && !gm.IsArena;
        next.gameObject.SetActive(nextOn);
        if (nextOn)
        {
            next.anchoredPosition = ToMap(z.NextCenter);
            float s = z.NextRadius * 2f / metres * Px;
            next.sizeDelta = new Vector2(s, s);
        }

        bool showPlane = gm.plane != null && (player.state == PlayerState.Plane || player.state == PlayerState.Freefall);
        plane.gameObject.SetActive(showPlane);
        if (showPlane)
        {
            Vector2 a = ToMap(gm.plane.start), b = ToMap(gm.plane.end);
            plane.anchoredPosition = (a + b) * 0.5f;
            plane.sizeDelta = new Vector2((b - a).magnitude, 4f);
            plane.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg);
        }

        int n = 0;
        if (!NetGame.InOnlineMatch)
        {
            foreach (var bot in gm.bots)
            {
                if (n >= allies.Count)
                    break;
                if (bot == null || bot.isDead || bot.team != 0 || !bot.gameObject.activeInHierarchy)
                    continue;
                allies[n].gameObject.SetActive(true);
                allies[n++].anchoredPosition = ToMap(bot.transform.position);
            }
        }
        else
        {
            foreach (var c in gm.Combatants)
            {
                if (n >= allies.Count)
                    break;
                var pup = c as NetPuppet;
                if (pup == null || pup.team != 0 || (pup.flags & NetProtocol.F_Dead) != 0)
                    continue;
                allies[n].gameObject.SetActive(true);
                allies[n++].anchoredPosition = ToMap(pup.transform.position);
            }
        }
        for (; n < allies.Count; n++)
            allies[n].gameObject.SetActive(false);
    }
}
