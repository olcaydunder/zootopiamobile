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
    private MatchMode selectedMode = MatchMode.Solo;
    private Image lobbyModeIcon, lobbyModeAccent;
    private Text lobbyModeTitle, lobbyModeSub, lobbyWinsText, lobbyTrophyText, lobbyPowerText;
    private readonly List<Image> lobbyWinPips = new List<Image>();
    private GameObject wheelBadge;
    private ModeSelectScreen modeSelect;
    private ProfileScreen profileScreen;
    private WheelScreen wheelScreen;
    private GunsmithScreen gunsmith;
    private LuckyDrawScreen luckyDraw;

    // Matchmaking
    private Text matchmakingText, matchmakingCount;
    private Image matchmakingIcon;

    // HUD
    private TouchControls touchControls;
    private RectTransform healthFill;
    private RectTransform armorFill;
    private RectTransform boostFill;
    private Text healthText;
    private Text weaponText;
    private Text ammoText;
    private Text aliveText;
    private GameObject teamScoreBox;
    private Text teamScoreOurs, teamScoreTheirs, teamScoreTime, teamScoreGoal, teamCapOurs, teamCapTheirs;
    // Hakimiyet / Soygun HUD
    private GameObject pointsRow, captureBar;
    private readonly Image[] pointBadges = new Image[3];
    private readonly Image[] pointFills = new Image[3];
    private Image captureFill;
    private Text captureText, objectiveText;
    private readonly List<Text> objMarkers = new List<Text>();
    private Text killsText;
    private Text zoneText;
    private Text zoneWarningText;
    private Text altitudeText;
    private Text toastText;
    private float toastUntil;
    private Text killFeedText;
    private readonly List<string> killFeed = new List<string>();
    private float killFeedClearTime;
    private Image damageFlash, smokeVeil, flashVeil;
    private float flashStrength, flashFade, flashHold;
    private readonly List<RectTransform> nightDots = new List<RectTransform>();
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
    private Text cookText;
    private ScoreboardPanel scoreboard;
    private BigMapPanel bigMap;
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
    private readonly List<Image> airdropDots = new List<Image>();
    private readonly List<Image> vehicleDots = new List<Image>();
    private readonly int[] vehicleDotKind = { -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 };
    private int driveHudKey = int.MinValue;
    private readonly List<Vector3> supplyScratch = new List<Vector3>();
    private readonly List<Image> markedIcons = new List<Image>();
    private readonly List<Image> footArrows = new List<Image>();
    private readonly Dictionary<BotAgent, Vector3> botLastPos = new Dictionary<BotAgent, Vector3>();
    private Text upgradeText;
    private static readonly List<BotAgent> noBots = new List<BotAgent>();

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
        if (NetGame.IsServer)
            return;   // the game server has no screen
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
        if (luckyDraw != null) luckyDraw.Hide();
        if (matchPrep != null) matchPrep.Hide();
        if (career != null) career.Hide();
        if (loadout != null) loadout.Hide();
        if (netLobby != null) netLobby.Hide();
        if (social != null) social.Hide();
        if (mapSelect != null) mapSelect.gameObject.SetActive(false);
        if (store != null) store.Hide();
        if (missions != null) missions.Hide();
        if (inventory != null) inventory.Hide();
        if (modeSelect != null) modeSelect.Hide();
        if (profileScreen != null) profileScreen.Hide();
        if (wheelScreen != null) wheelScreen.Hide();
        if (inviteBanner != null) inviteBanner.SetActive(false);
        if (shopPanel != null) shopPanel.SetActive(false);
        if (pausePanel != null) pausePanel.SetActive(false);
        ClearVeils();
    }

    private void ClearVeils()
    {
        flashStrength = flashHold = flashFade = 0f;
        if (flashVeil != null)
            flashVeil.gameObject.SetActive(false);
        if (smokeVeil != null)
            smokeVeil.enabled = false;
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
        var rename = UIUtil.CreateButton(prof, "İSİM", new Vector2(1f, 0.5f), new Vector2(-52f, 20f), new Vector2(84f, 46f), Theme.PanelLight, false, 20, out unused);
        rename.onClick.AddListener(() => { HideAll(); netLobby.OpenRename(ShowLobby); });
        // The whole box opens the profile.
        var profButton = prof.gameObject.AddComponent<Button>();
        profButton.transition = Selectable.Transition.None;
        profButton.onClick.AddListener(OpenProfile);

        // GÜÇ under the profile
        var powerBox = Theme.Box(t, "Power", new Vector2(0f, 1f), new Vector2(650f, -150f), new Vector2(190f, 46f), Theme.Panel, false).transform;
        Icons.Create(powerBox, "power", new Vector2(0f, 0.5f), new Vector2(24f, 0f), new Vector2(38f, 38f)).raycastTarget = false;
        lobbyPowerText = UIUtil.CreateText(powerBox, "", new Vector2(0f, 0.5f), new Vector2(115f, 0f), new Vector2(150f, 40f), 22, TextAnchor.MiddleLeft);
        lobbyPowerText.color = new Color(0.85f, 0.6f, 1f);
        lobbyPowerText.fontStyle = FontStyle.Bold;
        // KUPA under it (opens the profile)
        var trophyBox = Theme.Box(t, "Trophies", new Vector2(0f, 1f), new Vector2(650f, -202f), new Vector2(190f, 46f), Theme.Panel, false).transform;
        Icons.Create(trophyBox, "trophy", new Vector2(0f, 0.5f), new Vector2(24f, 0f), new Vector2(38f, 38f)).raycastTarget = false;
        lobbyTrophyText = UIUtil.CreateText(trophyBox, "", new Vector2(0f, 0.5f), new Vector2(115f, 0f), new Vector2(150f, 40f), 22, TextAnchor.MiddleLeft);
        lobbyTrophyText.color = new Color(1f, 0.85f, 0.3f);
        lobbyTrophyText.fontStyle = FontStyle.Bold;
        var trophyBtn = trophyBox.gameObject.AddComponent<Button>();
        trophyBtn.transition = Selectable.Transition.None;
        trophyBtn.onClick.AddListener(OpenProfile);
        var powerBtn = powerBox.gameObject.AddComponent<Button>();
        powerBtn.transition = Selectable.Transition.None;
        powerBtn.onClick.AddListener(() => { HideAll(); inventory.Open(InventoryScreen.Tab.Armor, ShowLobby); });
        // Lucky wheel (round, next to the profile)
        var wheelBtn = UIUtil.CreateButton(t, "", new Vector2(0f, 1f), new Vector2(640f, -70f), new Vector2(104f, 104f), new Color(0.12f, 0.13f, 0.17f, 0.9f), true, 20, out unused);
        Icons.Create(wheelBtn.transform, "wheel", new Vector2(0.5f, 0.5f), new Vector2(0f, 6f), new Vector2(80f, 80f)).raycastTarget = false;
        var wl = UIUtil.CreateText(wheelBtn.transform, "ÇARK", new Vector2(0.5f, 0f), new Vector2(0f, -6f), new Vector2(110f, 26f), 18, TextAnchor.MiddleCenter);
        wl.fontStyle = FontStyle.Bold;
        wheelBtn.onClick.AddListener(() => { HideAll(); wheelScreen.Open(ShowLobby); });
        var wb = UIUtil.CreateImage(wheelBtn.transform, "Badge", new Vector2(1f, 1f), new Vector2(-10f, -10f), new Vector2(30f, 30f), Theme.Good, true);
        wb.raycastTarget = false;
        wheelBadge = wb.gameObject;

        // Coins + settings (top-right)
        var coins = Theme.Box(t, "Coins", new Vector2(1f, 1f), new Vector2(-355f, -70f), new Vector2(290f, 80f), Theme.Panel, false).transform;
        Icons.Create(coins, "currency", new Vector2(0f, 0.5f), new Vector2(44f, 0f), new Vector2(58f, 58f));
        lobbyCoinsText = UIUtil.CreateText(coins, "", new Vector2(0f, 0.5f), new Vector2(170f, 0f), new Vector2(200f, 60f), 34, TextAnchor.MiddleLeft);
        lobbyCoinsText.fontStyle = FontStyle.Bold;
        UIUtil.CreateButton(t, "AYARLAR", new Vector2(1f, 1f), new Vector2(-110f, -70f), new Vector2(180f, 80f), Theme.Panel, false, 26, out unused)
            .onClick.AddListener(() => OpenSettings(false));
        var friendsButton = UIUtil.CreateButton(t, "ARKADAŞLAR", new Vector2(1f, 1f), new Vector2(-615f, -70f), new Vector2(210f, 80f), Theme.Panel, false, 26, out unused);
        friendsButton.onClick.AddListener(() => { HideAll(); social.Open(SocialScreen.Tab.Friends, ShowLobby); });
        var fb = UIUtil.CreateImage(friendsButton.transform, "Badge", new Vector2(1f, 1f), new Vector2(-6f, -6f), new Vector2(40f, 40f), Theme.Red, true);
        fb.raycastTarget = false;
        friendsBadgeText = UIUtil.CreateText(fb.transform, "", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(40f, 40f), 22, TextAnchor.MiddleCenter);
        friendsBadge = fb.gameObject;
        friendsBadge.SetActive(false);
        var storeButton = UIUtil.CreateButton(t, "   MAĞAZA", new Vector2(1f, 1f), new Vector2(-840f, -70f), new Vector2(220f, 80f), new Color(0.55f, 0.35f, 0.08f, 0.95f), false, 26, out unused);
        Icons.Create(storeButton.transform, "store", new Vector2(0f, 0.5f), new Vector2(38f, 0f), new Vector2(60f, 60f));
        storeButton.onClick.AddListener(() => { HideAll(); store.Open(StoreScreen.Tab.Deals, ShowLobby); });
        var dealDot = UIUtil.CreateImage(storeButton.transform, "Badge", new Vector2(1f, 1f), new Vector2(-6f, -6f), new Vector2(30f, 30f), Theme.Good, true);
        dealDot.raycastTarget = false;
        storeBadge = dealDot.gameObject;
        var invButton = UIUtil.CreateButton(t, "     ENVANTER", new Vector2(1f, 1f), new Vector2(-1070f, -70f), new Vector2(220f, 80f), new Color(0.3f, 0.2f, 0.45f, 0.95f), false, 26, out unused);
        Icons.Create(invButton.transform, "inventory", new Vector2(0f, 0.5f), new Vector2(38f, 0f), new Vector2(60f, 60f));
        invButton.onClick.AddListener(() => { HideAll(); inventory.Open(InventoryScreen.Tab.Armor, ShowLobby); });

        // Missions (bottom-left, under the tiles), with how many are ready to collect.
        var missionsButton = UIUtil.CreateButton(t, "", new Vector2(0f, 0.5f), new Vector2(260f, -345f), new Vector2(440f, 88f), Theme.Panel, false, 20, out unused);
        missionsButton.onClick.AddListener(() => { HideAll(); missions.Open(ShowLobby); });
        UIUtil.CreateImage(missionsButton.transform, "Accent", new Vector2(0f, 0.5f), new Vector2(4f, 0f), new Vector2(8f, 88f), Theme.Good, false).raycastTarget = false;
        Icons.Create(missionsButton.transform, "missions", new Vector2(0f, 0.5f), new Vector2(58f, 0f), new Vector2(72f, 72f));
        var mTitle = UIUtil.CreateText(missionsButton.transform, "GÖREVLER", new Vector2(0f, 0.5f), new Vector2(230f, 12f), new Vector2(240f, 44f), 32, TextAnchor.MiddleLeft);
        mTitle.fontStyle = FontStyle.Bold;
        lobbyMissionsText = UIUtil.CreateText(missionsButton.transform, "", new Vector2(0f, 0.5f), new Vector2(230f, -22f), new Vector2(240f, 30f), 20, TextAnchor.MiddleLeft);
        lobbyMissionsText.color = Theme.TextDim;
        var mb = UIUtil.CreateImage(missionsButton.transform, "Badge", new Vector2(1f, 1f), new Vector2(-6f, -6f), new Vector2(40f, 40f), Theme.Red, true);
        mb.raycastTarget = false;
        missionsBadgeText = UIUtil.CreateText(mb.transform, "", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(40f, 40f), 22, TextAnchor.MiddleCenter);
        missionsBadge = mb.gameObject;

        // Room invite from a friend (shown over the lobby).
        var banner = Theme.Box(canvas.transform, "InviteBanner", new Vector2(0.5f, 1f), new Vector2(0f, -190f), new Vector2(860f, 120f), new Color(0.1f, 0.2f, 0.4f, 0.97f), true);
        inviteBanner = banner.gameObject;
        inviteText = UIUtil.CreateText(banner.transform, "", new Vector2(0f, 0.5f), new Vector2(245f, 0f), new Vector2(440f, 100f), 28, TextAnchor.MiddleLeft);
        inviteText.horizontalOverflow = HorizontalWrapMode.Wrap;
        Text bl;
        var joinInvite = UIUtil.CreateButton(banner.transform, "KATIL", new Vector2(1f, 0.5f), new Vector2(-300f, 0f), new Vector2(180f, 84f), Theme.Accent, false, 32, out bl);
        bl.color = new Color(0.1f, 0.08f, 0.02f);
        bl.GetComponent<Shadow>().enabled = false;
        joinInvite.onClick.AddListener(() => { inviteBanner.SetActive(false); JoinRoomByCode(inviteRoom); });
        UIUtil.CreateButton(banner.transform, "YOKSAY", new Vector2(1f, 0.5f), new Vector2(-105f, 0f), new Vector2(180f, 84f), Theme.PanelLight, false, 28, out bl)
            .onClick.AddListener(() => { inviteBanner.SetActive(false); OnlineService.DismissInvite(inviteFrom, null); });
        inviteBanner.SetActive(false);

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

        // Lucky draw: round button beside the gunsmith tile
        var drawBtn = UIUtil.CreateButton(t, "", new Vector2(0f, 0.5f), new Vector2(560f, 150f), new Vector2(112f, 112f), new Color(0.36f, 0.18f, 0.55f, 0.95f), true, 20, out unused);
        Icons.Create(drawBtn.transform, "crate_diamond", new Vector2(0.5f, 0.5f), new Vector2(0f, 10f), new Vector2(74f, 74f)).raycastTarget = false;
        var dl = UIUtil.CreateText(drawBtn.transform, "ÇEKİLİŞ", new Vector2(0.5f, 0f), new Vector2(0f, -8f), new Vector2(130f, 26f), 18, TextAnchor.MiddleCenter);
        dl.fontStyle = FontStyle.Bold;
        drawBtn.onClick.AddListener(() => OpenLuckyDraw(ShowLobby));

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

        // Right: the game mode (opens the mode screen) and today's victories.
        var modeTile = UIUtil.CreateButton(t, "", new Vector2(1f, 0.5f), new Vector2(-280f, 178f), new Vector2(480f, 140f), Theme.Panel, false, 20, out unused);
        modeTile.onClick.AddListener(() => { HideAll(); modeSelect.Open(selectedMode, m => { SelectMode(m); ShowLobby(); }, ShowLobby); });
        var mtt0 = modeTile.transform;
        lobbyModeAccent = UIUtil.CreateImage(mtt0, "Accent", new Vector2(0f, 0.5f), new Vector2(4f, 0f), new Vector2(8f, 140f), Theme.Accent, false);
        lobbyModeAccent.raycastTarget = false;
        lobbyModeIcon = Icons.Create(mtt0, "mode_br", new Vector2(0f, 0.5f), new Vector2(76f, 0f), new Vector2(108f, 108f));
        lobbyModeIcon.raycastTarget = false;
        var modeCaption = UIUtil.CreateText(mtt0, "OYUN MODU", new Vector2(0f, 0.5f), new Vector2(285f, 44f), new Vector2(300f, 26f), 18, TextAnchor.MiddleLeft);
        modeCaption.color = Theme.TextDim;
        lobbyModeTitle = UIUtil.CreateText(mtt0, "", new Vector2(0f, 0.5f), new Vector2(285f, 10f), new Vector2(300f, 46f), 32, TextAnchor.MiddleLeft);
        lobbyModeTitle.fontStyle = FontStyle.Bold;
        lobbyModeSub = UIUtil.CreateText(mtt0, "", new Vector2(0f, 0.5f), new Vector2(285f, -36f), new Vector2(300f, 46f), 17, TextAnchor.UpperLeft);
        lobbyModeSub.horizontalOverflow = HorizontalWrapMode.Wrap;
        lobbyModeSub.color = Theme.TextDim;
        var chg = UIUtil.CreateText(mtt0, "DEĞİŞTİR ›", new Vector2(1f, 1f), new Vector2(-70f, -22f), new Vector2(130f, 26f), 18, TextAnchor.MiddleRight);
        chg.color = Theme.Accent;

        var winsTile = UIUtil.CreateButton(t, "", new Vector2(1f, 0.5f), new Vector2(-280f, 48f), new Vector2(480f, 100f), Theme.Panel, false, 20, out unused);
        winsTile.onClick.AddListener(() => { HideAll(); missions.Open(MissionsScreen.Page.Wins, ShowLobby); });
        var wtt = winsTile.transform;
        var wcap = UIUtil.CreateText(wtt, "GÜNLÜK ZAFERLER", new Vector2(0f, 1f), new Vector2(150f, -22f), new Vector2(260f, 30f), 20, TextAnchor.MiddleLeft);
        wcap.fontStyle = FontStyle.Bold;
        lobbyWinsText = UIUtil.CreateText(wtt, "", new Vector2(1f, 1f), new Vector2(-90f, -22f), new Vector2(160f, 30f), 20, TextAnchor.MiddleRight);
        lobbyWinsText.color = Theme.TextDim;
        for (int i = 0; i < Missions.WinBoxes; i++)
        {
            var pip = Icons.Create(wtt, "crate_" + Missions.WinRewards[i].id, new Vector2(0f, 0f), new Vector2(50f + i * 76f, 34f), new Vector2(56f, 56f));
            pip.raycastTarget = false;
            lobbyWinPips.Add(pip);
        }
        // Right, above the modes: the map (picture, name) — opens the map selection.
        var info = MapCatalog.CurrentInfo;
        var mapTile = UIUtil.CreateButton(t, "", new Vector2(1f, 0.5f), new Vector2(-280f, 300f), new Vector2(480f, 84f), Theme.Panel, false, 20, out unused);
        mapTile.onClick.AddListener(() => { HideAll(); mapSelect.Open(ShowLobby); });
        var mtt = mapTile.transform;
        var thumb = UIUtil.CreateRect(mtt, "Thumb", new Vector2(0f, 0.5f), new Vector2(46f, 0f), new Vector2(76f, 76f));
        var thumbImg = thumb.gameObject.AddComponent<RawImage>();
        thumbImg.texture = MapCatalog.Preview(info.id);
        thumbImg.raycastTarget = false;
        var mapCaption = UIUtil.CreateText(mtt, "HARİTA  •  BATTLE ROYALE  •  25 OYUNCU", new Vector2(0f, 0.5f), new Vector2(286f, 20f), new Vector2(380f, 30f), 18, TextAnchor.MiddleLeft);
        mapCaption.color = Theme.TextDim;
        var mapName = UIUtil.CreateText(mtt, info.name, new Vector2(0f, 0.5f), new Vector2(286f, -14f), new Vector2(380f, 40f), 30, TextAnchor.MiddleLeft);
        mapName.fontStyle = FontStyle.Bold;
        mapName.color = Theme.Accent;
        var change = UIUtil.CreateText(mtt, "DEĞİŞTİR ›", new Vector2(1f, 0.5f), new Vector2(-70f, -14f), new Vector2(130f, 30f), 20, TextAnchor.MiddleRight);
        change.color = Color.white;

        var startButton = UIUtil.CreateButton(t, "BAŞLAT", new Vector2(1f, 0f), new Vector2(-280f, 110f), new Vector2(480f, 130f), Theme.Accent, false, 54, out lobbyStartLabel);
        startButton.onClick.AddListener(() => { HideAll(); matchPrep.Open(selectedMode); });
        lobbyStartLabel.color = new Color(0.1f, 0.08f, 0.02f);
        lobbyStartLabel.GetComponent<Shadow>().enabled = false;
        lobbyStartLabel.rectTransform.anchoredPosition = new Vector2(0f, 10f);
        var offline = UIUtil.CreateText(startButton.transform, "çevrimdışı  •  botlarla", new Vector2(0.5f, 0f), new Vector2(0f, 22f), new Vector2(460f, 30f), 20, TextAnchor.MiddleCenter);
        offline.color = new Color(0.1f, 0.08f, 0.02f, 0.75f);
        offline.GetComponent<Shadow>().enabled = false;

        // Online: quick match on the game server, or a private room with a code.
        Color onlineBlue = new Color(0.16f, 0.45f, 0.95f, 0.95f);
        Text onlineLabel;
        var quick = UIUtil.CreateButton(t, "ÇEVRİMİÇİ  •  HIZLI MAÇ", new Vector2(1f, 0.5f), new Vector2(-280f, -165f), new Vector2(480f, 92f), onlineBlue, false, 32, out onlineLabel);
        quick.onClick.AddListener(() =>
        {
            if (!Modes.Online(selectedMode))
            {
                LobbyToast(Modes.Short(selectedMode) + " şimdilik yalnız botlarla: BAŞLAT'a bas");
                return;
            }
            HideAll();
            netLobby.OpenQuick(selectedMode, ShowLobby);
        });
        var create = UIUtil.CreateButton(t, "ODA KUR", new Vector2(1f, 0.5f), new Vector2(-404f, -267f), new Vector2(232f, 84f), Theme.Panel, false, 28, out onlineLabel);
        create.onClick.AddListener(() =>
        {
            if (!Modes.Online(selectedMode))
            {
                LobbyToast(Modes.Short(selectedMode) + " şimdilik yalnız botlarla: BAŞLAT'a bas");
                return;
            }
            HideAll();
            netLobby.OpenCreate(selectedMode, ShowLobby);
        });
        UIUtil.CreateImage(create.transform, "Accent", new Vector2(0.5f, 0f), new Vector2(0f, 3f), new Vector2(232f, 6f), onlineBlue, false).raycastTarget = false;
        var join = UIUtil.CreateButton(t, "ODAYA KATIL", new Vector2(1f, 0.5f), new Vector2(-156f, -267f), new Vector2(232f, 84f), Theme.Panel, false, 28, out onlineLabel);
        join.onClick.AddListener(() => { HideAll(); netLobby.OpenJoin(ShowLobby); });
        UIUtil.CreateImage(join.transform, "Accent", new Vector2(0.5f, 0f), new Vector2(0f, 3f), new Vector2(232f, 6f), onlineBlue, false).raycastTarget = false;

        // Bottom-left: title + credit
        var title = UIUtil.CreateText(t, GameTitle, new Vector2(0f, 0f), new Vector2(330f, 120f), new Vector2(600f, 60f), 46, TextAnchor.MiddleLeft);
        title.fontStyle = FontStyle.Bold;
        title.color = Theme.Accent;
        var credit = UIUtil.CreateText(t, ProducerCredit, new Vector2(0f, 0f), new Vector2(330f, 75f), new Vector2(600f, 40f), 24, TextAnchor.MiddleLeft);
        credit.color = Theme.TextDim;
        if (MapData.Loaded)
        {
            var osm = UIUtil.CreateText(t, "Harita: " + info.name + ", " + info.place + "  •  © OpenStreetMap katkıcıları", new Vector2(0f, 0f), new Vector2(330f, 40f), new Vector2(600f, 30f), 18, TextAnchor.MiddleLeft);
            osm.color = Theme.TextDim;
        }

        gunsmith = GunsmithScreen.Create(canvas.transform);
        matchPrep = MatchPrepScreen.Create(canvas.transform);
        career = CareerScreen.Create(canvas.transform);
        loadout = LoadoutScreen.Create(canvas.transform);
        netLobby = NetLobbyScreen.Create(canvas.transform);
        social = SocialScreen.Create(canvas.transform);
        mapSelect = MapSelectScreen.Create(canvas.transform);
        store = StoreScreen.Create(canvas.transform);
        missions = MissionsScreen.Create(canvas.transform);
        inventory = InventoryScreen.Create(canvas.transform);
        modeSelect = ModeSelectScreen.Create(canvas.transform);
        profileScreen = ProfileScreen.Create(canvas.transform);
        wheelScreen = WheelScreen.Create(canvas.transform);
        luckyDraw = LuckyDrawScreen.Create(canvas.transform);
        matchPrep.gameObject.AddComponent<PopIn>();
        gunsmith.gameObject.AddComponent<PopIn>();
        SelectMode((MatchMode)Mathf.Clamp(PlayerPrefs.GetInt("zm_mode", 0), 0, (int)MatchMode.Heist));
    }

    private void SelectMode(MatchMode m)
    {
        selectedMode = m;
        PlayerPrefs.SetInt("zm_mode", (int)m);
        Color mc = Modes.Color(m);
        lobbyModeAccent.color = mc;
        Icons.Set(lobbyModeIcon, Modes.Icon(m));
        lobbyModeTitle.text = Modes.Arena(m) ? Modes.Title(m) : "BATTLE ROYALE  •  " + Modes.Short(m);
        lobbyModeTitle.color = Color.Lerp(mc, Color.white, 0.45f);
        lobbyModeSub.text = Modes.Description(m);
        if (lobbyStartLabel != null)
            lobbyStartLabel.text = "BAŞLAT  •  " + Modes.Short(m);
    }

    public static string ModeLabel(MatchMode m)
    {
        return Modes.Short(m);
    }

    private void OpenProfile()
    {
        HideAll();
        profileScreen.Open(ShowLobby, () => netLobby.OpenRename(ShowLobby), () => inventory.Open(InventoryScreen.Tab.Armor, ShowLobby));
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

    /// <summary>ŞANS ÇEKİLİŞİ (from the lobby or the gunsmith); <paramref name="back"/> runs when it closes.</summary>
    public void OpenLuckyDraw(System.Action back)
    {
        HideAll();
        luckyDraw.Open(back ?? ShowLobby);
    }

    private void OpenGunsmith()
    {
        lobbyPanel.SetActive(false);
        gunsmith.Open();
    }

    private void BuildMatchmaking()
    {
        // Light veil (the character on the stage shows through), the mode box on the left, today's best on the right.
        matchmakingPanel = CreateFullPanel("MatchmakingPanel", new Color(0.03f, 0.06f, 0.1f, 0.3f));
        var mt = matchmakingPanel.transform;
        var box = UIUtil.CreateImage(mt, "ModeBox", new Vector2(0f, 1f), new Vector2(400f, -170f), new Vector2(660f, 170f), new Color(0.13f, 0.33f, 0.82f, 0.95f), false);
        box.raycastTarget = false;
        var edge = UIUtil.CreateImage(box.transform, "Edge", new Vector2(0.5f, 0f), new Vector2(0f, 4f), new Vector2(660f, 8f), new Color(0.07f, 0.18f, 0.5f, 1f), false);
        edge.raycastTarget = false;
        matchmakingIcon = Icons.Create(box.transform, "mode_br", new Vector2(0f, 0.5f), new Vector2(90f, 6f), new Vector2(120f, 120f));
        matchmakingIcon.raycastTarget = false;
        matchmakingText = UIUtil.CreateText(box.transform, "", new Vector2(0f, 0.5f), new Vector2(410f, 30f), new Vector2(460f, 60f), 32, TextAnchor.MiddleCenter);
        matchmakingText.fontStyle = FontStyle.Bold;
        matchmakingCount = UIUtil.CreateText(box.transform, "", new Vector2(0f, 0.5f), new Vector2(410f, -30f), new Vector2(440f, 60f), 46, TextAnchor.MiddleCenter);
        matchmakingCount.fontStyle = FontStyle.Bold;
        TopPlayersPanel.Create(mt, new Vector2(-270f, -20f));
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
        // Inside a smoke cloud the view turns grey.
        smokeVeil = UIUtil.CreateStretch(t, "SmokeVeil").gameObject.AddComponent<Image>();
        smokeVeil.color = new Color(0.72f, 0.74f, 0.77f, 0f);
        smokeVeil.raycastTarget = false;
        smokeVeil.enabled = false;

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
        crosshair = Crosshair.Create(t);
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
        // 5v5 score: your team (blue) • time • the other team (red)
        teamScoreBox = UIUtil.CreateImage(t, "TeamScore", top, new Vector2(0f, -48f), new Vector2(520f, 76f), new Color(0f, 0f, 0f, 0.55f), false).gameObject;
        teamScoreBox.GetComponent<Image>().raycastTarget = false;
        var ours = UIUtil.CreateImage(teamScoreBox.transform, "Ours", new Vector2(0f, 0.5f), new Vector2(80f, 0f), new Vector2(150f, 66f), new Color(0.2f, 0.45f, 0.95f, 0.9f), false);
        ours.raycastTarget = false;
        teamScoreOurs = UIUtil.CreateText(ours.transform, "0", c, Vector2.zero, new Vector2(150f, 66f), 46, TextAnchor.MiddleCenter);
        teamScoreOurs.fontStyle = FontStyle.Bold;
        var theirs = UIUtil.CreateImage(teamScoreBox.transform, "Theirs", new Vector2(1f, 0.5f), new Vector2(-80f, 0f), new Vector2(150f, 66f), new Color(0.9f, 0.25f, 0.2f, 0.9f), false);
        theirs.raycastTarget = false;
        teamScoreTheirs = UIUtil.CreateText(theirs.transform, "0", c, Vector2.zero, new Vector2(150f, 66f), 46, TextAnchor.MiddleCenter);
        teamScoreTheirs.fontStyle = FontStyle.Bold;
        teamScoreTime = UIUtil.CreateText(teamScoreBox.transform, "", c, new Vector2(0f, 10f), new Vector2(200f, 40f), 32, TextAnchor.MiddleCenter);
        teamScoreTime.fontStyle = FontStyle.Bold;
        var goal = UIUtil.CreateText(teamScoreBox.transform, "hedef " + TeamMatch.ScoreToWin, c, new Vector2(0f, -20f), new Vector2(200f, 26f), 18, TextAnchor.MiddleCenter);
        teamScoreGoal = goal;
        teamCapOurs = UIUtil.CreateText(teamScoreBox.transform, "TAKIMIN", new Vector2(0f, 0f), new Vector2(80f, -14f), new Vector2(150f, 24f), 16, TextAnchor.MiddleCenter);
        teamCapTheirs = UIUtil.CreateText(teamScoreBox.transform, "DÜŞMAN", new Vector2(1f, 0f), new Vector2(-80f, -14f), new Vector2(150f, 24f), 16, TextAnchor.MiddleCenter);
        BuildModeHud(t);
        goal.color = Theme.TextDim;
        teamScoreBox.SetActive(false);

        zoneWarningText = UIUtil.CreateText(t, "BÖLGENİN DIŞINDASIN!", top, new Vector2(0f, -145f), new Vector2(800f, 50f), 34, TextAnchor.MiddleCenter);
        zoneWarningText.color = new Color(1f, 0.35f, 0.3f);
        zoneWarningText.fontStyle = FontStyle.Bold;

        // Cooking a grenade: what it is and the seconds left on its fuse, under the crosshair.
        cookText = UIUtil.CreateText(t, "", c, new Vector2(0f, -110f), new Vector2(600f, 70f), 44, TextAnchor.MiddleCenter);
        cookText.fontStyle = FontStyle.Bold;
        cookText.supportRichText = true;
        toastText = UIUtil.CreateText(t, "", c, new Vector2(0f, 230f), new Vector2(1000f, 60f), 38, TextAnchor.MiddleCenter);
        toastText.fontStyle = FontStyle.Bold;

        altitudeText = UIUtil.CreateText(t, "", new Vector2(0f, 0.5f), new Vector2(200f, 120f), new Vector2(360f, 50f), 34, TextAnchor.MiddleLeft);
        altitudeText.fontStyle = FontStyle.Bold;

        BuildMinimap(t);
        killFeedText = UIUtil.CreateText(t, "", new Vector2(1f, 1f), new Vector2(-230f, -440f), new Vector2(420f, 160f), 26, TextAnchor.UpperRight);

        // Tap the score / players-left at the top: the scoreboard. Tap the minimap: the big map.
        scoreboard = ScoreboardPanel.Create(t);
        bigMap = BigMapPanel.Create(t);
        var scoreTap = UIUtil.CreateImage(t, "ScoreTap", top, new Vector2(0f, -55f), new Vector2(580f, 110f), new Color(0f, 0f, 0f, 0.001f), false);
        var st = scoreTap.gameObject.AddComponent<Button>();
        st.transition = Selectable.Transition.None;
        st.onClick.AddListener(() => scoreboard.Toggle());

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

        // Voice chat (online, with teammates): microphone and speaker toggles + who is talking.
        var mic = UIUtil.CreateButton(t, "MİK", new Vector2(0f, 1f), new Vector2(185f, -60f), new Vector2(120f, 80f), new Color(0f, 0f, 0f, 0.45f), false, 24, out micLabel);
        mic.onClick.AddListener(VoiceChat.ToggleMic);
        micButton = mic.gameObject;
        var spk = UIUtil.CreateButton(t, "SES", new Vector2(0f, 1f), new Vector2(315f, -60f), new Vector2(120f, 80f), new Color(0f, 0f, 0f, 0.45f), false, 24, out speakerLabel);
        spk.onClick.AddListener(() => VoiceChat.SpeakerOn = !VoiceChat.SpeakerOn);
        speakerButton = spk.gameObject;
        talkingText = UIUtil.CreateText(t, "", new Vector2(0f, 1f), new Vector2(620f, -60f), new Vector2(460f, 40f), 24, TextAnchor.MiddleLeft);
        talkingText.color = new Color(0.5f, 1f, 0.6f);
        micButton.SetActive(false);
        speakerButton.SetActive(false);

        // Flash grenade: everything (the touch buttons too) goes white for a moment.
        flashVeil = UIUtil.CreateStretch(canvas.transform, "FlashVeil").gameObject.AddComponent<Image>();
        flashVeil.color = new Color(1f, 1f, 0.97f, 0f);
        flashVeil.raycastTarget = false;
        flashVeil.gameObject.SetActive(false);
    }

    /// <summary>Blinded by a flash grenade: strength 0..1, for about this many seconds.</summary>
    public void Flashbang(float strength, float seconds)
    {
        flashStrength = Mathf.Max(flashStrength, Mathf.Clamp01(strength));
        flashHold = Mathf.Max(flashHold, seconds * 0.45f);
        flashFade = Mathf.Max(flashFade, seconds * 0.55f);
        flashVeil.gameObject.SetActive(true);
        flashVeil.transform.SetAsLastSibling();
        Haptics.Tap(80);
    }

    private void UpdateVeils(PlayerController player)
    {
        float dt = Time.deltaTime;
        if (flashVeil.gameObject.activeSelf)
        {
            if (flashHold > 0f)
                flashHold -= dt;
            else
            {
                flashStrength = flashFade > 0f ? Mathf.Max(0f, flashStrength - dt * flashStrength / Mathf.Max(0.05f, flashFade)) : 0f;
                flashFade = Mathf.Max(0f, flashFade - dt);
            }
            flashVeil.color = new Color(1f, 1f, 0.97f, Mathf.Clamp01(flashStrength * 1.05f));
            if (flashStrength <= 0.01f || player.isDead)
            {
                flashStrength = 0f;
                flashVeil.gameObject.SetActive(false);
            }
        }
        float smokeK = AreaEffect.Active.Count > 0 && player.playerCamera != null ? AreaEffect.SmokeAt(player.playerCamera.transform.position) : 0f;
        if (smokeVeil.enabled != smokeK > 0.01f)
            smokeVeil.enabled = smokeK > 0.01f;
        if (smokeK > 0.01f)
            smokeVeil.color = new Color(0.72f, 0.74f, 0.77f, smokeK * 0.88f);
    }

    /// <summary>Voice buttons follow the mic / speaker state; shown only when someone can hear you.</summary>
    private void UpdateVoiceHud()
    {
        var net = NetClient.Instance;
        bool show = net != null && net.InMatch && net.VoiceAvailable;
        if (micButton.activeSelf != show)
        {
            micButton.SetActive(show);
            speakerButton.SetActive(show);
        }
        if (!show)
        {
            if (talkingText.text.Length > 0)
                talkingText.text = "";
            return;
        }
        VoiceLabels(micLabel, speakerLabel);
        string talking = VoiceChat.TalkingNow();
        talkingText.text = talking.Length > 0 ? "Konuşuyor: " + talking : "";
    }

    /// <summary>Shared by the HUD and the waiting room.</summary>
    public static void VoiceLabels(Text mic, Text speaker)
    {
        string micText = !VoiceChat.MicOn ? "MİK\nKAPALI" : VoiceChat.MicProblem != null ? "MİK\nİZİN YOK" : VoiceChat.Speaking ? "MİK\n•••" : "MİK\nAÇIK";
        if (mic.text != micText)
        {
            mic.text = micText;
            mic.color = !VoiceChat.MicOn || VoiceChat.MicProblem != null ? new Color(1f, 0.5f, 0.45f) : VoiceChat.Speaking ? new Color(0.5f, 1f, 0.6f) : Color.white;
        }
        string spkText = VoiceChat.SpeakerOn ? "SES\nAÇIK" : "SES\nKAPALI";
        if (speaker.text != spkText)
        {
            speaker.text = spkText;
            speaker.color = VoiceChat.SpeakerOn ? Color.white : new Color(1f, 0.5f, 0.45f);
        }
    }

    /// <summary>From a private room's waiting room: friends list (to invite), back to the room after.</summary>
    public void OpenSocialFromRoom(GameObject room)
    {
        room.SetActive(false);
        social.Open(SocialScreen.Tab.Friends, () => room.SetActive(true));
    }

    /// <summary>Join a private room by its code (friend's invite or the friends list).</summary>
    /// <summary>The store in gift mode: what to send to this friend.</summary>
    public void OpenStore(StoreScreen.Tab tab)
    {
        HideAll();
        store.Open(tab, ShowLobby);
    }

    public void OpenStoreForGift(string personId, string personName)
    {
        HideAll();
        store.OpenForGift(personId, personName, StoreScreen.Tab.Characters, ShowLobby);
    }

    public void JoinRoomByCode(string code)
    {
        HideAll();
        netLobby.OpenJoinCode(code, ShowLobby);
    }

    /// <summary>Lobby: refresh friends now and then (badge for requests / invites, invite banner).</summary>
    private void PollSocial()
    {
        if (socialPolling || Time.unscaledTime < nextSocialPoll)
            return;
        var gm = GameManager.Instance;
        if (gm == null || gm.currentState != GameState.Lobby)
            return;
        var net = NetClient.Instance;
        bool room = net != null && net.State == NetClient.Phase.Lobby && net.PrivateRoom;
        nextSocialPoll = Time.unscaledTime + 12f;
        socialPolling = true;
        OnlineService.RefreshSocial(room ? "room" : "lobby", room ? net.RoomCode : "", net != null ? net.Mode : MatchMode.Solo, view =>
        {
            socialPolling = false;
            if (!view.ok || view.friends == null)
                return;
            int count = view.incoming.Length + view.invites.Length + view.unread + view.gifts;
            friendsBadge.SetActive(count > 0);
            friendsBadgeText.text = count.ToString();
            foreach (var inv in view.invites)
            {
                string key = inv.id + inv.room;
                if (seenInvites.Contains(key))
                    continue;
                if (lobbyPanel.activeSelf && !(net != null && net.State != NetClient.Phase.Idle))
                {
                    seenInvites.Add(key);   // shown once; still listed in ARKADAŞLAR until it expires
                    inviteRoom = inv.room;
                    inviteFrom = inv.id;
                    inviteText.text = inv.name + " seni " + (inv.mode ?? "").ToUpper() + " odasına çağırıyor";
                    inviteBanner.SetActive(true);
                    inviteBanner.transform.SetAsLastSibling();
                    UiSound.Confirm();
                }
            }
        });
    }

    private void BuildMinimap(Transform parent)
    {
        var anchor = new Vector2(1f, 1f);
        var frame = UIUtil.CreateImage(parent, "MinimapFrame", anchor, new Vector2(-185f, -185f), new Vector2(MapPx + 12f, MapPx + 12f), new Color(0f, 0f, 0f, 0.55f), false);
        var openMap = frame.gameObject.AddComponent<Button>();   // tap: the big map
        openMap.transition = Selectable.Transition.None;
        openMap.onClick.AddListener(() => { if (bigMap != null) bigMap.Toggle(); });

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

        for (int i = 0; i < 4; i++)
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
        for (int i = 0; i < 24; i++)
        {
            var vi = Icons.Create(mapRect, "veh_offroad", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(26f, 13f));
            vi.color = new Color(1f, 1f, 1f, 0.85f);
            vehicleDots.Add(vi);
        }
        for (int i = 0; i < 4; i++)
            airdropDots.Add(Icons.Create(mapRect, "airdrop_crate", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(24f, 24f)));
        for (int i = 0; i < 8; i++)
        {
            var d = UIUtil.CreateImage(mapRect, "Near", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(10f, 10f), new Color(1f, 0.35f, 0.3f, 0.9f), true);
            d.raycastTarget = false;
            d.gameObject.SetActive(false);
            nightDots.Add(d.rectTransform);
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
    private Crosshair crosshair;
    private int crosshairFrame;
    private bool enemyInSights;

    public void CrosshairKick(float amount)
    {
        if (crosshair != null)
            crosshair.Kick(amount);
    }
    private MatchPrepScreen matchPrep;
    private CareerScreen career;
    private LoadoutScreen loadout;
    private Image lobbyClassIcon;
    private Text lobbyClassText;
    private Image lobbyRankIcon;
    private NetLobbyScreen netLobby;
    private SocialScreen social;
    private MapSelectScreen mapSelect;
    private StoreScreen store;
    private MissionsScreen missions;
    private InventoryScreen inventory;
    private GameObject storeBadge;
    private GameObject missionsBadge;
    private Text missionsBadgeText, lobbyMissionsText;
    private GameObject friendsBadge, inviteBanner;
    private Text friendsBadgeText, inviteText;
    private string inviteRoom = "", inviteFrom = "";
    private readonly HashSet<string> seenInvites = new HashSet<string>();
    private float nextSocialPoll;
    private bool socialPolling;
    private GameObject micButton, speakerButton;
    private Text micLabel, speakerLabel, talkingText;
    private readonly List<Vector3> teammateScratch = new List<Vector3>();
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

        // Grid sized to the number of characters: 5 columns, rows share the space between the header and the footer.
        const int shopCols = 5;
        int shopRows = (ModelLibrary.ShopSkins.Length + shopCols - 1) / shopCols;
        float cellH = Mathf.Min(130f, 330f / Mathf.Max(1, shopRows));
        for (int i = 0; i < ModelLibrary.ShopSkins.Length; i++)
        {
            int index = i;
            float x = (i % shopCols - (shopCols - 1) * 0.5f) * 236f;
            float y = 150f - cellH * 0.5f - (i / shopCols) * cellH;
            Text label;
            var b = UIUtil.CreateButton(box, "", c, new Vector2(x, y), new Vector2(224f, cellH - 10f), new Color(0.2f, 0.25f, 0.35f, 1f), false, 24, out label);
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
        pausePanel = CreateOverlay("PausePanel", new Vector2(640f, 800f));
        pausePanel.AddComponent<PopIn>();
        var box = pausePanel.transform.Find("Box");
        var c = new Vector2(0.5f, 0.5f);
        var title = UIUtil.CreateText(box, "DURAKLATILDI", c, new Vector2(0f, 320f), new Vector2(600f, 70f), 46, TextAnchor.MiddleCenter);
        title.fontStyle = FontStyle.Bold;
        Text unused;
        UIUtil.CreateButton(box, "DEVAM ET", c, new Vector2(0f, 190f), new Vector2(420f, 96f), new Color(0.2f, 0.6f, 0.3f, 1f), false, 36, out unused)
            .onClick.AddListener(ClosePause);
        UIUtil.CreateButton(box, "AYARLAR", c, new Vector2(0f, 75f), new Vector2(420f, 96f), new Color(0.35f, 0.38f, 0.45f, 1f), false, 36, out unused)
            .onClick.AddListener(() => OpenSettings(true));
        UIUtil.CreateButton(box, "OYUNCULAR / ŞİKAYET", c, new Vector2(0f, -40f), new Vector2(420f, 96f), new Color(0.35f, 0.38f, 0.45f, 1f), false, 30, out unused)
            .onClick.AddListener(() => { pausePanel.SetActive(false); social.Open(SocialScreen.Tab.Players, () => pausePanel.SetActive(true)); });
        UIUtil.CreateButton(box, "HATA BİLDİR", c, new Vector2(0f, -155f), new Vector2(420f, 96f), new Color(0.35f, 0.38f, 0.45f, 1f), false, 32, out unused)
            .onClick.AddListener(() => { pausePanel.SetActive(false); social.Open(SocialScreen.Tab.Bug, () => pausePanel.SetActive(true)); });
        UIUtil.CreateButton(box, "MAÇTAN ÇIK", c, new Vector2(0f, -290f), new Vector2(420f, 96f), new Color(0.7f, 0.2f, 0.18f, 1f), false, 36, out unused)
            .onClick.AddListener(() => { Time.timeScale = 1f; GameManager.Instance.JoinLobby(); });
    }

    private void OpenPause()
    {
        var gm = GameManager.Instance;
        if (gm == null || gm.currentState != GameState.InGame)
            return;
        if (!NetGame.InOnlineMatch)
            Time.timeScale = 0f;   // online the match goes on without you
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
        if (GameManager.Instance.player != null)
            GameManager.Instance.player.ApplyMask();   // a mask may have come from a box, a deal or the wheel
        lobbyNameText.text = p.playerName;
        lobbyLevelText.text = p.RankName.ToUpper() + "  •  SV " + p.level + (p.IsMaxLevel ? "" : "   •   " + p.xp + " / " + p.XpForNextLevel + " XP");
        lobbyXpFill.sizeDelta = new Vector2(360f * (p.IsMaxLevel ? 1f : Mathf.Clamp01((float)p.xp / Mathf.Max(1, p.XpForNextLevel))), 8f);
        Icons.Set(lobbyRankIcon, Icons.Rank(p.RankIndex));
        var cls = ClassDefs.Get(ClassDefs.Selected);
        Icons.Set(lobbyClassIcon, cls.icon);
        lobbyClassText.text = "Sınıf: " + cls.name + "  •  jeton ve kamuflaj";
        lobbyCoinsText.text = p.coins.ToString("N0");
        storeBadge.SetActive(Deals.FreeReady);
        wheelBadge.SetActive(Deals.FreeSpinReady);
        lobbyTrophyText.text = "KUPA " + p.trophies.ToString("N0");
        lobbyPowerText.text = "GÜÇ " + Gear.Power(p).ToString("N0");
        int winsToday = Missions.WinsToday;
        lobbyWinsText.text = Mathf.Min(winsToday, Missions.WinBoxes) + " / " + Missions.WinBoxes;
        for (int i = 0; i < lobbyWinPips.Count; i++)
        {
            bool claimed = Missions.WinClaimed(i), canOpen = Missions.WinReady(i);
            lobbyWinPips[i].color = claimed ? new Color(1f, 1f, 1f, 0.25f) : canOpen ? Color.white : new Color(0.45f, 0.45f, 0.5f, 0.9f);
            lobbyWinPips[i].rectTransform.localScale = Vector3.one * (canOpen ? 1.12f : 1f);
        }
        int ready = Missions.ReadyCount();
        missionsBadge.SetActive(ready > 0);
        missionsBadgeText.text = ready.ToString();
        lobbyMissionsText.text = ready > 0 ? ready + " ödül toplanmayı bekliyor" : "Günlük ve haftalık görevler";
        float winRate = p.matches > 0 ? 100f * p.wins / p.matches : 0f;
        lobbyStatsText.text = "Maç " + p.matches + "    Zafer " + p.wins + "    %" + Mathf.RoundToInt(winRate) + "\nÖldürme " + p.totalKills + "    Kupa " + p.trophies + " (" + p.League + ")";
        var rifle = Gunsmith.Apply(WeaponData.CreateRifle());
        int count = 0;
        foreach (var a in rifle.attachments)
            if (!string.IsNullOrEmpty(a))
                count++;
        lobbyGunText.text = "Aparat ve kamuflaj  •  " + rifle.weaponName + " (" + count + "/5)";
        int skin = System.Array.IndexOf(ModelLibrary.ShopSkins, p.equippedSkin);
        lobbySkinText.text = "Kuşanılan: " + (skin >= 0 ? ModelLibrary.ShopNames[skin] : p.equippedSkin);
        lobbyPanel.SetActive(true);
        TopPlayersPanel.Refresh();   // ready for the next match's waiting screen
    }

    /// <summary>The match is being prepared: <paramref name="message"/> in the mode box, <paramref name="count"/> under it.</summary>
    public void ShowMatchmaking(string message, string count, MatchMode mode)
    {
        HideAll();
        matchmakingText.text = message;
        matchmakingCount.text = count;
        Icons.Set(matchmakingIcon, Modes.Icon(mode));
        matchmakingPanel.SetActive(true);
    }

    // ----- Arena mode HUD: score captions, capture points, the money bag, objective markers -----

    private void BuildModeHud(Transform t)
    {
        var top = new Vector2(0.5f, 1f);
        var c = new Vector2(0.5f, 0.5f);
        pointsRow = UIUtil.CreateRect(t, "Points", top, new Vector2(0f, -130f), new Vector2(240f, 64f)).gameObject;
        string[] names = { "A", "B", "C" };
        for (int i = 0; i < 3; i++)
        {
            var bg = UIUtil.CreateImage(pointsRow.transform, "Pt" + names[i], c, new Vector2((i - 1) * 76f, 0f), new Vector2(58f, 58f), new Color(0.15f, 0.15f, 0.15f, 0.85f), true);
            bg.raycastTarget = false;
            pointBadges[i] = bg;
            var fill = UIUtil.CreateImage(bg.transform, "Fill", c, Vector2.zero, new Vector2(58f, 58f), Color.white, true);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Radial360;
            fill.fillOrigin = 2;
            fill.raycastTarget = false;
            pointFills[i] = fill;
            var ring = UIUtil.CreateImage(bg.transform, "Inner", c, Vector2.zero, new Vector2(44f, 44f), new Color(0.08f, 0.08f, 0.1f, 0.85f), true);
            ring.raycastTarget = false;
            var l = UIUtil.CreateText(bg.transform, names[i], c, Vector2.zero, new Vector2(58f, 58f), 30, TextAnchor.MiddleCenter);
            l.fontStyle = FontStyle.Bold;
        }
        pointsRow.SetActive(false);

        captureBar = UIUtil.CreateImage(t, "Capture", top, new Vector2(0f, -205f), new Vector2(460f, 26f), new Color(0f, 0f, 0f, 0.55f), false).gameObject;
        captureBar.GetComponent<Image>().raycastTarget = false;
        captureFill = UIUtil.CreateImage(captureBar.transform, "Fill", new Vector2(0f, 0.5f), Vector2.zero, new Vector2(0f, 26f), ArenaObjectives.TeamColors[0], false);
        captureFill.rectTransform.pivot = new Vector2(0f, 0.5f);
        captureFill.raycastTarget = false;
        captureText = UIUtil.CreateText(captureBar.transform, "", c, new Vector2(0f, 30f), new Vector2(600f, 34f), 24, TextAnchor.MiddleCenter);
        captureText.fontStyle = FontStyle.Bold;
        captureBar.SetActive(false);

        objectiveText = UIUtil.CreateText(t, "", top, new Vector2(0f, -132f), new Vector2(900f, 40f), 26, TextAnchor.MiddleCenter);
        objectiveText.fontStyle = FontStyle.Bold;

        for (int i = 0; i < 4; i++)
        {
            var m = UIUtil.CreateText(t, "", c, Vector2.zero, new Vector2(160f, 60f), 24, TextAnchor.MiddleCenter);
            m.fontStyle = FontStyle.Bold;
            m.enabled = false;
            objMarkers.Add(m);
        }
    }

    private void UpdateModeHud(GameManager gm, PlayerController player)
    {
        var obj = ArenaObjectives.Instance;
        bool dom = obj != null && obj.Domination && gm.IsArena;
        bool heist = obj != null && obj.Heist && gm.IsArena;
        if (pointsRow.activeSelf != dom)
            pointsRow.SetActive(dom);
        int marker = 0;
        var cam = Camera.main;
        var hudRect = (RectTransform)hudPanel.transform;
        if (dom)
        {
            for (int i = 0; i < 3 && i < obj.points.Count; i++)
            {
                var p = obj.points[i];
                Color own = p.owner >= 0 ? ArenaObjectives.TeamColors[p.owner] : new Color(0.45f, 0.45f, 0.45f);
                pointBadges[i].color = new Color(own.r * 0.6f, own.g * 0.6f, own.b * 0.6f, 0.9f);
                pointFills[i].color = p.progress >= 0f ? ArenaObjectives.TeamColors[0] : ArenaObjectives.TeamColors[1];
                pointFills[i].fillAmount = Mathf.Abs(p.progress);
                Marker(ref marker, cam, hudRect, p.pos + Vector3.up * 3f, p.name, own, player);
            }
            var at = player.isDead ? null : obj.PointAt(player.transform.position);
            bool show = at != null;
            if (captureBar.activeSelf != show)
                captureBar.SetActive(show);
            if (show)
            {
                float ours = Mathf.Clamp01(at.progress);
                float theirs = Mathf.Clamp01(-at.progress);
                bool contested = at.inside0 > 0 && at.inside1 > 0;
                captureFill.color = theirs > 0f ? ArenaObjectives.TeamColors[1] : ArenaObjectives.TeamColors[0];
                captureFill.rectTransform.sizeDelta = new Vector2(460f * Mathf.Max(ours, theirs), 26f);
                captureText.text = contested ? at.name + " ÇEKİŞMELİ!" : at.owner == 0 ? at.name + " SENİN TAKIMININ" :
                    theirs > 0f ? at.name + " DÜŞMANDAN ALINIYOR" : at.name + " ALINIYOR  %" + Mathf.RoundToInt(ours * 100f);
                captureText.color = contested ? new Color(1f, 0.8f, 0.3f) : Color.white;
            }
        }
        else if (captureBar.activeSelf)
            captureBar.SetActive(false);

        string line = "";
        if (heist)
        {
            bool mine = ReferenceEquals(obj.carrier, player);
            if (obj.bagGone)
                line = "Yeni çanta geliyor...";
            else if (mine)
                line = "ÇANTA SENDE!  Mavi üsse götür";
            else if (obj.carrier == null)
                line = obj.bagHome ? "Para çantası kasada: kap ve üssüne götür" : "Para çantası yerde!";
            else if (obj.carrier.Team == 0)
                line = obj.carrier.DisplayName + " çantayı taşıyor: koru!";
            else
                line = "DÜŞMAN ÇANTAYI KAÇIRIYOR: durdur!";
            objectiveText.color = mine ? new Color(0.4f, 1f, 0.5f) : obj.carrier != null && obj.carrier.Team != 0 ? new Color(1f, 0.45f, 0.35f) : Color.white;
            if (!obj.bagGone && !mine)
                Marker(ref marker, cam, hudRect, obj.bagPos + Vector3.up * 1.6f, "$", new Color(0.4f, 1f, 0.45f), player);
            if (mine)
                Marker(ref marker, cam, hudRect, obj.bases[0] + Vector3.up * 2f, "ÜS", ArenaObjectives.TeamColors[0], player);
        }
        if (objectiveText.text != line)
            objectiveText.text = line;
        for (int i = marker; i < objMarkers.Count; i++)
            if (objMarkers[i].enabled)
                objMarkers[i].enabled = false;
    }

    /// <summary>A label with the distance over an objective (kept on screen at the edges).</summary>
    private void Marker(ref int index, Camera cam, RectTransform hudRect, Vector3 world, string label, Color color, PlayerController player)
    {
        if (index >= objMarkers.Count || cam == null)
            return;
        Vector3 sp = cam.WorldToScreenPoint(world);
        if (sp.z < 0f)
        {
            // behind us: pin it to the bottom edge on that side
            sp.x = Screen.width - sp.x;
            sp.y = 0f;
        }
        sp.x = Mathf.Clamp(sp.x, Screen.width * 0.06f, Screen.width * 0.94f);
        sp.y = Mathf.Clamp(sp.y, Screen.height * 0.12f, Screen.height * 0.86f);
        Vector2 local;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(hudRect, sp, null, out local))
            return;
        var m = objMarkers[index++];
        m.enabled = true;
        m.rectTransform.anchoredPosition = local;
        int d = Mathf.RoundToInt(Vector3.Distance(player.transform.position, world));
        m.text = label + "\n" + d + " m";
        m.color = color;
    }

    public void ShowBattleHud()
    {
        hudHealth = hudAmmo = int.MinValue;
        hudAlive = hudKills = -1;
        hudWeapon = null;
        hudSlowTick = 0f;
        if (weaponText != null)
            weaponText.text = ammoText.text = "";
        HideAll();
        killFeed.Clear();
        killFeedText.text = "";
        toastText.text = "";
        damageAlpha = 0f;
        touchControls.ResetState();
        botLastPos.Clear();

        hudPanel.SetActive(true);
        if (scoreboard != null) scoreboard.gameObject.SetActive(false);
        if (bigMap != null) bigMap.gameObject.SetActive(false);

        var mode = GameManager.Instance.currentMode;
        bool ffa = mode == MatchMode.FreeForAll;
        teamCapOurs.text = ffa ? "SEN" : "TAKIMIN";
        teamCapTheirs.text = ffa ? "LİDER" : "DÜŞMAN";
        string unit = mode == MatchMode.Domination ? " puan" : mode == MatchMode.Heist ? " çanta" : " öldürme";
        teamScoreGoal.text = "hedef " + TeamMatch.GoalFor(mode) + unit;
        objectiveText.text = "";
    }

    public void ShowResult(bool won, int place, int teams, int kills, int xp, int coins, int trophies)
    {
        HideAll();
        var mode = GameManager.Instance.currentMode;
        bool teamArena = Modes.TwoTeams(mode);
        resultTitle.text = teamArena ? (won ? "ZAFER!" : "YENİLGİ") : won ? "ZAFER! #1" : "#" + place + " / " + teams;
        resultTitle.color = won ? new Color(1f, 0.85f, 0.3f) : Color.white;
        string line = teamArena ? (won ? "Takımın " + Modes.Short(mode) + " maçını kazandı!" : "Takımın kaybetti. Bir dahaki sefere!")
            : won ? (mode == MatchMode.FreeForAll ? "Herkesi geride bıraktın!" : "Ayakta kalan son kişi sensin!") : "Elendin. Bir dahaki sefere!";
        resultDetails.text = line + "\nÖldürme: " + kills + "     +" + xp + " XP     +" + coins + " Kredi     " +
                             (trophies >= 0 ? "+" : "") + trophies + " Kupa";

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

    /// <summary>While a throw is held: its name and the fuse counting down (red near the end); null hides it.</summary>
    public void ShowCook(string what, float secondsLeft)
    {
        if (cookText == null)
            return;
        if (string.IsNullOrEmpty(what))
        {
            cookText.text = "";
            return;
        }
        string time = secondsLeft >= 0f ? "  <color=" + (secondsLeft < 2f ? "#ff4d3d" : "#ffd23f") + ">" + secondsLeft.ToString("0.0") + " sn</color>" : "";
        cookText.text = what + time + "\n<size=24>bırakınca atılır</size>";
    }

    public void Toast(string message)
    {
        if (canvas == null)
            return;   // game server
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
        if (killFeedText == null)
            return;   // game server
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

    private int hudHealth = int.MinValue, hudAmmo = int.MinValue, hudAlive = -1, hudKills = -1;
    private WeaponData hudWeapon;
    private float hudSlowTick;

    private void Update()
    {
        if (canvas == null)
            return;   // game server
        if (lobbyPanel.activeSelf || (social != null && social.gameObject.activeSelf))
            PollSocial();
        if (hudPanel.activeSelf)
            UpdateVoiceHud();
        if (hudPanel == null || !hudPanel.activeSelf)
            return;

        var gm = GameManager.Instance;
        var player = gm != null ? gm.player : null;
        if (player == null)
            return;

        healthFill.sizeDelta = new Vector2(BarWidth * Mathf.Clamp01(player.health / player.maxHealth), healthFill.sizeDelta.y);
        if ((++crosshairFrame & 3) == 0)
            enemyInSights = player.EnemyInSights();   // one ray every 4th frame is plenty for the colour
        crosshair.Tick(player, enemyInSights);
        bool scoped = player.IsScoped && !player.isDead;
        if (scopeOverlay.activeSelf != scoped)
            scopeOverlay.SetActive(scoped);
        armorFill.sizeDelta = new Vector2(BarWidth * Mathf.Clamp01(player.armor / player.maxArmor), armorFill.sizeDelta.y);
        boostFill.sizeDelta = new Vector2(BarWidth * Mathf.Clamp01(player.BoostRemaining / 60f), boostFill.sizeDelta.y);
        // Texts are only rebuilt when what they show changes (no new strings every frame: smoother, less garbage).
        int hp = Mathf.CeilToInt(player.health);
        if (hp != hudHealth)
        {
            hudHealth = hp;
            healthText.text = hp.ToString();
        }

        bool onFoot = player.state == PlayerState.Ground;
        var w = player.currentWeapon;
        if (onFoot && w != null && w.weaponData != null)
        {
            int key = w.isReloading ? -1 : w.currentAmmo * 100000 + w.reserveAmmo;
            if (key != hudAmmo || w.weaponData != hudWeapon)
            {
                hudAmmo = key;
                hudWeapon = w.weaponData;
                weaponText.text = w.weaponData.weaponName;
                ammoText.text = w.isReloading ? "Dolduruluyor..." : w.currentAmmo + " / " + w.reserveAmmo;
            }
        }
        else if (hudAmmo != int.MinValue || player.state == PlayerState.Driving)
        {
            hudAmmo = int.MinValue;
            hudWeapon = null;
            string vehicleName = player.state == PlayerState.Driving && player.vehicle != null ? player.vehicle.DisplayName : "";
            if (weaponText.text != vehicleName)
                weaponText.text = vehicleName;
            ammoText.text = "";
        }

        if (player.IsAirborne)
            altitudeText.text = (player.state == PlayerState.Plane ? "Uçakta  " : "") + "Yükseklik " + Mathf.Max(0, Mathf.RoundToInt(player.HeightAboveGround)) + " m";
        else if (player.state == PlayerState.Driving && player.vehicle != null)
        {
            var veh = player.vehicle;
            int kmh = Mathf.RoundToInt(Mathf.Abs(veh.speed) * 3.6f);
            int alt = veh.def.flying ? Mathf.RoundToInt(veh.HeightAboveGround) : 0;
            int hpPct = Mathf.CeilToInt(veh.Health01 * 100f);
            int key = (kmh * 1000 + alt) * 101 + hpPct;
            if (key != driveHudKey)
            {
                driveHudKey = key;
                altitudeText.text = kmh + " km/s" + (veh.def.flying ? "   •   " + alt + " m" : "") + "   •   Araç %" + hpPct;
            }
        }
        else
        {
            altitudeText.text = "";
            driveHudKey = int.MinValue;
        }

        bool team = gm.IsArena;
        if (teamScoreBox.activeSelf != team)
        {
            teamScoreBox.SetActive(team);
            aliveText.gameObject.SetActive(!team);
            killsText.rectTransform.anchoredPosition = team ? new Vector2(420f, -45f) : new Vector2(110f, -45f);
        }
        if (Time.unscaledTime >= hudSlowTick)
        {
            // Counters and the zone line: a few times a second is plenty.
            hudSlowTick = Time.unscaledTime + 0.2f;
            if (team)
            {
                teamScoreOurs.text = TeamMatch.Score[0].ToString();
                teamScoreTheirs.text = (TeamMatch.FreeForAll ? TeamMatch.BestOther(0) : TeamMatch.Score[1]).ToString();
                teamScoreTime.text = TeamMatch.TimeText;
            }
            else
            {
                int alive = gm.AliveCount();
                if (alive != hudAlive)
                {
                    hudAlive = alive;
                    aliveText.text = "Kalan: " + alive;
                }
            }
            if (player.kills != hudKills)
            {
                hudKills = player.kills;
                killsText.text = "Öldürme: " + player.kills;
            }
            zoneText.text = gm.safeZone != null && !team ? gm.safeZone.StatusText : "";
        }
        zoneWarningText.enabled = gm.safeZone != null && gm.safeZone.active && !player.IsAirborne && gm.safeZone.IsOutside(player.transform.position);

        bool vehicleNearby = onFoot && gm.NearestVehicle(player.transform.position, 4.5f) != null;
        touchControls.UpdateContext(player, vehicleNearby);

        UpdateMinimap(gm, player);
        UpdateAbilityHud(gm, player);
        UpdateModeHud(gm, player);

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
        UpdateVeils(player);

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
        if (NetGame.InOnlineMatch)
        {
            NetClient.Instance.TeammatePositions(teammateScratch);
            foreach (var pos in teammateScratch)
            {
                if (dot >= allyDots.Count)
                    break;
                allyDots[dot].gameObject.SetActive(true);
                allyDots[dot].anchoredPosition = MapPos(pos);
                dot++;
            }
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

        // Empty vehicles (and the tank drop).
        int vd = 0;
        foreach (var v in gm.vehicles)
        {
            if (vd >= vehicleDots.Count)
                break;
            if (v == null || v.Destroyed || v.driver != null)
                continue;
            int slot = vd++;
            var img = vehicleDots[slot];
            if (vehicleDotKind[slot] != (int)v.def.kind)
            {
                vehicleDotKind[slot] = (int)v.def.kind;
                Icons.Set(img, v.def.icon);
            }
            img.color = v.def.cannon || v.def.flying ? new Color(1f, 0.8f, 0.3f, 1f) : new Color(1f, 1f, 1f, 0.85f);
            Place(img, v.transform.position);
        }
        foreach (var drop in TankDrop.Active)
        {
            if (drop == null || vd >= vehicleDots.Count)
                continue;
            int slot = vd++;
            var img = vehicleDots[slot];
            if (vehicleDotKind[slot] != (int)VehicleKind.Tank)
            {
                vehicleDotKind[slot] = (int)VehicleKind.Tank;
                Icons.Set(img, "veh_tank");
            }
            img.color = new Color(1f, 0.5f, 0.2f, 0.6f + 0.4f * Mathf.Sin(Time.time * 6f));
            Place(img, drop.Target);
        }
        for (; vd < vehicleDots.Count; vd++)
            if (vehicleDots[vd].enabled)
                vehicleDots[vd].enabled = false;

        // Air drops: on the way (smoke) and landed but not opened.
        int ad = 0;
        foreach (var call in AirdropCall.Active)
            if (call != null && ad < airdropDots.Count)
                Place(airdropDots[ad++], call.Target);
        if (gm.lootSystem != null)
        {
            gm.lootSystem.SupplyCrates(supplyScratch);
            foreach (var pos in supplyScratch)
                if (ad < airdropDots.Count)
                    Place(airdropDots[ad++], pos);
        }
        for (; ad < airdropDots.Count; ad++)
            if (airdropDots[ad].enabled)
                airdropDots[ad].enabled = false;

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

        // Gece Görüşlü Kask: enemies close by show on the map.
        int nd = 0;
        if (Gear.NightVision && gm.currentState == GameState.InGame)
        {
            Vector3 me = player.transform.position;
            foreach (var c in gm.Combatants)
            {
                if (nd >= nightDots.Count)
                    break;
                if (c == null || c.IsDead || c.IsAirborne || c.Team == 0 || ReferenceEquals(c, player))
                    continue;
                if ((c.transform.position - me).sqrMagnitude > 28f * 28f)
                    continue;
                if (!nightDots[nd].gameObject.activeSelf)
                    nightDots[nd].gameObject.SetActive(true);
                nightDots[nd].anchoredPosition = MapPos(c.transform.position);
                nd++;
            }
        }
        for (; nd < nightDots.Count; nd++)
            if (nightDots[nd].gameObject.activeSelf)
                nightDots[nd].gameObject.SetActive(false);
    }

    private void Place(Image dot, Vector3 world)
    {
        if (!dot.enabled)
            dot.enabled = true;
        dot.rectTransform.anchoredPosition = MapPos(world);
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
        foreach (var bot in k9 ? gm.bots : noBots)
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
