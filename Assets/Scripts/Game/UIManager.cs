using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Builds every screen from code: lobby, match preparation, battle HUD and results.</summary>
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
    private Text healthText;
    private Text weaponText;
    private Text ammoText;
    private Text aliveText;
    private Text killsText;
    private Text zoneText;
    private Text zoneWarningText;
    private Text toastText;
    private float toastUntil;
    private Text killFeedText;
    private readonly List<string> killFeed = new List<string>();
    private float killFeedClearTime;
    private Image damageFlash;
    private float damageAlpha;

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
        HideAll();
    }

    private void HideAll()
    {
        lobbyPanel.SetActive(false);
        matchmakingPanel.SetActive(false);
        hudPanel.SetActive(false);
        resultPanel.SetActive(false);
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
        lobbyPanel = CreateFullPanel("LobbyPanel", new Color(0.03f, 0.06f, 0.1f, 0.55f));
        var t = lobbyPanel.transform;
        var center = new Vector2(0.5f, 0.5f);

        var title = UIUtil.CreateText(t, GameTitle, center, new Vector2(0f, 330f), new Vector2(1400f, 140f), 110, TextAnchor.MiddleCenter);
        title.fontStyle = FontStyle.Bold;
        title.color = new Color(1f, 0.85f, 0.3f);

        UIUtil.CreateText(t, "Adada son kalan sen ol", center, new Vector2(0f, 240f), new Vector2(1200f, 60f), 36, TextAnchor.MiddleCenter);

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

        // Crosshair
        var c = new Vector2(0.5f, 0.5f);
        var crossColor = new Color(1f, 1f, 1f, 0.85f);
        UIUtil.CreateImage(t, "CrossH", c, Vector2.zero, new Vector2(28f, 3f), crossColor, false).raycastTarget = false;
        UIUtil.CreateImage(t, "CrossV", c, Vector2.zero, new Vector2(3f, 28f), crossColor, false).raycastTarget = false;

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

        killFeedText = UIUtil.CreateText(t, "", new Vector2(1f, 1f), new Vector2(-230f, -90f), new Vector2(420f, 160f), 26, TextAnchor.UpperRight);

        // Bottom centre: health & armor bars, weapon & ammo
        var bottom = new Vector2(0.5f, 0f);
        UIUtil.CreateImage(t, "HealthBg", bottom, new Vector2(0f, 175f), new Vector2(BarWidth, 26f), new Color(0f, 0f, 0f, 0.5f), false).raycastTarget = false;
        healthFill = CreateFill(t, "HealthFill", new Vector2(0f, 175f), 26f, new Color(0.95f, 0.95f, 0.95f, 0.95f));
        healthText = UIUtil.CreateText(t, "", bottom, new Vector2(0f, 175f), new Vector2(BarWidth, 26f), 20, TextAnchor.MiddleCenter);
        healthText.color = Color.black;
        healthText.GetComponent<Shadow>().enabled = false;

        UIUtil.CreateImage(t, "ArmorBg", bottom, new Vector2(0f, 150f), new Vector2(BarWidth, 12f), new Color(0f, 0f, 0f, 0.5f), false).raycastTarget = false;
        armorFill = CreateFill(t, "ArmorFill", new Vector2(0f, 150f), 12f, new Color(0.3f, 0.6f, 1f, 0.95f));

        weaponText = UIUtil.CreateText(t, "", bottom, new Vector2(0f, 255f), new Vector2(600f, 40f), 28, TextAnchor.MiddleCenter);
        ammoText = UIUtil.CreateText(t, "", bottom, new Vector2(0f, 215f), new Vector2(600f, 44f), 36, TextAnchor.MiddleCenter);
        ammoText.fontStyle = FontStyle.Bold;

        // Touch controls live on their own full-screen layer inside the HUD.
        var controlsRect = UIUtil.CreateStretch(t, "TouchControls");
        touchControls = controlsRect.gameObject.AddComponent<TouchControls>();
        touchControls.Build(canvas);
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
        healthText.text = Mathf.CeilToInt(player.health).ToString();

        var w = player.currentWeapon;
        if (w != null && w.weaponData != null)
        {
            weaponText.text = w.weaponData.weaponName;
            ammoText.text = w.isReloading ? "Dolduruluyor..." : w.currentAmmo + " / " + w.reserveAmmo;
        }

        aliveText.text = "Kalan: " + gm.AliveCount();
        killsText.text = "Öldürme: " + player.kills;
        zoneText.text = gm.safeZone != null ? gm.safeZone.StatusText : "";
        zoneWarningText.enabled = gm.safeZone != null && gm.safeZone.active && gm.safeZone.IsOutside(player.transform.position);

        touchControls.SetMedkitCount(player.inventory.medkits);

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
    }
}
