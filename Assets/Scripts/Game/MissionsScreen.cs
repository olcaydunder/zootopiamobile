using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// GÖREVLER: today's three missions (+ a bonus box for finishing all three) and this week's three, with
/// progress bars, rewards and the time until they change. TOPLA gives the reward; a box opens right away.
/// </summary>
public class MissionsScreen : MonoBehaviour
{
    private System.Action onClose;
    private RectTransform dailyList, weeklyList;
    private Text dailyTimer, weeklyTimer, statusText;
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
        var hint = UIUtil.CreateText(t, "Görevler her maçın sonunda ilerler (botlu ya da çevrimiçi). Bitirince TOPLA'ya bas.",
            new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(1600f, 40f), 26, TextAnchor.MiddleCenter);
        hint.color = Theme.TextDim;

        dailyList = Column(t, "GÜNLÜK GÖREVLER", -440f, out dailyTimer);
        weeklyList = Column(t, "HAFTALIK GÖREVLER", 440f, out weeklyTimer);
        statusText = UIUtil.CreateText(t, "", new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(1600f, 40f), 26, TextAnchor.MiddleCenter);
        statusText.color = Theme.Accent;
    }

    private static RectTransform Column(Transform t, string heading, float x, out Text timer)
    {
        var col = UIUtil.CreateRect(t, heading, new Vector2(0.5f, 0.5f), new Vector2(x, -80f), new Vector2(840f, 760f));
        var h = UIUtil.CreateText(col, heading, new Vector2(0f, 1f), new Vector2(240f, -26f), new Vector2(460f, 50f), 34, TextAnchor.MiddleLeft);
        h.fontStyle = FontStyle.Bold;
        h.color = Theme.Accent;
        timer = UIUtil.CreateText(col, "", new Vector2(1f, 1f), new Vector2(-200f, -26f), new Vector2(380f, 40f), 24, TextAnchor.MiddleRight);
        timer.color = Theme.TextDim;
        var list = UIUtil.CreateRect(col, "List", new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(840f, 10f));
        list.pivot = new Vector2(0.5f, 1f);
        return list;
    }

    public void Open(System.Action closed)
    {
        onClose = closed;
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        statusText.text = "";
        Rebuild();
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

    private void Update()
    {
        if (Time.unscaledTime < nextTimer)
            return;
        nextTimer = Time.unscaledTime + 20f;
        dailyTimer.text = "Yenilenmesine " + Missions.TimeLeft(false);
        weeklyTimer.text = "Yenilenmesine " + Missions.TimeLeft(true);
    }

    private void Rebuild()
    {
        nextTimer = 0f;
        Fill(dailyList, false);
        Fill(weeklyList, true);
    }

    private void Fill(RectTransform list, bool weekly)
    {
        for (int i = list.childCount - 1; i >= 0; i--)
        {
            var ch = list.GetChild(i);
            ch.SetParent(null, false);
            Destroy(ch.gameObject);
        }
        float y = 0f;
        foreach (var s in Missions.Current(weekly))
            y = Row(list, s, y);
        if (!weekly)
            Row(list, Missions.Bonus(), y);
    }

    private float Row(RectTransform list, Missions.State s, float y)
    {
        bool bonus = s.def == Missions.DailyBonus;
        var bg = UIUtil.CreateImage(list, "Mission", new Vector2(0.5f, 1f), new Vector2(0f, -y - 80f), new Vector2(830f, 150f),
            s.claimed ? new Color(0.08f, 0.1f, 0.1f, 0.7f) : bonus ? new Color(0.22f, 0.16f, 0.05f, 0.95f) : Theme.Panel, false);
        var t = bg.transform;
        RewardView.Create(t, s.def.reward, new Vector2(0f, 0.5f), new Vector2(80f, 0f), 110f);
        var text = UIUtil.CreateText(t, Missions.Text(s.def), new Vector2(0f, 0.5f), new Vector2(360f, 34f), new Vector2(400f, 44f), 28, TextAnchor.MiddleLeft);
        text.fontStyle = FontStyle.Bold;
        if (s.claimed)
            text.color = new Color(1f, 1f, 1f, 0.45f);
        var reward = UIUtil.CreateText(t, "Ödül: " + s.def.reward.Name, new Vector2(0f, 0.5f), new Vector2(360f, 0f), new Vector2(400f, 30f), 22, TextAnchor.MiddleLeft);
        reward.color = new Color(1f, 0.85f, 0.3f);
        // progress
        float frac = Mathf.Clamp01((float)s.progress / Mathf.Max(1, s.def.goal));
        UIUtil.CreateImage(t, "Bar", new Vector2(0f, 0.5f), new Vector2(360f, -38f), new Vector2(400f, 14f), new Color(1f, 1f, 1f, 0.12f), false).raycastTarget = false;
        var fill = UIUtil.CreateImage(t, "Fill", new Vector2(0f, 0.5f), new Vector2(160f, -38f), new Vector2(400f * frac, 14f), s.Done ? Theme.Good : Theme.Accent, false);
        fill.rectTransform.pivot = new Vector2(0f, 0.5f);
        fill.raycastTarget = false;
        if (!bonus)
        {
            var pr = UIUtil.CreateText(t, s.progress.ToString("N0") + " / " + s.def.goal.ToString("N0"), new Vector2(0f, 0.5f), new Vector2(614f, -38f), new Vector2(100f, 30f), 20, TextAnchor.MiddleLeft);
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
            var b = UIUtil.CreateButton(t, "TOPLA", new Vector2(1f, 0.5f), new Vector2(-88f, 0f), new Vector2(146f, 84f), Theme.Good, false, 30, out label);
            label.color = new Color(0.05f, 0.12f, 0.05f);
            label.GetComponent<Shadow>().enabled = false;
            b.gameObject.AddComponent<Pulse>();
            var st = s;
            b.onClick.AddListener(() => Collect(st));
        }
        return y + 162f;
    }

    private void Collect(Missions.State s)
    {
        var p = GameManager.Instance.profile;
        GrantedReward g;
        if (!Missions.Claim(s, p, out g))
            return;
        UiSound.Confirm();
        if (g.reward.kind == RewardKind.Crate)
        {
            // The mission's box opens right away (it was put with the player's boxes by Claim).
            var given = Shop.OpenCrate(p, g.reward.id);
            CrateOpening.Show(g.reward.id, given, "GÖREV ÖDÜLÜ: " + Shop.Crate(g.reward.id).name, Rebuild);
        }
        else
        {
            statusText.text = "+" + g.reward.Name;
            Sfx.Play(SoundBank.Pickup, 0.8f, 1.2f);
        }
        Rebuild();
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
