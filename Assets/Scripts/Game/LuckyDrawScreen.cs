using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ŞANS ÇEKİLİŞİ screen: the week's ten prizes on a board (two big tiles for the Mitik and Efsanevi camos), a running
/// light that slows down on the prize won, a reveal card, and on the right the showcase gun turning in the Mitik camo.
/// Each draw costs more than the last; the week's first draw is half price. "ANİMASYONU ATLA" skips the running light.
/// </summary>
public class LuckyDrawScreen : MonoBehaviour
{
    private static readonly Vector3 StagePos = new Vector3(40f, 650f, 0f);
    /// <summary>Running-light order around the board (clockwise from the top-left).</summary>
    private static readonly int[] Ring = { 0, 1, 2, 3, 5, 9, 8, 7, 6, 4 };
    /// <summary>Gun pictures the camo prizes are shown on (each tile its own gun).</summary>
    private static readonly string[] CamoGuns = { "Rifle_AK19", "SMG_Akrep", "Sniper_K98", "Shotgun_P870", "Rifle_AR15", "Pistol_Klasik", "SMG_U45", "Sniper_G28" };

    private System.Action onClose;
    private RectTransform board;
    private readonly RectTransform[] tiles = new RectTransform[LuckyDraw.Count];
    private readonly GameObject[] takenMarks = new GameObject[LuckyDraw.Count];
    private readonly Image[] flashes = new Image[LuckyDraw.Count];
    private Text titleText, timerText, coinsText, drawLabel, oldPriceText, progressText, prizeTitle, prizeSub;
    private GameObject discountTag;
    private Image drawButtonImage, skipCheck;
    private RectTransform progressPips;
    private readonly Image[] pips = new Image[LuckyDraw.Count];
    private GameObject resultPanel, infoPanel;
    private string builtFor;
    private bool busy;

    // 3D showcase
    private Transform stage;
    private Camera previewCam;
    private RenderTexture previewRT;
    private WeaponController previewGun;
    private float yaw = -30f, fitDistance = 2f;

    private static bool SkipAnimation
    {
        get { return PlayerPrefs.GetInt("zm_draw_skip", 0) == 1; }
        set { PlayerPrefs.SetInt("zm_draw_skip", value ? 1 : 0); }
    }

    public static LuckyDrawScreen Create(Transform canvas)
    {
        var rect = UIUtil.CreateStretch(canvas, "LuckyDraw");
        rect.gameObject.AddComponent<Image>().color = new Color(0.025f, 0.035f, 0.06f, 0.94f);
        var s = rect.gameObject.AddComponent<LuckyDrawScreen>();
        s.Build();
        rect.gameObject.AddComponent<PopIn>();
        rect.gameObject.SetActive(false);
        return s;
    }

    // ----- Layout -----

