using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// MAĞAZA (Kredi only): gift boxes (buy, open the ones you have), characters, camouflages for guns,
/// vehicles and parachutes. Every paid item can also be bought as a gift for a friend (HEDİYE ET).
/// Opened for a friend (from the friends screen) every item shows GÖNDER instead.
/// </summary>
public class StoreScreen : MonoBehaviour
{
    public enum Tab { Boxes, Characters, Camos }

    private Tab tab;
    private System.Action onClose;
    private readonly List<Image> tabImages = new List<Image>();
    private readonly List<Text> tabLabels = new List<Text>();
    private RectTransform content;
    private ScrollRect scroll;
    private Text coinsText, titleText, statusText;
    private string giftTo = "", giftName = "";

    public static StoreScreen Create(Transform canvas)
    {
        var rect = UIUtil.CreateStretch(canvas, "Store");
        rect.gameObject.AddComponent<Image>().color = new Color(0.03f, 0.05f, 0.09f, 0.97f);
        var s = rect.gameObject.AddComponent<StoreScreen>();
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
        Icons.Create(t, "store", new Vector2(0f, 1f), new Vector2(190f, -70f), new Vector2(84f, 84f));
        titleText = UIUtil.CreateText(t, "MAĞAZA", new Vector2(0f, 1f), new Vector2(460f, -70f), new Vector2(500f, 80f), 52, TextAnchor.MiddleLeft);
        titleText.fontStyle = FontStyle.Bold;

        var coins = Theme.Box(t, "Coins", new Vector2(1f, 1f), new Vector2(-230f, -70f), new Vector2(380f, 84f), Theme.Panel, false);
        Icons.Create(coins.transform, "currency", new Vector2(0f, 0.5f), new Vector2(50f, 0f), new Vector2(64f, 64f));
        coinsText = UIUtil.CreateText(coins.transform, "", new Vector2(0f, 0.5f), new Vector2(220f, 0f), new Vector2(260f, 60f), 38, TextAnchor.MiddleLeft);
        coinsText.fontStyle = FontStyle.Bold;
        coinsText.color = new Color(1f, 0.85f, 0.3f);

        string[] tabs = { "KUTULAR", "KARAKTERLER", "KAMUFLAJLAR" };
        for (int i = 0; i < tabs.Length; i++)
        {
            int index = i;
            var b = UIUtil.CreateButton(t, tabs[i], new Vector2(0.5f, 1f), new Vector2(-340f + i * 340f, -175f), new Vector2(320f, 76f), Theme.Panel, false, 30, out label);
            b.onClick.AddListener(() => Show((Tab)index));
            tabImages.Add(b.GetComponent<Image>());
            tabLabels.Add(label);
        }

        var view = UIUtil.CreateRect(t, "View", new Vector2(0.5f, 0.5f), new Vector2(0f, -95f), new Vector2(1760f, 730f));
        view.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.001f);
        view.gameObject.AddComponent<RectMask2D>();
        content = UIUtil.CreateRect(view, "Content", new Vector2(0.5f, 1f), Vector2.zero, new Vector2(1760f, 10f));
        content.pivot = new Vector2(0.5f, 1f);
        scroll = view.gameObject.AddComponent<ScrollRect>();
        scroll.content = content;
        scroll.viewport = view;
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 40f;

