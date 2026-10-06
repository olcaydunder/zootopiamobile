using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// MAĞAZA: KREDİ (Kredi packs bought through Google Play, a rewarded ad for free Kredi), FIRSATLAR (free daily gift, six daily deals, weekly offers), gift boxes (buy, open
/// the ones you have), characters, TEÇHİZAT (armour, equipment, perk cards, masks) and camouflages for guns,
/// vehicles and parachutes. Every paid item can also be bought as a gift for a friend (HEDİYE ET).
/// Opened for a friend (from the friends screen) every item shows GÖNDER instead.
/// </summary>
public class StoreScreen : MonoBehaviour
{
    public enum Tab { Deals, Boxes, Characters, Gear, Camos, Credits }

    // order of the tab buttons
    private static readonly Tab[] TabOrder = { Tab.Credits, Tab.Deals, Tab.Boxes, Tab.Characters, Tab.Gear, Tab.Camos };

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

        string[] tabs = { "KREDİ", "FIRSATLAR", "KUTULAR", "KARAKTERLER", "TEÇHİZAT", "KAMUFLAJLAR" };
        for (int i = 0; i < tabs.Length; i++)
        {
            Tab which = TabOrder[i];
            var b = UIUtil.CreateButton(t, tabs[i], new Vector2(0.5f, 1f), new Vector2((i - 2.5f) * 290f, -175f), new Vector2(276f, 76f), Theme.Panel, false, 28, out label);
            b.onClick.AddListener(() => Show(which));
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
            bool sel = TabOrder[i] == tab;
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
            case Tab.Deals: h = Gifting ? BuildBoxes() : BuildDeals(); break;
            case Tab.Boxes: h = BuildBoxes(); break;
            case Tab.Characters: h = BuildCharacters(); break;
            case Tab.Gear: h = BuildGear(); break;
            case Tab.Credits: h = BuildCredits(); break;
            default: h = BuildCamos(); break;
        }
        content.sizeDelta = new Vector2(1760f, h);
    }

    private bool Gifting { get { return giftTo.Length > 0; } }

    // ----- Kredi (Google Play) -----

    private void OnEnable()
    {
        Purchases.Changed += OnPurchasesChanged;
    }

    private void OnDisable()
    {
        Purchases.Changed -= OnPurchasesChanged;
    }

    private bool shownAdReady, shownStoreReady;

    private void Update()
    {
        // the Kredi page changes by itself when the ad finishes loading or Google Play answers
        if (tab == Tab.Credits && (Ads.RewardReady != shownAdReady || Purchases.Available != shownStoreReady))
            Rebuild();
    }

    private void OnPurchasesChanged()
    {
        if (!gameObject.activeInHierarchy)
            return;
        if (Purchases.Message.Length > 0)
            SetStatus(Purchases.Message);
        Rebuild();
    }

    private float BuildCredits()
    {
        shownAdReady = Ads.RewardReady;
        shownStoreReady = Purchases.Available;
        var c = new Vector2(0.5f, 1f);
        var mid = new Vector2(0.5f, 0.5f);
        if (Gifting)
        {
            var g = UIUtil.CreateText(content, "Kredi paketleri hediye edilemez. Arkadaşına kutu, karakter ya da kamuflaj gönderebilirsin.", c, new Vector2(0f, -80f), new Vector2(1400f, 80f), 30, TextAnchor.MiddleCenter);
            g.color = Theme.TextDim;
            return 200f;
        }
        Text label;
        var packs = PlayConfig.CreditPacks;
        for (int i = 0; i <= packs.Length; i++)
        {
            float x = (i % 3 - 1) * 580f, y = -170f - (i / 3) * 330f;
            bool adCard = i == packs.Length;
            var card = UIUtil.CreateImage(content, adCard ? "AdCard" : "Pack", c, new Vector2(x, y), new Vector2(560f, 310f),
                adCard ? new Color(0.1f, 0.2f, 0.16f, 0.95f) : new Color(0.12f, 0.1f, 0.05f, 0.95f), false);
            var o = card.gameObject.AddComponent<Outline>();
            o.effectColor = adCard ? new Color(0.35f, 0.9f, 0.55f, 0.6f) : new Color(1f, 0.8f, 0.3f, 0.6f);
            o.effectDistance = new Vector2(3f, -3f);
            var ct = card.transform;
            Icons.Create(ct, "currency", mid, new Vector2(-170f, 30f), new Vector2(150f, 150f));
            if (adCard)
            {
                var t1 = UIUtil.CreateText(ct, "REKLAM İZLE", mid, new Vector2(80f, 85f), new Vector2(360f, 50f), 34, TextAnchor.MiddleCenter);
                t1.fontStyle = FontStyle.Bold;
                var t2 = UIUtil.CreateText(ct, "+" + PlayConfig.AdReward + " KREDİ", mid, new Vector2(80f, 35f), new Vector2(360f, 50f), 38, TextAnchor.MiddleCenter);
                t2.fontStyle = FontStyle.Bold;
                t2.color = Theme.Good;
                var t3 = UIUtil.CreateText(ct, "Bugün kalan: " + Ads.LeftToday + " / " + PlayConfig.AdsPerDay, mid, new Vector2(80f, -10f), new Vector2(360f, 36f), 24, TextAnchor.MiddleCenter);
                t3.color = Theme.TextDim;
                bool ready = Ads.RewardReady;
                string text = Ads.LeftToday <= 0 ? "YARIN TEKRAR" : ready ? "İZLE" : "HAZIRLANIYOR";
                var watch = UIUtil.CreateButton(ct, text, mid, new Vector2(0f, -95f), new Vector2(480f, 76f), ready ? Theme.Good : Theme.PanelLight, false, 30, out label);
                if (ready)
                    Dark(label);
                watch.onClick.AddListener(() =>
                {
                    if (!Ads.ShowRewarded(() =>
                    {
                        Profile.coins += PlayConfig.AdReward;
                        Profile.Save();
                        UiSound.Confirm();
                        SetStatus(PlayConfig.AdReward + " Kredi kazandın.");
                        Rebuild();
                    }))
                        SetStatus(Ads.LeftToday <= 0 ? "Bugünkü reklam hakkın bitti." : "Reklam henüz hazır değil, birazdan tekrar dene.");
                });
                continue;
            }
            var pack = packs[i];
            string id = pack.id;
            var amount = UIUtil.CreateText(ct, pack.credits.ToString("N0") + " KREDİ", mid, new Vector2(80f, 70f), new Vector2(380f, 60f), 42, TextAnchor.MiddleCenter);
            amount.fontStyle = FontStyle.Bold;
            amount.color = new Color(1f, 0.85f, 0.3f);
            if (pack.bonus > 0)
            {
                var bonus = UIUtil.CreateText(ct, "+" + pack.bonus.ToString("N0") + " BONUS", mid, new Vector2(80f, 22f), new Vector2(380f, 40f), 28, TextAnchor.MiddleCenter);
                bonus.color = Theme.Good;
            }
            if (pack.tag.Length > 0)
            {
                var tagBg = UIUtil.CreateImage(ct, "Tag", new Vector2(1f, 1f), new Vector2(-110f, -24f), new Vector2(200f, 40f), new Color(0.85f, 0.2f, 0.25f, 1f), false);
                tagBg.raycastTarget = false;
                var tagText = UIUtil.CreateText(tagBg.transform, pack.tag, mid, Vector2.zero, new Vector2(200f, 40f), 20, TextAnchor.MiddleCenter);
                tagText.fontStyle = FontStyle.Bold;
            }
            string price = Purchases.Price(id);
            bool can = price.Length > 0 && !Purchases.Busy;
            var buy = UIUtil.CreateButton(ct, price.Length > 0 ? price : (Purchases.Available ? "SATIŞTA DEĞİL" : "BAĞLANIYOR..."), mid, new Vector2(0f, -95f), new Vector2(480f, 76f),
                can ? Theme.Accent : Theme.PanelLight, false, 32, out label);
            if (can)
                Dark(label);
            buy.onClick.AddListener(() =>
            {
                if (!Purchases.Buy(id))
                    SetStatus(Application.platform == RuntimePlatform.Android ? "Google Play'e bağlanılamadı. İnternet bağlantını kontrol edip tekrar dene."
                                                                              : "Satın alma yalnızca Google Play'den indirilen oyunda çalışır.");
                else
                    SetStatus("Google Play açılıyor...");
            });
        }
        var note = UIUtil.CreateText(content, "Ödemeler Google Play üzerinden alınır. Satın alınan Kredi yalnızca bu cihazdaki oyunda geçerlidir; para iadesi Google Play kurallarına tabidir." +
                                              (PlayConfig.TestAds ? "  (Test sürümü: reklamlar Google test reklamıdır.)" : ""),
                                     c, new Vector2(0f, -680f), new Vector2(1700f, 70f), 22, TextAnchor.MiddleCenter);
        note.horizontalOverflow = HorizontalWrapMode.Wrap;
        note.color = Theme.TextDim;
        return 740f;
    }

    // ----- Boxes -----

    private float BuildBoxes()
    {
        var c = new Vector2(0.5f, 1f);
        for (int i = 0; i < Shop.Crates.Length; i++)
        {
            var cr = Shop.Crates[i];
            string id = cr.id;
            float x = (i - (Shop.Crates.Length - 1) * 0.5f) * 350f;
            var card = UIUtil.CreateImage(content, "Box", c, new Vector2(x, -350f), new Vector2(336f, 680f), new Color(cr.color.r * 0.18f, cr.color.g * 0.18f, cr.color.b * 0.18f, 0.95f), false);
            var o = card.gameObject.AddComponent<Outline>();
            o.effectColor = new Color(cr.color.r, cr.color.g, cr.color.b, 0.7f);
            o.effectDistance = new Vector2(3f, -3f);
            var ct = card.transform;
            var mid = new Vector2(0.5f, 0.5f);
            Icons.Create(ct, "crate_" + id, mid, new Vector2(0f, 170f), new Vector2(250f, 250f));
            var n = UIUtil.CreateText(ct, cr.name, mid, new Vector2(0f, 10f), new Vector2(320f, 50f), 32, TextAnchor.MiddleCenter);
            n.fontStyle = FontStyle.Bold;
            n.color = Color.Lerp(cr.color, Color.white, 0.3f);
            var rar = UIUtil.CreateText(ct, MapCatalog.TrUpper(cr.rarity), mid, new Vector2(0f, -28f), new Vector2(320f, 30f), 20, TextAnchor.MiddleCenter);
            rar.color = Theme.Rarity(cr.rarity);
            var bl = UIUtil.CreateText(ct, cr.blurb, mid, new Vector2(0f, -96f), new Vector2(300f, 96f), 20, TextAnchor.UpperCenter);
            bl.horizontalOverflow = HorizontalWrapMode.Wrap;
            bl.color = Theme.TextDim;
            Text label;
            // the chances, before buying (Google Play's rule for random items)
            var odds = UIUtil.CreateButton(ct, "OLASILIKLAR", mid, new Vector2(0f, 312f), new Vector2(220f, 44f), new Color(0f, 0f, 0f, 0.55f), false, 20, out label);
            odds.onClick.AddListener(() => OddsPanel.Show(transform, cr.name + " – OLASILIKLAR", Shop.OddsLines(cr)));
            if (Gifting)
            {
                var send = UIUtil.CreateButton(ct, "GÖNDER  " + cr.price.ToString("N0"), mid, new Vector2(0f, -200f), new Vector2(300f, 84f), Theme.Accent, false, 28, out label);
                Dark(label);
                send.onClick.AddListener(() => SendGift("box", id, cr.name));
                continue;
            }
            var buy = UIUtil.CreateButton(ct, cr.price.ToString("N0") + " Kredi", mid, new Vector2(0f, -185f), new Vector2(300f, 80f), Theme.Accent, false, 30, out label);
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
            var haveText = UIUtil.CreateText(ct, "Sende: " + have, mid, new Vector2(0f, -240f), new Vector2(300f, 34f), 24, TextAnchor.MiddleCenter);
            haveText.color = have > 0 ? Theme.Good : Theme.TextDim;
            var open = UIUtil.CreateButton(ct, "AÇ", mid, new Vector2(-45f, -295f), new Vector2(200f, 66f), have > 0 ? Theme.Good : Theme.PanelLight, false, 30, out label);
            if (have > 0)
                Dark(label);
            open.onClick.AddListener(() => OpenBox(id));
            var gift = UIUtil.CreateButton(ct, "", mid, new Vector2(110f, -295f), new Vector2(84f, 66f), Theme.PanelLight, false, 20, out label);
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

    // ----- Deals: free gift, daily deals, weekly offers -----

    private float BuildDeals()
    {
        var p = Profile;
        var top = new Vector2(0.5f, 1f);
        var mid = new Vector2(0.5f, 0.5f);
        Text label;
        var head = UIUtil.CreateText(content, "GÜNLÜK FIRSATLAR", top, new Vector2(-430f, -30f), new Vector2(900f, 44f), 30, TextAnchor.MiddleLeft);
        head.fontStyle = FontStyle.Bold;
        head.color = Theme.Accent;
        var timer = UIUtil.CreateText(content, "Yenilenmesine " + Deals.TimeLeft, top, new Vector2(430f, -30f), new Vector2(900f, 40f), 24, TextAnchor.MiddleRight);
        timer.color = Theme.TextDim;

        const int cols = 4;
        const float w = 420f, h = 330f, gap = 13f;
        float y0 = -64f;
        // the free gift is the first card
        {
            var card = UIUtil.CreateImage(content, "Free", top, new Vector2((0 - (cols - 1) * 0.5f) * (w + gap), y0 - h * 0.5f), new Vector2(w, h), new Color(0.08f, 0.2f, 0.12f, 0.95f), false);
            var o = card.gameObject.AddComponent<Outline>();
            o.effectColor = new Color(0.35f, 0.9f, 0.45f, 0.7f);
            o.effectDistance = new Vector2(3f, -3f);
            var ct = card.transform;
            Icons.Create(ct, "free", mid, new Vector2(0f, 60f), new Vector2(170f, 170f));
            var n = UIUtil.CreateText(ct, "GÜNLÜK HEDİYE", mid, new Vector2(0f, -45f), new Vector2(400f, 44f), 30, TextAnchor.MiddleCenter);
            n.fontStyle = FontStyle.Bold;
            if (Deals.FreeReady)
            {
                var claim = UIUtil.CreateButton(ct, "ÜCRETSİZ AL", mid, new Vector2(0f, -115f), new Vector2(330f, 74f), Theme.Good, false, 30, out label);
                Dark(label);
                claim.onClick.AddListener(() =>
                {
                    var given = Deals.ClaimFree(Profile);
                    if (given.Count == 0)
                        return;
                    UiSound.Confirm();
                    CrateOpening.ShowQuick("wood", given, "GÜNLÜK HEDİYE", Rebuild);
                    Rebuild();
                });
            }
            else
            {
                var later = UIUtil.CreateText(ct, "Alındı  •  yarın yeniden", mid, new Vector2(0f, -115f), new Vector2(400f, 40f), 24, TextAnchor.MiddleCenter);
                later.color = Theme.TextDim;
            }
        }
        var deals = Deals.Today();
        for (int i = 0; i < deals.Count; i++)
        {
            var d = deals[i];
            int k = i + 1;
            float x = (k % cols - (cols - 1) * 0.5f) * (w + gap);
            float yy = y0 - h * 0.5f - (k / cols) * (h + gap);
            string rarity = d.reward.Rarity;
            Color rc = Theme.Rarity(rarity);
            var card = UIUtil.CreateImage(content, "Deal", top, new Vector2(x, yy), new Vector2(w, h), new Color(rc.r * 0.16f, rc.g * 0.16f, rc.b * 0.16f, 0.95f), false);
            var ct = card.transform;
            UIUtil.CreateImage(ct, "Accent", new Vector2(0.5f, 1f), new Vector2(0f, -4f), new Vector2(w, 8f), rc, false).raycastTarget = false;
            RewardView.Create(ct, d.reward, mid, new Vector2(-110f, 40f), 150f);
            var n = UIUtil.CreateText(ct, d.title, mid, new Vector2(80f, 70f), new Vector2(220f, 80f), 24, TextAnchor.MiddleLeft);
            n.fontStyle = FontStyle.Bold;
            n.horizontalOverflow = HorizontalWrapMode.Wrap;
            var rl = UIUtil.CreateText(ct, MapCatalog.TrUpper(rarity), mid, new Vector2(80f, 18f), new Vector2(220f, 28f), 18, TextAnchor.MiddleLeft);
            rl.color = rc;
            var old = UIUtil.CreateText(ct, d.oldPrice.ToString("N0"), mid, new Vector2(80f, -16f), new Vector2(220f, 30f), 22, TextAnchor.MiddleLeft);
            old.color = new Color(1f, 1f, 1f, 0.4f);
            UIUtil.CreateImage(old.transform, "Strike", new Vector2(0f, 0.5f), new Vector2(35f, 0f), new Vector2(70f, 3f), new Color(1f, 0.35f, 0.3f, 0.8f), false).raycastTarget = false;
            var badge = UIUtil.CreateImage(ct, "Off", new Vector2(1f, 1f), new Vector2(-50f, -36f), new Vector2(84f, 44f), Theme.Red, false);
            badge.raycastTarget = false;
            UIUtil.CreateText(badge.transform, "-%" + d.Discount, mid, Vector2.zero, new Vector2(84f, 44f), 22, TextAnchor.MiddleCenter).fontStyle = FontStyle.Bold;
            bool maskOwned = d.reward.kind == RewardKind.Gear && Gear.Find(d.reward.id) != null && Gear.Find(d.reward.id).Cosmetic && Gear.Owns(d.reward.id);
            if (d.bought || maskOwned)
            {
                var done = UIUtil.CreateText(ct, d.bought ? "ALINDI" : "SENDE", mid, new Vector2(0f, -115f), new Vector2(380f, 60f), 30, TextAnchor.MiddleCenter);
                done.color = Theme.Good;
                continue;
            }
            var buy = UIUtil.CreateButton(ct, d.price.ToString("N0") + " Kredi", mid, new Vector2(0f, -115f), new Vector2(360f, 72f), Theme.Accent, false, 30, out label);
            Dark(label);
            var deal = d;
            buy.onClick.AddListener(() =>
            {
                GrantedReward given;
                if (!Deals.BuyDeal(Profile, deal, out given))
                {
                    SetStatus("Yetersiz Kredi: " + deal.price.ToString("N0") + " gerekli.");
                    return;
                }
                UiSound.Confirm();
                if (given.reward.kind == RewardKind.Crate)
                    SetStatus(deal.title + " alındı. KUTULAR'dan açabilirsin.");
                else
                    CrateOpening.ShowQuick("bronze", new List<GrantedReward> { given }, deal.title, Rebuild);
                Rebuild();
            });
        }
        int rows = (deals.Count + 1 + cols - 1) / cols;
        float y = -y0 + rows * (h + gap) + 30f;

        var oh = UIUtil.CreateText(content, "HAFTALIK TEKLİFLER", top, new Vector2(-430f, -y - 22f), new Vector2(900f, 44f), 30, TextAnchor.MiddleLeft);
        oh.fontStyle = FontStyle.Bold;
        oh.color = Theme.Accent;
        var ot = UIUtil.CreateText(content, "Her teklif haftada bir kez  •  " + Missions.TimeLeft(true), top, new Vector2(430f, -y - 22f), new Vector2(900f, 40f), 24, TextAnchor.MiddleRight);
        ot.color = Theme.TextDim;
        y += 56f;
        const float ow = 866f, oh2 = 240f;
        for (int i = 0; i < Deals.Offers.Length; i++)
        {
            var o = Deals.Offers[i];
            float x = (i % 2 == 0 ? -1f : 1f) * (ow + gap) * 0.5f;
            if (i == Deals.Offers.Length - 1 && i % 2 == 0)
                x = 0f;
            float yy = -y - oh2 * 0.5f - (i / 2) * (oh2 + gap);
            var card = UIUtil.CreateImage(content, "Offer", top, new Vector2(x, yy), new Vector2(ow, oh2), new Color(o.color.r * 0.2f, o.color.g * 0.2f, o.color.b * 0.2f, 0.96f), false);
            var edge = card.gameObject.AddComponent<Outline>();
            edge.effectColor = new Color(o.color.r, o.color.g, o.color.b, 0.6f);
            edge.effectDistance = new Vector2(3f, -3f);
            var ct = card.transform;
            for (int r = 0; r < o.rewards.Length; r++)
                RewardView.Create(ct, o.rewards[r], new Vector2(0f, 0.5f), new Vector2(90f + r * 150f, 0f), 135f);
            float tx = 90f + o.rewards.Length * 150f;
            var title = UIUtil.CreateText(ct, o.title, new Vector2(0f, 0.5f), new Vector2(tx + 180f, 70f), new Vector2(360f, 46f), 32, TextAnchor.MiddleLeft);
            title.fontStyle = FontStyle.Bold;
            title.color = Color.Lerp(o.color, Color.white, 0.35f);
            var bl = UIUtil.CreateText(ct, o.blurb, new Vector2(0f, 0.5f), new Vector2(tx + 180f, 20f), new Vector2(360f, 56f), 20, TextAnchor.UpperLeft);
            bl.horizontalOverflow = HorizontalWrapMode.Wrap;
            bl.color = Theme.TextDim;
            var worth = UIUtil.CreateText(ct, "Değeri " + o.worth.ToString("N0"), new Vector2(0f, 0.5f), new Vector2(tx + 180f, -30f), new Vector2(360f, 30f), 20, TextAnchor.MiddleLeft);
            worth.color = new Color(1f, 1f, 1f, 0.45f);
            if (Deals.OfferBought(o))
            {
                var done = UIUtil.CreateText(ct, "BU HAFTA ALINDI", new Vector2(0f, 0.5f), new Vector2(tx + 180f, -78f), new Vector2(360f, 44f), 26, TextAnchor.MiddleLeft);
                done.color = Theme.Good;
                continue;
            }
            var buy = UIUtil.CreateButton(ct, o.price.ToString("N0") + " Kredi", new Vector2(0f, 0.5f), new Vector2(tx + 130f, -80f), new Vector2(260f, 64f), Theme.Accent, false, 26, out label);
            Dark(label);
            var offer = o;
            buy.onClick.AddListener(() =>
            {
                var given = new List<GrantedReward>();
                if (!Deals.BuyOffer(Profile, offer, given))
                {
                    SetStatus("Yetersiz Kredi: " + offer.price.ToString("N0") + " gerekli.");
                    return;
                }
                UiSound.Confirm();
                CrateOpening.ShowQuick("gold", given, offer.title, Rebuild);
                Rebuild();
            });
        }
        y += ((Deals.Offers.Length + 1) / 2) * (oh2 + gap) + 20f;
        return y;
    }

    // ----- Gear: armour, equipment, perk cards, masks -----

    private float BuildGear()
    {
        y = 0f;
        var p = Profile;
        GearSlot[] order = { GearSlot.Mask, GearSlot.Head, GearSlot.Body, GearSlot.Legs, GearSlot.Explosive, GearSlot.Tactical, GearSlot.Medical, GearSlot.Perk };
        foreach (var slot in order)
        {
            var items = new List<GearDef>();
            foreach (var g in Gear.OfSlot(slot))
                if (g.price > 0)
                    items.Add(g);
            if (items.Count == 0)
                continue;
            var head = UIUtil.CreateText(content, slot == GearSlot.Mask ? "MASKELER" : Gear.SlotNames[(int)slot], new Vector2(0.5f, 1f), new Vector2(0f, -y - 30f), new Vector2(1720f, 44f), 28, TextAnchor.MiddleLeft);
            head.fontStyle = FontStyle.Bold;
            head.color = Theme.TextDim;
            y += 64f;
            const int cols = 6;
            const float w = 276f, h = 330f;
            int k = 0;
            foreach (var g in items)
            {
                float x = (k % cols - (cols - 1) * 0.5f) * (w + 12f);
                float yy = -y - h * 0.5f - (k / cols) * (h + 12f);
                k++;
                var r = new Reward { kind = RewardKind.Gear, id = g.id, amount = 1 };
                var card = UIUtil.CreateImage(content, "Gear", new Vector2(0.5f, 1f), new Vector2(x, yy), new Vector2(w, h), Theme.Panel, false);
                var ct = card.transform;
                var mid = new Vector2(0.5f, 0.5f);
                RewardView.Create(ct, r, mid, new Vector2(0f, 75f), 150f);
                var n = UIUtil.CreateText(ct, g.name, mid, new Vector2(0f, -22f), new Vector2(w - 12f, 36f), 22, TextAnchor.MiddleCenter);
                n.fontStyle = FontStyle.Bold;
                var rl = UIUtil.CreateText(ct, MapCatalog.TrUpper(g.rarity), mid, new Vector2(0f, -50f), new Vector2(w - 12f, 26f), 18, TextAnchor.MiddleCenter);
                rl.color = Theme.Rarity(g.rarity);
                var eff = UIUtil.CreateText(ct, g.Cosmetic ? "Kafana giyilir" : Gear.Describe(g, Mathf.Max(1, Gear.Level(g.id))), mid, new Vector2(0f, -86f), new Vector2(w - 16f, 48f), 17, TextAnchor.UpperCenter);
                eff.horizontalOverflow = HorizontalWrapMode.Wrap;
                eff.color = Theme.TextDim;
                Text label;
                var gd = g;
                if (Gifting)
                {
                    var send = UIUtil.CreateButton(ct, "GÖNDER " + g.price.ToString("N0"), mid, new Vector2(0f, -135f), new Vector2(w - 30f, 58f), Theme.Accent, false, 22, out label);
                    Dark(label);
                    send.onClick.AddListener(() => SendGift("gear", gd.id, gd.name));
                    continue;
                }
                bool owned = Gear.Owns(g.id);
                if (owned && g.Cosmetic)
                {
                    var have = UIUtil.CreateText(ct, "SENDE", mid, new Vector2(-40f, -135f), new Vector2(160f, 50f), 24, TextAnchor.MiddleCenter);
                    have.color = Theme.Good;
                }
                else
                {
                    var buy = UIUtil.CreateButton(ct, (owned ? "+3 KART  " : "") + g.price.ToString("N0"), mid, new Vector2(-40f, -135f), new Vector2(180f, 58f), owned ? Theme.PanelLight : Theme.Accent, false, 22, out label);
                    if (!owned)
                        Dark(label);
                    buy.onClick.AddListener(() =>
                    {
                        bool had = Gear.Owns(gd.id);
                        if (!Gear.Buy(gd, Profile))
                        {
                            SetStatus("Yetersiz Kredi: " + gd.price.ToString("N0") + " gerekli.");
                            return;
                        }
                        UiSound.Confirm();
                        SetStatus(had ? gd.name + ": +3 kart. ENVANTER'den yükseltebilirsin." : gd.name + " artık senin! ENVANTER'den kuşan.");
                        if (gd.Cosmetic && GameManager.Instance.player != null)
                            GameManager.Instance.player.ApplyMask();
                        Rebuild();
                    });
                }
                var gift = UIUtil.CreateButton(ct, "", mid, new Vector2(95f, -135f), new Vector2(66f, 58f), Theme.PanelLight, false, 20, out label);
                Icons.Create(gift.transform, "gift", mid, Vector2.zero, new Vector2(48f, 48f));
                gift.onClick.AddListener(() => GiftPanel.ForItem("gear", gd.id, gd.name, gd.price, Rebuild));
            }
            y += ((k + cols - 1) / cols) * (h + 12f) + 20f;
        }
        return y + 20f;
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
