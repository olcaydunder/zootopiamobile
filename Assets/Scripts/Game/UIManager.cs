using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Builds every screen from code: lobby, match preparation, battle HUD (with minimap) and results.</summary>
public class UIManager : MonoBehaviour
{
    public const string GameTitle = "ZOOTOPIA MOBILE";
    public const string ProducerCredit = "Yapımcı: Olcay Yasin Dünder";

    private Canvas canvas;

    private GameObject lobbyPanel;
    private GameObject matchmakingPanel;
    private GameObject hudPanel;
    private GameObject scopeOverlay;
    private GameObject resultPanel;

    // Lobby
    private Text lobbyNameText, lobbyLevelText, lobbyCoinsText, lobbyStatsText, lobbyGunText, lobbySkinText, lobbyStartLabel;
    private RectTransform lobbyXpFill;
    private readonly List<Image> modeButtons = new List<Image>();
    private readonly List<Image> modeAccents = new List<Image>();
    private readonly List<Text> modeTitles = new List<Text>();
    private readonly List<Text> modeSubs = new List<Text>();
    private MatchMode selectedMode = MatchMode.Solo;
    private GunsmithScreen gunsmith;

    // Matchmaking
    private Text matchmakingText;

    // HUD
    private TouchControls touchControls;
    private RectTransform healthFill;
    private RectTransform armorFill;
    private RectTransform boostFill;
    private Text healthText;
    private Text weaponText;
    private Text ammoText;
    private Text aliveText;
    private Text killsText;
    private Text zoneText;
    private Text zoneWarningText;
    private Text altitudeText;
    private Text toastText;
    private float toastUntil;
    private Text killFeedText;
    private readonly List<string> killFeed = new List<string>();
    private float killFeedClearTime;
    private Image damageFlash;
    private float damageAlpha;
    private Image[] hitMarks;
    private float hitMarkUntil;

    // Damage numbers
    private class Popup
    {
        public Text text;
        public float born;
        public Vector2 start;
    }
    private readonly List<Popup> popups = new List<Popup>();
    private int nextPopup;

    // Minimap
    private const float MapPx = 300f;
    private RawImage minimapRaw;
    private float mapZoom = 1f;
    private Vector2 mapCenterUV = new Vector2(0.5f, 0.5f);
    private RectTransform mapRect;
    private RectTransform playerMarker;
    private RectTransform zoneRing;
    private RectTransform nextZoneRing;
    private RectTransform planeLine;
    private readonly List<RectTransform> allyDots = new List<RectTransform>();
    private readonly List<RectTransform> markDots = new List<RectTransform>();
    private readonly List<Image> stationDots = new List<Image>();
    private readonly List<Image> markedIcons = new List<Image>();
    private readonly List<Image> footArrows = new List<Image>();
    private readonly Dictionary<BotAgent, Vector3> botLastPos = new Dictionary<BotAgent, Vector3>();
    private Text upgradeText;

    // Overlays: settings, shop, pause
    private GameObject settingsPanel;
    private GameObject shopPanel;
    private GameObject pausePanel;
    private Text shopCoinsText;
    private readonly List<Text> shopLabels = new List<Text>();
    private readonly List<Image> shopButtons = new List<Image>();
    private bool settingsFromPause;

    // Downed + damage direction
    private GameObject downedGroup;
    private RectTransform reviveFill;
    private Text downedText;
    private readonly List<Image> damageArrows = new List<Image>();
    private readonly List<float> damageArrowTimes = new List<float>();
    private int nextArrow;

    // Result
    private Text resultTitle;
    private Text resultDetails;

    private const float BarWidth = 420f;

    private void Awake()
    {
        canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;

        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 1f;

        gameObject.AddComponent<GraphicRaycaster>();

        BuildLobby();
        BuildMatchmaking();
        BuildHud();
        BuildResult();
        BuildSettings();
        BuildShop();
        BuildPause();
        HideAll();
    }

    private void HideAll()
    {
        lobbyPanel.SetActive(false);
        matchmakingPanel.SetActive(false);
        hudPanel.SetActive(false);
        resultPanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (gunsmith != null) gunsmith.Hide();
        if (matchPrep != null) matchPrep.Hide();
        if (career != null) career.Hide();
        if (loadout != null) loadout.Hide();
        if (shopPanel != null) shopPanel.SetActive(false);
        if (pausePanel != null) pausePanel.SetActive(false);
    }

    // ----- Builders -----

    private GameObject CreateFullPanel(string panelName, Color background)
    {
        var rect = UIUtil.CreateStretch(canvas.transform, panelName);
        var image = rect.gameObject.AddComponent<Image>();
        image.color = background;
        image.raycastTarget = background.a > 0.01f;
        return rect.gameObject;
    }

