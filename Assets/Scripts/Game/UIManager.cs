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
    private GameObject resultPanel;

    // Lobby
    private Text lobbyProfileText;
    private Text lobbyStatsText;

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
    private RectTransform mapRect;
    private RectTransform playerMarker;
    private RectTransform zoneRing;
    private RectTransform nextZoneRing;
    private RectTransform planeLine;
    private readonly List<RectTransform> allyDots = new List<RectTransform>();

    // Overlays: settings, shop, pause
    private GameObject settingsPanel;
    private GameObject shopPanel;
    private GameObject pausePanel;
    private Text sensText, qualityText, volumeText;
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
        lobbyPanel = CreateFullPanel("LobbyPanel", new Color(0.03f, 0.06f, 0.1f, 0.45f));
        var t = lobbyPanel.transform;
        var center = new Vector2(0.5f, 0.5f);

        var title = UIUtil.CreateText(t, GameTitle, center, new Vector2(0f, 330f), new Vector2(1400f, 140f), 110, TextAnchor.MiddleCenter);
        title.fontStyle = FontStyle.Bold;
        title.color = new Color(1f, 0.85f, 0.3f);

        UIUtil.CreateText(t, "Uçaktan atla, ganimet topla, adada son kalan sen ol", center, new Vector2(0f, 240f), new Vector2(1400f, 60f), 34, TextAnchor.MiddleCenter);

        lobbyProfileText = UIUtil.CreateText(t, "", center, new Vector2(0f, 160f), new Vector2(1400f, 60f), 34, TextAnchor.MiddleCenter);

        Text unused;
        var blue = new Color(0.2f, 0.5f, 1f, 0.95f);
        UIUtil.CreateButton(t, "SOLO", center, new Vector2(-340f, 10f), new Vector2(300f, 120f), blue, false, 44, out unused)
            .onClick.AddListener(() => GameManager.Instance.StartMatch(MatchMode.Solo));
        UIUtil.CreateButton(t, "DUO", center, new Vector2(0f, 10f), new Vector2(300f, 120f), blue, false, 44, out unused)
            .onClick.AddListener(() => GameManager.Instance.StartMatch(MatchMode.Duo));
        UIUtil.CreateButton(t, "SQUAD", center, new Vector2(340f, 10f), new Vector2(300f, 120f), blue, false, 44, out unused)
            .onClick.AddListener(() => GameManager.Instance.StartMatch(MatchMode.Squad));

        UIUtil.CreateText(t, "Solo: tek başına  •  Duo: 1 bot takım arkadaşı  •  Squad: 3 bot takım arkadaşı", center,
            new Vector2(0f, -100f), new Vector2(1600f, 50f), 28, TextAnchor.MiddleCenter);

        lobbyStatsText = UIUtil.CreateText(t, "", center, new Vector2(0f, -190f), new Vector2(1400f, 50f), 30, TextAnchor.MiddleCenter);

        UIUtil.CreateButton(t, "KARAKTERLER", center, new Vector2(-200f, -290f), new Vector2(330f, 86f), new Color(0.95f, 0.65f, 0.15f, 0.95f), false, 32, out unused)
            .onClick.AddListener(OpenShop);
        UIUtil.CreateButton(t, "AYARLAR", center, new Vector2(200f, -290f), new Vector2(330f, 86f), new Color(0.35f, 0.38f, 0.45f, 0.95f), false, 32, out unused)
            .onClick.AddListener(() => OpenSettings(false));

        var credit = UIUtil.CreateText(t, ProducerCredit, new Vector2(0.5f, 0f), new Vector2(0f, 60f), new Vector2(1200f, 50f), 30, TextAnchor.MiddleCenter);
        credit.color = new Color(1f, 1f, 1f, 0.8f);
    }

    private void BuildMatchmaking()
    {
        matchmakingPanel = CreateFullPanel("MatchmakingPanel", new Color(0.03f, 0.06f, 0.1f, 0.75f));
        matchmakingText = UIUtil.CreateText(matchmakingPanel.transform, "", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1200f, 120f), 64, TextAnchor.MiddleCenter);
        matchmakingText.fontStyle = FontStyle.Bold;
    }

    private void BuildHud()
    {
        hudPanel = CreateFullPanel("BattleHud", new Color(0f, 0f, 0f, 0f));
        var t = hudPanel.transform;

        damageFlash = UIUtil.CreateStretch(t, "DamageFlash").gameObject.AddComponent<Image>();
        damageFlash.color = new Color(0.9f, 0f, 0f, 0f);
        damageFlash.raycastTarget = false;

        // Crosshair + hit marker
        var c = new Vector2(0.5f, 0.5f);
        var crossColor = new Color(1f, 1f, 1f, 0.85f);
        UIUtil.CreateImage(t, "CrossH", c, Vector2.zero, new Vector2(28f, 3f), crossColor, false).raycastTarget = false;
        UIUtil.CreateImage(t, "CrossV", c, Vector2.zero, new Vector2(3f, 28f), crossColor, false).raycastTarget = false;
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

        var marker = UIUtil.CreateImage(mapRect, "Player", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(14f, 14f), new Color(1f, 0.85f, 0.2f), true);
        marker.raycastTarget = false;
        playerMarker = marker.rectTransform;
        var nose = UIUtil.CreateImage(playerMarker, "Facing", new Vector2(0.5f, 0.5f), new Vector2(0f, 11f), new Vector2(4f, 12f), new Color(1f, 0.85f, 0.2f), false);
        nose.raycastTarget = false;

        UIUtil.CreateText(frame.transform, "K", new Vector2(0.5f, 1f), new Vector2(0f, -16f), new Vector2(30f, 30f), 22, TextAnchor.MiddleCenter);
    }

    private static Vector2 MapPos(Vector3 world)
    {
        Vector2 uv = World.ToMapUV(world);
        return (uv - new Vector2(0.5f, 0.5f)) * MapPx;
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
        var t = resultPanel.transform;
        var c = new Vector2(0.5f, 0.5f);

        resultTitle = UIUtil.CreateText(t, "", c, new Vector2(0f, 200f), new Vector2(1500f, 140f), 96, TextAnchor.MiddleCenter);
        resultTitle.fontStyle = FontStyle.Bold;
        resultDetails = UIUtil.CreateText(t, "", c, new Vector2(0f, 30f), new Vector2(1400f, 160f), 40, TextAnchor.MiddleCenter);

        Text unused;
        UIUtil.CreateButton(t, "LOBİYE DÖN", c, new Vector2(0f, -180f), new Vector2(420f, 120f), new Color(0.2f, 0.5f, 1f, 0.95f), false, 40, out unused)
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

    private void BuildSettings()
    {
        settingsPanel = CreateOverlay("SettingsPanel", new Vector2(860f, 620f));
        var box = settingsPanel.transform.Find("Box");
        var c = new Vector2(0.5f, 0.5f);
        var title = UIUtil.CreateText(box, "AYARLAR", c, new Vector2(0f, 240f), new Vector2(600f, 70f), 48, TextAnchor.MiddleCenter);
        title.fontStyle = FontStyle.Bold;

        sensText = SettingRow(box, "Bakış hassasiyeti", 120f,
            () => { GameSettings.Sensitivity = Mathf.Max(0.4f, GameSettings.Sensitivity - 0.1f); SettingsChanged(); },
            () => { GameSettings.Sensitivity = Mathf.Min(2f, GameSettings.Sensitivity + 0.1f); SettingsChanged(); });
        qualityText = SettingRow(box, "Grafik kalitesi", 20f,
            () => { GameSettings.Quality = Mathf.Max(0, GameSettings.Quality - 1); SettingsChanged(); },
            () => { GameSettings.Quality = Mathf.Min(2, GameSettings.Quality + 1); SettingsChanged(); });
        volumeText = SettingRow(box, "Ses", -80f,
            () => { GameSettings.Volume = Mathf.Max(0f, GameSettings.Volume - 0.1f); SettingsChanged(); },
            () => { GameSettings.Volume = Mathf.Min(1f, GameSettings.Volume + 0.1f); SettingsChanged(); });

        Text unused;
        UIUtil.CreateButton(box, "KAPAT", c, new Vector2(0f, -220f), new Vector2(300f, 90f), new Color(0.2f, 0.5f, 1f, 0.95f), false, 34, out unused)
            .onClick.AddListener(CloseSettings);
    }

    private void SettingsChanged()
    {
        GameSettings.Save();
        RefreshSettings();
    }

    private void RefreshSettings()
    {
        sensText.text = GameSettings.Sensitivity.ToString("0.0") + "x";
        qualityText.text = GameSettings.QualityNames[GameSettings.Quality];
        volumeText.text = Mathf.RoundToInt(GameSettings.Volume * 100f) + "%";
    }

    public void OpenSettings(bool fromPause)
    {
        settingsFromPause = fromPause;
        if (pausePanel != null) pausePanel.SetActive(false);
        RefreshSettings();
        settingsPanel.SetActive(true);
    }

    private void CloseSettings()
    {
        settingsPanel.SetActive(false);
        if (settingsFromPause)
            pausePanel.SetActive(true);
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
        shopCoinsText.text = "Altın: " + p.coins + "   (maç kazanarak ve öldürerek kazanılır)";
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
                status = ModelLibrary.ShopPrices[i] + " altın";
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
                shopCoinsText.text = "Yetersiz altın! Gereken: " + ModelLibrary.ShopPrices[index];
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
        lobbyProfileText.text = p.playerName + "   •   Seviye " + p.level + "   •   XP " + p.xp + "/" + p.XpForNextLevel + "   •   Altın " + p.coins;
        lobbyStatsText.text = "Maç: " + p.matches + "    Zafer: " + p.wins + "    Toplam öldürme: " + p.totalKills;
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
        hudPanel.SetActive(true);
    }

    public void ShowResult(bool won, int place, int teams, int kills, int xp, int coins)
    {
        HideAll();
        resultTitle.text = won ? "ZAFER! #1" : "#" + place + " / " + teams;
        resultTitle.color = won ? new Color(1f, 0.85f, 0.3f) : Color.white;
        resultDetails.text = (won ? "Adada son kalan sensin!" : "Elendin. Bir dahaki sefere!") +
                             "\nÖldürme: " + kills + "     +" + xp + " XP     +" + coins + " Altın";
        resultPanel.SetActive(true);
    }

    // ----- HUD feedback -----

    public void Toast(string message)
    {
        if (toastText == null)
            return;
        toastText.text = message;
        toastUntil = Time.time + 2.2f;
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
        playerMarker.anchoredPosition = MapPos(player.transform.position);
        float yaw = player.state == PlayerState.Driving && player.vehicle != null ? player.vehicle.Yaw : player.transform.eulerAngles.y;
        playerMarker.localRotation = Quaternion.Euler(0f, 0f, -yaw);

        var zone = gm.safeZone;
        bool zoneOn = zone != null && zone.active;
        zoneRing.gameObject.SetActive(zoneOn);
        if (zoneOn)
        {
            zoneRing.anchoredPosition = MapPos(zone.center);
            float size = zone.radius * 2f / World.MapSize * MapPx;
            zoneRing.sizeDelta = new Vector2(size, size);
        }
        bool nextOn = zoneOn && zone.HasNext;
        nextZoneRing.gameObject.SetActive(nextOn);
        if (nextOn)
        {
            nextZoneRing.anchoredPosition = MapPos(zone.NextCenter);
            float size = zone.NextRadius * 2f / World.MapSize * MapPx;
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
    }
}
