using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// TEÇHİZAT: everything the player sets up before a match — class, which tokens to bring,
/// parachute camo and vehicle camo (owned ones are equipped, others bought with Kredi).
/// Character and starting weapon stay on the HAZIRLIK screen.
/// </summary>
public class LoadoutScreen : MonoBehaviour
{
    private static readonly string[] Tabs = { "SINIF", "JETONLAR", "PARAŞÜT", "ARAÇ KAMUFLAJI" };
    private static readonly Color Back = new Color(0.035f, 0.05f, 0.075f, 0.98f);
    private static readonly Color Card = new Color(0.1f, 0.12f, 0.15f, 0.95f);
    private static readonly Color CardOn = new Color(0.22f, 0.19f, 0.08f, 0.98f);

    private System.Action onClose;
    private RectTransform content;
    private Text coinsText;
    private readonly List<Image> tabImages = new List<Image>();
    private readonly List<Text> tabTexts = new List<Text>();
    private int tab;

    public static LoadoutScreen Create(Transform canvas)
    {
        var rect = UIUtil.CreateStretch(canvas, "LoadoutScreen");
        rect.gameObject.AddComponent<Image>().color = Back;
        var s = rect.gameObject.AddComponent<LoadoutScreen>();
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
        var title = UIUtil.CreateText(t, "TEÇHİZAT", new Vector2(0f, 1f), new Vector2(330f, -70f), new Vector2(400f, 80f), 52, TextAnchor.MiddleLeft);
        title.fontStyle = FontStyle.Bold;

        var coins = Theme.Box(t, "Coins", new Vector2(1f, 1f), new Vector2(-190f, -70f), new Vector2(300f, 80f), Theme.Panel, false).transform;
        Icons.Create(coins, "currency", new Vector2(0f, 0.5f), new Vector2(44f, 0f), new Vector2(58f, 58f));
        coinsText = UIUtil.CreateText(coins, "", new Vector2(0f, 0.5f), new Vector2(180f, 0f), new Vector2(200f, 60f), 34, TextAnchor.MiddleLeft);
        coinsText.fontStyle = FontStyle.Bold;

        for (int i = 0; i < Tabs.Length; i++)
        {
            int index = i;
            Text label;
            var b = UIUtil.CreateButton(t, Tabs[i], new Vector2(0.5f, 1f), new Vector2(-480f + i * 320f, -170f), new Vector2(300f, 70f), Card, false, 28, out label);
            b.onClick.AddListener(() => ShowTab(index));
            tabImages.Add(b.GetComponent<Image>());
            tabTexts.Add(label);
        }

        content = UIUtil.CreateStretch(t, "Content");
        content.offsetMin = new Vector2(60f, 40f);
        content.offsetMax = new Vector2(-60f, -230f);
    }

