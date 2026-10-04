using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// KARİYER: rank insignia and progress, the rank ladder, the 500-level reward track, tokens and
/// spares (duplicate rewards) that can be sold for credits.
/// </summary>
public class CareerScreen : MonoBehaviour
{
    private const int CardsPerPage = 5;

    private System.Action onClose;
    private Image rankIcon, nextRankIcon;
    private Text rankName, levelText, xpText, nextRankText, statsText, sparesText, sparesValue;
    private RectTransform xpFill;
    private RectTransform track;
    private RectTransform sparesList;
    private readonly List<Image> ladder = new List<Image>();
    private readonly Text[] tokenCounts = new Text[Progression.TokenTypes];
    private int pageStart = 2;

    private static readonly Color Back = new Color(0.035f, 0.05f, 0.075f, 0.98f);
    private static readonly Color Card = new Color(0.1f, 0.12f, 0.15f, 0.95f);

    public static CareerScreen Create(Transform canvas)
    {
        var rect = UIUtil.CreateStretch(canvas, "CareerScreen");
        rect.gameObject.AddComponent<Image>().color = Back;
        var s = rect.gameObject.AddComponent<CareerScreen>();
        s.Build();
        rect.gameObject.AddComponent<PopIn>();
        rect.gameObject.SetActive(false);
        return s;
    }

    private static ProfileData Profile { get { return GameManager.Instance != null ? GameManager.Instance.profile : null; } }

