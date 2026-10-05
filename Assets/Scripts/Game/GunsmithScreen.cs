using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Lobby gunsmith ("SİLAH ATÖLYESİ"): pick a weapon class, see the gun rotating in 3D, compare
/// stats, buy/equip attachments in five slots and buy/equip camos. Everything saved on the device
/// and applied to every gun of that class the player picks up in a match.
/// </summary>
public class GunsmithScreen : MonoBehaviour, IDragHandler
{
    public const int PreviewLayer = 8;
    private static readonly Vector3 StagePos = new Vector3(0f, 600f, 0f);

    private RectTransform root;
    private int weaponIndex;
    private int slotIndex = -1;   // index into Gunsmith.SlotOrder, SlotCount = camo, -1 nothing open

    // Preview
    private Camera previewCam;
    private RenderTexture previewRT;
    private Transform stage;
    private WeaponController previewGun;
    private float yaw = -20f;
    private float lastDragTime = -10f;
    private float fitDistance = 2f;

    // UI references
    private readonly List<Image> categoryButtons = new List<Image>();
    private readonly List<Text> categoryLabels = new List<Text>();
    private readonly List<Text> categoryCounts = new List<Text>();
    private Text weaponTitle;
    private Text coinsText;
    private readonly Text[] statValues = new Text[6];
    private readonly RectTransform[] statBase = new RectTransform[6];
    private readonly Image[] statDelta = new Image[6];
    private readonly List<Image> slotButtons = new List<Image>();
    private readonly List<Text> slotLabels = new List<Text>();
    private readonly List<Image> dots = new List<Image>();
    private RectTransform strip;
    private RectTransform stripContent;
    private ScrollRect stripScroll;
    private readonly List<Text> slotSubLabels = new List<Text>();
    private Text stripTitle;
    private Text message;
    private float messageUntil;
    private const float StatBarWidth = 190f;

    public static GunsmithScreen Create(Transform canvas)
    {
        var rect = UIUtil.CreateStretch(canvas, "Gunsmith");
        var bg = rect.gameObject.AddComponent<Image>();
        bg.color = new Color(0.03f, 0.04f, 0.06f, 0.72f);
        var screen = rect.gameObject.AddComponent<GunsmithScreen>();
        screen.root = rect;
        screen.Build();
        rect.gameObject.SetActive(false);
        return screen;
    }

    // ----- Building the UI -----