    private void Build()
    {
        var t = transform;
        Text label;
        var c = new Vector2(0.5f, 0.5f);

        // Header
        var back = UIUtil.CreateButton(t, "<", new Vector2(0f, 1f), new Vector2(80f, -70f), new Vector2(84f, 84f), Theme.Accent, false, 54, out label);
        label.color = new Color(0.1f, 0.1f, 0.1f);
        label.GetComponent<Shadow>().enabled = false;
        back.onClick.AddListener(Close);
        titleText = UIUtil.CreateText(t, "ŞANS ÇEKİLİŞİ", new Vector2(0f, 1f), new Vector2(470f, -58f), new Vector2(620f, 64f), 50, TextAnchor.MiddleLeft);
        titleText.fontStyle = FontStyle.Bold;
        var sub = UIUtil.CreateText(t, "Her çekilişte panodaki ödüllerden biri senin — 10 çekilişte hepsi!", new Vector2(0f, 1f), new Vector2(470f, -104f), new Vector2(620f, 30f), 20, TextAnchor.MiddleLeft);
        sub.color = Theme.TextDim;
        var coins = Theme.Box(t, "Coins", new Vector2(1f, 1f), new Vector2(-230f, -70f), new Vector2(380f, 84f), Theme.Panel, false);
        Icons.Create(coins.transform, "currency", new Vector2(0f, 0.5f), new Vector2(50f, 0f), new Vector2(64f, 64f));
        coinsText = UIUtil.CreateText(coins.transform, "", new Vector2(0f, 0.5f), new Vector2(220f, 0f), new Vector2(260f, 60f), 38, TextAnchor.MiddleLeft);
        coinsText.fontStyle = FontStyle.Bold;
        coinsText.color = new Color(1f, 0.85f, 0.3f);

        // The board: a steel frame with a title strip
        var outer = UIUtil.CreateImage(t, "Frame", c, new Vector2(-300f, 20f), new Vector2(1024f, 800f), new Color(0.22f, 0.24f, 0.3f, 1f), false);
        outer.raycastTarget = false;
        board = UIUtil.CreateImage(outer.transform, "Board", c, Vector2.zero, new Vector2(1000f, 776f), new Color(0.07f, 0.08f, 0.11f, 1f), false).rectTransform;
        var strip = UIUtil.CreateImage(board, "TitleStrip", new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(1000f, 48f), new Color(0.13f, 0.14f, 0.19f, 1f), false);
        strip.raycastTarget = false;
        var poolName = UIUtil.CreateText(strip.transform, "", new Vector2(0.5f, 0.5f), new Vector2(-120f, 0f), new Vector2(700f, 44f), 28, TextAnchor.MiddleCenter);
        poolName.fontStyle = FontStyle.Bold;
        poolName.color = Theme.Accent;
        poolName.name = "PoolName";
        timerText = UIUtil.CreateText(strip.transform, "", new Vector2(1f, 0.5f), new Vector2(-110f, 0f), new Vector2(200f, 40f), 22, TextAnchor.MiddleRight);
        timerText.color = Theme.TextDim;

        // Skip-animation toggle and the draw button under the board
        var skip = UIUtil.CreateButton(t, "", c, new Vector2(-690f, -445f), new Vector2(330f, 70f), new Color(0f, 0f, 0f, 0.01f), false, 20, out label);
        var box = UIUtil.CreateImage(skip.transform, "Box", new Vector2(0f, 0.5f), new Vector2(30f, 0f), new Vector2(44f, 44f), new Color(0.15f, 0.16f, 0.2f, 1f), false);
        box.raycastTarget = false;
        skipCheck = UIUtil.CreateImage(box.transform, "Check", c, Vector2.zero, new Vector2(28f, 28f), Theme.Accent, false);
        skipCheck.raycastTarget = false;
        var skipText = UIUtil.CreateText(skip.transform, "ANİMASYONU ATLA", new Vector2(0f, 0.5f), new Vector2(180f, 0f), new Vector2(260f, 40f), 24, TextAnchor.MiddleLeft);
        skipText.fontStyle = FontStyle.Bold;
        skip.onClick.AddListener(() => { SkipAnimation = !SkipAnimation; skipCheck.enabled = SkipAnimation; });

        var draw = UIUtil.CreateButton(t, "", c, new Vector2(-300f, -445f), new Vector2(440f, 100f), Theme.Accent, false, 40, out drawLabel);
        drawLabel.color = new Color(0.1f, 0.08f, 0.02f);
        drawLabel.GetComponent<Shadow>().enabled = false;
        drawLabel.supportRichText = true;
        drawButtonImage = draw.GetComponent<Image>();
        draw.onClick.AddListener(DoDraw);
        var tag = UIUtil.CreateImage(t, "FirstTag", c, new Vector2(-10f, -414f), new Vector2(170f, 44f), Theme.Red, false);
        tag.raycastTarget = false;
        var tagText = UIUtil.CreateText(tag.transform, "İLK ÇEKİLİŞ\n%50", c, Vector2.zero, new Vector2(170f, 44f), 17, TextAnchor.MiddleCenter);
        tagText.fontStyle = FontStyle.Bold;
        tagText.lineSpacing = 0.85f;
        discountTag = tag.gameObject;
        oldPriceText = UIUtil.CreateText(t, "", c, new Vector2(-10f, -466f), new Vector2(170f, 34f), 24, TextAnchor.MiddleCenter);
        oldPriceText.color = new Color(1f, 1f, 1f, 0.55f);

        // Right: the showcase gun and the grand prize card
        var previewRect = UIUtil.CreateRect(t, "Showcase", c, new Vector2(620f, 200f), new Vector2(720f, 360f));
        var raw = previewRect.gameObject.AddComponent<RawImage>();
        raw.raycastTarget = false;
        previewRT = new RenderTexture(1152, 576, 24, RenderTextureFormat.ARGB32);
        previewRT.antiAliasing = QualitySettings.GetQualityLevel() >= 2 ? 4 : 2;
        raw.texture = previewRT;

        var card = Theme.Box(t, "PrizeCard", c, new Vector2(620f, -170f), new Vector2(640f, 320f), Theme.Panel, false).transform;
        var head = UIUtil.CreateImage(card, "Head", new Vector2(0.5f, 1f), new Vector2(0f, -34f), new Vector2(640f, 68f), new Color(0.92f, 0.55f, 0.12f, 1f), false);
        head.raycastTarget = false;
        prizeTitle = UIUtil.CreateText(head.transform, "", c, new Vector2(10f, 0f), new Vector2(600f, 60f), 28, TextAnchor.MiddleLeft);
        prizeTitle.fontStyle = FontStyle.Bold;
        prizeSub = UIUtil.CreateText(card, "", new Vector2(0.5f, 1f), new Vector2(0f, -120f), new Vector2(590f, 90f), 22, TextAnchor.UpperLeft);
        prizeSub.horizontalOverflow = HorizontalWrapMode.Wrap;
        prizeSub.supportRichText = true;
        progressText = UIUtil.CreateText(card, "", new Vector2(0.5f, 0f), new Vector2(0f, 92f), new Vector2(590f, 34f), 24, TextAnchor.MiddleLeft);
        progressText.fontStyle = FontStyle.Bold;
        progressPips = UIUtil.CreateRect(card, "Pips", new Vector2(0.5f, 0f), new Vector2(0f, 46f), new Vector2(590f, 26f));
        for (int i = 0; i < LuckyDraw.Count; i++)
        {
            pips[i] = UIUtil.CreateImage(progressPips, "Pip" + i, new Vector2(0f, 0.5f), new Vector2(27f + i * 59f, 0f), new Vector2(50f, 12f), new Color(1f, 1f, 1f, 0.18f), false);
            pips[i].raycastTarget = false;
        }

        BuildResultPanel();
        BuildInfoPanel();
    }