    private void Build()
    {
        var t = transform;
        Text unused;

        var back = UIUtil.CreateButton(t, "<", new Vector2(0f, 1f), new Vector2(80f, -70f), new Vector2(84f, 84f), Theme.Accent, false, 54, out unused);
        unused.color = new Color(0.1f, 0.1f, 0.1f);
        unused.GetComponent<Shadow>().enabled = false;
        back.onClick.AddListener(Close);
        var title = UIUtil.CreateText(t, "KARİYER", new Vector2(0f, 1f), new Vector2(330f, -70f), new Vector2(400f, 80f), 52, TextAnchor.MiddleLeft);
        title.fontStyle = FontStyle.Bold;

        // Rank ladder across the top.
        var strip = UIUtil.CreateRect(t, "Ladder", new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(1500f, 80f));
        for (int i = 0; i < Progression.RankNames.Length; i++)
        {
            var img = Icons.Create(strip, Icons.Rank(i), new Vector2(0f, 0.5f), new Vector2(34f + i * 68f, 0f), new Vector2(60f, 60f));
            ladder.Add(img);
        }

        // Left: current rank.
        var left = Theme.Box(t, "Rank", new Vector2(0f, 0.5f), new Vector2(360f, -70f), new Vector2(620f, 700f), Card, false).transform;
        rankIcon = Icons.Create(left, Icons.Rank(0), new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(240f, 240f));
        rankName = UIUtil.CreateText(left, "", new Vector2(0.5f, 1f), new Vector2(0f, -305f), new Vector2(580f, 60f), 46, TextAnchor.MiddleCenter);
        rankName.fontStyle = FontStyle.Bold;
        rankName.color = Theme.Accent;
        levelText = UIUtil.CreateText(left, "", new Vector2(0.5f, 1f), new Vector2(0f, -355f), new Vector2(580f, 40f), 30, TextAnchor.MiddleCenter);
        UIUtil.CreateImage(left, "XpBg", new Vector2(0.5f, 1f), new Vector2(0f, -400f), new Vector2(500f, 16f), new Color(1f, 1f, 1f, 0.12f), false).raycastTarget = false;
        var fill = UIUtil.CreateImage(left, "Xp", new Vector2(0.5f, 1f), new Vector2(-250f, -400f), new Vector2(0f, 16f), Theme.Accent, false);
        fill.raycastTarget = false;
        xpFill = fill.rectTransform;
        xpFill.pivot = new Vector2(0f, 0.5f);
        xpText = UIUtil.CreateText(left, "", new Vector2(0.5f, 1f), new Vector2(0f, -432f), new Vector2(580f, 34f), 24, TextAnchor.MiddleCenter);
        xpText.color = Theme.TextDim;
        nextRankIcon = Icons.Create(left, Icons.Rank(1), new Vector2(0f, 1f), new Vector2(90f, -510f), new Vector2(72f, 72f));
        nextRankText = UIUtil.CreateText(left, "", new Vector2(0f, 1f), new Vector2(350f, -510f), new Vector2(460f, 70f), 24, TextAnchor.MiddleLeft);
        nextRankText.horizontalOverflow = HorizontalWrapMode.Wrap;
        statsText = UIUtil.CreateText(left, "", new Vector2(0.5f, 0f), new Vector2(0f, 75f), new Vector2(580f, 110f), 26, TextAnchor.MiddleCenter);
        statsText.color = Theme.TextDim;

        // Right top: reward track.
        var head = UIUtil.CreateText(t, "ÖDÜL YOLU", new Vector2(1f, 0.5f), new Vector2(-620f, 330f), new Vector2(1060f, 50f), 34, TextAnchor.MiddleLeft);
        head.fontStyle = FontStyle.Bold;
        var sub = UIUtil.CreateText(t, "Her seviyede 1 ödül  •  sahip olduğun ödül Yedekler'e gider", new Vector2(1f, 0.5f), new Vector2(-620f, 292f), new Vector2(1060f, 34f), 22, TextAnchor.MiddleLeft);
        sub.color = Theme.TextDim;
        UIUtil.CreateButton(t, "<", new Vector2(1f, 0.5f), new Vector2(-1130f, 120f), new Vector2(64f, 240f), Card, false, 34, out unused)
            .onClick.AddListener(() => Page(-CardsPerPage));
        UIUtil.CreateButton(t, ">", new Vector2(1f, 0.5f), new Vector2(-70f, 120f), new Vector2(64f, 240f), Card, false, 34, out unused)
            .onClick.AddListener(() => Page(CardsPerPage));
        track = UIUtil.CreateRect(t, "Track", new Vector2(1f, 0.5f), new Vector2(-600f, 120f), new Vector2(1000f, 270f));

        // Right bottom: tokens and spares.
        var bottom = Theme.Box(t, "Spares", new Vector2(1f, 0.5f), new Vector2(-600f, -250f), new Vector2(1124f, 380f), Card, false).transform;
        var tk = UIUtil.CreateText(bottom, "JETONLAR", new Vector2(0f, 1f), new Vector2(170f, -34f), new Vector2(300f, 40f), 28, TextAnchor.MiddleLeft);
        tk.fontStyle = FontStyle.Bold;
        for (int i = 0; i < Progression.TokenTypes; i++)
        {
            int index = i;
            float y = -100f - i * 88f;
            Icons.Create(bottom, Progression.TokenIcons[i], new Vector2(0f, 1f), new Vector2(60f, y), new Vector2(76f, 76f));
            var n = UIUtil.CreateText(bottom, Progression.TokenNames[i], new Vector2(0f, 1f), new Vector2(250f, y + 14f), new Vector2(260f, 34f), 22, TextAnchor.MiddleLeft);
            n.fontStyle = FontStyle.Bold;
            tokenCounts[i] = UIUtil.CreateText(bottom, "", new Vector2(0f, 1f), new Vector2(250f, y - 18f), new Vector2(260f, 30f), 22, TextAnchor.MiddleLeft);
            tokenCounts[i].color = Theme.Accent;
            UIUtil.CreateButton(bottom, "SAT +" + Progression.TokenSellValue[i], new Vector2(0f, 1f), new Vector2(470f, y), new Vector2(150f, 56f), Theme.PanelLight, false, 20, out unused)
                .onClick.AddListener(() => SellToken((TokenType)index));
        }

        UIUtil.CreateImage(bottom, "Divider", new Vector2(0f, 0.5f), new Vector2(570f, 0f), new Vector2(2f, 330f), new Color(1f, 1f, 1f, 0.12f), false).raycastTarget = false;
        var sp = UIUtil.CreateText(bottom, "YEDEKLER", new Vector2(0f, 1f), new Vector2(740f, -34f), new Vector2(300f, 40f), 28, TextAnchor.MiddleLeft);
        sp.fontStyle = FontStyle.Bold;
        sparesText = UIUtil.CreateText(bottom, "", new Vector2(0f, 1f), new Vector2(840f, -76f), new Vector2(500f, 34f), 22, TextAnchor.MiddleLeft);
        sparesText.color = Theme.TextDim;
        sparesList = UIUtil.CreateRect(bottom, "SpareList", new Vector2(0f, 1f), new Vector2(845f, -170f), new Vector2(510f, 110f));
        sparesValue = UIUtil.CreateText(bottom, "", new Vector2(0f, 0f), new Vector2(740f, 52f), new Vector2(300f, 40f), 24, TextAnchor.MiddleLeft);
        sparesValue.color = Theme.Accent;
        var conv = UIUtil.CreateButton(bottom, "PARAYA ÇEVİR", new Vector2(1f, 0f), new Vector2(-150f, 52f), new Vector2(260f, 70f), Theme.Accent, false, 26, out unused);
        unused.color = new Color(0.1f, 0.08f, 0.02f);
        unused.GetComponent<Shadow>().enabled = false;
        conv.onClick.AddListener(ConvertSpares);
    }

