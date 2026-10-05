using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// GÖREVLER: this week's mission box track at the top (boxes at 1, 5 and 8 collected missions), then three
/// pages: today's three missions (+ a bonus box for all three) and this week's three; the season missions
/// with five stages each; and today's victories (a box for each of the first six wins).
/// TOPLA gives the reward; a box opens right away.
/// </summary>
public class MissionsScreen : MonoBehaviour
{
    public enum Page { Missions, Season, Wins }

    private System.Action onClose;
    private Page page;
    private RectTransform chestStrip, missionsPage, seasonPage, winsPage, dailyList, weeklyList, seasonList;
    private Text dailyTimer, weeklyTimer, seasonTimer, winsTimer, statusText;
    private readonly Image[] tabImages = new Image[3];
    private readonly Text[] tabLabels = new Text[3];
    private float nextTimer;

    public static MissionsScreen Create(Transform canvas)
    {
        var rect = UIUtil.CreateStretch(canvas, "Missions");
        rect.gameObject.AddComponent<Image>().color = new Color(0.03f, 0.05f, 0.09f, 0.97f);
        var s = rect.gameObject.AddComponent<MissionsScreen>();
        s.Build();
        rect.gameObject.AddComponent<PopIn>();
        rect.gameObject.SetActive(false);
        return s;
    }

    private void Build()
    {
        var t = transform;
        Text label;
        var back = UIUtil.CreateButton(t, "<", new Vector2(0f, 1f), new Vector2(80f, -70f), new Vector2(84f, 84f), Theme.Accent, false, 54, out label);
        label.color = new Color(0.1f, 0.1f, 0.1f);
        label.GetComponent<Shadow>().enabled = false;
        back.onClick.AddListener(Close);
        Icons.Create(t, "missions", new Vector2(0f, 1f), new Vector2(190f, -70f), new Vector2(84f, 84f));
        var title = UIUtil.CreateText(t, "GÖREVLER", new Vector2(0f, 1f), new Vector2(460f, -70f), new Vector2(500f, 80f), 52, TextAnchor.MiddleLeft);
        title.fontStyle = FontStyle.Bold;

        string[] tabs = { "GÜNLÜK  •  HAFTALIK", "SEZON", "GÜNLÜK ZAFERLER" };
        for (int i = 0; i < tabs.Length; i++)
        {
            int index = i;
            var b = UIUtil.CreateButton(t, tabs[i], new Vector2(1f, 1f), new Vector2(-1140f + i * 380f, -70f), new Vector2(360f, 76f), Theme.Panel, false, 26, out label);
            b.onClick.AddListener(() => Show((Page)index));
            tabImages[i] = b.GetComponent<Image>();
            tabLabels[i] = label;
        }

        chestStrip = UIUtil.CreateRect(t, "ChestTrack", new Vector2(0.5f, 1f), new Vector2(0f, -200f), new Vector2(1700f, 120f));

        // Daily + weekly
        missionsPage = UIUtil.CreateRect(t, "MissionsPage", new Vector2(0.5f, 0.5f), new Vector2(0f, -100f), new Vector2(1760f, 700f));
        dailyList = Column(missionsPage, "GÜNLÜK GÖREVLER", -440f, out dailyTimer);
        weeklyList = Column(missionsPage, "HAFTALIK GÖREVLER", 440f, out weeklyTimer);

        // Season
        seasonPage = UIUtil.CreateRect(t, "SeasonPage", new Vector2(0.5f, 0.5f), new Vector2(0f, -100f), new Vector2(1760f, 700f));
        var sh = UIUtil.CreateText(seasonPage, "", new Vector2(0f, 1f), new Vector2(460f, -22f), new Vector2(900f, 44f), 30, TextAnchor.MiddleLeft);
        sh.fontStyle = FontStyle.Bold;
        sh.color = Theme.Accent;
        seasonTimer = sh;
        var view = UIUtil.CreateRect(seasonPage, "View", new Vector2(0.5f, 0.5f), new Vector2(0f, -30f), new Vector2(1720f, 630f));
        view.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.001f);
        view.gameObject.AddComponent<RectMask2D>();
        seasonList = UIUtil.CreateRect(view, "List", new Vector2(0.5f, 1f), Vector2.zero, new Vector2(1720f, 10f));
        seasonList.pivot = new Vector2(0.5f, 1f);
        var sc = view.gameObject.AddComponent<ScrollRect>();
        sc.content = seasonList;
        sc.viewport = view;
        sc.horizontal = false;
        sc.movementType = ScrollRect.MovementType.Clamped;
        sc.scrollSensitivity = 40f;