    private void Build()
    {
        var t = root.transform;
        Text unused;

        // Header
        UIUtil.CreateButton(t, "< GERİ", new Vector2(0f, 1f), new Vector2(120f, -60f), new Vector2(180f, 76f), Theme.Accent, false, 30, out unused)
            .onClick.AddListener(Close);
        var title = UIUtil.CreateText(t, "SİLAH ATÖLYESİ", new Vector2(0f, 1f), new Vector2(470f, -60f), new Vector2(500f, 70f), 46, TextAnchor.MiddleLeft);
        title.fontStyle = FontStyle.Bold;
        coinsText = UIUtil.CreateText(t, "", new Vector2(1f, 1f), new Vector2(-230f, -60f), new Vector2(400f, 60f), 34, TextAnchor.MiddleRight);
        coinsText.color = Theme.Accent;
        coinsText.fontStyle = FontStyle.Bold;
        Text drawLabel;
        var drawButton = UIUtil.CreateButton(t, "ŞANS ÇEKİLİŞİ", new Vector2(1f, 1f), new Vector2(-610f, -60f), new Vector2(300f, 76f), new Color(0.42f, 0.2f, 0.62f, 0.95f), false, 28, out drawLabel);
        drawButton.onClick.AddListener(OpenDraw);

        // Weapon classes (left)
        for (int i = 0; i < Gunsmith.Weapons.Length; i++)
        {
            int index = i;
            Text label;
            var b = UIUtil.CreateButton(t, Gunsmith.CategoryNames[i], new Vector2(0f, 0.5f), new Vector2(210f, 280f - i * 104f), new Vector2(340f, 88f), Theme.Panel, false, 30, out label);
            label.alignment = TextAnchor.MiddleLeft;
            var lr = label.rectTransform;
            lr.anchorMin = Vector2.zero;
            lr.anchorMax = Vector2.one;
            lr.offsetMin = new Vector2(26f, 0f);
            lr.offsetMax = new Vector2(-70f, 0f);
            // how many gun models the class has
            var count = UIUtil.CreateText(b.transform, ModelLibrary.GunSkins(Gunsmith.Weapons[i]).Length + " model", new Vector2(1f, 0.5f), new Vector2(-48f, 0f), new Vector2(90f, 40f), 20, TextAnchor.MiddleRight);
            count.color = Theme.TextDim;
            categoryCounts.Add(count);
            b.onClick.AddListener(() => SelectWeapon(index));
            categoryButtons.Add(b.GetComponent<Image>());
            categoryLabels.Add(label);
        }

        // 3D preview (centre)
        var previewRect = UIUtil.CreateRect(t, "Preview", new Vector2(0.5f, 0.5f), new Vector2(-150f, 120f), new Vector2(1000f, 560f));
        var raw = previewRect.gameObject.AddComponent<RawImage>();
        raw.raycastTarget = true;
        previewRT = new RenderTexture(1280, 716, 24, RenderTextureFormat.ARGB32);
        previewRT.antiAliasing = QualitySettings.GetQualityLevel() >= 2 ? 4 : 2;
        raw.texture = previewRT;
        var zoomHint = UIUtil.CreateText(previewRect, "Döndürmek için kaydır", new Vector2(0.5f, 0f), new Vector2(0f, 10f), new Vector2(600f, 36f), 22, TextAnchor.MiddleCenter);
        zoomHint.color = Theme.TextDim;

        // Right panel: name, stats, slots
        var panel = Theme.Box(t, "Info", new Vector2(1f, 0.5f), new Vector2(-300f, 150f), new Vector2(560f, 680f), Theme.Panel, false).transform;
        var head = UIUtil.CreateImage(panel, "Head", new Vector2(0.5f, 1f), new Vector2(0f, -36f), new Vector2(560f, 72f), Theme.Red, false);
        weaponTitle = UIUtil.CreateText(head.transform, "", new Vector2(0.5f, 0.5f), new Vector2(10f, 0f), new Vector2(520f, 70f), 30, TextAnchor.MiddleLeft);
        weaponTitle.fontStyle = FontStyle.Bold;

        for (int i = 0; i < 6; i++)
        {
            float x = i % 2 == 0 ? -135f : 135f;
            float y = 225f - (i / 2) * 72f;
            // name on the left of the bar, value at its right end (the two columns never touch)
            Theme.Label(panel, Gunsmith.StatNames[i], new Vector2(x - 20f, y + 14f), new Vector2(150f, 34f), 24, TextAnchor.MiddleLeft, Color.white, true);
            statValues[i] = Theme.Label(panel, "", new Vector2(x + 45f, y + 14f), new Vector2(100f, 34f), 26, TextAnchor.MiddleRight, Color.white, true);
            UIUtil.CreateImage(panel, "BarBg", new Vector2(0.5f, 0.5f), new Vector2(x, y - 14f), new Vector2(StatBarWidth, 8f), new Color(1f, 1f, 1f, 0.15f), false).raycastTarget = false;
            var baseBar = UIUtil.CreateImage(panel, "Bar", new Vector2(0.5f, 0.5f), new Vector2(x - StatBarWidth * 0.5f, y - 14f), new Vector2(0f, 8f), Color.white, false);
            baseBar.raycastTarget = false;
            baseBar.rectTransform.pivot = new Vector2(0f, 0.5f);
            statBase[i] = baseBar.rectTransform;
            var delta = UIUtil.CreateImage(panel, "Delta", new Vector2(0.5f, 0.5f), new Vector2(x - StatBarWidth * 0.5f, y - 14f), new Vector2(0f, 8f), Theme.Good, false);
            delta.raycastTarget = false;
            delta.rectTransform.pivot = new Vector2(0f, 0.5f);
            statDelta[i] = delta;
        }

        Theme.Label(panel, "APARATLAR (en fazla 5)", new Vector2(-110f, 32f), new Vector2(320f, 40f), 26, TextAnchor.MiddleLeft, Color.white, true);
        for (int i = 0; i < Gunsmith.MaxEquipped; i++)
        {
            var dot = UIUtil.CreateImage(panel, "Dot", new Vector2(0.5f, 0.5f), new Vector2(118f + i * 32f, 32f), new Vector2(20f, 20f), new Color(1f, 1f, 1f, 0.25f), true);
            dot.raycastTarget = false;
            dots.Add(dot);
        }

        // 9 attachment slots + camo, 5 per row.
        for (int i = 0; i <= Gunsmith.SlotCount; i++)
        {
            int index = i;
            float x = -216f + (i % 5) * 108f;
            float y = -42f - (i / 5) * 120f;
            Text label;
            var b = UIUtil.CreateButton(panel, "", new Vector2(0.5f, 0.5f), new Vector2(x, y), new Vector2(102f, 112f), Theme.PanelLight, false, 16, out label);
            b.onClick.AddListener(() => SelectSlot(index));
            bool camoSlot = i == Gunsmith.SlotCount;
            if (camoSlot)
            {
                var sw = UIUtil.CreateImage(b.transform, "CamoIcon", new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(44f, 44f), new Color(0.55f, 0.6f, 0.4f), true);
                sw.raycastTarget = false;
            }
            else
                Icons.Create(b.transform, Gunsmith.SlotIcon(Gunsmith.SlotOrder[i]), new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(48f, 48f));
            var name = UIUtil.CreateText(b.transform, camoSlot ? "KAMUFLAJ" : Gunsmith.SlotNames[(int)Gunsmith.SlotOrder[i]].ToUpper(), new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(100f, 24f), 15, TextAnchor.MiddleCenter);
            name.fontStyle = FontStyle.Bold;
            var sub = UIUtil.CreateText(b.transform, "-", new Vector2(0.5f, 0f), new Vector2(0f, 16f), new Vector2(100f, 24f), 13, TextAnchor.MiddleCenter);
            sub.color = Theme.TextDim;
            slotButtons.Add(b.GetComponent<Image>());
            slotLabels.Add(name);
            slotSubLabels.Add(sub);
        }

        // Bottom strip with options
        var stripBg = Theme.Box(t, "Strip", new Vector2(0.5f, 0f), new Vector2(0f, 115f), new Vector2(1880f, 210f), new Color(0.05f, 0.06f, 0.08f, 0.9f), true);
        strip = stripBg.rectTransform;
        stripTitle = UIUtil.CreateText(strip, "", new Vector2(0f, 1f), new Vector2(250f, 18f), new Vector2(460f, 36f), 26, TextAnchor.MiddleLeft);
        stripTitle.color = Theme.Accent;
        stripTitle.fontStyle = FontStyle.Bold;

        // Horizontally scrolling row of option cards.
        var viewport = UIUtil.CreateStretch(strip, "Viewport");
        viewport.offsetMin = new Vector2(16f, 6f);
        viewport.offsetMax = new Vector2(-16f, -8f);
        viewport.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.01f);
        viewport.gameObject.AddComponent<RectMask2D>();
        stripContent = UIUtil.CreateRect(viewport, "Content", new Vector2(0f, 0.5f), Vector2.zero, new Vector2(100f, 170f));
        stripContent.pivot = new Vector2(0f, 0.5f);
        stripScroll = viewport.gameObject.AddComponent<ScrollRect>();
        stripScroll.content = stripContent;
        stripScroll.viewport = viewport;
        stripScroll.horizontal = true;
        stripScroll.vertical = false;
        stripScroll.movementType = ScrollRect.MovementType.Clamped;
        stripScroll.scrollSensitivity = 40f;