    public void Open(System.Action closed)
    {
        onClose = closed;
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        ShowTab(tab);
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

    private void ShowTab(int index)
    {
        tab = index;
        for (int i = 0; i < tabImages.Count; i++)
        {
            bool on = i == index;
            tabImages[i].color = on ? Theme.Selected : Card;
            tabTexts[i].color = on ? new Color(0.08f, 0.08f, 0.1f) : Color.white;
            tabTexts[i].GetComponent<Shadow>().enabled = !on;
        }
        for (int i = content.childCount - 1; i >= 0; i--)
            Destroy(content.GetChild(i).gameObject);
        var p = Profile;
        coinsText.text = p != null ? p.coins.ToString("N0") : "0";
        switch (index)
        {
            case 0: BuildClasses(); break;
            case 1: BuildTokens(); break;
            case 2: BuildCamos(Cosmetics.ParachuteCamos, false); break;
            default: BuildCamos(Cosmetics.VehicleCamos, true); break;
        }
    }

    // ----- SINIF -----

    private void BuildClasses()
    {
        var sel = ClassDefs.Get(ClassDefs.Selected);
        for (int i = 0; i < ClassDefs.All.Length; i++)
        {
            var c = ClassDefs.All[i];
            bool on = c.type == sel.type;
            float x = 150f + (i % 4) * 270f;
            float y = -150f - (i / 4) * 300f;
            Text unused;
            var b = UIUtil.CreateButton(content, "", new Vector2(0f, 1f), new Vector2(x, y), new Vector2(250f, 280f), on ? CardOn : Card, false, 20, out unused);
            var captured = c.type;
            b.onClick.AddListener(() => { ClassDefs.Selected = captured; UiSound.Confirm(); ShowTab(0); });
            var bt = b.transform;
            if (on)
            {
                var frame = Icons.Create(bt, "rarity_frame", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(250f, 280f));
                frame.preserveAspect = false;
                frame.color = Theme.Accent;
            }
            Icons.Create(bt, c.icon, new Vector2(0.5f, 1f), new Vector2(0f, -95f), new Vector2(150f, 150f));
            var n = UIUtil.CreateText(bt, c.name, new Vector2(0.5f, 0f), new Vector2(0f, 82f), new Vector2(240f, 40f), 28, TextAnchor.MiddleCenter);
            n.fontStyle = FontStyle.Bold;
            n.color = on ? Theme.Accent : Color.white;
            var a = UIUtil.CreateText(bt, c.ability, new Vector2(0.5f, 0f), new Vector2(0f, 42f), new Vector2(240f, 32f), 22, TextAnchor.MiddleCenter);
            a.color = c.color;
        }

        // Details of the selected class.
        var d = Theme.Box(content, "Details", new Vector2(1f, 0.5f), new Vector2(-330f, 0f), new Vector2(640f, 760f), Card, false).transform;
        Icons.Create(d, sel.icon, new Vector2(0.5f, 1f), new Vector2(0f, -120f), new Vector2(190f, 190f));
        var name = UIUtil.CreateText(d, sel.name.ToUpper(), new Vector2(0.5f, 1f), new Vector2(0f, -245f), new Vector2(600f, 60f), 44, TextAnchor.MiddleCenter);
        name.fontStyle = FontStyle.Bold;
        name.color = sel.color;
        Paragraph(d, "AKTİF  •  " + sel.ability.ToUpper() + "  (" + Mathf.RoundToInt(sel.cooldown) + " sn)", sel.abilityInfo, -300f, sel.color);
        Paragraph(d, "PASİF", sel.passive, -450f, Theme.Accent);
        Paragraph(d, "SEVİYE 2 (Güçlendirme Noktası / Jeton)", sel.level2, -580f, new Color(0.75f, 0.45f, 1f));
    }

    private static void Paragraph(Transform parent, string head, string body, float y, Color headColor)
    {
        var h = UIUtil.CreateText(parent, head, new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(580f, 34f), 24, TextAnchor.MiddleLeft);
        h.fontStyle = FontStyle.Bold;
        h.color = headColor;
        var b = UIUtil.CreateText(parent, body, new Vector2(0.5f, 1f), new Vector2(0f, y - 62f), new Vector2(580f, 90f), 24, TextAnchor.UpperLeft);
        b.horizontalOverflow = HorizontalWrapMode.Wrap;
        b.color = Theme.TextDim;
    }

    // ----- JETONLAR -----

    private void BuildTokens()
    {
        var note = UIUtil.CreateText(content, "Açık olan jetonlardan maça birer tane götürürsün; jeton sadece kullanırsan harcanır.", new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(1400f, 40f), 26, TextAnchor.MiddleCenter);
        note.color = Theme.TextDim;
        for (int i = 0; i < Progression.TokenTypes; i++)
        {
            var type = (TokenType)i;
            var card = Theme.Box(content, "Token", new Vector2(0.5f, 0.5f), new Vector2(-460f + i * 460f, -40f), new Vector2(420f, 640f), Card, false).transform;
            Icons.Create(card, Progression.TokenIcons[i], new Vector2(0.5f, 1f), new Vector2(0f, -130f), new Vector2(200f, 200f));
            var n = UIUtil.CreateText(card, Progression.TokenNames[i].ToUpper(), new Vector2(0.5f, 1f), new Vector2(0f, -260f), new Vector2(400f, 44f), 30, TextAnchor.MiddleCenter);
            n.fontStyle = FontStyle.Bold;
            int count = Progression.TokenCount(type);
            var c = UIUtil.CreateText(card, "Elinde: " + count, new Vector2(0.5f, 1f), new Vector2(0f, -305f), new Vector2(400f, 36f), 26, TextAnchor.MiddleCenter);
            c.color = count > 0 ? Theme.Accent : Theme.TextDim;
            var info = UIUtil.CreateText(card, Progression.TokenInfo[i], new Vector2(0.5f, 1f), new Vector2(0f, -390f), new Vector2(370f, 120f), 24, TextAnchor.UpperCenter);
            info.horizontalOverflow = HorizontalWrapMode.Wrap;
            info.color = Theme.TextDim;
            bool carry = Progression.CarryToken(type);
            Text label;
            var b = UIUtil.CreateButton(card, carry ? "MAÇA GÖTÜR: AÇIK" : "MAÇA GÖTÜR: KAPALI", new Vector2(0.5f, 0f), new Vector2(0f, 60f), new Vector2(360f, 72f),
                carry ? new Color(0.2f, 0.6f, 0.3f, 1f) : Theme.PanelLight, false, 24, out label);
            b.onClick.AddListener(() => { Progression.SetCarryToken(type, !Progression.CarryToken(type)); ShowTab(1); });
        }
    }

    // ----- PARAŞÜT / ARAÇ -----

    private void BuildCamos(List<CamoDef> list, bool vehicle)
    {
        string equipped = vehicle ? Cosmetics.EquippedVehicleCamo : Cosmetics.EquippedParachuteCamo;
        var note = UIUtil.CreateText(content, vehicle ? "Kullandığın her araç bu kamuflajla görünür." : "Paraşütün açılınca herkes bu deseni görür.",
            new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(1400f, 40f), 26, TextAnchor.MiddleCenter);
        note.color = Theme.TextDim;
        for (int i = 0; i < list.Count; i++)
        {
            var camo = list[i];
            bool owned = vehicle ? Cosmetics.OwnsVehicleCamo(camo.id) : Cosmetics.OwnsParachuteCamo(camo.id);
            bool on = camo.id == equipped;
            float x = -2f * 300f + (i % 5) * 300f;
            float y = 200f - (i / 5) * 250f;
            Text unused;
            var b = UIUtil.CreateButton(content, "", new Vector2(0.5f, 0.5f), new Vector2(x, y - 30f), new Vector2(280f, 230f), on ? CardOn : Card, false, 20, out unused);
            var captured = camo;
            b.onClick.AddListener(() => PickCamo(captured, vehicle));
            var bt = b.transform;
            Color rc = Theme.Rarity(camo.rarity);
            UIUtil.CreateImage(bt, "Rarity", new Vector2(0.5f, 1f), new Vector2(0f, -4f), new Vector2(280f, 8f), rc, false).raycastTarget = false;
            if (string.IsNullOrEmpty(camo.id))
            {
                var plain = UIUtil.CreateImage(bt, "Plain", new Vector2(0.5f, 1f), new Vector2(0f, -78f), new Vector2(250f, 120f), new Color(0.45f, 0.42f, 0.35f), false);
                plain.raycastTarget = false;
            }
            else
            {
                var sw = UIUtil.CreateRawSwatch(bt, WeaponDressing.Pattern(camo), new Vector2(0f, 37f), new Vector2(250f, 120f));
                sw.uvRect = new Rect(0f, 0f, 0.9f, 0.43f);
                sw.raycastTarget = false;
            }
            var n = UIUtil.CreateText(bt, camo.name, new Vector2(0.5f, 0f), new Vector2(0f, 62f), new Vector2(270f, 34f), 24, TextAnchor.MiddleCenter);
            n.fontStyle = FontStyle.Bold;
            string status = on ? "KUŞANILDI" : (owned ? camo.rarity + "  •  KUŞAN" : camo.price.ToString("N0") + " Kredi");
            var st = UIUtil.CreateText(bt, status, new Vector2(0.5f, 0f), new Vector2(0f, 26f), new Vector2(270f, 30f), 20, TextAnchor.MiddleCenter);
            st.color = on ? Theme.Good : (owned ? rc * 0.4f + Color.white * 0.6f : Theme.Accent);
            if (on)
            {
                var frame = Icons.Create(bt, "rarity_frame", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(280f, 230f));
                frame.preserveAspect = false;
                frame.color = Theme.Accent;
            }
        }
    }

    private void PickCamo(CamoDef camo, bool vehicle)
    {
        bool owned = vehicle ? Cosmetics.OwnsVehicleCamo(camo.id) : Cosmetics.OwnsParachuteCamo(camo.id);
        var p = Profile;
        if (!owned)
        {
            if (p == null || p.coins < camo.price)
            {
                if (GameManager.Instance != null && GameManager.Instance.uiManager != null)
                    GameManager.Instance.uiManager.Toast("Yetersiz Kredi! Gereken: " + camo.price.ToString("N0"));
                return;
            }
            p.coins -= camo.price;
            if (vehicle) Cosmetics.AddVehicleCamo(camo.id);
            else Cosmetics.AddParachuteCamo(camo.id);
            p.Save();
        }
        if (vehicle) Cosmetics.EquippedVehicleCamo = camo.id;
        else Cosmetics.EquippedParachuteCamo = camo.id;
        UiSound.Confirm();
        ShowTab(tab);
    }
}