    private void BuildLobby()
    {
        // Transparent: the 3D character stands in the middle of the screen.
        lobbyPanel = UIUtil.CreateStretch(canvas.transform, "LobbyPanel").gameObject;
        lobbyPanel.AddComponent<PopIn>();
        var t = lobbyPanel.transform;
        Text unused;

        // Soft dark gradients on both sides so the panels read well over the 3D scene.
        var leftShade = UIUtil.CreateImage(t, "ShadeL", new Vector2(0f, 0.5f), new Vector2(300f, 0f), new Vector2(600f, 1400f), new Color(0f, 0f, 0f, 0.35f), false);
        leftShade.raycastTarget = false;
        var rightShade = UIUtil.CreateImage(t, "ShadeR", new Vector2(1f, 0.5f), new Vector2(-300f, 0f), new Vector2(600f, 1400f), new Color(0f, 0f, 0f, 0.35f), false);
        rightShade.raycastTarget = false;

        // Profile (top-left)
        var prof = Theme.Box(t, "Profile", new Vector2(0f, 1f), new Vector2(300f, -70f), new Vector2(540f, 110f), Theme.Panel, true).transform;
        lobbyRankIcon = Icons.Create(prof, Icons.Rank(0), new Vector2(0f, 0.5f), new Vector2(66f, 0f), new Vector2(96f, 96f));
        lobbyNameText = UIUtil.CreateText(prof, "", new Vector2(0f, 0.5f), new Vector2(300f, 20f), new Vector2(360f, 44f), 32, TextAnchor.MiddleLeft);
        lobbyNameText.fontStyle = FontStyle.Bold;
        lobbyLevelText = UIUtil.CreateText(prof, "", new Vector2(0f, 0.5f), new Vector2(300f, -16f), new Vector2(360f, 30f), 22, TextAnchor.MiddleLeft);
        lobbyLevelText.color = Theme.TextDim;
        UIUtil.CreateImage(prof, "XpBg", new Vector2(0f, 0.5f), new Vector2(300f, -38f), new Vector2(360f, 8f), new Color(1f, 1f, 1f, 0.15f), false).raycastTarget = false;
        var xp = UIUtil.CreateImage(prof, "Xp", new Vector2(0f, 0.5f), new Vector2(120f, -38f), new Vector2(0f, 8f), Theme.Accent, false);
        xp.raycastTarget = false;
        xp.rectTransform.pivot = new Vector2(0f, 0.5f);
        lobbyXpFill = xp.rectTransform;

        // Coins + settings (top-right)
        var coins = Theme.Box(t, "Coins", new Vector2(1f, 1f), new Vector2(-420f, -70f), new Vector2(300f, 80f), Theme.Panel, false).transform;
        Icons.Create(coins, "currency", new Vector2(0f, 0.5f), new Vector2(44f, 0f), new Vector2(58f, 58f));
        lobbyCoinsText = UIUtil.CreateText(coins, "", new Vector2(0f, 0.5f), new Vector2(170f, 0f), new Vector2(200f, 60f), 34, TextAnchor.MiddleLeft);
        lobbyCoinsText.fontStyle = FontStyle.Bold;
        UIUtil.CreateButton(t, "AYARLAR", new Vector2(1f, 1f), new Vector2(-140f, -70f), new Vector2(220f, 80f), Theme.Panel, false, 28, out unused)
            .onClick.AddListener(() => OpenSettings(false));

        // Left tiles
        var loadTile = UIUtil.CreateButton(t, "", new Vector2(0f, 0.5f), new Vector2(260f, 335f), new Vector2(440f, 130f), Theme.Panel, false, 20, out unused);
        loadTile.onClick.AddListener(OpenLoadout);
        var lt = loadTile.transform;
        UIUtil.CreateImage(lt, "Accent", new Vector2(0f, 0.5f), new Vector2(4f, 0f), new Vector2(8f, 130f), new Color(0.3f, 0.85f, 0.5f), false).raycastTarget = false;
        var lTitle = UIUtil.CreateText(lt, "TEÇHİZAT", new Vector2(0f, 1f), new Vector2(240f, -38f), new Vector2(300f, 50f), 32, TextAnchor.MiddleLeft);
        lTitle.fontStyle = FontStyle.Bold;
        lobbyClassIcon = Icons.Create(lt, "class_medic", new Vector2(0f, 0.5f), new Vector2(56f, 0f), new Vector2(84f, 84f));
        lobbyClassText = UIUtil.CreateText(lt, "", new Vector2(0f, 0f), new Vector2(240f, 36f), new Vector2(300f, 40f), 22, TextAnchor.MiddleLeft);
        lobbyClassText.color = Theme.TextDim;

        var gunsmithTile = UIUtil.CreateButton(t, "", new Vector2(0f, 0.5f), new Vector2(260f, 150f), new Vector2(440f, 170f), Theme.Panel, false, 20, out unused);
        gunsmithTile.onClick.AddListener(OpenGunsmith);
        var gt = gunsmithTile.transform;
        UIUtil.CreateImage(gt, "Accent", new Vector2(0f, 0.5f), new Vector2(4f, 0f), new Vector2(8f, 170f), Theme.Accent, false).raycastTarget = false;
        var gTitle = UIUtil.CreateText(gt, "SİLAH ATÖLYESİ", new Vector2(0f, 1f), new Vector2(200f, -40f), new Vector2(360f, 50f), 34, TextAnchor.MiddleLeft);
        gTitle.fontStyle = FontStyle.Bold;
        gTitle.color = Theme.Accent;
        lobbyGunText = UIUtil.CreateText(gt, "", new Vector2(0f, 0f), new Vector2(200f, 55f), new Vector2(360f, 70f), 22, TextAnchor.MiddleLeft);
        lobbyGunText.color = Theme.TextDim;

        var charTile = UIUtil.CreateButton(t, "", new Vector2(0f, 0.5f), new Vector2(260f, -40f), new Vector2(440f, 130f), Theme.Panel, false, 20, out unused);
        charTile.onClick.AddListener(() => { HideAll(); matchPrep.Open(selectedMode); });   // character + weapon select
        var ct = charTile.transform;
        UIUtil.CreateImage(ct, "Accent", new Vector2(0f, 0.5f), new Vector2(4f, 0f), new Vector2(8f, 130f), new Color(0.3f, 0.7f, 1f), false).raycastTarget = false;
        var cTitle = UIUtil.CreateText(ct, "KARAKTERLER", new Vector2(0f, 1f), new Vector2(200f, -38f), new Vector2(360f, 50f), 32, TextAnchor.MiddleLeft);
        cTitle.fontStyle = FontStyle.Bold;
        lobbySkinText = UIUtil.CreateText(ct, "", new Vector2(0f, 0f), new Vector2(200f, 36f), new Vector2(360f, 40f), 22, TextAnchor.MiddleLeft);
        lobbySkinText.color = Theme.TextDim;

        var statsTile = UIUtil.CreateButton(t, "", new Vector2(0f, 0.5f), new Vector2(260f, -210f), new Vector2(440f, 170f), Theme.Panel, false, 20, out unused);
        statsTile.onClick.AddListener(OpenCareer);
        var stats = statsTile.transform;
        UIUtil.CreateImage(stats, "Accent", new Vector2(0f, 0.5f), new Vector2(4f, 0f), new Vector2(8f, 170f), new Color(0.75f, 0.45f, 1f), false).raycastTarget = false;
        var sTitle = UIUtil.CreateText(stats, "KARİYER  •  ÖDÜLLER", new Vector2(0f, 1f), new Vector2(200f, -32f), new Vector2(360f, 40f), 26, TextAnchor.MiddleLeft);
        sTitle.fontStyle = FontStyle.Bold;
        lobbyStatsText = UIUtil.CreateText(stats, "", new Vector2(0f, 0f), new Vector2(200f, 62f), new Vector2(380f, 100f), 24, TextAnchor.MiddleLeft);
        lobbyStatsText.color = Theme.TextDim;

        // Right: game modes
        string[] modes = { "SOLO", "DUO", "SQUAD" };
        string[] subs = { "Tek başına hayatta kal", "1 takım arkadaşıyla", "3 takım arkadaşıyla" };
        for (int i = 0; i < 3; i++)
        {
            int index = i;
            var b = UIUtil.CreateButton(t, "", new Vector2(1f, 0.5f), new Vector2(-280f, 190f - i * 118f), new Vector2(480f, 104f), Theme.Panel, false, 20, out unused);
            b.onClick.AddListener(() => SelectMode(index));
            var bt = b.transform;
            var accent = UIUtil.CreateImage(bt, "Accent", new Vector2(0f, 0.5f), new Vector2(4f, 0f), new Vector2(8f, 104f), Theme.Accent, false);
            accent.raycastTarget = false;
            var mt = UIUtil.CreateText(bt, modes[i], new Vector2(0f, 0.5f), new Vector2(200f, 16f), new Vector2(340f, 50f), 36, TextAnchor.MiddleLeft);
            mt.fontStyle = FontStyle.Bold;
            var ms = UIUtil.CreateText(bt, subs[i], new Vector2(0f, 0.5f), new Vector2(200f, -24f), new Vector2(340f, 30f), 22, TextAnchor.MiddleLeft);
            modeButtons.Add(b.GetComponent<Image>());
            modeAccents.Add(accent);
            modeTitles.Add(mt);
            modeSubs.Add(ms);
        }
        var mapLabel = UIUtil.CreateText(t, MapData.Loaded ? "BATTLE ROYALE  •  Çekmeköy  •  25 oyuncu" : "BATTLE ROYALE  •  Zootopia Adası  •  25 oyuncu", new Vector2(1f, 0.5f), new Vector2(-280f, 290f), new Vector2(480f, 40f), 22, TextAnchor.MiddleLeft);
        mapLabel.color = Theme.Accent;

        UIUtil.CreateButton(t, "BAŞLAT", new Vector2(1f, 0f), new Vector2(-280f, 110f), new Vector2(480f, 130f), Theme.Accent, false, 54, out lobbyStartLabel)
            .onClick.AddListener(() => { HideAll(); matchPrep.Open(selectedMode); });
        lobbyStartLabel.color = new Color(0.1f, 0.08f, 0.02f);
        lobbyStartLabel.GetComponent<Shadow>().enabled = false;

        // Bottom-left: title + credit
        var title = UIUtil.CreateText(t, GameTitle, new Vector2(0f, 0f), new Vector2(330f, 120f), new Vector2(600f, 60f), 46, TextAnchor.MiddleLeft);
        title.fontStyle = FontStyle.Bold;
        title.color = Theme.Accent;
        var credit = UIUtil.CreateText(t, ProducerCredit, new Vector2(0f, 0f), new Vector2(330f, 75f), new Vector2(600f, 40f), 24, TextAnchor.MiddleLeft);
        credit.color = Theme.TextDim;
        if (MapData.Loaded)
        {
            var osm = UIUtil.CreateText(t, "Harita: Ekşioğlu, Çekmeköy  •  © OpenStreetMap katkıcıları", new Vector2(0f, 0f), new Vector2(330f, 40f), new Vector2(600f, 30f), 18, TextAnchor.MiddleLeft);
            osm.color = Theme.TextDim;
        }

        gunsmith = GunsmithScreen.Create(canvas.transform);
        matchPrep = MatchPrepScreen.Create(canvas.transform);
        career = CareerScreen.Create(canvas.transform);
        loadout = LoadoutScreen.Create(canvas.transform);
        matchPrep.gameObject.AddComponent<PopIn>();
        gunsmith.gameObject.AddComponent<PopIn>();
        SelectMode(0);
    }

