using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ENVANTER: armour (helmet, vest, boots), equipment (explosive, tactical, medical), perk cards (three can be
/// on) and masks. The equipped slots are at the top, every item below (locked ones dimmed); tapping an item
/// shows it on the right: its effect now and at the next level, its cards, YÜKSELT (cards + Kredi) and
/// KUŞAN. GÜÇ (power) at the top sums the equipped items up.
/// </summary>
public class InventoryScreen : MonoBehaviour
{
    public enum Tab { Armor, Equipment, Perks, Masks }

    private static readonly string[] TabNames = { "ZIRH", "TEÇHİZAT", "GÜÇLENDİRİCİ", "MASKE" };
    private static readonly string[] TabIcons = { "inv_body", "inv_explosive", "inv_perk", "gear_k_cat" };

    private Tab tab;
    private string selected = "";
    private System.Action onClose;
    private readonly List<Image> tabImages = new List<Image>();
    private readonly List<Text> tabLabels = new List<Text>();
    private RectTransform slots, grid, detail;
    private ScrollRect scroll;
    private Text coinsText, powerText, statusText;

    public static InventoryScreen Create(Transform canvas)
    {
        var rect = UIUtil.CreateStretch(canvas, "Inventory");
        rect.gameObject.AddComponent<Image>().color = new Color(0.03f, 0.05f, 0.09f, 0.97f);
        var s = rect.gameObject.AddComponent<InventoryScreen>();
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
        Icons.Create(t, "inventory", new Vector2(0f, 1f), new Vector2(190f, -70f), new Vector2(84f, 84f));
        var title = UIUtil.CreateText(t, "ENVANTER", new Vector2(0f, 1f), new Vector2(460f, -70f), new Vector2(500f, 80f), 52, TextAnchor.MiddleLeft);
        title.fontStyle = FontStyle.Bold;

        var power = Theme.Box(t, "Power", new Vector2(1f, 1f), new Vector2(-640f, -70f), new Vector2(340f, 84f), Theme.Panel, false);
        Icons.Create(power.transform, "power", new Vector2(0f, 0.5f), new Vector2(46f, 0f), new Vector2(64f, 64f));
        var pl = UIUtil.CreateText(power.transform, "GÜÇ", new Vector2(0f, 0.5f), new Vector2(130f, 0f), new Vector2(80f, 50f), 26, TextAnchor.MiddleLeft);
        pl.color = Theme.TextDim;
        powerText = UIUtil.CreateText(power.transform, "", new Vector2(0f, 0.5f), new Vector2(250f, 0f), new Vector2(170f, 60f), 40, TextAnchor.MiddleLeft);
        powerText.fontStyle = FontStyle.Bold;
        powerText.color = new Color(0.85f, 0.6f, 1f);

        var coins = Theme.Box(t, "Coins", new Vector2(1f, 1f), new Vector2(-230f, -70f), new Vector2(380f, 84f), Theme.Panel, false);
        Icons.Create(coins.transform, "currency", new Vector2(0f, 0.5f), new Vector2(50f, 0f), new Vector2(64f, 64f));
        coinsText = UIUtil.CreateText(coins.transform, "", new Vector2(0f, 0.5f), new Vector2(220f, 0f), new Vector2(260f, 60f), 38, TextAnchor.MiddleLeft);
        coinsText.fontStyle = FontStyle.Bold;
        coinsText.color = new Color(1f, 0.85f, 0.3f);

        for (int i = 0; i < TabNames.Length; i++)
        {
            int index = i;
            var b = UIUtil.CreateButton(t, "     " + TabNames[i], new Vector2(0.5f, 1f), new Vector2(-640f + i * 330f, -175f), new Vector2(310f, 76f), Theme.Panel, false, 28, out label);
            Icons.Create(b.transform, TabIcons[i], new Vector2(0f, 0.5f), new Vector2(42f, 0f), new Vector2(58f, 58f));
            b.onClick.AddListener(() => { selected = ""; Show((Tab)index); });
            tabImages.Add(b.GetComponent<Image>());
            tabLabels.Add(label);
        }

        slots = UIUtil.CreateRect(t, "Slots", new Vector2(0.5f, 0.5f), new Vector2(-330f, 215f), new Vector2(1240f, 190f));

        var view = UIUtil.CreateRect(t, "View", new Vector2(0.5f, 0.5f), new Vector2(-330f, -200f), new Vector2(1240f, 610f));
        view.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.001f);
        view.gameObject.AddComponent<RectMask2D>();
        grid = UIUtil.CreateRect(view, "Grid", new Vector2(0.5f, 1f), Vector2.zero, new Vector2(1240f, 10f));
        grid.pivot = new Vector2(0.5f, 1f);
        scroll = view.gameObject.AddComponent<ScrollRect>();
        scroll.content = grid;
        scroll.viewport = view;
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 40f;

        var d = Theme.Box(t, "Detail", new Vector2(0.5f, 0.5f), new Vector2(640f, -60f), new Vector2(600f, 860f), Theme.Panel, true);
        detail = (RectTransform)d.transform;

        statusText = UIUtil.CreateText(t, "", new Vector2(0.5f, 0f), new Vector2(0f, 26f), new Vector2(1600f, 40f), 24, TextAnchor.MiddleCenter);
        statusText.color = Theme.Accent;
    }

    // ----- Opening -----

    public void Open(Tab which, System.Action closed)
    {
        onClose = closed;
        selected = "";
        statusText.text = "";
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        Show(which);
    }

    public void Hide() { gameObject.SetActive(false); }

    private void Close()
    {
        Hide();
        if (onClose != null)
            onClose();
    }

    private ProfileData Profile { get { return GameManager.Instance.profile; } }

    private static GearSlot[] SlotsOf(Tab t)
    {
        switch (t)
        {
            case Tab.Armor: return new[] { GearSlot.Head, GearSlot.Body, GearSlot.Legs };
            case Tab.Equipment: return new[] { GearSlot.Explosive, GearSlot.Tactical, GearSlot.Medical };
            case Tab.Perks: return new[] { GearSlot.Perk };
            default: return new[] { GearSlot.Mask };
        }
    }

    private void Show(Tab which)
    {
        tab = which;
        for (int i = 0; i < tabImages.Count; i++)
        {
            bool sel = i == (int)tab;
            tabImages[i].color = sel ? Theme.Selected : Theme.Panel;
            tabLabels[i].color = sel ? new Color(0.08f, 0.08f, 0.1f) : Color.white;
            tabLabels[i].GetComponent<Shadow>().enabled = !sel;
        }
        if (selected.Length == 0)
        {
            // Start on the first equipped item of the tab.
            foreach (var s in SlotsOf(tab))
            {
                string id = s == GearSlot.Perk ? Gear.Perk(0) : Gear.Equipped(s);
                if (!string.IsNullOrEmpty(id))
                {
                    selected = id;
                    break;
                }
            }
            if (selected.Length == 0)
                selected = Gear.OfSlot(SlotsOf(tab)[0])[0].id;
        }
        Rebuild();
    }

    private void Rebuild()
    {
        coinsText.text = Profile.coins.ToString("N0");
        powerText.text = Gear.Power(Profile).ToString("N0");
        Clear(slots);
        Clear(grid);
        Clear(detail);
        BuildSlots();
        BuildGrid();
        BuildDetail();
    }

    private static void Clear(RectTransform r)
    {
        for (int i = r.childCount - 1; i >= 0; i--)
            Destroy(r.GetChild(i).gameObject);
    }

    private void SetStatus(string s) { statusText.text = s; }

    // ----- The equipped slots -----

    private void BuildSlots()
    {
        var kinds = SlotsOf(tab);
        var ids = new List<string>();
        var names = new List<string>();
        if (tab == Tab.Perks)
        {
            for (int i = 0; i < Gear.PerkSlots; i++)
            {
                ids.Add(Gear.Perk(i));
                names.Add("GÜÇLENDİRİCİ " + (i + 1));
            }
        }
        else
        {
            foreach (var k in kinds)
            {
                ids.Add(Gear.Equipped(k));
                names.Add(Gear.SlotNames[(int)k]);
            }
        }
        float w = ids.Count == 1 ? 600f : 400f;
        for (int i = 0; i < ids.Count; i++)
        {
            float x = (i - (ids.Count - 1) * 0.5f) * (w + 20f);
            var g = Gear.Find(ids[i]);
            Color rc = g != null ? Theme.Rarity(g.rarity) : new Color(1f, 1f, 1f, 0.2f);
            Text label;
            var b = UIUtil.CreateButton(slots, "", new Vector2(0.5f, 0.5f), new Vector2(x, 0f), new Vector2(w, 180f),
                new Color(rc.r * 0.18f, rc.g * 0.18f, rc.b * 0.18f, 0.95f), false, 20, out label);
            var o = b.gameObject.AddComponent<Outline>();
            o.effectColor = new Color(rc.r, rc.g, rc.b, 0.6f);
            o.effectDistance = new Vector2(3f, -3f);
            var bt = b.transform;
            var head = UIUtil.CreateText(bt, names[i], new Vector2(0f, 1f), new Vector2(w * 0.5f + 70f, -26f), new Vector2(w - 160f, 30f), 20, TextAnchor.MiddleLeft);
            head.color = Theme.TextDim;
            if (g == null)
            {
                var empty = UIUtil.CreateText(bt, "BOŞ  •  aşağıdan seç", new Vector2(0.5f, 0.5f), new Vector2(0f, -10f), new Vector2(w - 20f, 40f), 24, TextAnchor.MiddleCenter);
                empty.color = Theme.TextDim;
                continue;
            }
            var r = new Reward { kind = RewardKind.Gear, id = g.id, amount = 1 };
            RewardView.Create(bt, r, new Vector2(0f, 0.5f), new Vector2(85f, -6f), 140f);
            var n = UIUtil.CreateText(bt, g.name, new Vector2(0f, 0.5f), new Vector2(170f + (w - 180f) * 0.5f, 18f), new Vector2(w - 180f, 40f), 26, TextAnchor.MiddleLeft);
            n.fontStyle = FontStyle.Bold;
            n.horizontalOverflow = HorizontalWrapMode.Wrap;
            var lv = UIUtil.CreateText(bt, LevelText(g), new Vector2(0f, 0.5f), new Vector2(170f + (w - 180f) * 0.5f, -22f), new Vector2(w - 180f, 30f), 22, TextAnchor.MiddleLeft);
            lv.color = Theme.Accent;
            var pw = UIUtil.CreateText(bt, g.Cosmetic ? "Görünüş" : "GÜÇ +" + Gear.ItemPower(g, Gear.Level(g.id)), new Vector2(0f, 0.5f), new Vector2(170f + (w - 180f) * 0.5f, -54f), new Vector2(w - 180f, 28f), 20, TextAnchor.MiddleLeft);
            pw.color = new Color(0.85f, 0.6f, 1f);
            string id = g.id;
            b.onClick.AddListener(() => { selected = id; Rebuild(); });
        }
    }

    private static string LevelText(GearDef g)
    {
        int lv = Mathf.Max(1, Gear.Level(g.id));
        if (g.Cosmetic)
            return MapCatalog.TrUpper(g.rarity);
        if (g.slot == GearSlot.Perk)
            return lv + " YILDIZ" + (lv >= g.MaxLevel ? " (EN ÜST)" : "");
        return "SEVİYE " + lv + (lv >= g.MaxLevel ? " (EN ÜST)" : "");
    }

    // ----- Every item of the tab -----

    private void BuildGrid()
    {
        var items = new List<GearDef>();
        foreach (var s in SlotsOf(tab))
            items.AddRange(Gear.OfSlot(s));
        // owned first, then by rarity
        items.Sort((a, b) =>
        {
            int oa = Gear.Owns(a.id) ? 0 : 1, ob = Gear.Owns(b.id) ? 0 : 1;
            if (oa != ob) return oa - ob;
            if (a.slot != b.slot) return a.slot - b.slot;
            return Gear.RarityFactor(a.rarity).CompareTo(Gear.RarityFactor(b.rarity));
        });
        const int cols = 5;
        const float w = 232f, h = 286f, gap = 10f;
        for (int i = 0; i < items.Count; i++)
        {
            var g = items[i];
            bool owned = Gear.Owns(g.id);
            bool eq = owned && Gear.IsEquipped(g.id);
            bool sel = g.id == selected;
            float x = (i % cols - (cols - 1) * 0.5f) * (w + gap);
            float y = -h * 0.5f - (i / cols) * (h + gap) - 6f;
            Color rc = Theme.Rarity(g.rarity);
            Text label;
            var b = UIUtil.CreateButton(grid, "", new Vector2(0.5f, 1f), new Vector2(x, y), new Vector2(w, h),
                sel ? new Color(0.22f, 0.25f, 0.32f, 0.98f) : Theme.Panel, false, 20, out label);
            if (sel)
            {
                var o = b.gameObject.AddComponent<Outline>();
                o.effectColor = Theme.Accent;
                o.effectDistance = new Vector2(4f, -4f);
            }
            var bt = b.transform;
            UIUtil.CreateImage(bt, "Accent", new Vector2(0.5f, 1f), new Vector2(0f, -4f), new Vector2(w, 8f), rc, false).raycastTarget = false;
            var view = RewardView.Create(bt, new Reward { kind = RewardKind.Gear, id = g.id, amount = 1 }, new Vector2(0.5f, 1f), new Vector2(0f, -94f), 150f);
            var n = UIUtil.CreateText(bt, g.name, new Vector2(0.5f, 0f), new Vector2(0f, 98f), new Vector2(w - 12f, 52f), 20, TextAnchor.MiddleCenter);
            n.fontStyle = FontStyle.Bold;
            n.horizontalOverflow = HorizontalWrapMode.Wrap;
            if (!owned)
            {
                foreach (var gr in view.GetComponentsInChildren<Graphic>())
                    gr.color = new Color(gr.color.r * 0.45f, gr.color.g * 0.45f, gr.color.b * 0.45f, gr.color.a);
                var lk = UIUtil.CreateText(bt, "KİLİTLİ", new Vector2(0.5f, 0f), new Vector2(0f, 46f), new Vector2(w - 12f, 30f), 20, TextAnchor.MiddleCenter);
                lk.color = Theme.TextDim;
            }
            else if (g.Cosmetic)
            {
                var have = UIUtil.CreateText(bt, MapCatalog.TrUpper(g.rarity), new Vector2(0.5f, 0f), new Vector2(0f, 46f), new Vector2(w - 12f, 30f), 20, TextAnchor.MiddleCenter);
                have.color = rc;
            }
            else
            {
                int lv = Gear.Level(g.id), need = Gear.CardsToNext(g, lv), have = Gear.Cards(g.id);
                if (g.slot == GearSlot.Perk)
                    Stars(bt, new Vector2(0.5f, 0f), new Vector2(0f, 58f), lv, g.MaxLevel, 24f);
                else
                {
                    var lt = UIUtil.CreateText(bt, "SV " + lv, new Vector2(0f, 0f), new Vector2(46f, 52f), new Vector2(80f, 28f), 20, TextAnchor.MiddleLeft);
                    lt.color = Theme.Accent;
                }
                Bar(bt, new Vector2(0.5f, 0f), new Vector2(26f, 30f), 150f, need > 0 ? (float)have / need : 1f,
                    need > 0 ? have + "/" + need : "EN ÜST", need > 0 && have >= need ? Theme.Good : new Color(0.3f, 0.6f, 1f));
            }
            if (eq)
            {
                var tag = UIUtil.CreateImage(bt, "Eq", new Vector2(1f, 1f), new Vector2(-48f, -30f), new Vector2(80f, 32f), Theme.Good, false);
                tag.raycastTarget = false;
                var tt = UIUtil.CreateText(tag.transform, "TAKILI", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(80f, 32f), 18, TextAnchor.MiddleCenter);
                tt.color = new Color(0.05f, 0.1f, 0.05f);
                tt.GetComponent<Shadow>().enabled = false;
            }
            else if (owned && Gear.CanUpgrade(g, Profile))
            {
                Icons.Create(bt, "up", new Vector2(1f, 1f), new Vector2(-28f, -32f), new Vector2(42f, 42f)).raycastTarget = false;
            }
            string id = g.id;
            b.onClick.AddListener(() => { selected = id; UiSound.Click(); Rebuild(); });
        }
        grid.sizeDelta = new Vector2(1240f, ((items.Count + cols - 1) / cols) * (h + gap) + 20f);
    }

    /// <summary>A row of stars (perk level): gold up to <paramref name="n"/>, dark after.</summary>
    public static void Stars(Transform parent, Vector2 anchor, Vector2 pos, int n, int max, float size)
    {
        for (int i = 0; i < max; i++)
        {
            var s = Icons.Create(parent, i < n ? "star" : "star_empty", anchor, pos + new Vector2((i - (max - 1) * 0.5f) * size * 1.05f, 0f), new Vector2(size, size));
            s.raycastTarget = false;
        }
    }

    private static void Bar(Transform parent, Vector2 anchor, Vector2 pos, float width, float fill, string text, Color color)
    {
        var bg = UIUtil.CreateImage(parent, "BarBg", anchor, pos, new Vector2(width, 22f), new Color(1f, 1f, 1f, 0.12f), false);
        bg.raycastTarget = false;
        var f = UIUtil.CreateImage(bg.transform, "Fill", new Vector2(0f, 0.5f), Vector2.zero, new Vector2(width * Mathf.Clamp01(fill), 22f), color, false);
        f.rectTransform.pivot = new Vector2(0f, 0.5f);
        f.raycastTarget = false;
        UIUtil.CreateText(bg.transform, text, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(width, 22f), 16, TextAnchor.MiddleCenter);
    }

    // ----- The selected item -----

    private void BuildDetail()
    {
        var g = Gear.Find(selected);
        if (g == null)
            return;
        var p = Profile;
        bool owned = Gear.Owns(g.id);
        int lv = Mathf.Max(1, Gear.Level(g.id));
        Color rc = Theme.Rarity(g.rarity);
        var top = new Vector2(0.5f, 1f);
        var d = detail;
        RewardView.Create(d, new Reward { kind = RewardKind.Gear, id = g.id, amount = 1 }, top, new Vector2(0f, -150f), 230f);
        var n = UIUtil.CreateText(d, g.name, top, new Vector2(0f, -300f), new Vector2(560f, 50f), 36, TextAnchor.MiddleCenter);
        n.fontStyle = FontStyle.Bold;
        var rl = UIUtil.CreateText(d, MapCatalog.TrUpper(g.rarity) + "  •  " + (g.slot == GearSlot.Mask ? "MASKE" : Gear.SlotNames[(int)g.slot]), top, new Vector2(0f, -342f), new Vector2(560f, 32f), 22, TextAnchor.MiddleCenter);
        rl.color = rc;
        Text label;
        if (g.Cosmetic)
        {
            var info = UIUtil.CreateText(d, "Karakterinin kafasına giyilir. Maçta herkes görür; oyuna etkisi yoktur.", top, new Vector2(0f, -420f), new Vector2(520f, 90f), 24, TextAnchor.UpperCenter);
            info.horizontalOverflow = HorizontalWrapMode.Wrap;
            info.color = Theme.TextDim;
        }
        else
        {
            if (g.slot == GearSlot.Perk)
                Stars(d, top, new Vector2(0f, -385f), lv, g.MaxLevel, 40f);
            else
            {
                var lvt = UIUtil.CreateText(d, LevelText(g) + "  /  " + g.MaxLevel, top, new Vector2(0f, -385f), new Vector2(560f, 36f), 26, TextAnchor.MiddleCenter);
                lvt.color = Theme.Accent;
            }
            var now = UIUtil.CreateText(d, Gear.Describe(g, lv), top, new Vector2(0f, -440f), new Vector2(540f, 64f), 25, TextAnchor.MiddleCenter);
            now.horizontalOverflow = HorizontalWrapMode.Wrap;
            if (lv < g.MaxLevel)
            {
                var next = UIUtil.CreateText(d, "Sonraki seviye: " + Gear.Describe(g, lv + 1), top, new Vector2(0f, -500f), new Vector2(540f, 56f), 21, TextAnchor.MiddleCenter);
                next.horizontalOverflow = HorizontalWrapMode.Wrap;
                next.color = Theme.Good;
            }
            var pw = UIUtil.CreateText(d, "GÜÇ  " + Gear.ItemPower(g, lv) + (lv < g.MaxLevel ? "  »  " + Gear.ItemPower(g, lv + 1) : ""), top, new Vector2(0f, -548f), new Vector2(540f, 34f), 22, TextAnchor.MiddleCenter);
            pw.color = new Color(0.85f, 0.6f, 1f);
        }

        if (!owned)
        {
            var how = UIUtil.CreateText(d, g.price > 0 ? "Mağazada ya da kutulardan" : "Yalnız kutulardan çıkar", top, new Vector2(0f, -620f), new Vector2(540f, 36f), 22, TextAnchor.MiddleCenter);
            how.color = Theme.TextDim;
            if (g.price > 0)
            {
                var buy = UIUtil.CreateButton(d, "SATIN AL  " + g.price.ToString("N0"), top, new Vector2(0f, -700f), new Vector2(460f, 90f), Theme.Accent, false, 32, out label);
                Dark(label);
                buy.onClick.AddListener(() =>
                {
                    if (!Gear.Buy(g, Profile))
                    {
                        SetStatus("Yetersiz Kredi: " + g.price.ToString("N0") + " gerekli.");
                        return;
                    }
                    UiSound.Confirm();
                    SetStatus(g.name + " artık senin!");
                    ApplyLook(g);
                    Rebuild();
                });
            }
            else
            {
                var boxes = UIUtil.CreateButton(d, "KUTULARA GİT  ›", top, new Vector2(0f, -700f), new Vector2(460f, 90f), Theme.PanelLight, false, 30, out label);
                boxes.onClick.AddListener(OpenBoxes);
            }
            return;
        }

        if (!g.Cosmetic)
        {
            int need = Gear.CardsToNext(g, lv), have = Gear.Cards(g.id), cost = Gear.CreditsToNext(g, lv);
            if (need > 0)
            {
                Bar(d, top, new Vector2(0f, -610f), 460f, (float)have / need, "KART  " + have + " / " + need, have >= need ? Theme.Good : new Color(0.3f, 0.6f, 1f));
                bool can = Gear.CanUpgrade(g, p);
                var up = UIUtil.CreateButton(d, "YÜKSELT  " + cost.ToString("N0"), top, new Vector2(-120f, -700f), new Vector2(330f, 90f), can ? Theme.Good : Theme.PanelLight, false, 28, out label);
                Icons.Create(up.transform, "currency", new Vector2(1f, 0.5f), new Vector2(-34f, 0f), new Vector2(40f, 40f));
                label.rectTransform.anchoredPosition = new Vector2(-20f, 0f);
                if (can)
                    Dark(label);
                up.onClick.AddListener(() =>
                {
                    if (Gear.Level(g.id) <= 0)
                        return;
                    if (Gear.Cards(g.id) < Gear.CardsToNext(g, Gear.Level(g.id)))
                    {
                        SetStatus("Yeterli kart yok: kutulardan ve günlük fırsatlardan kart topla.");
                        return;
                    }
                    if (!Gear.Upgrade(g, Profile))
                    {
                        SetStatus("Yetersiz Kredi: " + Gear.CreditsToNext(g, Gear.Level(g.id)).ToString("N0") + " gerekli.");
                        return;
                    }
                    UiSound.Confirm();
                    Haptics.Tap(40);
                    SetStatus(g.name + " seviye " + Gear.Level(g.id) + " oldu!");
                    Rebuild();
                });
            }
            else
            {
                var max = UIUtil.CreateText(d, "EN ÜST SEVİYEDE", top, new Vector2(-120f, -700f), new Vector2(330f, 60f), 26, TextAnchor.MiddleCenter);
                max.color = Theme.Accent;
            }
        }

        bool eq = Gear.IsEquipped(g.id);
        bool canRemove = g.slot == GearSlot.Perk || g.Cosmetic;
        string text = eq ? (canRemove ? "ÇIKAR" : "TAKILI") : "KUŞAN";
        float ex = g.Cosmetic ? 0f : 175f;
        var equip = UIUtil.CreateButton(d, text, top, new Vector2(ex, -700f), new Vector2(g.Cosmetic ? 460f : 220f, 90f), eq ? Theme.PanelLight : Theme.Accent, false, 32, out label);
        if (!eq)
            Dark(label);
        equip.onClick.AddListener(() =>
        {
            if (eq && !canRemove)
                return;
            SetStatus(Gear.Toggle(g.id));
            UiSound.Confirm();
            ApplyLook(g);
            Rebuild();
        });
    }

    private static void ApplyLook(GearDef g)
    {
        var gm = GameManager.Instance;
        if (g.Cosmetic && gm != null && gm.player != null)
            gm.player.ApplyMask();
    }

    private void OpenBoxes()
    {
        var gm = GameManager.Instance;
        Hide();
        if (gm != null && gm.uiManager != null)
            gm.uiManager.OpenStore(StoreScreen.Tab.Boxes);
    }

    private static void Dark(Text label)
    {
        label.color = new Color(0.1f, 0.08f, 0.02f);
        label.GetComponent<Shadow>().enabled = false;
    }
}