        statusText = UIUtil.CreateText(t, "", new Vector2(0.5f, 0f), new Vector2(0f, 34f), new Vector2(1600f, 40f), 26, TextAnchor.MiddleCenter);
        statusText.color = Theme.Accent;
    }

    // ----- Opening -----

    public void Open(Tab which, System.Action closed)
    {
        giftTo = "";
        giftName = "";
        onClose = closed;
        titleText.text = "MAĞAZA";
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        Show(which);
    }

    /// <summary>Choosing a gift for a friend: every item shows GÖNDER (paid from your Kredi).</summary>
    public void OpenForGift(string personId, string personName, Tab which, System.Action closed)
    {
        Open(which, closed);
        giftTo = personId;
        giftName = personName;
        titleText.text = "HEDİYE: " + personName;
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

    private ProfileData Profile { get { return GameManager.Instance.profile; } }

    private void SetStatus(string s) { statusText.text = s; }

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
        Rebuild();
    }

    private void Rebuild()
    {
        coinsText.text = Profile.coins.ToString("N0");
        for (int i = content.childCount - 1; i >= 0; i--)
            Destroy(content.GetChild(i).gameObject);
        float h;
        switch (tab)
        {
            case Tab.Boxes: h = BuildBoxes(); break;
            case Tab.Characters: h = BuildCharacters(); break;
            default: h = BuildCamos(); break;
        }
        content.sizeDelta = new Vector2(1760f, h);
    }

    private bool Gifting { get { return giftTo.Length > 0; } }

    // ----- Boxes -----

    private float BuildBoxes()
    {
        var c = new Vector2(0.5f, 1f);
        for (int i = 0; i < Shop.Crates.Length; i++)
        {
            var cr = Shop.Crates[i];
            string id = cr.id;
            float x = (i - 1) * 570f;
            var card = UIUtil.CreateImage(content, "Box", c, new Vector2(x, -350f), new Vector2(540f, 680f), new Color(cr.color.r * 0.18f, cr.color.g * 0.18f, cr.color.b * 0.18f, 0.95f), false);
            var o = card.gameObject.AddComponent<Outline>();
            o.effectColor = new Color(cr.color.r, cr.color.g, cr.color.b, 0.7f);
            o.effectDistance = new Vector2(3f, -3f);
            var ct = card.transform;
            var mid = new Vector2(0.5f, 0.5f);
            Icons.Create(ct, "crate_" + id, mid, new Vector2(0f, 150f), new Vector2(300f, 300f));
            var n = UIUtil.CreateText(ct, cr.name, mid, new Vector2(0f, -25f), new Vector2(520f, 60f), 44, TextAnchor.MiddleCenter);
            n.fontStyle = FontStyle.Bold;
            n.color = Color.Lerp(cr.color, Color.white, 0.3f);
            var bl = UIUtil.CreateText(ct, cr.blurb, mid, new Vector2(0f, -85f), new Vector2(480f, 70f), 22, TextAnchor.UpperCenter);
            bl.horizontalOverflow = HorizontalWrapMode.Wrap;
            bl.color = Theme.TextDim;
            Text label;
            if (Gifting)
            {
                var send = UIUtil.CreateButton(ct, "GÖNDER  " + cr.price.ToString("N0"), mid, new Vector2(0f, -190f), new Vector2(440f, 90f), Theme.Accent, false, 34, out label);
                Dark(label);
                send.onClick.AddListener(() => SendGift("box", id, cr.name));
                continue;
            }
            var buy = UIUtil.CreateButton(ct, "SATIN AL  " + cr.price.ToString("N0"), mid, new Vector2(0f, -170f), new Vector2(440f, 84f), Theme.Accent, false, 32, out label);
            Dark(label);
            buy.onClick.AddListener(() =>
            {
                if (!Shop.BuyCrate(Profile, id))
                {
                    SetStatus("Yetersiz Kredi. Görevler, maçlar ve kutularla Kredi kazan.");
                    return;
                }
                UiSound.Confirm();
                SetStatus(Shop.Crate(id).name + " alındı. Hemen açabilirsin.");
                Rebuild();
            });
            int have = Shop.CrateCount(id);
            var haveText = UIUtil.CreateText(ct, "Sende: " + have, mid, new Vector2(-120f, -272f), new Vector2(240f, 50f), 30, TextAnchor.MiddleCenter);
            haveText.color = have > 0 ? Theme.Good : Theme.TextDim;
            var open = UIUtil.CreateButton(ct, "AÇ", mid, new Vector2(70f, -272f), new Vector2(150f, 70f), have > 0 ? Theme.Good : Theme.PanelLight, false, 30, out label);
            if (have > 0)
                Dark(label);
            open.onClick.AddListener(() => OpenBox(id));
            var gift = UIUtil.CreateButton(ct, "", mid, new Vector2(195f, -272f), new Vector2(84f, 70f), Theme.PanelLight, false, 20, out label);
            Icons.Create(gift.transform, "gift", mid, Vector2.zero, new Vector2(60f, 60f));
            gift.onClick.AddListener(() => GiftPanel.ForItem("box", id, cr.name, cr.price, Rebuild));
        }
        return 720f;
    }

    private void OpenBox(string id)
    {
        if (Shop.CrateCount(id) <= 0)
        {
            SetStatus("Bu kutudan sende yok. Satın al ya da görevlerden kazan.");
            return;
        }
        var given = Shop.OpenCrate(Profile, id);
        CrateOpening.Show(id, given, Shop.Crate(id).name, Rebuild);
    }

    // ----- Characters -----

    private float BuildCharacters()
    {
        var p = Profile;
        int cols = 5;
        float w = 330f, h = 250f;
        int shown = 0;
        for (int i = 0; i < ModelLibrary.ShopSkins.Length; i++)
        {
            string skin = ModelLibrary.ShopSkins[i];
            int price = ModelLibrary.ShopPrices[i];
            if (Gifting && price <= 0)
                continue;
            int k = shown++;
            float x = (k % cols - (cols - 1) * 0.5f) * (w + 14f);
            float y = -h * 0.5f - (k / cols) * (h + 14f);
            string rarity = Shop.SkinRarity(skin);
            Color rc = Theme.Rarity(rarity);
            var card = UIUtil.CreateImage(content, "Char", new Vector2(0.5f, 1f), new Vector2(x, y), new Vector2(w, h), Theme.Panel, false);
            var ct = card.transform;
            var mid = new Vector2(0.5f, 0.5f);
            UIUtil.CreateImage(ct, "Accent", new Vector2(0.5f, 1f), new Vector2(0f, -4f), new Vector2(w, 8f), rc, false).raycastTarget = false;
            Icons.Create(ct, "skin", new Vector2(0f, 1f), new Vector2(64f, -70f), new Vector2(90f, 90f));
            var n = UIUtil.CreateText(ct, ModelLibrary.ShopNames[i], new Vector2(0f, 1f), new Vector2(225f, -48f), new Vector2(200f, 40f), 28, TextAnchor.MiddleLeft);
            n.fontStyle = FontStyle.Bold;
            var role = UIUtil.CreateText(ct, ModelLibrary.ShopRoles[i], new Vector2(0f, 1f), new Vector2(225f, -84f), new Vector2(200f, 30f), 20, TextAnchor.MiddleLeft);
            role.color = Theme.TextDim;
            var rl = UIUtil.CreateText(ct, MapCatalog.TrUpper(rarity), new Vector2(0f, 1f), new Vector2(225f, -114f), new Vector2(200f, 26f), 18, TextAnchor.MiddleLeft);
            rl.color = rc;
            Text label;
            bool owned = p.OwnsSkin(skin) || price <= 0;
            if (Gifting)
            {
                var send = UIUtil.CreateButton(ct, "GÖNDER  " + price.ToString("N0"), mid, new Vector2(0f, -70f), new Vector2(290f, 70f), Theme.Accent, false, 26, out label);
                Dark(label);
                string sn = ModelLibrary.ShopNames[i];
                send.onClick.AddListener(() => SendGift("skin", skin, sn));
                continue;
            }
            if (owned)
            {
                bool eq = p.equippedSkin == skin;
                var use = UIUtil.CreateButton(ct, eq ? "KUŞANILDI" : "KUŞAN", mid, new Vector2(price > 0 ? -45f : 0f, -70f), new Vector2(price > 0 ? 200f : 290f, 70f), eq ? Theme.PanelLight : Theme.Good, false, 26, out label);
                if (!eq)
                    Dark(label);
                use.onClick.AddListener(() =>
                {
                    if (!p.OwnsSkin(skin))
                        p.BuySkin(skin, 0);
                    p.EquipSkin(skin);
                    var gm = GameManager.Instance;
                    if (gm.player != null)
                        gm.player.ApplySkin(skin);
                    UiSound.Confirm();
                    Rebuild();
                });
            }
            else
            {
                var buy = UIUtil.CreateButton(ct, price.ToString("N0") + " Kredi", mid, new Vector2(-45f, -70f), new Vector2(200f, 70f), Theme.Accent, false, 26, out label);
                Dark(label);
                int ip = price;
                string sn = ModelLibrary.ShopNames[i];
                buy.onClick.AddListener(() =>
                {
                    if (!p.BuySkin(skin, ip))
                    {
                        SetStatus("Yetersiz Kredi: " + ip.ToString("N0") + " gerekli.");
                        return;
                    }
                    UiSound.Confirm();
                    SetStatus(sn + " artık senin! KUŞAN'a basarak giy.");
                    Rebuild();
                });
            }
            if (price > 0)
            {
                var gift = UIUtil.CreateButton(ct, "", mid, new Vector2(115f, -70f), new Vector2(76f, 70f), Theme.PanelLight, false, 20, out label);
                Icons.Create(gift.transform, "gift", mid, Vector2.zero, new Vector2(56f, 56f));
                string sn2 = ModelLibrary.ShopNames[i];
                int ip2 = price;
                gift.onClick.AddListener(() => GiftPanel.ForItem("skin", skin, sn2, ip2, Rebuild));
            }
        }
        return ((shown + cols - 1) / cols) * (h + 14f) + 20f;
    }

    // ----- Camouflages -----

    private float y;

    private float BuildCamos()
    {
        y = 0f;
        CamoSection("SİLAH KAMUFLAJLARI", Gunsmith.Camos, RewardKind.WeaponCamo);
        CamoSection("ARAÇ BOYALARI", Cosmetics.VehicleCamos, RewardKind.VehicleCamo);
        CamoSection("PARAŞÜT DESENLERİ", Cosmetics.ParachuteCamos, RewardKind.ParachuteCamo);
        return y + 20f;
    }

    private void CamoSection(string title, List<CamoDef> list, RewardKind kind)
    {
        var head = UIUtil.CreateText(content, title, new Vector2(0.5f, 1f), new Vector2(0f, -y - 30f), new Vector2(1720f, 44f), 28, TextAnchor.MiddleLeft);
        head.fontStyle = FontStyle.Bold;
        head.color = Theme.TextDim;
        y += 64f;
        int cols = 6;
        float w = 276f, h = 300f;
        int k = 0;
        foreach (var cd in list)
        {
            if (string.IsNullOrEmpty(cd.id) || cd.price <= 0)
                continue;
            var r = new Reward { kind = kind, id = cd.id };
            float x = (k % cols - (cols - 1) * 0.5f) * (w + 12f);
            float yy = -y - h * 0.5f - (k / cols) * (h + 12f);
            k++;
            var card = UIUtil.CreateImage(content, "Camo", new Vector2(0.5f, 1f), new Vector2(x, yy), new Vector2(w, h), Theme.Panel, false);
            var ct = card.transform;
            var mid = new Vector2(0.5f, 0.5f);
            RewardView.Create(ct, r, mid, new Vector2(0f, 50f), 150f);
            var n = UIUtil.CreateText(ct, cd.name, mid, new Vector2(0f, -48f), new Vector2(w - 16f, 36f), 24, TextAnchor.MiddleCenter);
            n.fontStyle = FontStyle.Bold;
            var rl = UIUtil.CreateText(ct, MapCatalog.TrUpper(cd.rarity), mid, new Vector2(0f, -78f), new Vector2(w - 16f, 26f), 18, TextAnchor.MiddleCenter);
            rl.color = Theme.Rarity(cd.rarity);
            bool owned = kind == RewardKind.WeaponCamo ? Gunsmith.OwnsCamo(cd.id)
                : kind == RewardKind.VehicleCamo ? Cosmetics.OwnsVehicleCamo(cd.id) : Cosmetics.OwnsParachuteCamo(cd.id);
            Text label;
            string item = Shop.ItemFromCamo(r);
            int price = cd.price;
            string cname = cd.name;
            if (Gifting)
            {
                var send = UIUtil.CreateButton(ct, "GÖNDER " + price.ToString("N0"), mid, new Vector2(0f, -120f), new Vector2(w - 30f, 60f), Theme.Accent, false, 22, out label);
                Dark(label);
                send.onClick.AddListener(() => SendGift("camo", item, cname));
                continue;
            }
            if (owned)
            {
                var have = UIUtil.CreateText(ct, "SENDE", mid, new Vector2(-40f, -120f), new Vector2(160f, 50f), 24, TextAnchor.MiddleCenter);
                have.color = Theme.Good;
            }
            else
            {
                var buy = UIUtil.CreateButton(ct, price.ToString("N0"), mid, new Vector2(-40f, -120f), new Vector2(160f, 60f), Theme.Accent, false, 24, out label);
                Dark(label);
                buy.onClick.AddListener(() => BuyCamo(r, price, cname));
            }
            var gift = UIUtil.CreateButton(ct, "", mid, new Vector2(90f, -120f), new Vector2(70f, 60f), Theme.PanelLight, false, 20, out label);
            Icons.Create(gift.transform, "gift", mid, Vector2.zero, new Vector2(50f, 50f));
            gift.onClick.AddListener(() => GiftPanel.ForItem("camo", item, cname, price, Rebuild));
        }
        y += ((k + cols - 1) / cols) * (h + 12f) + 20f;
    }

    private void BuyCamo(Reward r, int price, string name)
    {
        var p = Profile;
        if (p.coins < price)
        {
            SetStatus("Yetersiz Kredi: " + price.ToString("N0") + " gerekli.");
            return;
        }
        p.coins -= price;
        p.Save();
        Shop.Give(p, r);
        UiSound.Confirm();
        SetStatus(name + " alındı. Silah Atölyesi / Teçhizat'tan kuşanabilirsin.");
        Rebuild();
    }

    // ----- Gifts -----

    private void SendGift(string kind, string item, string what)
    {
        GiftPanel.Send(giftTo, giftName, kind, item, 0, what, ok =>
        {
            SetStatus(ok);
            Rebuild();
        });
    }

    private static void Dark(Text label)
    {
        label.color = new Color(0.1f, 0.08f, 0.02f);
        label.GetComponent<Shadow>().enabled = false;
    }
}