    private void SelectMode(int index)
    {
        selectedMode = (MatchMode)index;
        for (int i = 0; i < modeButtons.Count; i++)
        {
            bool sel = i == index;
            modeButtons[i].color = sel ? Theme.Selected : Theme.Panel;
            modeAccents[i].enabled = sel;
            modeTitles[i].color = sel ? new Color(0.08f, 0.08f, 0.1f) : Color.white;
            modeTitles[i].GetComponent<Shadow>().enabled = !sel;
            modeSubs[i].color = sel ? new Color(0.2f, 0.2f, 0.24f) : Theme.TextDim;
            modeSubs[i].GetComponent<Shadow>().enabled = !sel;
        }
        if (lobbyStartLabel != null)
            lobbyStartLabel.text = "BAŞLAT  •  " + selectedMode.ToString().ToUpper();
    }

    private void OpenLoadout()
    {
        HideAll();
        loadout.Open(ShowLobby);
    }

    private void OpenCareer()
    {
        HideAll();
        career.Open(ShowLobby);
    }

    private void OpenGunsmith()
    {
        lobbyPanel.SetActive(false);
        gunsmith.Open();
    }

    private void BuildMatchmaking()
    {
        matchmakingPanel = CreateFullPanel("MatchmakingPanel", new Color(0.03f, 0.06f, 0.1f, 0.75f));
        matchmakingText = UIUtil.CreateText(matchmakingPanel.transform, "", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1200f, 120f), 64, TextAnchor.MiddleCenter);
        matchmakingText.fontStyle = FontStyle.Bold;
    }

    private static Texture2D scopeTex;

    /// <summary>Black ring with a clear lens, thin reticle and a soft dark edge.</summary>
    private static Texture2D ScopeTexture()
    {
        if (scopeTex != null)
            return scopeTex;
        const int n = 512;
        scopeTex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        scopeTex.wrapMode = TextureWrapMode.Clamp;
        var px = new Color32[n * n];
        float h = n * 0.5f;
        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                float dx = x + 0.5f - h, dy = y + 0.5f - h;
                float d = Mathf.Sqrt(dx * dx + dy * dy) / h;
                float a = Mathf.Clamp01((d - 0.9f) / 0.08f);           // lens edge vignette
                float adx = Mathf.Abs(dx), ady = Mathf.Abs(dy);
                bool line = (adx < 1.2f || ady < 1.2f) && d < 0.9f && (adx > 6f || ady > 6f);
                bool post = d > 0.32f && d < 0.9f && (adx < 3.5f || ady < 3.5f) && !(dy > 0f && adx < 3.5f);  // thick posts except top
                if (line || post)
                    a = Mathf.Max(a, 0.9f);
                bool dot = adx < 2.5f && ady < 2.5f;
                px[y * n + x] = dot ? new Color32(255, 40, 30, 255) : new Color32(0, 0, 0, (byte)(a * 255f));
            }
        }
        scopeTex.SetPixels32(px);
        scopeTex.Apply();
        return scopeTex;
    }

    private void BuildHud()
    {
        hudPanel = CreateFullPanel("BattleHud", new Color(0f, 0f, 0f, 0f));
        var t = hudPanel.transform;

        damageFlash = UIUtil.CreateStretch(t, "DamageFlash").gameObject.AddComponent<Image>();
        damageFlash.color = new Color(0.9f, 0f, 0f, 0f);
        damageFlash.raycastTarget = false;

        // Sniper/3x/6x scope view (under the rest of the HUD).
        scopeOverlay = UIUtil.CreateStretch(t, "ScopeOverlay").gameObject;
        var so = scopeOverlay.transform;
        var lens = UIUtil.CreateRect(so, "Lens", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1080f, 1080f)).gameObject.AddComponent<RawImage>();
        lens.texture = ScopeTexture();
        lens.raycastTarget = false;
        UIUtil.CreateImage(so, "SideL", new Vector2(0.5f, 0.5f), new Vector2(-540f - 1500f, 0f), new Vector2(3000f, 1100f), Color.black, false).raycastTarget = false;
        UIUtil.CreateImage(so, "SideR", new Vector2(0.5f, 0.5f), new Vector2(540f + 1500f, 0f), new Vector2(3000f, 1100f), Color.black, false).raycastTarget = false;
        scopeOverlay.SetActive(false);

        // Crosshair + hit marker
        var c = new Vector2(0.5f, 0.5f);
        var crossColor = new Color(1f, 1f, 1f, 0.85f);
        crossH = UIUtil.CreateImage(t, "CrossH", c, Vector2.zero, new Vector2(28f, 3f), crossColor, false);
        crossH.raycastTarget = false;
        crossV = UIUtil.CreateImage(t, "CrossV", c, Vector2.zero, new Vector2(3f, 28f), crossColor, false);
        crossV.raycastTarget = false;
        hitMarks = new Image[4];
        for (int i = 0; i < 4; i++)
        {
            float sx = i % 2 == 0 ? -1f : 1f;
            float sy = i < 2 ? -1f : 1f;
            var img = UIUtil.CreateImage(t, "HitMark", c, new Vector2(sx * 16f, sy * 16f), new Vector2(14f, 3f), Color.white, false);
            img.raycastTarget = false;
            img.rectTransform.localRotation = Quaternion.Euler(0f, 0f, sx * sy > 0 ? 45f : -45f);
            img.enabled = false;
            hitMarks[i] = img;
        }

        // Top: alive / kills / zone
        var top = new Vector2(0.5f, 1f);
        aliveText = UIUtil.CreateText(t, "", top, new Vector2(-110f, -45f), new Vector2(220f, 50f), 34, TextAnchor.MiddleCenter);
        killsText = UIUtil.CreateText(t, "", top, new Vector2(110f, -45f), new Vector2(220f, 50f), 34, TextAnchor.MiddleCenter);
        zoneText = UIUtil.CreateText(t, "", top, new Vector2(0f, -95f), new Vector2(700f, 44f), 28, TextAnchor.MiddleCenter);
        zoneWarningText = UIUtil.CreateText(t, "BÖLGENİN DIŞINDASIN!", top, new Vector2(0f, -145f), new Vector2(800f, 50f), 34, TextAnchor.MiddleCenter);
        zoneWarningText.color = new Color(1f, 0.35f, 0.3f);
        zoneWarningText.fontStyle = FontStyle.Bold;

        toastText = UIUtil.CreateText(t, "", c, new Vector2(0f, 230f), new Vector2(1000f, 60f), 38, TextAnchor.MiddleCenter);
        toastText.fontStyle = FontStyle.Bold;

        altitudeText = UIUtil.CreateText(t, "", new Vector2(0f, 0.5f), new Vector2(200f, 120f), new Vector2(360f, 50f), 34, TextAnchor.MiddleLeft);
        altitudeText.fontStyle = FontStyle.Bold;

        BuildMinimap(t);
        killFeedText = UIUtil.CreateText(t, "", new Vector2(1f, 1f), new Vector2(-230f, -440f), new Vector2(420f, 160f), 26, TextAnchor.UpperRight);

        // Bottom centre: health, armor and boost bars, weapon & ammo
        var bottom = new Vector2(0.5f, 0f);
        UIUtil.CreateImage(t, "HealthBg", bottom, new Vector2(0f, 175f), new Vector2(BarWidth, 26f), new Color(0f, 0f, 0f, 0.5f), false).raycastTarget = false;
        healthFill = CreateFill(t, "HealthFill", new Vector2(0f, 175f), 26f, new Color(0.95f, 0.95f, 0.95f, 0.95f));
        healthText = UIUtil.CreateText(t, "", bottom, new Vector2(0f, 175f), new Vector2(BarWidth, 26f), 20, TextAnchor.MiddleCenter);
        healthText.color = Color.black;
        healthText.GetComponent<Shadow>().enabled = false;

        UIUtil.CreateImage(t, "ArmorBg", bottom, new Vector2(0f, 150f), new Vector2(BarWidth, 12f), new Color(0f, 0f, 0f, 0.5f), false).raycastTarget = false;
        armorFill = CreateFill(t, "ArmorFill", new Vector2(0f, 150f), 12f, new Color(0.3f, 0.6f, 1f, 0.95f));
        boostFill = CreateFill(t, "BoostFill", new Vector2(0f, 192f), 5f, new Color(1f, 0.6f, 0.85f, 0.95f));

        weaponText = UIUtil.CreateText(t, "", bottom, new Vector2(0f, 255f), new Vector2(600f, 40f), 28, TextAnchor.MiddleCenter);
        ammoText = UIUtil.CreateText(t, "", bottom, new Vector2(0f, 215f), new Vector2(600f, 44f), 36, TextAnchor.MiddleCenter);
        ammoText.fontStyle = FontStyle.Bold;

        // Damage number pool
        for (int i = 0; i < 10; i++)
        {
            var p = UIUtil.CreateText(t, "", c, Vector2.zero, new Vector2(200f, 50f), 34, TextAnchor.MiddleCenter);
            p.fontStyle = FontStyle.Bold;
            p.enabled = false;
            popups.Add(new Popup { text = p, born = -10f });
        }

        // Damage direction arrows around the crosshair.
        for (int i = 0; i < 4; i++)
        {
            var arrow = UIUtil.CreateImage(t, "DamageDir", c, Vector2.zero, new Vector2(90f, 14f), new Color(1f, 0.15f, 0.1f, 0f), false);
            arrow.raycastTarget = false;
            damageArrows.Add(arrow);
            damageArrowTimes.Add(-10f);
        }

        // Class abilities: marked enemies (red diamonds over their heads), K9 footstep arrows, upgrade progress.
        for (int i = 0; i < 10; i++)
        {
            var m = UIUtil.CreateImage(t, "Marked", c, Vector2.zero, new Vector2(24f, 24f), new Color(1f, 0.2f, 0.15f, 0.95f), false);
            m.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            m.raycastTarget = false;
            m.enabled = false;
            markedIcons.Add(m);
        }
        for (int i = 0; i < 4; i++)
        {
            var a = UIUtil.CreateImage(t, "Footstep", c, Vector2.zero, new Vector2(70f, 12f), new Color(1f, 0.6f, 0.15f, 0f), false);
            a.raycastTarget = false;
            footArrows.Add(a);
        }
        upgradeText = UIUtil.CreateText(t, "", c, new Vector2(0f, -220f), new Vector2(800f, 50f), 34, TextAnchor.MiddleCenter);
        upgradeText.fontStyle = FontStyle.Bold;
        upgradeText.color = new Color(0.75f, 0.55f, 1f);

        // Knocked-down overlay.
        downedGroup = UIUtil.CreateStretch(t, "Downed").gameObject;
        var dg = downedGroup.transform;
        downedText = UIUtil.CreateText(dg, "YERE DÜŞTÜN", c, new Vector2(0f, -120f), new Vector2(900f, 60f), 44, TextAnchor.MiddleCenter);
        downedText.color = new Color(1f, 0.4f, 0.35f);
        downedText.fontStyle = FontStyle.Bold;
        UIUtil.CreateImage(dg, "ReviveBg", c, new Vector2(0f, -175f), new Vector2(BarWidth, 18f), new Color(0f, 0f, 0f, 0.55f), false).raycastTarget = false;
        var fill = UIUtil.CreateImage(dg, "ReviveFill", c, new Vector2(-BarWidth * 0.5f, -175f), new Vector2(0f, 18f), new Color(0.3f, 1f, 0.45f, 0.95f), false);
        fill.raycastTarget = false;
        reviveFill = fill.rectTransform;
        reviveFill.pivot = new Vector2(0f, 0.5f);
        downedGroup.SetActive(false);

        // Touch controls live on their own full-screen layer inside the HUD.
        var controlsRect = UIUtil.CreateStretch(t, "TouchControls");
        touchControls = controlsRect.gameObject.AddComponent<TouchControls>();
        touchControls.Build(canvas);

        // Pause button (top-left, above the touch layer).
        Text pauseLabel;
        UIUtil.CreateButton(t, "II", new Vector2(0f, 1f), new Vector2(70f, -60f), new Vector2(90f, 80f), new Color(0f, 0f, 0f, 0.45f), false, 34, out pauseLabel)
            .onClick.AddListener(OpenPause);
    }

    private void BuildMinimap(Transform parent)
    {
        var anchor = new Vector2(1f, 1f);
        var frame = UIUtil.CreateImage(parent, "MinimapFrame", anchor, new Vector2(-185f, -185f), new Vector2(MapPx + 12f, MapPx + 12f), new Color(0f, 0f, 0f, 0.55f), false);
        frame.raycastTarget = false;

        mapRect = UIUtil.CreateRect(frame.transform, "Minimap", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(MapPx, MapPx));
        var raw = mapRect.gameObject.AddComponent<RawImage>();
        raw.texture = World.MinimapTexture;
        minimapRaw = raw;
        raw.raycastTarget = false;
        mapRect.gameObject.AddComponent<RectMask2D>();

        var line = UIUtil.CreateImage(mapRect, "PlanePath", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(10f, 3f), new Color(1f, 0.9f, 0.3f, 0.9f), false);
        line.raycastTarget = false;
        planeLine = line.rectTransform;

        var next = UIUtil.CreateImage(mapRect, "NextZone", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(10f, 10f), new Color(1f, 1f, 1f, 0.9f), false);
        next.sprite = UIUtil.Ring;
        next.raycastTarget = false;
        nextZoneRing = next.rectTransform;

        var zone = UIUtil.CreateImage(mapRect, "Zone", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(10f, 10f), new Color(0.35f, 0.65f, 1f, 1f), false);
        zone.sprite = UIUtil.Ring;
        zone.raycastTarget = false;
        zoneRing = zone.rectTransform;

        for (int i = 0; i < 3; i++)
        {
            var dot = UIUtil.CreateImage(mapRect, "Ally", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(11f, 11f), new Color(0.3f, 1f, 0.45f), true);
            dot.raycastTarget = false;
            allyDots.Add(dot.rectTransform);
        }

        for (int i = 0; i < UpgradeStation.Count; i++)
        {
            var s = Icons.Create(mapRect, "upgrade_station", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(26f, 26f));
            stationDots.Add(s);
        }
        for (int i = 0; i < 10; i++)
        {
            var d = UIUtil.CreateImage(mapRect, "Marked", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(12f, 12f), new Color(1f, 0.2f, 0.15f), false);
            d.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            d.raycastTarget = false;
            markDots.Add(d.rectTransform);
        }

        var marker = UIUtil.CreateImage(mapRect, "Player", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(14f, 14f), new Color(1f, 0.85f, 0.2f), true);
        marker.raycastTarget = false;
        playerMarker = marker.rectTransform;
        var nose = UIUtil.CreateImage(playerMarker, "Facing", new Vector2(0.5f, 0.5f), new Vector2(0f, 11f), new Vector2(4f, 12f), new Color(1f, 0.85f, 0.2f), false);
        nose.raycastTarget = false;

        UIUtil.CreateText(frame.transform, "K", new Vector2(0.5f, 1f), new Vector2(0f, -16f), new Vector2(30f, 30f), 22, TextAnchor.MiddleCenter);
    }

    /// <summary>World position to minimap pixels (relative to the current view centre and zoom).</summary>
    private Vector2 MapPos(Vector3 world)
    {
        Vector2 uv = World.ToMapUV(world);
        return (uv - mapCenterUV) * MapPx * mapZoom;
    }

    private RectTransform CreateFill(Transform parent, string fillName, Vector2 position, float height, Color color)
    {
        var img = UIUtil.CreateImage(parent, fillName, new Vector2(0.5f, 0f), position + new Vector2(-BarWidth * 0.5f, 0f), new Vector2(BarWidth, height), color, false);
        img.raycastTarget = false;
        var rect = img.rectTransform;
        rect.pivot = new Vector2(0f, 0.5f);
        return rect;
    }

    private void BuildResult()
    {
        resultPanel = CreateFullPanel("ResultPanel", new Color(0.02f, 0.04f, 0.08f, 0.8f));
        resultPanel.AddComponent<PopIn>();
        var t = resultPanel.transform;
        var c = new Vector2(0.5f, 0.5f);

        resultTitle = UIUtil.CreateText(t, "", c, new Vector2(0f, 330f), new Vector2(1500f, 140f), 96, TextAnchor.MiddleCenter);
        resultTitle.fontStyle = FontStyle.Bold;
        resultDetails = UIUtil.CreateText(t, "", c, new Vector2(0f, 190f), new Vector2(1400f, 120f), 38, TextAnchor.MiddleCenter);
        resultLevel = UIUtil.CreateText(t, "", c, new Vector2(0f, 95f), new Vector2(1400f, 50f), 36, TextAnchor.MiddleCenter);
        resultLevel.fontStyle = FontStyle.Bold;
        resultLevel.color = Theme.Accent;
        resultRewards = UIUtil.CreateRect(t, "Rewards", c, new Vector2(0f, -70f), new Vector2(1400f, 220f));

        Text unused;
        UIUtil.CreateButton(t, "LOBİYE DÖN", c, new Vector2(0f, -330f), new Vector2(420f, 120f), new Color(0.2f, 0.5f, 1f, 0.95f), false, 40, out unused)
            .onClick.AddListener(() => GameManager.Instance.JoinLobby());
    }

    // ----- Settings / shop / pause -----

    private GameObject CreateOverlay(string name, Vector2 size)
    {
        var dim = CreateFullPanel(name, new Color(0f, 0f, 0f, 0.6f));
        var box = UIUtil.CreateImage(dim.transform, "Box", new Vector2(0.5f, 0.5f), Vector2.zero, size, new Color(0.07f, 0.1f, 0.15f, 0.97f), false);
        box.raycastTarget = true;
        return dim;
    }

    private Text SettingRow(Transform box, string label, float y, UnityEngine.Events.UnityAction minus, UnityEngine.Events.UnityAction plus)
    {
        var c = new Vector2(0.5f, 0.5f);
        UIUtil.CreateText(box, label, c, new Vector2(-250f, y), new Vector2(320f, 60f), 34, TextAnchor.MiddleLeft);
        Text unused;
        UIUtil.CreateButton(box, "-", c, new Vector2(40f, y), new Vector2(80f, 70f), new Color(0.25f, 0.3f, 0.4f, 1f), false, 40, out unused).onClick.AddListener(minus);
        var value = UIUtil.CreateText(box, "", c, new Vector2(175f, y), new Vector2(180f, 60f), 34, TextAnchor.MiddleCenter);
        UIUtil.CreateButton(box, "+", c, new Vector2(310f, y), new Vector2(80f, 70f), new Color(0.25f, 0.3f, 0.4f, 1f), false, 40, out unused).onClick.AddListener(plus);
        return value;
    }

    private SettingsScreen settingsScreen;
    private Image crossH, crossV;
    private MatchPrepScreen matchPrep;
    private CareerScreen career;
    private LoadoutScreen loadout;
    private Image lobbyClassIcon;
    private Text lobbyClassText;
    private Image lobbyRankIcon;
    private RectTransform resultRewards;
    private Text resultLevel;

    private void BuildSettings()
    {
        settingsScreen = SettingsScreen.Create(canvas.transform);
        settingsScreen.gameObject.AddComponent<PopIn>();
        settingsPanel = settingsScreen.gameObject;
    }

    public void OpenSettings(bool fromPause)
    {
        settingsFromPause = fromPause;
        if (pausePanel != null) pausePanel.SetActive(false);
        settingsScreen.Open(() =>
        {
            if (settingsFromPause && pausePanel != null)
                pausePanel.SetActive(true);
        });
    }

    private void BuildShop()
    {
        shopPanel = CreateFullPanel("ShopPanel", new Color(0f, 0f, 0f, 0.15f));
        var boxImg = UIUtil.CreateImage(shopPanel.transform, "Box", new Vector2(0.5f, 0f), new Vector2(0f, 300f), new Vector2(1240f, 560f), new Color(0.07f, 0.1f, 0.15f, 0.92f), false);
        var box = boxImg.transform;
        var c = new Vector2(0.5f, 0.5f);
        var title = UIUtil.CreateText(box, "KARAKTERLER", c, new Vector2(0f, 235f), new Vector2(800f, 60f), 42, TextAnchor.MiddleCenter);
        title.fontStyle = FontStyle.Bold;
        shopCoinsText = UIUtil.CreateText(box, "", c, new Vector2(0f, 185f), new Vector2(1100f, 44f), 28, TextAnchor.MiddleCenter);
        shopCoinsText.color = new Color(1f, 0.85f, 0.3f);

        for (int i = 0; i < ModelLibrary.ShopSkins.Length; i++)
        {
            int index = i;
            float x = (i % 3 - 1) * 390f;
            float y = i < 3 ? 75f : -75f;
            Text label;
            var b = UIUtil.CreateButton(box, "", c, new Vector2(x, y), new Vector2(360f, 130f), new Color(0.2f, 0.25f, 0.35f, 1f), false, 30, out label);
            b.onClick.AddListener(() => ShopClicked(index));
            shopLabels.Add(label);
            shopButtons.Add(b.GetComponent<Image>());
        }

        UIUtil.CreateText(box, "Seçtiğin karakter yukarıda lobide hemen görünür", c, new Vector2(-300f, -215f), new Vector2(560f, 40f), 24, TextAnchor.MiddleCenter);
        Text unused;
        UIUtil.CreateButton(box, "KAPAT", c, new Vector2(330f, -215f), new Vector2(280f, 76f), new Color(0.2f, 0.5f, 1f, 0.95f), false, 34, out unused)
            .onClick.AddListener(() => { shopPanel.SetActive(false); ShowLobby(); });
    }

    private void OpenShop()
    {
        RefreshShop();
        shopPanel.SetActive(true);
    }

    private void RefreshShop()
    {
        var p = GameManager.Instance.profile;
        shopCoinsText.text = "Kredi: " + p.coins + "   (maç kazanarak ve öldürerek kazanılır)";
        for (int i = 0; i < ModelLibrary.ShopSkins.Length; i++)
        {
            string skin = ModelLibrary.ShopSkins[i];
            string status;
            Color color;
            if (p.equippedSkin == skin)
            {
                status = "KUŞANILDI";
                color = new Color(0.2f, 0.6f, 0.3f, 1f);
            }
            else if (p.OwnsSkin(skin))
            {
                status = "Kuşan";
                color = new Color(0.2f, 0.35f, 0.55f, 1f);
            }
            else
            {
                status = ModelLibrary.ShopPrices[i] + " Kredi";
                color = p.coins >= ModelLibrary.ShopPrices[i] ? new Color(0.55f, 0.4f, 0.12f, 1f) : new Color(0.25f, 0.25f, 0.28f, 1f);
            }
            shopLabels[i].text = ModelLibrary.ShopNames[i] + "\n" + status;
            shopButtons[i].color = color;
        }
    }

    private void ShopClicked(int index)
    {
        var gm = GameManager.Instance;
        var p = gm.profile;
        string skin = ModelLibrary.ShopSkins[index];
        if (!p.OwnsSkin(skin))
        {
            if (!p.BuySkin(skin, ModelLibrary.ShopPrices[index]))
            {
                shopCoinsText.text = "Yetersiz Kredi! Gereken: " + ModelLibrary.ShopPrices[index];
                return;
            }
            Sfx.Play(SoundBank.Pickup, 0.6f);
        }
        p.EquipSkin(skin);
        if (gm.player != null)
            gm.player.ApplySkin(skin);
        RefreshShop();
    }

    private void BuildPause()
    {
        pausePanel = CreateOverlay("PausePanel", new Vector2(640f, 560f));
        pausePanel.AddComponent<PopIn>();
        var box = pausePanel.transform.Find("Box");
        var c = new Vector2(0.5f, 0.5f);
        var title = UIUtil.CreateText(box, "DURAKLATILDI", c, new Vector2(0f, 200f), new Vector2(600f, 70f), 46, TextAnchor.MiddleCenter);
        title.fontStyle = FontStyle.Bold;
        Text unused;
        UIUtil.CreateButton(box, "DEVAM ET", c, new Vector2(0f, 70f), new Vector2(420f, 96f), new Color(0.2f, 0.6f, 0.3f, 1f), false, 36, out unused)
            .onClick.AddListener(ClosePause);
        UIUtil.CreateButton(box, "AYARLAR", c, new Vector2(0f, -50f), new Vector2(420f, 96f), new Color(0.35f, 0.38f, 0.45f, 1f), false, 36, out unused)
            .onClick.AddListener(() => OpenSettings(true));
        UIUtil.CreateButton(box, "MAÇTAN ÇIK", c, new Vector2(0f, -170f), new Vector2(420f, 96f), new Color(0.7f, 0.2f, 0.18f, 1f), false, 36, out unused)
            .onClick.AddListener(() => { Time.timeScale = 1f; GameManager.Instance.JoinLobby(); });
    }

    private void OpenPause()
    {
        var gm = GameManager.Instance;
        if (gm == null || gm.currentState != GameState.InGame)
            return;
        Time.timeScale = 0f;
        touchControls.ResetState();
        pausePanel.SetActive(true);
    }

    private void ClosePause()
    {
        pausePanel.SetActive(false);
        settingsPanel.SetActive(false);
        Time.timeScale = 1f;
    }

    /// <summary>Red arc around the crosshair pointing toward whoever shot the player.</summary>
    public void ShowDamageDirection(Vector3 source)
    {
        var cam = Camera.main;
        if (cam == null)
            return;
        Vector3 to = source - cam.transform.position;
        to.y = 0f;
        Vector3 fwd = cam.transform.forward;
        fwd.y = 0f;
        if (to.sqrMagnitude < 0.01f || fwd.sqrMagnitude < 0.01f)
            return;
        float angle = Vector3.SignedAngle(fwd, to, Vector3.up);   // + = to the right
        var arrow = damageArrows[nextArrow];
        damageArrowTimes[nextArrow] = Time.time;
        nextArrow = (nextArrow + 1) % damageArrows.Count;
        float rad = angle * Mathf.Deg2Rad;
        arrow.rectTransform.anchoredPosition = new Vector2(Mathf.Sin(rad), Mathf.Cos(rad)) * 170f;
        arrow.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -angle);
    }

    // ----- Screens -----

    public void ShowLobby()
    {
        HideAll();
        var p = GameManager.Instance.profile;
        lobbyNameText.text = p.playerName;
        lobbyLevelText.text = p.RankName.ToUpper() + "  •  SV " + p.level + (p.IsMaxLevel ? "" : "   •   " + p.xp + " / " + p.XpForNextLevel + " XP");
        lobbyXpFill.sizeDelta = new Vector2(360f * (p.IsMaxLevel ? 1f : Mathf.Clamp01((float)p.xp / Mathf.Max(1, p.XpForNextLevel))), 8f);
        Icons.Set(lobbyRankIcon, Icons.Rank(p.RankIndex));
        var cls = ClassDefs.Get(ClassDefs.Selected);
        Icons.Set(lobbyClassIcon, cls.icon);
        lobbyClassText.text = "Sınıf: " + cls.name + "  •  jeton ve kamuflaj";
        lobbyCoinsText.text = p.coins.ToString("N0");
        float winRate = p.matches > 0 ? 100f * p.wins / p.matches : 0f;
        lobbyStatsText.text = "Maç " + p.matches + "    Zafer " + p.wins + "    %" + Mathf.RoundToInt(winRate) + "\nToplam öldürme " + p.totalKills;
        var rifle = Gunsmith.Apply(WeaponData.CreateRifle());
        int count = 0;
        foreach (var a in rifle.attachments)
            if (!string.IsNullOrEmpty(a))
                count++;
        lobbyGunText.text = "Aparat ve kamuflaj  •  " + rifle.weaponName + " (" + count + "/5)";
        int skin = System.Array.IndexOf(ModelLibrary.ShopSkins, p.equippedSkin);
        lobbySkinText.text = "Kuşanılan: " + (skin >= 0 ? ModelLibrary.ShopNames[skin] : p.equippedSkin);
        lobbyPanel.SetActive(true);
    }

    public void ShowMatchmaking(string message)
    {
        HideAll();
        matchmakingText.text = message;
        matchmakingPanel.SetActive(true);
    }

    public void ShowBattleHud()
    {
        HideAll();
        killFeed.Clear();
        killFeedText.text = "";
        toastText.text = "";
        damageAlpha = 0f;
        touchControls.ResetState();
        if (crossH != null)
        {
            crossH.color = GameSettings.CrosshairColors[GameSettings.CrosshairColor];
            crossV.color = crossH.color;
        }
        hudPanel.SetActive(true);
    }

    public void ShowResult(bool won, int place, int teams, int kills, int xp, int coins)
    {
        HideAll();
        resultTitle.text = won ? "ZAFER! #1" : "#" + place + " / " + teams;
        resultTitle.color = won ? new Color(1f, 0.85f, 0.3f) : Color.white;
        resultDetails.text = (won ? "Ayakta kalan son kişi sensin!" : "Elendin. Bir dahaki sefere!") +
                             "\nÖldürme: " + kills + "     +" + xp + " XP     +" + coins + " Kredi";

        // Levels gained and their rewards.
        for (int i = resultRewards.childCount - 1; i >= 0; i--)
            Destroy(resultRewards.GetChild(i).gameObject);
        var p = GameManager.Instance.profile;
        var got = p.lastRewards;
        if (got.Count == 0)
        {
            resultLevel.text = p.RankName.ToUpper() + "  •  SV " + p.level + (p.IsMaxLevel ? "" : "  •  " + p.xp + " / " + p.XpForNextLevel + " XP");
            resultLevel.color = Theme.TextDim;
        }
        else
        {
            bool promoted = Progression.RankIndex(p.lastLevelBefore) != p.RankIndex;
            resultLevel.text = (promoted ? "RÜTBE YÜKSELDİ: " + p.RankName.ToUpper() : "SEVİYE ATLADIN") + "  •  SV " + p.level;
            resultLevel.color = Theme.Accent;
            int shown = Mathf.Min(got.Count, 6);
            float x0 = -(shown - 1) * 105f;
            for (int i = 0; i < shown; i++)
            {
                var g = got[got.Count - shown + i];
                var view = RewardView.Create(resultRewards, g.reward, new Vector2(0.5f, 0.5f), new Vector2(x0 + i * 210f, 30f), 140f);
                var label = UIUtil.CreateText(resultRewards, g.reward.Name + (g.duplicate ? "\n(yedeğe eklendi)" : ""), new Vector2(0.5f, 0.5f), new Vector2(x0 + i * 210f, -78f), new Vector2(200f, 60f), 20, TextAnchor.UpperCenter);
                label.horizontalOverflow = HorizontalWrapMode.Wrap;
                label.color = g.duplicate ? Theme.TextDim : Color.white;
                if (promoted && i == shown - 1)
                    Icons.Create(view, Icons.Rank(p.RankIndex), new Vector2(1f, 1f), new Vector2(-10f, -10f), new Vector2(56f, 56f));
            }
        }
        resultPanel.SetActive(true);
    }

    // ----- HUD feedback -----

    public void Toast(string message)
    {
        if (hudPanel == null || !hudPanel.activeSelf)
        {
            LobbyToast(message);
            return;
        }
        if (toastText == null)
            return;
        toastText.text = message;
        toastUntil = Time.time + 2.2f;
    }

    private Text lobbyToast;
    private Coroutine lobbyToastRoutine;

    /// <summary>Message shown over the menus (the battle HUD toast is hidden there).</summary>
    private void LobbyToast(string message)
    {
        if (lobbyToast == null)
        {
            var box = UIUtil.CreateImage(canvas.transform, "LobbyToast", new Vector2(0.5f, 1f), new Vector2(0f, -170f), new Vector2(1000f, 80f), new Color(0f, 0f, 0f, 0.75f), false);
            box.raycastTarget = false;
            lobbyToast = UIUtil.CreateText(box.transform, "", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(980f, 70f), 36, TextAnchor.MiddleCenter);
            lobbyToast.color = Theme.Accent;
            lobbyToast.fontStyle = FontStyle.Bold;
        }
        lobbyToast.text = message;
        var go = lobbyToast.transform.parent.gameObject;
        go.transform.SetAsLastSibling();
        go.SetActive(true);
        if (lobbyToastRoutine != null)
            StopCoroutine(lobbyToastRoutine);
        lobbyToastRoutine = StartCoroutine(HideLobbyToast(go));
    }

    private System.Collections.IEnumerator HideLobbyToast(GameObject go)
    {
        yield return new WaitForSeconds(3f);
        go.SetActive(false);
        lobbyToastRoutine = null;
    }

    public void AddKillFeed(string line)
    {
        killFeed.Add(line);
        while (killFeed.Count > 4)
            killFeed.RemoveAt(0);
        killFeedText.text = string.Join("\n", killFeed.ToArray());
        killFeedClearTime = Time.time + 6f;
    }

    public void FlashDamage()
    {
        damageAlpha = 0.35f;
        Haptics.Tap(25);
    }

    /// <summary>Hit marker on the crosshair plus a floating damage number at the hit point.</summary>
    public void ShowHit(Vector3 worldPoint, float damage, bool killed, bool headshot)
    {
        Color markColor = killed ? new Color(1f, 0.25f, 0.2f) : Color.white;
        foreach (var m in hitMarks)
        {
            m.color = markColor;
            m.enabled = true;
        }
        hitMarkUntil = Time.time + (killed ? 0.35f : 0.15f);
        Haptics.Tap(killed ? 45 : 12);
        if (!GameSettings.DamageNumbers)
            return;

        var cam = Camera.main;
        if (cam == null)
            return;
        Vector3 sp = cam.WorldToScreenPoint(worldPoint + Vector3.up * 0.5f);
        if (sp.z <= 0f)
            return;
        Vector2 local;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)hudPanel.transform, sp, null, out local))
            return;

        var p = popups[nextPopup];
        nextPopup = (nextPopup + 1) % popups.Count;
        p.text.text = Mathf.RoundToInt(damage).ToString();
        p.text.color = headshot ? new Color(1f, 0.85f, 0.2f) : (killed ? new Color(1f, 0.35f, 0.3f) : Color.white);
        p.text.fontSize = headshot || killed ? 40 : 32;
        p.start = local + new Vector2(Random.Range(-20f, 20f), 0f);
        p.born = Time.time;
        p.text.enabled = true;
        p.text.rectTransform.anchoredPosition = p.start;
    }

    private void Update()
    {
        if (hudPanel == null || !hudPanel.activeSelf)
            return;

        var gm = GameManager.Instance;
        var player = gm != null ? gm.player : null;
        if (player == null)
            return;

        healthFill.sizeDelta = new Vector2(BarWidth * Mathf.Clamp01(player.health / player.maxHealth), healthFill.sizeDelta.y);
        if (crossH != null)
        {
            Color cc = GameSettings.CrosshairColors[GameSettings.CrosshairColor];
            if (crossH.color != cc)
            {
                crossH.color = cc;
                crossV.color = cc;
            }
        }
        bool scoped = player.IsScoped && !player.isDead;
        if (scopeOverlay.activeSelf != scoped)
            scopeOverlay.SetActive(scoped);
        armorFill.sizeDelta = new Vector2(BarWidth * Mathf.Clamp01(player.armor / player.maxArmor), armorFill.sizeDelta.y);
        boostFill.sizeDelta = new Vector2(BarWidth * Mathf.Clamp01(player.BoostRemaining / 60f), boostFill.sizeDelta.y);
        healthText.text = Mathf.CeilToInt(player.health).ToString();

        bool onFoot = player.state == PlayerState.Ground;
        var w = player.currentWeapon;
        if (onFoot && w != null && w.weaponData != null)
        {
            weaponText.text = w.weaponData.weaponName;
            ammoText.text = w.isReloading ? "Dolduruluyor..." : w.currentAmmo + " / " + w.reserveAmmo;
        }
        else
        {
            weaponText.text = player.state == PlayerState.Driving ? "Cip" : "";
            ammoText.text = "";
        }

        if (player.IsAirborne)
            altitudeText.text = (player.state == PlayerState.Plane ? "Uçakta  " : "") + "Yükseklik " + Mathf.Max(0, Mathf.RoundToInt(player.HeightAboveGround)) + " m";
        else if (player.state == PlayerState.Driving && player.vehicle != null)
            altitudeText.text = Mathf.RoundToInt(Mathf.Abs(player.vehicle.speed) * 3.6f) + " km/s";
        else
            altitudeText.text = "";

        aliveText.text = "Kalan: " + gm.AliveCount();
        killsText.text = "Öldürme: " + player.kills;
        zoneText.text = gm.safeZone != null ? gm.safeZone.StatusText : "";
        zoneWarningText.enabled = gm.safeZone != null && gm.safeZone.active && !player.IsAirborne && gm.safeZone.IsOutside(player.transform.position);

        bool vehicleNearby = onFoot && gm.NearestVehicle(player.transform.position, 4.5f) != null;
        touchControls.UpdateContext(player, vehicleNearby);

        UpdateMinimap(gm, player);
        UpdateAbilityHud(gm, player);

        if (toastText.text.Length > 0 && Time.time > toastUntil)
            toastText.text = "";

        if (killFeed.Count > 0 && Time.time > killFeedClearTime)
        {
            killFeed.Clear();
            killFeedText.text = "";
        }

        if (damageAlpha > 0f)
        {
            damageAlpha = Mathf.Max(0f, damageAlpha - Time.deltaTime * 1.2f);
            damageFlash.color = new Color(0.9f, 0f, 0f, damageAlpha);
        }

        downedGroup.SetActive(player.isDowned);
        if (player.isDowned)
        {
            reviveFill.sizeDelta = new Vector2(BarWidth * Mathf.Clamp01(player.reviveProgress / PlayerController.ReviveTime), 18f);
            downedText.text = player.reviveProgress > 0f ? "KALDIRILIYORSUN..." : "YERE DÜŞTÜN - takım arkadaşın geliyor";
        }

        for (int i = 0; i < damageArrows.Count; i++)
        {
            float age = Time.time - damageArrowTimes[i];
            float a = age < 1f ? 0.85f * (1f - age) : 0f;
            damageArrows[i].color = new Color(1f, 0.15f, 0.1f, a);
        }

        if (hitMarks[0].enabled && Time.time > hitMarkUntil)
            foreach (var m in hitMarks)
                m.enabled = false;

        foreach (var p in popups)
        {
            if (!p.text.enabled)
                continue;
            float age = Time.time - p.born;
            if (age > 0.8f)
            {
                p.text.enabled = false;
                continue;
            }
            p.text.rectTransform.anchoredPosition = p.start + new Vector2(0f, age * 90f);
            Color col = p.text.color;
            col.a = 1f - age / 0.8f;
            p.text.color = col;
        }
    }

    private void UpdateMinimap(GameManager gm, PlayerController player)
    {
        // Whole map from the plane / in the air, zoomed around the player on the ground (~270 m across).
        bool air = player.state == PlayerState.Plane || player.state == PlayerState.Freefall || player.state == PlayerState.Parachute;
        float wantZoom = air ? 1f : World.MapSize / 270f;
        mapZoom = Mathf.Lerp(mapZoom, wantZoom, Time.deltaTime * 3f);
        if (Mathf.Abs(mapZoom - wantZoom) < 0.01f)
            mapZoom = wantZoom;
        float halfView = 0.5f / mapZoom;
        Vector2 puv = World.ToMapUV(player.transform.position);
        mapCenterUV = new Vector2(Mathf.Clamp(puv.x, halfView, 1f - halfView), Mathf.Clamp(puv.y, halfView, 1f - halfView));
        if (minimapRaw != null)
            minimapRaw.uvRect = new Rect(mapCenterUV.x - halfView, mapCenterUV.y - halfView, halfView * 2f, halfView * 2f);

        playerMarker.anchoredPosition = MapPos(player.transform.position);
        float yaw = player.state == PlayerState.Driving && player.vehicle != null ? player.vehicle.Yaw : player.transform.eulerAngles.y;
        playerMarker.localRotation = Quaternion.Euler(0f, 0f, -yaw);

        var zone = gm.safeZone;
        bool zoneOn = zone != null && zone.active;
        zoneRing.gameObject.SetActive(zoneOn);
        if (zoneOn)
        {
            zoneRing.anchoredPosition = MapPos(zone.center);
            float size = zone.radius * 2f / World.MapSize * MapPx * mapZoom;
            zoneRing.sizeDelta = new Vector2(size, size);
        }
        bool nextOn = zoneOn && zone.HasNext;
        nextZoneRing.gameObject.SetActive(nextOn);
        if (nextOn)
        {
            nextZoneRing.anchoredPosition = MapPos(zone.NextCenter);
            float size = zone.NextRadius * 2f / World.MapSize * MapPx * mapZoom;
            nextZoneRing.sizeDelta = new Vector2(size, size);
        }

        bool showPlane = gm.plane != null && (player.state == PlayerState.Plane || player.state == PlayerState.Freefall);
        planeLine.gameObject.SetActive(showPlane);
        if (showPlane)
        {
            Vector2 a = MapPos(gm.plane.start);
            Vector2 b = MapPos(gm.plane.end);
            planeLine.anchoredPosition = (a + b) * 0.5f;
            planeLine.sizeDelta = new Vector2(Vector2.Distance(a, b), 3f);
            planeLine.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg);
        }

        int dot = 0;
        foreach (var bot in gm.bots)
        {
            if (dot >= allyDots.Count)
                break;
            if (bot == null || bot.team != 0 || bot.isDead)
                continue;
            allyDots[dot].gameObject.SetActive(true);
            allyDots[dot].anchoredPosition = MapPos(bot.transform.position);
            dot++;
        }
        for (; dot < allyDots.Count; dot++)
            allyDots[dot].gameObject.SetActive(false);

        // Upgrade stations the player has not used yet.
        for (int i = 0; i < stationDots.Count; i++)
        {
            var st = i < UpgradeStation.All.Count ? UpgradeStation.All[i] : null;
            bool show = st != null && !st.UsedBy(player);
            if (stationDots[i].enabled != show)
                stationDots[i].enabled = show;
            if (show)
                stationDots[i].rectTransform.anchoredPosition = MapPos(st.transform.position);
        }

        // Enemies marked by the team's abilities.
        var marked = Marks.MarkedFor(0);
        for (int i = 0; i < markDots.Count; i++)
        {
            bool show = i < marked.Count;
            if (markDots[i].gameObject.activeSelf != show)
                markDots[i].gameObject.SetActive(show);
            if (show)
                markDots[i].anchoredPosition = MapPos(marked[i].transform.position);
        }
    }

    /// <summary>Markers over marked enemies, K9 footstep arrows and the upgrade-station progress.</summary>
    private void UpdateAbilityHud(GameManager gm, PlayerController player)
    {
        var cam = Camera.main;
        var hudRect = (RectTransform)hudPanel.transform;
        var marked = Marks.MarkedFor(0);
        for (int i = 0; i < markedIcons.Count; i++)
        {
            bool show = false;
            if (cam != null && i < marked.Count)
            {
                Vector3 sp = cam.WorldToScreenPoint(marked[i].AimPoint + Vector3.up * 0.9f);
                Vector2 local;
                if (sp.z > 0f && RectTransformUtility.ScreenPointToLocalPointInRectangle(hudRect, sp, null, out local))
                {
                    markedIcons[i].rectTransform.anchoredPosition = local;
                    float pulse = 1f + 0.15f * Mathf.Sin(Time.time * 8f);
                    markedIcons[i].rectTransform.localScale = Vector3.one * pulse;
                    show = true;
                }
            }
            if (markedIcons[i].enabled != show)
                markedIcons[i].enabled = show;
        }

        // K9 Eğitmeni passive: arrows toward enemies moving within 25 m.
        int arrow = 0;
        bool k9 = player.Ability != null && player.Ability.cls == PlayerClass.K9 && player.state == PlayerState.Ground && cam != null;
        foreach (var bot in gm.bots)
        {
            if (bot == null)
                continue;
            Vector3 last;
            bool moved = botLastPos.TryGetValue(bot, out last) && (bot.transform.position - last).sqrMagnitude > 0.0004f;
            botLastPos[bot] = bot.transform.position;
            if (!k9 || arrow >= footArrows.Count || bot.isDead || bot.team == 0 || bot.IsAirborne || !moved || ClassAbility.IsSilent(bot) || ClassAbility.IsStealthed(bot))
                continue;
            Vector3 to = bot.transform.position - player.transform.position;
            if (to.sqrMagnitude > 25f * 25f)
                continue;
            to.y = 0f;
            Vector3 fwd = cam.transform.forward;
            fwd.y = 0f;
            float angle = Vector3.SignedAngle(fwd, to, Vector3.up);
            float rad = angle * Mathf.Deg2Rad;
            var a = footArrows[arrow++];
            a.rectTransform.anchoredPosition = new Vector2(Mathf.Sin(rad), Mathf.Cos(rad)) * 235f;
            a.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -angle);
            a.color = new Color(1f, 0.6f, 0.15f, 0.55f + 0.3f * (1f - to.magnitude / 25f));
        }
        for (; arrow < footArrows.Count; arrow++)
            if (footArrows[arrow].color.a > 0f)
                footArrows[arrow].color = new Color(1f, 0.6f, 0.15f, 0f);

        var st = UpgradeStation.PlayerCharging();
        upgradeText.text = st != null ? "GÜÇLENDİRİLİYOR  %" + Mathf.RoundToInt(Mathf.Clamp01(st.PlayerProgress) * 100f) : "";
    }
}
