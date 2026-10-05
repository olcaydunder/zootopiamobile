using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// OYUN MODU: Battle Royale (solo, duo, squad), 5v5 Takım Ölüm Maçı, Hakimiyet, Herkes Tek and Soygun as
/// cards with what each is about and where it can be played (online and/or with bots). Picking one returns
/// to the lobby with that mode selected.
/// </summary>
public class ModeSelectScreen : MonoBehaviour
{
    private System.Action<MatchMode> picked;
    private System.Action closed;
    private MatchMode current;
    private RectTransform cards;

    public static ModeSelectScreen Create(Transform canvas)
    {
        var rect = UIUtil.CreateStretch(canvas, "ModeSelect");
        rect.gameObject.AddComponent<Image>().color = new Color(0.03f, 0.05f, 0.09f, 0.97f);
        var s = rect.gameObject.AddComponent<ModeSelectScreen>();
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
        Icons.Create(t, "modes", new Vector2(0f, 1f), new Vector2(190f, -70f), new Vector2(84f, 84f));
        var title = UIUtil.CreateText(t, "OYUN MODU", new Vector2(0f, 1f), new Vector2(460f, -70f), new Vector2(500f, 80f), 52, TextAnchor.MiddleLeft);
        title.fontStyle = FontStyle.Bold;
        var hint = UIUtil.CreateText(t, "Bir mod seç: lobide BAŞLAT botlarla, ÇEVRİMİÇİ gerçek oyuncularla oynatır.", new Vector2(0.5f, 1f), new Vector2(0f, -160f), new Vector2(1600f, 40f), 26, TextAnchor.MiddleCenter);
        hint.color = Theme.TextDim;
        cards = UIUtil.CreateRect(t, "Cards", new Vector2(0.5f, 0.5f), new Vector2(0f, -70f), new Vector2(1800f, 760f));
    }

    public void Open(MatchMode selected, System.Action<MatchMode> onPick, System.Action onClose)
    {
        current = selected;
        picked = onPick;
        closed = onClose;
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        Rebuild();
    }

    public void Hide() { gameObject.SetActive(false); }

    private void Close()
    {
        Hide();
        if (closed != null)
            closed();
    }

    private void Pick(MatchMode m)
    {
        UiSound.Confirm();
        Hide();
        if (picked != null)
            picked(m);
    }

    private void Rebuild()
    {
        for (int i = cards.childCount - 1; i >= 0; i--)
            Destroy(cards.GetChild(i).gameObject);
        MatchMode[] order = { MatchMode.Solo, MatchMode.Team5, MatchMode.Domination, MatchMode.FreeForAll, MatchMode.Heist };
        const float w = 340f, h = 720f;
        for (int i = 0; i < order.Length; i++)
        {
            var m = order[i];
            bool br = m == MatchMode.Solo;
            bool sel = br ? !Modes.Arena(current) : current == m;
            Color mc = Modes.Color(m);
            float x = (i - (order.Length - 1) * 0.5f) * (w + 14f);
            Text label;
            var card = UIUtil.CreateButton(cards, "", new Vector2(0.5f, 0.5f), new Vector2(x, 0f), new Vector2(w, h),
                sel ? new Color(mc.r * 0.3f, mc.g * 0.3f, mc.b * 0.3f, 0.98f) : new Color(mc.r * 0.12f, mc.g * 0.12f, mc.b * 0.12f, 0.95f), false, 20, out label);
            var o = card.gameObject.AddComponent<Outline>();
            o.effectColor = sel ? mc : new Color(mc.r, mc.g, mc.b, 0.35f);
            o.effectDistance = sel ? new Vector2(5f, -5f) : new Vector2(2f, -2f);
            var ct = card.transform;
            var top = new Vector2(0.5f, 1f);
            Icons.Create(ct, Modes.Icon(m), top, new Vector2(0f, -120f), new Vector2(190f, 190f)).raycastTarget = false;
            var name = UIUtil.CreateText(ct, br ? "BATTLE ROYALE" : Modes.Title(m), top, new Vector2(0f, -250f), new Vector2(w - 16f, 50f), 32, TextAnchor.MiddleCenter);
            name.fontStyle = FontStyle.Bold;
            name.color = Color.Lerp(mc, Color.white, 0.35f);
            var desc = UIUtil.CreateText(ct, Modes.Description(m), top, new Vector2(0f, -340f), new Vector2(w - 34f, 120f), 22, TextAnchor.UpperCenter);
            desc.horizontalOverflow = HorizontalWrapMode.Wrap;
            desc.color = Theme.TextDim;
            string where = Modes.Online(m) ? "ÇEVRİMİÇİ  +  BOTLU" : "YALNIZ BOTLU";
            var tag = UIUtil.CreateImage(ct, "Where", top, new Vector2(0f, -440f), new Vector2(w - 60f, 36f), Modes.Online(m) ? new Color(0.16f, 0.45f, 0.95f, 0.85f) : new Color(0.35f, 0.38f, 0.45f, 0.85f), false);
            tag.raycastTarget = false;
            UIUtil.CreateText(tag.transform, where, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(w - 60f, 36f), 18, TextAnchor.MiddleCenter).fontStyle = FontStyle.Bold;
            var players = UIUtil.CreateText(ct, m == MatchMode.FreeForAll ? "8 oyuncu" : br ? "25 oyuncu" : "5'e 5", top, new Vector2(0f, -486f), new Vector2(w - 20f, 30f), 22, TextAnchor.MiddleCenter);
            players.color = Theme.TextDim;
            if (br)
            {
                // Battle Royale: pick the squad size right here.
                MatchMode[] sizes = { MatchMode.Solo, MatchMode.Duo, MatchMode.Squad };
                for (int k = 0; k < sizes.Length; k++)
                {
                    var sm = sizes[k];
                    bool on = current == sm;
                    Text sl;
                    var b = UIUtil.CreateButton(ct, Modes.Short(sm), new Vector2(0.5f, 0f), new Vector2((k - 1) * 106f, 120f), new Vector2(98f, 76f), on ? Theme.Accent : Theme.PanelLight, false, 24, out sl);
                    if (on)
                    {
                        sl.color = new Color(0.1f, 0.08f, 0.02f);
                        sl.GetComponent<Shadow>().enabled = false;
                    }
                    b.onClick.AddListener(() => Pick(sm));
                }
                card.onClick.AddListener(() => Pick(Modes.Arena(current) ? MatchMode.Solo : current));
            }
            else
            {
                var go = UIUtil.CreateButton(ct, sel ? "SEÇİLİ" : "SEÇ", new Vector2(0.5f, 0f), new Vector2(0f, 120f), new Vector2(w - 60f, 76f), sel ? Theme.PanelLight : Theme.Accent, false, 30, out label);
                if (!sel)
                {
                    label.color = new Color(0.1f, 0.08f, 0.02f);
                    label.GetComponent<Shadow>().enabled = false;
                }
                go.onClick.AddListener(() => Pick(m));
                card.onClick.AddListener(() => Pick(m));
            }
            if (sel)
            {
                var chk = UIUtil.CreateText(ct, "SEÇİLİ", new Vector2(0.5f, 0f), new Vector2(0f, 44f), new Vector2(w - 20f, 34f), 22, TextAnchor.MiddleCenter);
                chk.color = mc;
                chk.fontStyle = FontStyle.Bold;
            }
        }
    }
}