        message = UIUtil.CreateText(t, "", new Vector2(0.5f, 0.5f), new Vector2(-150f, -190f), new Vector2(900f, 44f), 30, TextAnchor.MiddleCenter);
        message.fontStyle = FontStyle.Bold;
    }

    // ----- Preview stage -----

    private void EnsureStage()
    {
        if (stage != null)
            return;
        stage = new GameObject("GunsmithStage").transform;
        stage.position = StagePos;

        var camGo = new GameObject("GunsmithCamera");
        camGo.transform.SetParent(stage, false);
        previewCam = camGo.AddComponent<Camera>();
        previewCam.clearFlags = CameraClearFlags.SolidColor;
        previewCam.backgroundColor = new Color(0f, 0f, 0f, 0f);
        previewCam.cullingMask = 1 << PreviewLayer;
        previewCam.fieldOfView = 22f;
        previewCam.nearClipPlane = 0.05f;
        previewCam.farClipPlane = 20f;
        previewCam.targetTexture = previewRT;
        previewCam.allowHDR = false;
        previewCam.enabled = false;

        // Studio lights only for the preview layer.
        var key = new GameObject("KeyLight").AddComponent<Light>();
        key.transform.SetParent(stage, false);
        key.type = LightType.Directional;
        key.intensity = 1.25f;
        key.color = new Color(1f, 0.96f, 0.9f);
        key.cullingMask = 1 << PreviewLayer;
        key.shadows = LightShadows.None;
        key.transform.rotation = Quaternion.Euler(35f, -60f, 0f);

        var rim = new GameObject("RimLight").AddComponent<Light>();
        rim.transform.SetParent(stage, false);
        rim.type = LightType.Directional;
        rim.intensity = 0.8f;
        rim.color = new Color(0.6f, 0.75f, 1f);
        rim.cullingMask = 1 << PreviewLayer;
        rim.shadows = LightShadows.None;
        rim.transform.rotation = Quaternion.Euler(20f, 130f, 0f);

        // Keep the world's sun and main camera away from the preview layer.
        if (RenderSettings.sun != null)
            RenderSettings.sun.cullingMask &= ~(1 << PreviewLayer);
        if (Camera.main != null)
            Camera.main.cullingMask &= ~(1 << PreviewLayer);

        var gunRoot = new GameObject("PreviewGun");
        gunRoot.layer = PreviewLayer;
        gunRoot.transform.SetParent(stage, false);
        previewGun = gunRoot.AddComponent<WeaponController>();
    }

    private void RebuildPreview()
    {
        EnsureStage();
        var data = Gunsmith.Apply(Gunsmith.BaseWeapon(Gunsmith.Weapons[weaponIndex]));
        previewGun.transform.localPosition = Vector3.zero;
        previewGun.Initialize(data, null);

        // Fit the camera to the dressed gun (side view, muzzle pointing right).
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
        fitDistance = length * 0.62f / Mathf.Tan(previewCam.fieldOfView * 0.5f * Mathf.Deg2Rad) / previewCam.aspect + length * 0.5f;
        previewGun.transform.localPosition = -(b.center - previewGun.transform.position);
    }

    public void OnDrag(PointerEventData eventData)
    {
        yaw += eventData.delta.x * 0.3f;
        lastDragTime = Time.unscaledTime;
    }

    private void LateUpdate()
    {
        if (previewCam == null)
            return;
        if (Time.unscaledTime - lastDragTime > 2f)
            yaw = Mathf.LerpAngle(yaw, -20f + Mathf.Sin(Time.unscaledTime * 0.6f) * 22f, Time.unscaledDeltaTime * 1.5f);
        stage.rotation = Quaternion.identity;
        Quaternion spin = Quaternion.Euler(0f, yaw, 0f);
        previewCam.transform.position = stage.position + spin * new Vector3(fitDistance, 0.06f, 0f);
        previewCam.transform.LookAt(stage.position);

        if (message.text.Length > 0 && Time.unscaledTime > messageUntil)
            message.text = "";
    }

    // ----- Open / close -----

    public void Open()
    {
        gameObject.SetActive(true);
        EnsureStage();
        stage.gameObject.SetActive(true);
        previewCam.enabled = true;
        SelectWeapon(weaponIndex);
    }

    /// <summary>Hides without side effects (used when other screens open).</summary>
    public void Hide()
    {
        if (previewCam != null)
            previewCam.enabled = false;
        if (stage != null)
            stage.gameObject.SetActive(false);   // its studio lights too (the lucky draw has its own)
        if (previewRT != null)
            previewRT.Release();   // frees the MSAA/depth buffers; recreated on the next render
        gameObject.SetActive(false);
    }

    public void Close()
    {
        Hide();
        var gm = GameManager.Instance;
        if (gm != null)
        {
            gm.RefreshLobbyWeapon();
            gm.uiManager.ShowLobby();
        }
    }

    // ----- Selection -----

    private void SelectWeapon(int index)
    {
        weaponIndex = index;
        slotIndex = -1;
        for (int i = 0; i < categoryButtons.Count; i++)
        {
            bool sel = i == index;
            categoryButtons[i].color = sel ? Theme.Selected : Theme.Panel;
            categoryLabels[i].color = sel ? new Color(0.08f, 0.08f, 0.1f) : Color.white;
            categoryLabels[i].GetComponent<Shadow>().enabled = !sel;
        }
        RebuildPreview();
        Refresh();
    }

    private void SelectSlot(int index)
    {
        slotIndex = slotIndex == index ? -1 : index;
        Refresh();
    }

    private void Toast(string text, Color color)
    {
        message.text = text;
        message.color = color;
        messageUntil = Time.unscaledTime + 2.5f;
    }

    // ----- Refresh everything -----

    private void Refresh()
    {
        var gm = GameManager.Instance;
        var profile = gm.profile;
        WeaponType w = Gunsmith.Weapons[weaponIndex];
        var baseData = Gunsmith.BaseWeapon(w);
        var current = Gunsmith.Apply(baseData);
        var loadout = Gunsmith.Loadout(w);
        var camo = Gunsmith.FindCamo(current.camo);

        coinsText.text = "KREDİ  " + profile.coins.ToString("N0");
        string model = ModelLibrary.SelectedGunSkin(w);
        weaponTitle.text = (model.Length == 0 ? baseData.weaponName : ModelLibrary.GunSkinName(w, model)) + (string.IsNullOrEmpty(camo.id) ? "" : "  -  " + camo.name);

        float[] before = Gunsmith.Stats(baseData);
        float[] after = Gunsmith.Stats(current);
        for (int i = 0; i < 6; i++)
        {
            float lo = Mathf.Min(before[i], after[i]);
            float hi = Mathf.Max(before[i], after[i]);
            statValues[i].text = Mathf.RoundToInt(after[i]).ToString();
            statValues[i].color = after[i] > before[i] + 0.5f ? Theme.Good : (after[i] < before[i] - 0.5f ? Theme.Bad : Color.white);
            statBase[i].sizeDelta = new Vector2(StatBarWidth * lo / 100f, 8f);
            var d = statDelta[i].rectTransform;
            d.anchoredPosition = new Vector2(statBase[i].anchoredPosition.x + StatBarWidth * lo / 100f, statBase[i].anchoredPosition.y);
            d.sizeDelta = new Vector2(StatBarWidth * (hi - lo) / 100f, 8f);
            statDelta[i].color = after[i] >= before[i] ? Theme.Good : Theme.Bad;
        }

        int equipped = 0;
        for (int i = 0; i < Gunsmith.SlotCount; i++)
        {
            var a = Gunsmith.FindAttachment(current.attachments[(int)Gunsmith.SlotOrder[i]]);
            if (a != null)
                equipped++;
            slotSubLabels[i].text = a != null ? a.name : "-";
            slotSubLabels[i].color = a != null ? Theme.Accent : Theme.TextDim;
        }
        slotSubLabels[Gunsmith.SlotCount].text = camo.name;
        slotSubLabels[Gunsmith.SlotCount].color = string.IsNullOrEmpty(camo.id) ? Theme.TextDim : Theme.Rarity(camo.rarity);
        for (int i = 0; i < dots.Count; i++)
            dots[i].color = i < equipped ? Theme.Accent : new Color(1f, 1f, 1f, 0.25f);
        for (int i = 0; i < slotButtons.Count; i++)
            slotButtons[i].color = i == slotIndex ? Theme.AccentDark : Theme.PanelLight;

        BuildStrip(w, loadout, profile);
    }

    private void ClearStrip()
    {
        for (int i = stripContent.childCount - 1; i >= 0; i--)
            Destroy(stripContent.GetChild(i).gameObject);
    }

    private void BuildStrip(WeaponType w, string[] loadout, ProfileData profile)
    {
        ClearStrip();
        if (slotIndex < 0)
        {
            // Gun models of the class (aparat yuvası seçilince yerini aparatlara bırakır).
            string[] skins = ModelLibrary.GunSkins(w);
            string selected = ModelLibrary.SelectedGunSkin(w);
            stripTitle.text = Gunsmith.CategoryNames[weaponIndex] + "  •  " + skins.Length + " MODEL   (aparat için yukarıdan bir yuva seç)";
            stripTitle.rectTransform.sizeDelta = new Vector2(1100f, 36f);
            stripTitle.rectTransform.anchoredPosition = new Vector2(570f, 18f);
            const float mw = 270f;
            for (int i = 0; i < skins.Length; i++)
            {
                string skin = skins[i];
                ModelCard(i, mw, w, skin, skin == selected);
            }
            FinishStrip(skins.Length, mw);
            return;
        }
        stripTitle.rectTransform.sizeDelta = new Vector2(460f, 36f);
        stripTitle.rectTransform.anchoredPosition = new Vector2(250f, 18f);

        if (slotIndex == Gunsmith.SlotCount)
        {
            stripTitle.text = "KAMUFLAJLAR  (" + Gunsmith.Camos.Count + ")";
            var camos = Gunsmith.Camos;
            float width = 200f;
            for (int i = 0; i < camos.Count; i++)
            {
                var c = camos[i];
                bool owned = Gunsmith.OwnsCamo(c.id);
                bool on = loadout[Gunsmith.CamoIndex] == c.id;
                string status = on ? "KUŞANILDI" : (owned ? "KUŞAN" : (c.drawOnly ? "ÇEKİLİŞTE" : c.price + " Kredi"));
                var card = Card(i, camos.Count, width, c.name, c.rarity, status, Theme.Rarity(c.rarity), on, owned || (!c.drawOnly && profile.coins >= c.price));
                if (!string.IsNullOrEmpty(c.id))
                {
                    var sw = UIUtil.CreateRawSwatch(card, WeaponDressing.Pattern(c), new Vector2(0f, 6f), new Vector2(width - 24f, 44f));
                    sw.raycastTarget = false;
                }
                var captured = c;
                card.GetComponent<Button>().onClick.AddListener(() => ChooseCamo(w, captured));
            }
            FinishStrip(camos.Count, width);
            return;
        }

        var slot = Gunsmith.SlotOrder[slotIndex];
        int si = (int)slot;
        stripTitle.text = Gunsmith.SlotNames[si].ToUpper() + "   •   " + Gunsmith.EquippedCount(loadout) + "/" + Gunsmith.MaxEquipped + " takılı";
        var options = Gunsmith.Options(w, slot);
        int total = options.Count + 1;
        float cw = 300f;

        // "None" card
        bool noneOn = string.IsNullOrEmpty(loadout[si]) || Gunsmith.FindAttachment(loadout[si]) == null;
        var none = Card(0, total, cw, "Yok", "", noneOn ? "TAKILI" : "ÇIKAR", new Color(0.5f, 0.5f, 0.55f), noneOn, true);
        none.GetComponent<Button>().onClick.AddListener(() => ChooseAttachment(w, slot, null));

        for (int i = 0; i < options.Count; i++)
        {
            var a = options[i];
            bool owned = Gunsmith.OwnsAttachment(a.id);
            bool on = loadout[si] == a.id;
            string status = on ? "TAKILI" : (owned ? "TAK" : a.price + " Kredi");
            var card = Card(i + 1, total, cw, a.name, Describe(a), status, Theme.Accent, on, owned || profile.coins >= a.price);
            var captured = a;
            card.GetComponent<Button>().onClick.AddListener(() => ChooseAttachment(w, slot, captured));
        }
        FinishStrip(total, cw);
    }

    /// <summary>Sizes the scrolling row to its cards and scrolls back to the start.</summary>
    private int stripShownFor = -2;

    private void FinishStrip(int count, float width)
    {
        stripContent.sizeDelta = new Vector2(count * (width + 10f) + 10f, 170f);
        int key = weaponIndex * 100 + slotIndex;
        if (key != stripShownFor)
        {
            // Back to the start only when a different slot opens (not after equipping something).
            stripShownFor = key;
            stripContent.anchoredPosition = Vector2.zero;
            if (stripScroll != null)
                stripScroll.StopMovement();
        }
    }

    private static string Describe(AttachmentDef a)
    {
        var parts = new List<string>();
        Add(parts, a.damage, "Hasar");
        Add(parts, a.fireRate, "Atış");
        Add(parts, a.accuracy, "İsabet");
        Add(parts, a.mobility, "Mobilite");
        Add(parts, a.range, "Menzil");
        Add(parts, a.control, "Kontrol");
        Add(parts, a.magazine, "Şarjör");
        Add(parts, a.reload, "Doldurma");
        if (a.suppressor)
            parts.Add("Sessiz");
        return string.Join("  ", parts.ToArray());
    }

    private static void Add(List<string> parts, float v, string name)
    {
        if (Mathf.Abs(v) < 0.5f)
            return;
        parts.Add((v > 0 ? "<color=#6fe37f>+" : "<color=#ff6b5e>") + Mathf.RoundToInt(v) + " " + name + "</color>");
    }

    private RectTransform Card(int index, int total, float width, string title, string sub, string status, Color accent, bool selected, bool affordable)
    {
        Text label;
        var b = UIUtil.CreateButton(stripContent, "", new Vector2(0f, 0.5f), new Vector2(10f + width * 0.5f + index * (width + 10f), -14f), new Vector2(width, 160f),
            selected ? new Color(0.25f, 0.22f, 0.08f, 1f) : Theme.PanelLight, false, 20, out label);
        b.name = "Card" + index;
        var rect = (RectTransform)b.transform;

        var bar = UIUtil.CreateImage(rect, "Bar", new Vector2(0.5f, 1f), new Vector2(0f, -3f), new Vector2(width, 6f), accent, false);
        bar.raycastTarget = false;
        var t = UIUtil.CreateText(rect, title, new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(width - 16f, 36f), 24, TextAnchor.MiddleCenter);
        t.fontStyle = FontStyle.Bold;
        var s = UIUtil.CreateText(rect, sub, new Vector2(0.5f, 0.5f), new Vector2(0f, 6f), new Vector2(width - 16f, 60f), 18, TextAnchor.MiddleCenter);
        s.horizontalOverflow = HorizontalWrapMode.Wrap;
        s.supportRichText = true;
        s.color = Theme.TextDim;
        var st = UIUtil.CreateText(rect, status, new Vector2(0.5f, 0f), new Vector2(0f, 22f), new Vector2(width - 16f, 32f), 22, TextAnchor.MiddleCenter);
        st.fontStyle = FontStyle.Bold;
        st.color = selected ? Theme.Accent : (affordable ? Color.white : Theme.Bad);
        return rect;
    }

    /// <summary>A gun model card: side-view picture (Resources/UI/Guns), name, and SEÇİLİ / SEÇ.</summary>
    private void ModelCard(int index, float width, WeaponType w, string skin, bool selected)
    {
        Text label;
        var b = UIUtil.CreateButton(stripContent, "", new Vector2(0f, 0.5f), new Vector2(10f + width * 0.5f + index * (width + 10f), -14f), new Vector2(width, 160f),
            selected ? new Color(0.25f, 0.22f, 0.08f, 1f) : Theme.PanelLight, false, 20, out label);
        b.name = "Model" + index;
        var rect = (RectTransform)b.transform;
        var bar = UIUtil.CreateImage(rect, "Bar", new Vector2(0.5f, 1f), new Vector2(0f, -3f), new Vector2(width, 6f), selected ? Theme.Accent : new Color(1f, 1f, 1f, 0.2f), false);
        bar.raycastTarget = false;
        var tex = Resources.Load<Texture2D>("UI/Guns/" + (skin.Length == 0 ? w.ToString() : w + "_" + skin));
        if (tex != null)
        {
            var pic = UIUtil.CreateRect(rect, "Picture", new Vector2(0.5f, 1f), new Vector2(0f, -58f), new Vector2(width - 20f, (width - 20f) * 0.5f * 0.82f));
            var raw = pic.gameObject.AddComponent<RawImage>();
            raw.texture = tex;
            raw.raycastTarget = false;
        }
        var name = UIUtil.CreateText(rect, ModelLibrary.GunSkinName(w, skin), new Vector2(0.5f, 0f), new Vector2(0f, 48f), new Vector2(width - 16f, 30f), 22, TextAnchor.MiddleCenter);
        name.fontStyle = FontStyle.Bold;
        var st = UIUtil.CreateText(rect, selected ? "SEÇİLİ" : "SEÇ", new Vector2(0.5f, 0f), new Vector2(0f, 18f), new Vector2(width - 16f, 28f), 20, TextAnchor.MiddleCenter);
        st.fontStyle = FontStyle.Bold;
        st.color = selected ? Theme.Accent : Theme.TextDim;
        b.onClick.AddListener(() => ChooseModel(w, skin));
    }

    private void ChooseModel(WeaponType w, string skin)
    {
        if (ModelLibrary.SelectedGunSkin(w) == skin)
            return;
        ModelLibrary.SelectGunSkin(w, skin);
        Sfx.Play(SoundBank.Reload, 0.35f, 1.15f);
        Toast(ModelLibrary.GunSkinName(w, skin) + " seçildi", Theme.Good);
        RebuildPreview();
        Refresh();
    }

    private void OpenDraw()
    {
        var gm = GameManager.Instance;
        if (gm == null)
            return;
        Hide();
        gm.uiManager.OpenLuckyDraw(() =>
        {
            Open();
        });
    }

    private void ChooseAttachment(WeaponType w, AttachmentSlot slot, AttachmentDef a)
    {
        var profile = GameManager.Instance.profile;
        var current = Gunsmith.Loadout(w);
        if (a != null && Gunsmith.FindAttachment(current[(int)slot]) == null && Gunsmith.EquippedCount(current) >= Gunsmith.MaxEquipped)
        {
            Toast("En fazla " + Gunsmith.MaxEquipped + " aparat takılabilir — önce birini çıkar", Theme.Bad);
            return;
        }
        if (a != null && !Gunsmith.OwnsAttachment(a.id))
        {
            if (!Gunsmith.Buy(profile, a.id, a.price, false))
            {
                Toast("Yetersiz Kredi! Gereken: " + a.price, Theme.Bad);
                return;
            }
            Sfx.Play(SoundBank.Pickup, 0.6f);
            Toast(a.name + " satın alındı", Theme.Good);
        }
        if (!Gunsmith.Equip(w, slot, a != null ? a.id : ""))
        {
            Toast("En fazla " + Gunsmith.MaxEquipped + " aparat takılabilir — önce birini çıkar", Theme.Bad);
            return;
        }
        Sfx.Play(SoundBank.Reload, 0.4f, 1.3f);
        RebuildPreview();
        Refresh();
    }

    private void ChooseCamo(WeaponType w, CamoDef c)
    {
        var profile = GameManager.Instance.profile;
        if (!Gunsmith.OwnsCamo(c.id) && c.drawOnly)
        {
            Toast(c.name + " yalnızca ŞANS ÇEKİLİŞİ'nden çıkar", Theme.Accent);
            return;
        }
        if (!Gunsmith.OwnsCamo(c.id))
        {
            if (!Gunsmith.Buy(profile, c.id, c.price, true))
            {
                Toast("Yetersiz Kredi! Gereken: " + c.price, Theme.Bad);
                return;
            }
            Sfx.Play(SoundBank.Pickup, 0.6f);
            Toast(c.name + " açıldı", Theme.Good);
        }
        Gunsmith.EquipCamo(w, c.id);
        RebuildPreview();
        Refresh();
    }
}