    /// <summary>(Re)builds the prize tiles for the current pool.</summary>
    private void BuildTiles()
    {
        var pool = LuckyDraw.Current;
        if (builtFor == pool.id + LuckyDraw.Week)
            return;
        builtFor = pool.id + LuckyDraw.Week;
        for (int i = 0; i < tiles.Length; i++)
            if (tiles[i] != null)
                Destroy(tiles[i].gameObject);
        var info = board.Find("Info");
        if (info != null)
            Destroy(info.gameObject);
        board.Find("TitleStrip/PoolName").GetComponent<Text>().text = pool.name + " ÇEKİLİŞİ";

        float[] xs = { -357f, -119f, 119f, 357f };
        var small = new Vector2(226f, 166f);
        var big = new Vector2(328f, 262f);
        for (int i = 0; i < 4; i++)
        {
            tiles[i] = Tile(i, new Vector2(xs[i], 238f), small, false);
            tiles[6 + i] = Tile(6 + i, new Vector2(xs[i], -266f), small, false);
        }
        tiles[LuckyDraw.GrandIndex] = Tile(LuckyDraw.GrandIndex, new Vector2(-306f, -14f), big, true);
        tiles[LuckyDraw.SecondIndex] = Tile(LuckyDraw.SecondIndex, new Vector2(306f, -14f), big, true);

        // Centre tile: the odds
        Text l;
        var infoButton = UIUtil.CreateButton(board, "", new Vector2(0.5f, 0.5f), new Vector2(0f, -14f), new Vector2(250f, 262f), new Color(0.2f, 0.17f, 0.08f, 1f), false, 20, out l);
        infoButton.name = "Info";
        var q = UIUtil.CreateText(infoButton.transform, "?", new Vector2(0.5f, 0.5f), new Vector2(-62f, 4f), new Vector2(80f, 110f), 92, TextAnchor.MiddleCenter);
        q.fontStyle = FontStyle.Bold;
        q.color = new Color(1f, 0.85f, 0.35f);
        var it = UIUtil.CreateText(infoButton.transform, "BİLGİ", new Vector2(0.5f, 0.5f), new Vector2(34f, 4f), new Vector2(140f, 60f), 36, TextAnchor.MiddleLeft);
        it.fontStyle = FontStyle.Bold;
        infoButton.onClick.AddListener(() => { if (!busy) infoPanel.SetActive(true); });

        ShowcaseGrand(pool);
    }