        // Today's victories
        winsPage = UIUtil.CreateRect(t, "WinsPage", new Vector2(0.5f, 0.5f), new Vector2(0f, -100f), new Vector2(1760f, 700f));
        var wh = UIUtil.CreateText(winsPage, "", new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(1600f, 50f), 34, TextAnchor.MiddleCenter);
        wh.fontStyle = FontStyle.Bold;
        wh.color = Theme.Accent;
        winsTimer = wh;

        statusText = UIUtil.CreateText(t, "", new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(1600f, 40f), 26, TextAnchor.MiddleCenter);
        statusText.color = Theme.Accent;
    }

    private static RectTransform Column(Transform t, string heading, float x, out Text timer)
    {
        var col = UIUtil.CreateRect(t, heading, new Vector2(0.5f, 0.5f), new Vector2(x, 0f), new Vector2(840f, 700f));
        var h = UIUtil.CreateText(col, heading, new Vector2(0f, 1f), new Vector2(240f, -22f), new Vector2(460f, 44f), 30, TextAnchor.MiddleLeft);
        h.fontStyle = FontStyle.Bold;
        h.color = Theme.Accent;
        timer = UIUtil.CreateText(col, "", new Vector2(1f, 1f), new Vector2(-200f, -22f), new Vector2(380f, 40f), 22, TextAnchor.MiddleRight);
        timer.color = Theme.TextDim;
        var list = UIUtil.CreateRect(col, "List", new Vector2(0.5f, 1f), new Vector2(0f, -54f), new Vector2(840f, 10f));
        list.pivot = new Vector2(0.5f, 1f);
        return list;
    }

    public void Open(System.Action closed)
    {
        Open(Page.Missions, closed);
    }

    public void Open(Page which, System.Action closed)
    {
        onClose = closed;
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        statusText.text = "";
        Show(which);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    private void Close()
    {
        Hide();
        if (onClose != null)
            onClose();
    }

    private void Show(Page which)
    {
        page = which;
        for (int i = 0; i < tabImages.Length; i++)
        {
            bool sel = i == (int)page;
            tabImages[i].color = sel ? Theme.Selected : Theme.Panel;
            tabLabels[i].color = sel ? new Color(0.08f, 0.08f, 0.1f) : Color.white;
            tabLabels[i].GetComponent<Shadow>().enabled = !sel;
        }
        missionsPage.gameObject.SetActive(page == Page.Missions);
        seasonPage.gameObject.SetActive(page == Page.Season);
        winsPage.gameObject.SetActive(page == Page.Wins);
        Rebuild();
    }

    private void Update()
    {
        if (Time.unscaledTime < nextTimer)
            return;
        nextTimer = Time.unscaledTime + 20f;
        dailyTimer.text = "Yenilenmesine " + Missions.TimeLeft(false);
        weeklyTimer.text = "Yenilenmesine " + Missions.TimeLeft(true);
        seasonTimer.text = "SEZON " + Missions.Season + "  •  bitmesine " + Missions.SeasonDaysLeft + " gün  •  her görevin 5 aşaması var";
        winsTimer.text = "BUGÜN " + Missions.WinsToday + " ZAFER  •  her zafer bir kutu açar (ilk 6)  •  yenilenmesine " + Missions.TimeLeft(false);
    }

    private void Rebuild()
    {
        nextTimer = 0f;
        BuildChests();
        switch (page)
        {
            case Page.Missions:
                Fill(dailyList, false);
                Fill(weeklyList, true);
                break;
            case Page.Season:
                FillSeason();
                break;
            default:
                FillWins();
                break;
        }
    }

    private static void Clear(RectTransform r)
    {
        for (int i = r.childCount - 1; i >= 0; i--)
        {
            var ch = r.GetChild(i);
            ch.SetParent(null, false);
            Destroy(ch.gameObject);
        }
    }

    // ----- This week's mission box track -----

    private void BuildChests()
    {
        Clear(chestStrip);
        var c = new Vector2(0.5f, 0.5f);
        var bg = UIUtil.CreateImage(chestStrip, "Bg", c, Vector2.zero, new Vector2(1700f, 120f), Theme.Panel, false);
        bg.raycastTarget = false;
        var head = UIUtil.CreateText(chestStrip, "GÖREV SANDIĞI", new Vector2(0f, 0.5f), new Vector2(170f, 18f), new Vector2(300f, 40f), 28, TextAnchor.MiddleLeft);
        head.fontStyle = FontStyle.Bold;
        head.color = Theme.Accent;
        int pts = Missions.ChestPoints;
        var sub = UIUtil.CreateText(chestStrip, "Bu hafta toplanan görev: " + pts, new Vector2(0f, 0.5f), new Vector2(170f, -20f), new Vector2(300f, 30f), 20, TextAnchor.MiddleLeft);
        sub.color = Theme.TextDim;
        // the track: 0..8
        const float x0 = -480f, x1 = 760f;
        int max = Missions.ChestAt[Missions.ChestAt.Length - 1];
        UIUtil.CreateImage(chestStrip, "Track", c, new Vector2((x0 + x1) * 0.5f, 0f), new Vector2(x1 - x0, 14f), new Color(1f, 1f, 1f, 0.12f), false).raycastTarget = false;
        float frac = Mathf.Clamp01((float)pts / max);
        var fill = UIUtil.CreateImage(chestStrip, "Fill", c, new Vector2(x0, 0f), new Vector2((x1 - x0) * frac, 14f), Theme.Good, false);
        fill.rectTransform.pivot = new Vector2(0f, 0.5f);
        fill.raycastTarget = false;
        for (int i = 0; i < Missions.ChestAt.Length; i++)
        {
            int at = Missions.ChestAt[i];
            float x = Mathf.Lerp(x0, x1, (float)at / max);
            bool ready = Missions.ChestReady(i), claimed = Missions.ChestClaimed(i);
            string box = Missions.ChestRewards[i].id;
            Text label;
            var b = UIUtil.CreateButton(chestStrip, "", c, new Vector2(x, 6f), new Vector2(104f, 104f), ready ? new Color(0.2f, 0.45f, 0.2f, 0.95f) : new Color(0f, 0f, 0f, 0.35f), false, 20, out label);
            var ic = Icons.Create(b.transform, "crate_" + box, c, new Vector2(0f, 6f), new Vector2(86f, 86f));
            if (claimed)
                ic.color = new Color(1f, 1f, 1f, 0.3f);
            var n = UIUtil.CreateText(b.transform, claimed ? "ALINDI" : at.ToString(), new Vector2(0.5f, 0f), new Vector2(0f, -4f), new Vector2(104f, 28f), 22, TextAnchor.MiddleCenter);
            n.fontStyle = FontStyle.Bold;
            n.color = ready ? Theme.Good : claimed ? Theme.TextDim : Color.white;
            if (ready)
                b.gameObject.AddComponent<Pulse>();
            int index = i;
            b.onClick.AddListener(() =>
            {
                GrantedReward g;
                if (!Missions.ClaimChest(index, GameManager.Instance.profile, out g))
                {
                    statusText.text = Missions.ChestClaimed(index) ? "Bu sandığı aldın." : "Bu sandık için bu hafta " + Missions.ChestAt[index] + " görev topla.";
                    return;
                }
                OpenBox(g, "GÖREV SANDIĞI");
            });
        }
    }

    // ----- Daily + weekly -----

    private void Fill(RectTransform list, bool weekly)
    {
        Clear(list);
        float y = 0f;
        foreach (var s in Missions.Current(weekly))
            y = Row(list, s, y);
        if (!weekly)
            Row(list, Missions.Bonus(), y);
    }

    private float Row(RectTransform list, Missions.State s, float y)
    {
        bool bonus = s.def == Missions.DailyBonus;
        var bg = UIUtil.CreateImage(list, "Mission", new Vector2(0.5f, 1f), new Vector2(0f, -y - 68f), new Vector2(830f, 136f),
            s.claimed ? new Color(0.08f, 0.1f, 0.1f, 0.7f) : bonus ? new Color(0.22f, 0.16f, 0.05f, 0.95f) : Theme.Panel, false);
        var t = bg.transform;
        RewardView.Create(t, s.def.reward, new Vector2(0f, 0.5f), new Vector2(76f, 0f), 104f);
        var text = UIUtil.CreateText(t, Missions.Text(s.def), new Vector2(0f, 0.5f), new Vector2(360f, 32f), new Vector2(400f, 44f), 26, TextAnchor.MiddleLeft);
        text.fontStyle = FontStyle.Bold;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        if (s.claimed)
            text.color = new Color(1f, 1f, 1f, 0.45f);
        var reward = UIUtil.CreateText(t, "Ödül: " + s.def.reward.Name, new Vector2(0f, 0.5f), new Vector2(360f, -2f), new Vector2(400f, 30f), 21, TextAnchor.MiddleLeft);
        reward.color = new Color(1f, 0.85f, 0.3f);
        float frac = Mathf.Clamp01((float)s.progress / Mathf.Max(1, s.def.goal));
        UIUtil.CreateImage(t, "Bar", new Vector2(0f, 0.5f), new Vector2(360f, -36f), new Vector2(400f, 14f), new Color(1f, 1f, 1f, 0.12f), false).raycastTarget = false;
        var fill = UIUtil.CreateImage(t, "Fill", new Vector2(0f, 0.5f), new Vector2(160f, -36f), new Vector2(400f * frac, 14f), s.Done ? Theme.Good : Theme.Accent, false);
        fill.rectTransform.pivot = new Vector2(0f, 0.5f);
        fill.raycastTarget = false;
        if (!bonus)
        {
            var pr = UIUtil.CreateText(t, s.progress.ToString("N0") + " / " + s.def.goal.ToString("N0"), new Vector2(0f, 0.5f), new Vector2(614f, -36f), new Vector2(100f, 30f), 20, TextAnchor.MiddleLeft);
            pr.color = Theme.TextDim;
        }
        Text label;
        if (s.claimed)
        {
            var done = UIUtil.CreateText(t, "ALINDI", new Vector2(1f, 0.5f), new Vector2(-90f, 0f), new Vector2(160f, 50f), 26, TextAnchor.MiddleCenter);
            done.color = Theme.Good;
        }
        else if (s.Done)
        {
            var b = UIUtil.CreateButton(t, "TOPLA", new Vector2(1f, 0.5f), new Vector2(-88f, 0f), new Vector2(146f, 80f), Theme.Good, false, 30, out label);
            label.color = new Color(0.05f, 0.12f, 0.05f);
            label.GetComponent<Shadow>().enabled = false;
            b.gameObject.AddComponent<Pulse>();
            var st = s;
            b.onClick.AddListener(() => Collect(st));
        }
        return y + 146f;
    }

    private void Collect(Missions.State s)
    {
        var p = GameManager.Instance.profile;
        GrantedReward g;
        if (!Missions.Claim(s, p, out g))
            return;
        OpenBox(g, "GÖREV ÖDÜLÜ");
    }

    /// <summary>A box reward opens right away; anything else is just announced.</summary>
    private void OpenBox(GrantedReward g, string heading)
    {
        var p = GameManager.Instance.profile;
        UiSound.Confirm();
        if (g.reward.kind == RewardKind.Crate)
        {
            var given = Shop.OpenCrate(p, g.reward.id);
            CrateOpening.Show(g.reward.id, given, heading + ": " + Shop.Crate(g.reward.id).name, Rebuild);
        }
        else
        {
            statusText.text = "+" + g.reward.Name;
            Sfx.Play(SoundBank.Pickup, 0.8f, 1.2f);
        }
        Rebuild();
    }

    // ----- Season -----

    private void FillSeason()
    {
        Clear(seasonList);
        var list = Missions.SeasonMissions();
        float y = 0f;
        foreach (var s in list)
        {
            var bg = UIUtil.CreateImage(seasonList, "Season", new Vector2(0.5f, 1f), new Vector2(0f, -y - 64f), new Vector2(1700f, 128f),
                s.AllDone ? new Color(0.08f, 0.1f, 0.1f, 0.7f) : Theme.Panel, false);
            var t = bg.transform;
            var text = UIUtil.CreateText(t, s.AllDone ? string.Format(s.def.text, s.def.goals[s.def.goals.Length - 1].ToString("N0")) : Missions.SeasonText(s),
                new Vector2(0f, 0.5f), new Vector2(250f, 22f), new Vector2(460f, 44f), 28, TextAnchor.MiddleLeft);
            text.fontStyle = FontStyle.Bold;
            if (s.AllDone)
                text.color = new Color(1f, 1f, 1f, 0.45f);
            int goal = s.Goal;
            int prev = s.claimed > 0 ? s.def.goals[s.claimed - 1] : 0;
            float frac = s.AllDone ? 1f : Mathf.Clamp01((float)(s.progress - prev) / Mathf.Max(1, goal - prev));
            UIUtil.CreateImage(t, "Bar", new Vector2(0f, 0.5f), new Vector2(250f, -24f), new Vector2(440f, 14f), new Color(1f, 1f, 1f, 0.12f), false).raycastTarget = false;
            var fill = UIUtil.CreateImage(t, "Fill", new Vector2(0f, 0.5f), new Vector2(30f, -24f), new Vector2(440f * frac, 14f), s.Ready ? Theme.Good : Theme.Accent, false);
            fill.rectTransform.pivot = new Vector2(0f, 0.5f);
            fill.raycastTarget = false;
            var pr = UIUtil.CreateText(t, Mathf.Min(s.progress, s.def.goals[s.def.goals.Length - 1]).ToString("N0") + " / " + goal.ToString("N0"),
                new Vector2(0f, 0.5f), new Vector2(560f, -24f), new Vector2(200f, 30f), 20, TextAnchor.MiddleLeft);
            pr.color = Theme.TextDim;
            // the five stages with their rewards
            for (int k = 0; k < s.def.goals.Length; k++)
            {
                float x = 760f + k * 150f;
                bool got = k < s.claimed, now = k == s.claimed && !s.AllDone;
                var rv = RewardView.Create(t, Missions.StageRewards[k], new Vector2(0f, 0.5f), new Vector2(x, 8f), 92f);
                if (got)
                    foreach (var gr in rv.GetComponentsInChildren<Graphic>())
                        gr.color = new Color(gr.color.r, gr.color.g, gr.color.b, gr.color.a * 0.3f);
                var st = UIUtil.CreateText(t, got ? "ALINDI" : (k + 1) + ". AŞAMA", new Vector2(0f, 0.5f), new Vector2(x, -50f), new Vector2(140f, 26f), 18, TextAnchor.MiddleCenter);
                st.color = got ? Theme.Good : now ? Theme.Accent : Theme.TextDim;
            }
            Text label;
            if (s.Ready)
            {
                var b = UIUtil.CreateButton(t, "TOPLA", new Vector2(1f, 0.5f), new Vector2(-95f, 0f), new Vector2(150f, 80f), Theme.Good, false, 30, out label);
                label.color = new Color(0.05f, 0.12f, 0.05f);
                label.GetComponent<Shadow>().enabled = false;
                b.gameObject.AddComponent<Pulse>();
                var ss = s;
                b.onClick.AddListener(() =>
                {
                    GrantedReward g;
                    if (Missions.ClaimStage(ss, GameManager.Instance.profile, out g))
                        OpenBox(g, "SEZON ÖDÜLÜ");
                });
            }
            else if (s.AllDone)
            {
                var done = UIUtil.CreateText(t, "TAMAM", new Vector2(1f, 0.5f), new Vector2(-95f, 0f), new Vector2(160f, 50f), 26, TextAnchor.MiddleCenter);
                done.color = Theme.Good;
            }
            y += 138f;
        }
        seasonList.sizeDelta = new Vector2(1720f, y + 10f);
    }

    // ----- Today's victories -----

    private void FillWins()
    {
        for (int i = winsPage.childCount - 1; i >= 1; i--)   // keep the heading
        {
            var ch = winsPage.GetChild(i);
            ch.SetParent(null, false);
            Destroy(ch.gameObject);
        }
        var c = new Vector2(0.5f, 0.5f);
        int wins = Missions.WinsToday;
        for (int i = 0; i < Missions.WinBoxes; i++)
        {
            float x = (i - (Missions.WinBoxes - 1) * 0.5f) * 280f;
            bool ready = Missions.WinReady(i), claimed = Missions.WinClaimed(i);
            var r = Missions.WinRewards[i];
            Color rc = Theme.Rarity(r.Rarity);
            var card = UIUtil.CreateImage(winsPage, "Win", c, new Vector2(x, 20f), new Vector2(260f, 420f),
                ready ? new Color(0.12f, 0.3f, 0.14f, 0.96f) : claimed ? new Color(0.06f, 0.07f, 0.08f, 0.8f) : new Color(rc.r * 0.15f, rc.g * 0.15f, rc.b * 0.15f, 0.95f), false);
            var o = card.gameObject.AddComponent<Outline>();
            o.effectColor = ready ? Theme.Good : new Color(rc.r, rc.g, rc.b, 0.5f);
            o.effectDistance = new Vector2(3f, -3f);
            var ct = card.transform;
            var n = UIUtil.CreateText(ct, (i + 1) + ". ZAFER", c, new Vector2(0f, 170f), new Vector2(250f, 44f), 30, TextAnchor.MiddleCenter);
            n.fontStyle = FontStyle.Bold;
            n.color = wins > i ? Theme.Good : Color.white;
            var ic = Icons.Create(ct, "crate_" + r.id, c, new Vector2(0f, 40f), new Vector2(190f, 190f));
            if (claimed)
                ic.color = new Color(1f, 1f, 1f, 0.3f);
            var nm = UIUtil.CreateText(ct, Shop.Crate(r.id).name, c, new Vector2(0f, -80f), new Vector2(250f, 60f), 22, TextAnchor.MiddleCenter);
            nm.horizontalOverflow = HorizontalWrapMode.Wrap;
            nm.color = Color.Lerp(rc, Color.white, 0.3f);
            Text label;
            if (claimed)
            {
                var done = UIUtil.CreateText(ct, "ALINDI", c, new Vector2(0f, -160f), new Vector2(240f, 50f), 28, TextAnchor.MiddleCenter);
                done.color = Theme.Good;
            }
            else if (ready)
            {
                var b = UIUtil.CreateButton(ct, "AÇ", c, new Vector2(0f, -160f), new Vector2(200f, 76f), Theme.Good, false, 32, out label);
                label.color = new Color(0.05f, 0.12f, 0.05f);
                label.GetComponent<Shadow>().enabled = false;
                b.gameObject.AddComponent<Pulse>();
                int index = i;
                b.onClick.AddListener(() =>
                {
                    GrantedReward g;
                    if (Missions.ClaimWin(index, GameManager.Instance.profile, out g))
                        OpenBox(g, (index + 1) + ". ZAFER");
                });
            }
            else
            {
                var lk = UIUtil.CreateText(ct, "Kilitli", c, new Vector2(0f, -160f), new Vector2(240f, 40f), 24, TextAnchor.MiddleCenter);
                lk.color = Theme.TextDim;
            }
        }
        var hint = UIUtil.CreateText(winsPage, "Botlu ya da çevrimiçi, her moddaki zafer sayılır. Sayaç gece yarısı sıfırlanır.", c, new Vector2(0f, -260f), new Vector2(1500f, 40f), 24, TextAnchor.MiddleCenter);
        hint.color = Theme.TextDim;
    }
}

/// <summary>Gently pulses a button's size (something to collect).</summary>
public class Pulse : MonoBehaviour
{
    private void Update()
    {
        transform.localScale = Vector3.one * (1f + 0.05f * Mathf.Sin(Time.unscaledTime * 5f));
    }
}