    public void Open(System.Action closed)
    {
        onClose = closed;
        var p = Profile;
        pageStart = Mathf.Clamp(p != null ? p.level + 1 : 2, 2, Progression.MaxLevel - CardsPerPage + 1);
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        Refresh();
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

    private void Page(int delta)
    {
        pageStart = Mathf.Clamp(pageStart + delta, 2, Progression.MaxLevel - CardsPerPage + 1);
        BuildTrack();
    }

    private void Refresh()
    {
        var p = Profile;
        if (p == null)
            return;
        int rank = p.RankIndex;
        Icons.Set(rankIcon, Icons.Rank(rank));
        rankName.text = p.RankName.ToUpper();
        levelText.text = "SEVİYE " + p.level + " / " + Progression.MaxLevel;
        int need = p.XpForNextLevel;
        xpFill.sizeDelta = new Vector2(500f * (p.IsMaxLevel ? 1f : Mathf.Clamp01((float)p.xp / Mathf.Max(1, need))), 16f);
        xpText.text = p.IsMaxLevel ? "En yüksek rütbe!" : p.xp.ToString("N0") + " / " + need.ToString("N0") + " XP";
        if (rank + 1 < Progression.RankNames.Length)
        {
            nextRankIcon.gameObject.SetActive(true);
            Icons.Set(nextRankIcon, Icons.Rank(rank + 1));
            nextRankText.text = "Sonraki rütbe: " + Progression.RankNames[rank + 1] + "\nSeviye " + Progression.RankStart[rank + 1];
        }
        else
        {
            nextRankIcon.gameObject.SetActive(false);
            nextRankText.text = "";
        }
        float winRate = p.matches > 0 ? 100f * p.wins / p.matches : 0f;
        float kpm = p.matches > 0 ? (float)p.totalKills / p.matches : 0f;
        statsText.text = "Maç " + p.matches + "     Zafer " + p.wins + "     %" + Mathf.RoundToInt(winRate) +
                         "\nÖldürme " + p.totalKills + "     Maç başına " + kpm.ToString("0.0");

        for (int i = 0; i < ladder.Count; i++)
        {
            bool reached = i <= rank;
            ladder[i].color = new Color(1f, 1f, 1f, reached ? 1f : 0.28f);
            ladder[i].rectTransform.localScale = Vector3.one * (i == rank ? 1.3f : 1f);
        }

        for (int i = 0; i < Progression.TokenTypes; i++)
            tokenCounts[i].text = "x" + Progression.TokenCount((TokenType)i);

        var spares = Progression.Spares();
        sparesText.text = spares.Count == 0 ? "Yedek yok" : spares.Count + " eşya";
        sparesValue.text = spares.Count == 0 ? "" : "Değer: " + Progression.SparesTotalValue().ToString("N0") + " Kredi";
        for (int i = sparesList.childCount - 1; i >= 0; i--)
            Destroy(sparesList.GetChild(i).gameObject);
        for (int i = 0; i < spares.Count && i < 5; i++)
            RewardView.Create(sparesList, spares[i], new Vector2(0f, 0.5f), new Vector2(50f + i * 104f, 0f), 92f);
        if (spares.Count > 5)
        {
            var more = UIUtil.CreateText(sparesList, "+" + (spares.Count - 5), new Vector2(1f, 0.5f), new Vector2(10f, 0f), new Vector2(80f, 40f), 26, TextAnchor.MiddleLeft);
            more.color = Theme.TextDim;
        }

        BuildTrack();
    }

    private void BuildTrack()
    {
        for (int i = track.childCount - 1; i >= 0; i--)
            Destroy(track.GetChild(i).gameObject);
        var p = Profile;
        int current = p != null ? p.level : 1;
        for (int i = 0; i < CardsPerPage; i++)
        {
            int level = pageStart + i;
            if (level > Progression.MaxLevel)
                break;
            BuildCard(level, current, new Vector2(-400f + i * 200f, 0f));
        }
    }

    private void BuildCard(int level, int current, Vector2 pos)
    {
        bool got = level <= current;
        bool next = level == current + 1;
        bool rankUp = Progression.IsRankUp(level) || level == Progression.MaxLevel;
        var r = Progression.RewardFor(level);

        var bg = UIUtil.CreateImage(track, "Card" + level, new Vector2(0.5f, 0.5f), pos, new Vector2(184f, 264f),
            next ? new Color(0.22f, 0.19f, 0.08f, 0.97f) : Card, false);
        bg.raycastTarget = false;
        var t = bg.transform;
        if (rankUp || next)
        {
            var border = Icons.Create(t, "rarity_frame", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(184f, 264f));
            border.preserveAspect = false;
            border.color = new Color(Theme.Accent.r, Theme.Accent.g, Theme.Accent.b, next ? 0.9f : 0.5f);
        }
        var lv = UIUtil.CreateText(t, "SV " + level, new Vector2(0.5f, 1f), new Vector2(0f, -26f), new Vector2(170f, 34f), 26, TextAnchor.MiddleCenter);
        lv.fontStyle = FontStyle.Bold;
        lv.color = got ? Theme.Good : (next ? Theme.Accent : Color.white);
        if (rankUp)
            Icons.Create(t, Icons.Rank(Progression.RankIndex(level)), new Vector2(1f, 1f), new Vector2(-26f, -26f), new Vector2(44f, 44f));

        RewardView.Create(t, r, new Vector2(0.5f, 0.5f), new Vector2(0f, 22f), 120f);
        var name = UIUtil.CreateText(t, r.Name, new Vector2(0.5f, 0f), new Vector2(0f, 62f), new Vector2(170f, 56f), 19, TextAnchor.MiddleCenter);
        name.horizontalOverflow = HorizontalWrapMode.Wrap;
        name.color = Theme.Rarity(r.Rarity) * 0.3f + Color.white * 0.7f;
        var state = UIUtil.CreateText(t, got ? "ALINDI" : (next ? "SIRADAKİ" : "KİLİTLİ"), new Vector2(0.5f, 0f), new Vector2(0f, 20f), new Vector2(170f, 30f), 18, TextAnchor.MiddleCenter);
        state.color = got ? Theme.Good : (next ? Theme.Accent : Theme.TextDim);
        if (!got)
            bg.color = new Color(bg.color.r, bg.color.g, bg.color.b, next ? 0.97f : 0.8f);
    }

    private void SellToken(TokenType t)
    {
        var p = Profile;
        if (p == null)
            return;
        if (Progression.SellToken(p, t))
        {
            UiSound.Confirm();
            Refresh();
        }
    }

    private void ConvertSpares()
    {
        var p = Profile;
        if (p == null || Progression.Spares().Count == 0)
            return;
        int gained = Progression.ConvertSpares(p);
        UiSound.Confirm();
        if (GameManager.Instance != null && GameManager.Instance.uiManager != null)
            GameManager.Instance.uiManager.Toast("+" + gained.ToString("N0") + " Kredi");
        Refresh();
    }
}