    private RectTransform Tile(int index, Vector2 pos, Vector2 size, bool big)
    {
        var pool = LuckyDraw.Current;
        var r = pool.prizes[index];
        Color rc = Theme.Rarity(r.Rarity);
        Color bg = big ? new Color(0.72f, 0.5f, 0.1f, 1f) : new Color(0.24f, 0.19f, 0.5f, 1f);
        var img = UIUtil.CreateImage(board, "Tile" + index, new Vector2(0.5f, 0.5f), pos, size, bg, false);
        img.raycastTarget = false;
        var t = (RectTransform)img.transform;
        var inner = UIUtil.CreateImage(t, "Inner", new Vector2(0.5f, 0.5f), Vector2.zero, size - new Vector2(10f, 10f),
            big ? new Color(0.92f, 0.68f, 0.18f, 1f) : new Color(0.33f, 0.27f, 0.62f, 1f), false);
        inner.raycastTarget = false;
        var shine = UIUtil.CreateImage(t, "Shine", new Vector2(0.5f, 1f), new Vector2(0f, -size.y * 0.25f - 5f), new Vector2(size.x - 10f, size.y * 0.5f), new Color(1f, 1f, 1f, 0.07f), false);
        shine.raycastTarget = false;
        var bar = UIUtil.CreateImage(t, "Rarity", new Vector2(0.5f, 1f), new Vector2(0f, -8f), new Vector2(size.x - 10f, 6f), rc, false);
        bar.raycastTarget = false;

        float picH = size.y - 58f;
        Prize(t, r, new Vector2(0f, 14f), new Vector2(size.x - 24f, picH), index == LuckyDraw.GrandIndex ? GunPicture(pool) : CamoGuns[index % CamoGuns.Length]);

        var name = UIUtil.CreateText(t, r.kind == RewardKind.Credits ? r.amount.ToString("N0") + " Kredi" : ShortName(r), new Vector2(0.5f, 0f), new Vector2(0f, 22f), new Vector2(size.x - 14f, 30f), big ? 24 : 19, TextAnchor.MiddleCenter);
        name.fontStyle = FontStyle.Bold;

        var flash = UIUtil.CreateImage(t, "Flash", new Vector2(0.5f, 0.5f), Vector2.zero, size + new Vector2(10f, 10f), new Color(1f, 1f, 0.85f, 0f), false);
        flash.raycastTarget = false;
        flashes[index] = flash;

        var taken = UIUtil.CreateImage(t, "Taken", new Vector2(0.5f, 0.5f), Vector2.zero, size, new Color(0f, 0f, 0f, 0.66f), false);
        taken.raycastTarget = false;
        var tt = UIUtil.CreateText(taken.transform, "ALINDI", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size.x, 50f), big ? 40 : 30, TextAnchor.MiddleCenter);
        tt.fontStyle = FontStyle.Bold;
        tt.color = Theme.Good;
        takenMarks[index] = taken.gameObject;
        return t;
    }

    private static string GunPicture(LuckyDraw.Pool pool)
    {
        return string.IsNullOrEmpty(pool.gunSkin) ? pool.gun.ToString() : pool.gun + "_" + pool.gunSkin;
    }

    private static string ShortName(Reward r)
    {
        if (r.kind == RewardKind.WeaponCamo)
            return Cosmetics.FindAny(r.id).name;
        return r.Name;
    }

    /// <summary>The prize picture: a camo shown on a gun silhouette, an attachment's icon, or Kredi.</summary>
    private static void Prize(Transform parent, Reward r, Vector2 pos, Vector2 size, string gun)
    {
        var c = new Vector2(0.5f, 0.5f);
        if (r.kind == RewardKind.WeaponCamo)
        {
            var camo = Cosmetics.FindAny(r.id);
            var gunTex = Resources.Load<Texture2D>("UI/Guns/" + gun);
            // fit a 2:1 picture into the box
            float w = Mathf.Min(size.x, size.y * 2f);
            var area = UIUtil.CreateRect(parent, "CamoGun", c, pos, new Vector2(w, w * 0.5f));
            if (gunTex == null)
            {
                var sw = UIUtil.CreateRawSwatch(area, WeaponDressing.Pattern(camo), Vector2.zero, new Vector2(w * 0.7f, w * 0.35f));
                sw.raycastTarget = false;
                return;
            }
            var maskImg = area.gameObject.AddComponent<RawImage>();
            maskImg.texture = gunTex;
            maskImg.raycastTarget = false;
            area.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            var pattern = UIUtil.CreateStretch(area, "Pattern").gameObject.AddComponent<RawImage>();
            pattern.texture = WeaponDressing.Pattern(camo);
            pattern.uvRect = new Rect(0f, 0f, 1.6f, 0.8f);
            pattern.raycastTarget = false;
            // the gun's own shading over the pattern
            var shade = UIUtil.CreateStretch(area, "Shade").gameObject.AddComponent<RawImage>();
            shade.texture = gunTex;
            shade.color = new Color(1f, 1f, 1f, 0.38f);
            shade.raycastTarget = false;
            return;
        }
        float s = Mathf.Min(size.x, size.y) * 0.86f;
        if (r.kind == RewardKind.Credits)
        {
            Icons.Create(parent, "currency", c, pos, new Vector2(s, s)).raycastTarget = false;
            return;
        }
        var icon = Icons.Create(parent, r.IconName, c, pos, new Vector2(s, s));
        icon.raycastTarget = false;
    }

    private void BuildResultPanel()
    {
        var veil = UIUtil.CreateStretch(transform, "Result");
        veil.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.8f);
        resultPanel = veil.gameObject;
        resultPanel.SetActive(false);
    }

    private void ShowResult(int index, GrantedReward g)
    {
        var veil = resultPanel.transform;
        for (int i = veil.childCount - 1; i >= 0; i--)
            Destroy(veil.GetChild(i).gameObject);
        var pool = LuckyDraw.Current;
        var r = g.reward;
        var c = new Vector2(0.5f, 0.5f);
        Color rc = Theme.Rarity(r.Rarity);

        var glow = UIUtil.CreateImage(veil, "Glow", c, new Vector2(0f, 90f), new Vector2(620f, 620f), new Color(rc.r, rc.g, rc.b, 0.22f), true);
        glow.raycastTarget = false;
        glow.gameObject.AddComponent<Spin>().speed = 40f;
        var head = UIUtil.CreateText(veil, "TEBRİKLER!", c, new Vector2(0f, 390f), new Vector2(900f, 80f), 60, TextAnchor.MiddleCenter);
        head.fontStyle = FontStyle.Bold;
        head.color = Theme.Accent;
        var frame = UIUtil.CreateImage(veil, "Frame", c, new Vector2(0f, 90f), new Vector2(520f, 380f), new Color(rc.r * 0.35f, rc.g * 0.35f, rc.b * 0.35f, 1f), false);
        frame.raycastTarget = false;
        var bar = UIUtil.CreateImage(frame.transform, "Bar", new Vector2(0.5f, 1f), new Vector2(0f, -6f), new Vector2(520f, 12f), rc, false);
        bar.raycastTarget = false;
        Prize(frame.transform, r, new Vector2(0f, 10f), new Vector2(480f, 300f), index == LuckyDraw.GrandIndex ? GunPicture(pool) : CamoGuns[index % CamoGuns.Length]);

        var name = UIUtil.CreateText(veil, r.Name, c, new Vector2(0f, -140f), new Vector2(1100f, 64f), 46, TextAnchor.MiddleCenter);
        name.fontStyle = FontStyle.Bold;
        var rarity = UIUtil.CreateText(veil, MapCatalog.TrUpper(r.Rarity) + (r.kind == RewardKind.WeaponCamo && Cosmetics.FindAny(r.id).drawOnly ? "  •  ÇEKİLİŞE ÖZEL" : ""),
            c, new Vector2(0f, -192f), new Vector2(900f, 40f), 28, TextAnchor.MiddleCenter);
        rarity.color = rc;
        if (g.duplicate)
        {
            var dup = UIUtil.CreateText(veil, "Zaten sendeydi: +" + Shop.DuplicateValue(r) + " Kredi verildi", c, new Vector2(0f, -236f), new Vector2(900f, 36f), 24, TextAnchor.MiddleCenter);
            dup.color = Theme.TextDim;
        }

        Text l;
        bool camo = r.kind == RewardKind.WeaponCamo && !g.duplicate;
        var ok = UIUtil.CreateButton(veil, "TAMAM", c, new Vector2(camo ? -170f : 0f, -330f), new Vector2(300f, 92f), Theme.PanelLight, false, 34, out l);
        ok.onClick.AddListener(() => resultPanel.SetActive(false));
        if (camo)
        {
            var wear = UIUtil.CreateButton(veil, "KUŞAN", c, new Vector2(170f, -330f), new Vector2(300f, 92f), Theme.Accent, false, 34, out l);
            l.color = new Color(0.1f, 0.08f, 0.02f);
            l.GetComponent<Shadow>().enabled = false;
            string id = r.id;
            var w = index == LuckyDraw.GrandIndex ? pool.gun : WeaponType.Rifle;
            wear.onClick.AddListener(() =>
            {
                Gunsmith.EquipCamo(w, id);
                resultPanel.SetActive(false);
                GameManager.Instance.uiManager.Toast(Cosmetics.FindAny(id).name + " kuşanıldı (" + Gunsmith.CategoryNames[System.Array.IndexOf(Gunsmith.Weapons, w)] + ")");
            });
        }
        resultPanel.transform.SetAsLastSibling();
        resultPanel.SetActive(true);
    }

    private void BuildInfoPanel()
    {
        var c = new Vector2(0.5f, 0.5f);
        var veil = UIUtil.CreateStretch(transform, "InfoVeil");
        veil.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.75f);
        infoPanel = veil.gameObject;
        var box = Theme.Box(veil, "Box", c, Vector2.zero, new Vector2(900f, 560f), new Color(0.08f, 0.09f, 0.12f, 0.98f), true).transform;
        var h = UIUtil.CreateText(box, "ŞANS ÇEKİLİŞİ NASIL ÇALIŞIR?", new Vector2(0.5f, 1f), new Vector2(0f, -50f), new Vector2(820f, 60f), 34, TextAnchor.MiddleCenter);
        h.fontStyle = FontStyle.Bold;
        h.color = Theme.Accent;
        string text =
            "• Her çekilişte panodaki ödüllerden biri çıkar ve panodan düşer; 10 çekilişte hepsi senin olur.\n" +
            "• Çekiliş ücreti her seferinde artar: 160 → 1.450 Kredi. Haftanın ilk çekilişi %50 indirimli.\n" +
            "• Şans oranları (kalan ödüller arasında): Kredi ve aparatlar yüksek, Epik orta, Efsanevi düşük,\n  Mitik kamuflaj en düşük — ödül azaldıkça şansı artar.\n" +
            "• Mitik kamuflaj yalnızca bu çekilişten çıkar, mağazada satılmaz.\n" +
            "• Zaten sahip olduğun bir ödül çıkarsa karşılığı Kredi olarak verilir.\n" +
            "• Pano her pazartesi yenilenir (3 farklı çekiliş sırayla döner).";
        var body = UIUtil.CreateText(box, text, new Vector2(0.5f, 0.5f), new Vector2(0f, -10f), new Vector2(820f, 360f), 24, TextAnchor.UpperLeft);
        body.horizontalOverflow = HorizontalWrapMode.Wrap;
        Text l;
        UIUtil.CreateButton(box, "KAPAT", new Vector2(0.5f, 0f), new Vector2(0f, 60f), new Vector2(260f, 80f), Theme.PanelLight, false, 30, out l)
            .onClick.AddListener(() => infoPanel.SetActive(false));
        infoPanel.SetActive(false);
    }

    // ----- Open / close -----

    public void Open(System.Action closed)
    {
        onClose = closed;
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        resultPanel.SetActive(false);
        infoPanel.SetActive(false);
        busy = false;
        EnsureStage();
        stage.gameObject.SetActive(true);
        previewCam.enabled = true;
        BuildTiles();
        Refresh();
    }

    public void Hide()
    {
        StopAllCoroutines();
        busy = false;
        if (previewCam != null)
            previewCam.enabled = false;
        if (stage != null)
            stage.gameObject.SetActive(false);
        if (previewRT != null)
            previewRT.Release();
        gameObject.SetActive(false);
    }

    private void Close()
    {
        if (busy)
            return;
        Hide();
        if (onClose != null)
            onClose();
    }

    private void Refresh()
    {
        BuildTiles();   // a new week's board if the week turned while this screen was open
        var p = GameManager.Instance.profile;
        var pool = LuckyDraw.Current;
        coinsText.text = p.coins.ToString("N0");
        timerText.text = LuckyDraw.TimeLeft + " kaldı";
        int done = LuckyDraw.DrawsDone;
        for (int i = 0; i < LuckyDraw.Count; i++)
        {
            if (takenMarks[i] != null)
                takenMarks[i].SetActive(LuckyDraw.Taken(i));
            pips[i].color = i < done ? Theme.Accent : new Color(1f, 1f, 1f, 0.18f);
        }
        skipCheck.enabled = SkipAnimation;

        if (LuckyDraw.Finished)
        {
            drawLabel.text = "TAMAMLANDI";
            drawButtonImage.color = new Color(0.35f, 0.36f, 0.4f, 1f);
            discountTag.SetActive(false);
            oldPriceText.text = "";
        }
        else
        {
            int cost = LuckyDraw.NextCost;
            drawLabel.text = "ÇEK  •  " + cost.ToString("N0") + " Kredi";
            drawButtonImage.color = p.coins >= cost ? Theme.Accent : new Color(0.6f, 0.5f, 0.2f, 1f);
            bool first = done == 0;
            discountTag.SetActive(first);
            oldPriceText.text = first ? "<color=#ff6b5e>" + LuckyDraw.ListPrice.ToString("N0") + "</color> yerine" : "";
            oldPriceText.supportRichText = true;
        }

        var grand = pool.prizes[LuckyDraw.GrandIndex];
        var camo = Cosmetics.FindAny(grand.id);
        prizeTitle.text = ModelLibrary.GunSkinName(pool.gun, pool.gunSkin) + "  –  " + camo.name;
        prizeSub.text = "<color=#" + ColorUtility.ToHtmlStringRGB(Theme.Rarity(camo.rarity)) + ">" + MapCatalog.TrUpper(camo.rarity) + "  •  ÇEKİLİŞE ÖZEL KAMUFLAJ</color>\n" +
            "Parlayan desen her silaha kuşanılabilir. Panoda ayrıca bir Efsanevi kamuflaj, aparatlar ve Kredi var." +
            (LuckyDraw.Taken(LuckyDraw.GrandIndex) ? "\n<color=#6fe37f>Bu hafta kazandın!</color>" : "");
        progressText.text = "ALINAN ÖDÜLLER  " + done + " / " + LuckyDraw.Count;
    }

    // ----- Drawing -----

    private void DoDraw()
    {
        if (busy)
            return;
        if (builtFor != LuckyDraw.Current.id + LuckyDraw.Week)
        {
            Refresh();   // the week turned: show the new board first
            return;
        }
        if (LuckyDraw.Finished)
        {
            GameManager.Instance.uiManager.Toast("Bu haftanın tüm ödüllerini aldın — pazartesi yeni pano!");
            return;
        }
        var p = GameManager.Instance.profile;
        if (p.coins < LuckyDraw.NextCost)
        {
            GameManager.Instance.uiManager.Toast("Yetersiz Kredi! Gereken: " + LuckyDraw.NextCost.ToString("N0"));
            return;
        }
        // remember where the light starts (any prize still on the board) before this one is taken
        int start = Random.Range(0, Ring.Length);
        GrantedReward g;
        int won = LuckyDraw.Draw(p, out g);
        if (won < 0)
            return;
        coinsText.text = p.coins.ToString("N0");
        if (SkipAnimation)
        {
            Refresh();
            Sfx.Play(SoundBank.Fanfare, 0.7f);
            ShowResult(won, g);
            return;
        }
        StartCoroutine(RunningLight(start, won, g));
    }

    private IEnumerator RunningLight(int startRing, int won, GrantedReward g)
    {
        busy = true;
        // Positions still lit: everything not taken before this draw (the prize just won included).
        var lit = new System.Collections.Generic.List<int>();
        for (int k = 0; k < Ring.Length; k++)
        {
            int idx = Ring[(startRing + k) % Ring.Length];
            if (!LuckyDraw.Taken(idx) || idx == won)
                lit.Add(idx);
        }
        int target = lit.IndexOf(won);
        int loops = lit.Count > 3 ? 2 : 4;
        int steps = loops * lit.Count + target;
        for (int s = 0; s <= steps; s++)
        {
            int idx = lit[s % lit.Count];
            for (int i = 0; i < flashes.Length; i++)
                if (flashes[i] != null)
                    flashes[i].color = new Color(1f, 1f, 0.85f, i == idx ? 0.5f : 0f);
            Sfx.Play(SoundBank.Tick, 0.5f, 1f + (s == steps ? 0.25f : 0f));
            float k = steps > 0 ? (float)s / steps : 1f;
            yield return new WaitForSecondsRealtime(Mathf.Lerp(0.045f, 0.34f, k * k * k));
        }
        // blink on the prize
        for (int b = 0; b < 3; b++)
        {
            flashes[won].color = new Color(1f, 1f, 0.85f, 0.75f);
            yield return new WaitForSecondsRealtime(0.11f);
            flashes[won].color = new Color(1f, 1f, 0.85f, 0.1f);
            yield return new WaitForSecondsRealtime(0.09f);
        }
        flashes[won].color = new Color(1f, 1f, 0.85f, 0f);
        Sfx.Play(SoundBank.Fanfare, 0.7f);
        Refresh();
        busy = false;
        ShowResult(won, g);
    }

    // ----- 3D showcase -----

    private void EnsureStage()
    {
        if (stage != null)
            return;
        int layer = GunsmithScreen.PreviewLayer;
        stage = new GameObject("LuckyDrawStage").transform;
        stage.position = StagePos;
        var camGo = new GameObject("LuckyDrawCamera");
        camGo.transform.SetParent(stage, false);
        previewCam = camGo.AddComponent<Camera>();
        previewCam.clearFlags = CameraClearFlags.SolidColor;
        previewCam.backgroundColor = new Color(0f, 0f, 0f, 0f);
        previewCam.cullingMask = 1 << layer;
        previewCam.fieldOfView = 22f;
        previewCam.nearClipPlane = 0.05f;
        previewCam.farClipPlane = 20f;
        previewCam.targetTexture = previewRT;
        previewCam.allowHDR = false;
        previewCam.enabled = false;

        var key = new GameObject("KeyLight").AddComponent<Light>();
        key.transform.SetParent(stage, false);
        key.type = LightType.Directional;
        key.intensity = 1.3f;
        key.color = new Color(1f, 0.96f, 0.9f);
        key.cullingMask = 1 << layer;
        key.shadows = LightShadows.None;
        key.transform.rotation = Quaternion.Euler(35f, -60f, 0f);
        var rim = new GameObject("RimLight").AddComponent<Light>();
        rim.transform.SetParent(stage, false);
        rim.type = LightType.Directional;
        rim.intensity = 0.9f;
        rim.color = new Color(0.75f, 0.6f, 1f);
        rim.cullingMask = 1 << layer;
        rim.shadows = LightShadows.None;
        rim.transform.rotation = Quaternion.Euler(20f, 130f, 0f);

        if (RenderSettings.sun != null)
            RenderSettings.sun.cullingMask &= ~(1 << layer);
        if (Camera.main != null)
            Camera.main.cullingMask &= ~(1 << layer);

        var gunRoot = new GameObject("ShowcaseGun");
        gunRoot.layer = layer;
        gunRoot.transform.SetParent(stage, false);
        previewGun = gunRoot.AddComponent<WeaponController>();
    }

    private void ShowcaseGrand(LuckyDraw.Pool pool)
    {
        EnsureStage();
        var data = Gunsmith.BaseWeapon(pool.gun).Clone();
        data.modelSkin = pool.gunSkin;
        data.camo = pool.prizes[LuckyDraw.GrandIndex].id;
        previewGun.transform.localPosition = Vector3.zero;
        previewGun.Initialize(data, null);
        Bounds b = new Bounds(previewGun.transform.position, Vector3.zero);
        bool first = true;
        foreach (var r in previewGun.GetComponentsInChildren<Renderer>())
        {
            if (r is LineRenderer || r.name == "MuzzleFlash")
                continue;
            if (first) { b = r.bounds; first = false; }
            else b.Encapsulate(r.bounds);
        }
        float length = Mathf.Max(0.25f, Mathf.Max(b.size.z, b.size.x));
        fitDistance = length * 0.6f / Mathf.Tan(previewCam.fieldOfView * 0.5f * Mathf.Deg2Rad) / Mathf.Max(0.5f, previewCam.aspect) + length * 0.5f;
        previewGun.transform.localPosition = -(b.center - previewGun.transform.position);
    }

    private void LateUpdate()
    {
        if (previewCam == null || !previewCam.enabled)
            return;
        yaw += Time.unscaledDeltaTime * 28f;
        stage.rotation = Quaternion.identity;
        Quaternion spin = Quaternion.Euler(0f, yaw, 0f);
        previewCam.transform.position = stage.position + spin * new Vector3(fitDistance, 0.08f, 0f);
        previewCam.transform.LookAt(stage.position);
    }
}

