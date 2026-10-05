using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Full-screen settings in the style of big mobile shooters: categories on the left
/// (Temel, Kontroller, Ses ve Grafikler, Hassasiyet, Künye), rows of segmented toggles,
/// option pickers and sliders on the right, VARSAYILAN (defaults) at the bottom.
/// Every change is saved and applied at once.
/// </summary>
public class SettingsScreen : MonoBehaviour
{
    private static readonly string[] Pages = { "TEMEL", "KONTROLLER", "SES VE GRAFİKLER", "HASSASİYET", "KÜNYE" };

    private static readonly Color Back = new Color(0.08f, 0.1f, 0.13f, 0.97f);
    private static readonly Color RowColor = new Color(0.15f, 0.17f, 0.21f, 0.96f);
    private static readonly Color SegOn = new Color(0.45f, 0.47f, 0.52f, 1f);
    private static readonly Color SegOff = new Color(0.1f, 0.11f, 0.14f, 1f);
    private static readonly Color TabOn = new Color(0.9f, 0.91f, 0.93f, 1f);
    private static readonly Color TabOff = new Color(0.17f, 0.19f, 0.23f, 1f);
    private static readonly Color Cyan = new Color(0.72f, 0.9f, 0.92f, 1f);

    private readonly List<Image> tabImages = new List<Image>();
    private readonly List<Text> tabTexts = new List<Text>();
    private readonly List<GameObject> tabAccents = new List<GameObject>();
    private RectTransform content;
    private ScrollRect scroll;
    private float cursorY;
    private int page;
    private UnityAction onClose;

    // Refreshers for the rows on the current page (re-read the settings after a change).
    private readonly List<System.Action> refreshers = new List<System.Action>();

    public static SettingsScreen Create(Transform canvas)
    {
        var rect = UIUtil.CreateStretch(canvas, "SettingsScreen");
        var img = rect.gameObject.AddComponent<Image>();
        img.color = Back;
        var s = rect.gameObject.AddComponent<SettingsScreen>();
        s.Build();
        rect.gameObject.SetActive(false);
        return s;
    }

    private void Build()
    {
        var t = transform;
        Text unused;

        // Header: back arrow + title
        var back = UIUtil.CreateButton(t, "<", new Vector2(0f, 1f), new Vector2(80f, -70f), new Vector2(84f, 84f), Theme.Accent, false, 54, out unused);
        unused.color = new Color(0.1f, 0.1f, 0.1f);
        unused.GetComponent<Shadow>().enabled = false;
        back.onClick.AddListener(Close);
        var title = UIUtil.CreateText(t, "AYARLAR", new Vector2(0f, 1f), new Vector2(330f, -70f), new Vector2(400f, 80f), 52, TextAnchor.MiddleLeft);
        title.fontStyle = FontStyle.Bold;
        UIUtil.CreateImage(t, "TitleLine", new Vector2(0f, 1f), new Vector2(330f, -112f), new Vector2(400f, 3f), new Color(1f, 1f, 1f, 0.25f), false).raycastTarget = false;

        // Left: categories
        for (int i = 0; i < Pages.Length; i++)
        {
            int index = i;
            Text label;
            var b = UIUtil.CreateButton(t, Pages[i], new Vector2(0f, 1f), new Vector2(200f, -200f - i * 104f), new Vector2(300f, 90f), TabOff, false, 30, out label);
            b.onClick.AddListener(() => ShowPage(index));
            tabImages.Add(b.GetComponent<Image>());
            tabTexts.Add(label);
            var accent = UIUtil.CreateImage(b.transform, "Accent", new Vector2(1f, 0.5f), new Vector2(-4f, 0f), new Vector2(8f, 90f), Theme.Accent, false);
            accent.raycastTarget = false;
            tabAccents.Add(accent.gameObject);
        }

        // Right: scrolling list of rows
        var area = UIUtil.CreateStretch(t, "Area");
        area.offsetMin = new Vector2(380f, 130f);
        area.offsetMax = new Vector2(-40f, -150f);
        var areaImg = area.gameObject.AddComponent<Image>();
        areaImg.color = new Color(0.12f, 0.14f, 0.17f, 0.9f);
        area.gameObject.AddComponent<RectMask2D>();
        content = UIUtil.CreateRect(area, "Content", new Vector2(0.5f, 1f), Vector2.zero, new Vector2(100f, 100f));
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.offsetMin = new Vector2(16f, content.offsetMin.y);
        content.offsetMax = new Vector2(-16f, content.offsetMax.y);
        scroll = area.gameObject.AddComponent<ScrollRect>();
        scroll.content = content;
        scroll.viewport = area;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 40f;

        // Bottom-right: defaults
        var def = UIUtil.CreateButton(t, "VARSAYILAN", new Vector2(1f, 0f), new Vector2(-170f, 66f), new Vector2(260f, 76f), Cyan, false, 30, out unused);
        unused.color = new Color(0.08f, 0.12f, 0.14f);
        unused.GetComponent<Shadow>().enabled = false;
        def.onClick.AddListener(() =>
        {
            if (page < 4)
            {
                GameSettings.ResetPage(page);
                ShowPage(page);
            }
        });
    }

    public void Open(UnityAction closed)
    {
        onClose = closed;
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        ShowPage(page);
    }

    public void Close()
    {
        gameObject.SetActive(false);
        if (onClose != null)
            onClose();
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    // ----- pages -----

    private void ShowPage(int index)
    {
        page = index;
        for (int i = 0; i < tabImages.Count; i++)
        {
            bool on = i == index;
            tabImages[i].color = on ? TabOn : TabOff;
            tabTexts[i].color = on ? new Color(0.12f, 0.12f, 0.14f) : Color.white;
            tabTexts[i].GetComponent<Shadow>().enabled = !on;
            tabAccents[i].SetActive(on);
        }

        for (int i = content.childCount - 1; i >= 0; i--)
        {
            var c = content.GetChild(i).gameObject;
            c.SetActive(false);
            Destroy(c);
        }
        refreshers.Clear();
        cursorY = -10f;

        switch (index)
        {
            case 0: BuildBasic(); break;
            case 1: BuildControls(); break;
            case 2: BuildGraphics(); break;
            case 3: BuildSensitivity(); break;
            default: BuildCredits(); break;
        }
        content.sizeDelta = new Vector2(content.sizeDelta.x, -cursorY + 20f);
        content.anchoredPosition = Vector2.zero;
        RefreshAll();
    }

    private void Changed()
    {
        GameSettings.Save();
        RefreshAll();
    }

    private void RefreshAll()
    {
        foreach (var r in refreshers)
            r();
    }

    private void BuildBasic()
    {
        Header("SİLAH AYARLARI");
        Segments("NİŞAN YARDIMI", new[] { "AÇIK", "KAPALI" }, () => GameSettings.AimAssist ? 0 : 1, v => GameSettings.AimAssist = v == 0);
        FireModeCards();
        bool custom = GameSettings.FirePreset == 3;
        if (custom)
        {
            string[] cats = { "Taarruz Tüfekleri", "Hafif Makineliler", "Pompalılar", "Keskin Nişancılar", "Tabancalar" };
            WeaponType[] types = { WeaponType.Rifle, WeaponType.SMG, WeaponType.Shotgun, WeaponType.Sniper, WeaponType.Pistol };
            for (int i = 0; i < cats.Length; i++)
            {
                int t = (int)types[i];
                Segments("   " + cats[i], new[] { "Tek Dokunuşla Nişangâh", "NİŞAN ALMADAN ATIŞ" },
                    () => GameSettings.CustomFire[t], v => GameSettings.CustomFire[t] = v, true);
            }
        }
        Note("Tek dokunuşla nişangâh: ateş düğmesine basınca nişangâh açılır.  Otomatik: nişangâh düşmanın üstüne gelince kendiliğinden ateş eder.");

        Header("NİŞANGÂH VE GÖSTERGELER");
        Segments("NİŞANGÂH ŞEKLİ", Crosshair.StyleNames, () => GameSettings.CrosshairStyle, v => GameSettings.CrosshairStyle = v, true);
        Segments("NİŞANGÂH RENGİ", GameSettings.CrosshairNames, () => GameSettings.CrosshairColor, v => GameSettings.CrosshairColor = v, true);
        Segments("HASAR SAYILARI", new[] { "AÇIK", "KAPALI" }, () => GameSettings.DamageNumbers ? 0 : 1, v => GameSettings.DamageNumbers = v == 0);

        Header("HATA MODU");
        Segments("HATA MODU (FPS ve hata kodları)", new[] { "AÇIK", "KAPALI" }, () => ErrorReporter.DebugMode ? 0 : 1, v => ErrorReporter.SetDebugMode(v == 0));
        ButtonRow("HATA EKRANI", "AÇ", () => { if (ErrorReporter.Instance != null) ErrorReporter.Instance.OpenPanel(); });
    }

    private void BuildControls()
    {
        Header("KONTROLLER");
        Segments("SABİT SAĞ ATEŞ DÜĞMESİ", new[] { "SABİT POZİSYON", "TAKİP ATIŞI" }, () => GameSettings.FireButtonFollow ? 1 : 0, v => GameSettings.FireButtonFollow = v == 1);
        Segments("KAMERAYI DÖNDÜRMEK İÇİN SAĞ ATEŞ DÜĞMESİ", new[] { "AÇIK", "KAPALI" }, () => GameSettings.FireButtonLook ? 0 : 1, v => GameSettings.FireButtonLook = v == 0);
        Segments("SOL OYUN KOLU MODU", new[] { "SABİT POZİSYON", "SOL KONTROL" }, () => GameSettings.JoystickMode, v => GameSettings.JoystickMode = v);
        Segments("SOL ATEŞ DÜĞMESİNİ GÖSTER", new[] { "AÇIK", "KAPALI" }, () => GameSettings.LeftFireButton ? 0 : 1, v => GameSettings.LeftFireButton = v == 0);
        Slider("DÜĞME GÖRÜNÜRLÜĞÜ", 30f, 100f, 5f, () => GameSettings.ButtonOpacity * 100f, v => GameSettings.ButtonOpacity = v / 100f, "0'%'");
        ActionRow("DÜĞME YERLEŞİMİ (HUD)", "DÜZENLE", () => HudEditorScreen.Open(transform.parent));

        Header("HİSSİYAT");
        Segments("TİTREŞİM", new[] { "AÇIK", "KAPALI" }, () => GameSettings.Vibration ? 0 : 1, v => GameSettings.Vibration = v == 0);
        Segments("ARAYÜZ SESLERİ", new[] { "AÇIK", "KAPALI" }, () => GameSettings.UiSounds ? 0 : 1, v => GameSettings.UiSounds = v == 0);
        Segments("KAMERA SARSINTISI", new[] { "AÇIK", "KAPALI" }, () => GameSettings.CameraShake ? 0 : 1, v => GameSettings.CameraShake = v == 0);
    }

    private void BuildGraphics()
    {
        Header("GRAFİK KALİTESİ");
        Segments("GRAFİK KALİTESİ", GameSettings.QualityNames, () => GameSettings.Quality, v =>
        {
            GameSettings.Quality = v;
            GameSettings.Shadows = v > 0;
            GameSettings.Bloom = v > 0;
            GameSettings.ViewDistance = Mathf.Min(v, 3);
            GameSettings.GrassDensity = v == 0 ? 0 : (v >= 3 ? 3 : 2);
            if (v >= 4)
            {
                GameSettings.AntiAliasing = true;
                GameSettings.RenderScale = 1f;
            }
        });
        Segments("KARE HIZI", GameSettings.FrameRateNames, () => GameSettings.FrameRate, v => GameSettings.FrameRate = v);
        Segments("DÜZGÜNLEŞTİRME", new[] { "AÇIK", "KAPALI" }, () => GameSettings.AntiAliasing ? 0 : 1, v => GameSettings.AntiAliasing = v == 0);
        Segments("GERÇEK ZAMANLI GÖLGELER", new[] { "AÇIK", "KAPALI" }, () => GameSettings.Shadows ? 0 : 1, v => GameSettings.Shadows = v == 0);
        Segments("PARLAKLIK (BLOOM)", new[] { "AÇIK", "KAPALI" }, () => GameSettings.Bloom ? 0 : 1, v => GameSettings.Bloom = v == 0);
        Segments("GÖRÜŞ MESAFESİ", GameSettings.ViewDistanceNames, () => GameSettings.ViewDistance, v => GameSettings.ViewDistance = v);
        Segments("ÇİMEN YOĞUNLUĞU", GameSettings.GrassNames, () => GameSettings.GrassDensity, v => GameSettings.GrassDensity = v);
        Slider("ÇÖZÜNÜRLÜK", 50f, 100f, 5f, () => GameSettings.RenderScale * 100f, v => GameSettings.RenderScale = v / 100f, "0'%'");
        Note("ULTRA: ekranın tam çözünürlüğü, 2K dokular, 4x kenar yumuşatma, uzak ve keskin gölgeler. Telefon ısınırsa veya FPS düşerse kaliteyi YÜKSEK ya da ORTA yap.");

        Header("SES");
        Slider("ANA SES", 0f, 100f, 5f, () => GameSettings.Volume * 100f, v => GameSettings.Volume = v / 100f, "0'%'");
    }

    private void BuildSensitivity()
    {
        Header("DÖNME MODU");
        Segments("DÖNME MODU", new[] { "SABİT", "HIZ İVMESİ" }, () => GameSettings.RotationMode, v => GameSettings.RotationMode = v);
        Slider("   İVME DEĞERİ", 0f, 200f, 1f, () => GameSettings.Acceleration, v => GameSettings.Acceleration = Mathf.RoundToInt(v), "0");

        Header("GÖRÜŞ");
        Slider("GÖRÜŞ AÇISI (FOV)", 60f, 90f, 1f, () => GameSettings.Fov, v => GameSettings.Fov = Mathf.RoundToInt(v), "0");

        Header("HASSASİYET");
        Slider("KAMERA HASSASİYETİ", 40f, 200f, 1f, () => GameSettings.Sensitivity * 100f, v => GameSettings.Sensitivity = v / 100f, "0");
        Slider("NİŞANGÂH HASSASİYETİ", 30f, 200f, 1f, () => GameSettings.AdsSensitivity * 100f, v => GameSettings.AdsSensitivity = v / 100f, "0");
        Slider("DÜRBÜN HASSASİYETİ (3x / 6x)", 30f, 200f, 1f, () => GameSettings.ScopeSensitivity * 100f, v => GameSettings.ScopeSensitivity = v / 100f, "0");

        Header("JİROSKOP");
        Segments("JİROSKOP", GameSettings.GyroNames, () => GameSettings.Gyro, v => GameSettings.Gyro = v);
        Slider("JİROSKOP HASSASİYETİ", 30f, 250f, 1f, () => GameSettings.GyroSensitivity * 100f, v => GameSettings.GyroSensitivity = v / 100f, "0");
        if (!SystemInfo.supportsGyroscope)
            Note("Bu telefonda jiroskop bulunamadı.");
    }

    private void BuildCredits()
    {
        Header("YAPIM");
        Note("Yapımcı: Olcay Yasin Dünder");
        Header("3D MODELLER (CC-BY 4.0)");
        Note("Kasap Leydi karakteri – \"Lady Butcher (WIP)\", Loves_Art (sketchfab.com/Loves_Art)");
        Note("Alev Kartalı tabanca – \"Custom Desert Eagle – Flame Edition\", Fevzi_Beydili");
        Note("AK-19 Taktik tüfek – \"low-poly AK-19\", D_U (sketchfab.com/DU1701)");
        Note("Gölge Avcı keskin nişancı – \"Sniper\", PSICOPATO (sketchfab.com/emily.archeo)");
        Note("Taktik kask ve yelek – \"Tactical Helmet with Headset\", \"Tactical Plate Carrier Vest\", Exactly (sketchfab.com/txyrm70)");
        Note("Operatör (Çöl/Gece) – \"Soldier Full Tactical Gear (LowPolyGameReady)\", DanlyVostok (skfb.ly/pMDAV)");
        Note("Piyade, Orman Piyadesi – \"Ukrainian Soldier\", doctortex (skfb.ly/ot9Ny)");
        Note("Paralı Asker, Kent Komandosu – \"Terrorista\", jeferson (skfb.ly/6xsAy)");
        Note("Maskeli – \"terrorist\", DJMaesen (skfb.ly/6AnKG)");
        Note("SWAT ve Özel Tim – \"S.W.A.T. Operator\", \"S.W.A.T. Operator- 4k Followers Special Remaster\", Mateusz Woliński (sketchfab.com/jeandiz)");
        Note("AR-15 Saha tüfek, Gravürlü 1911 tabanca – \"AR-15 style rifle\", \"Pistol with Engravings\", Mateusz Woliński (sketchfab.com/jeandiz)");
        Note("Nova (Kızıl/Gece/Orman) – \"Free Test Character Asuna\", Markus Schüler / MSGDI (sketchfab.com/MSGDI)");
        Note("Lisans: creativecommons.org/licenses/by/4.0 – modeller oyun için küçültüldü, yeniden boyandı ve animasyonları aktarıldı.");
        Header("CC0 VE HARİTA");
        Note("Karakterler, silahlar, siperler: Quaternius (CC0)");
        Note("Zemin, cephe, asfalt, kiremit, beton dokuları: Poly Haven (CC0) – Rob Tuytel, Amal Kumar, Stephan Seeliger, Dimitrios Savva, Rico Cilliers, Jenelle van Heerden");
        Note("Yazı tipi: Barlow Condensed (SIL Open Font License)");
        Note("Haritalar: Ekşioğlu (Çekmeköy), Senir Kasabası (Keçiborlu), Fırat Üniversitesi (Elazığ) – © OpenStreetMap katkıcıları (ODbL)");
        Note("Arazi yükseltisi: AWS Terrain Tiles (Mapzen, SRTM)");
    }

    // ----- row builders -----

    /// <summary>Small grey diamond at the start of a row (the "favourite" marker of the reference UI).</summary>
    private static void Diamond(RectTransform row, Vector2 anchor, Vector2 pos)
    {
        var d = UIUtil.CreateImage(row, "Mark", anchor, pos, new Vector2(16f, 16f), new Color(0.6f, 0.62f, 0.66f), false);
        d.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
        d.raycastTarget = false;
    }

    private RectTransform Row(float height)
    {
        var row = UIUtil.CreateRect(content, "Row", new Vector2(0.5f, 1f), new Vector2(0f, cursorY - height * 0.5f), new Vector2(100f, height));
        row.anchorMin = new Vector2(0f, 1f);
        row.anchorMax = new Vector2(1f, 1f);
        row.offsetMin = new Vector2(0f, row.offsetMin.y);
        row.offsetMax = new Vector2(0f, row.offsetMax.y);
        cursorY -= height + 8f;
        return row;
    }

    private void Header(string text)
    {
        var row = Row(70f);
        var t = UIUtil.CreateText(row, text, new Vector2(0f, 0.5f), new Vector2(400f, -4f), new Vector2(780f, 60f), 36, TextAnchor.MiddleLeft);
        t.fontStyle = FontStyle.Bold;
        var line = UIUtil.CreateImage(row, "Line", new Vector2(0.5f, 0f), Vector2.zero, new Vector2(100f, 3f), new Color(1f, 1f, 1f, 0.2f), false);
        line.rectTransform.anchorMin = new Vector2(0f, 0f);
        line.rectTransform.anchorMax = new Vector2(1f, 0f);
        line.rectTransform.sizeDelta = new Vector2(0f, 3f);
        line.raycastTarget = false;
    }

    private void Note(string text)
    {
        var row = Row(56f);
        var t = UIUtil.CreateText(row, text, new Vector2(0f, 0.5f), new Vector2(30f, 0f), new Vector2(1400f, 50f), 24, TextAnchor.MiddleLeft);
        t.rectTransform.pivot = new Vector2(0f, 0.5f);
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.color = Theme.TextDim;
    }

    private RectTransform LabeledRow(string label, out RectTransform right)
    {
        var row = Row(84f);
        var bg = row.gameObject.AddComponent<Image>();
        bg.color = RowColor;
        Diamond(row, new Vector2(0f, 0.5f), new Vector2(34f, 0f));
        var t = UIUtil.CreateText(row, label, new Vector2(0f, 0.5f), new Vector2(64f, 0f), new Vector2(760f, 60f), 30, TextAnchor.MiddleLeft);
        t.rectTransform.pivot = new Vector2(0f, 0.5f);
        right = UIUtil.CreateRect(row, "Right", new Vector2(1f, 0.5f), new Vector2(-330f, 0f), new Vector2(640f, 64f));
        return row;
    }

    private void ActionRow(string label, string buttonText, System.Action action)
    {
        RectTransform right;
        LabeledRow(label, out right);
        Text text;
        var b = UIUtil.CreateButton(right, buttonText, new Vector2(1f, 0.5f), new Vector2(-130f, 0f), new Vector2(260f, 56f), Cyan, false, 28, out text);
        text.color = new Color(0.08f, 0.12f, 0.14f);
        text.GetComponent<Shadow>().enabled = false;
        b.onClick.AddListener(() => action());
    }

    private void Segments(string label, string[] options, System.Func<int> get, System.Action<int> set, bool small = false)
    {
        RectTransform right;
        LabeledRow(label, out right);
        var frame = right.gameObject.AddComponent<Image>();
        frame.color = new Color(0.06f, 0.07f, 0.09f, 1f);
        float w = 640f / options.Length;
        var images = new Image[options.Length];
        var texts = new Text[options.Length];
        for (int i = 0; i < options.Length; i++)
        {
            int index = i;
            Text label2;
            var b = UIUtil.CreateButton(right, options[i], new Vector2(0f, 0.5f), new Vector2(w * (i + 0.5f), 0f), new Vector2(w - 6f, 56f), SegOff, false, small || options.Length > 2 ? 22 : 28, out label2);
            b.onClick.AddListener(() => { set(index); Changed(); if (label == "ATEŞ ETME MODU") ShowPage(page); });
            images[i] = b.GetComponent<Image>();
            texts[i] = label2;
        }
        refreshers.Add(() =>
        {
            int v = get();
            for (int i = 0; i < images.Length; i++)
            {
                images[i].color = i == v ? SegOn : SegOff;
                texts[i].color = i == v ? Color.white : Theme.TextDim;
            }
        });
    }

    private void Slider(string label, float min, float max, float step, System.Func<float> get, System.Action<float> set, string format)
    {
        RectTransform right;
        LabeledRow(label, out right);
        var valueBox = UIUtil.CreateImage(right, "Value", new Vector2(0f, 0.5f), new Vector2(45f, 0f), new Vector2(90f, 56f), new Color(0.06f, 0.07f, 0.09f, 1f), false);
        var valueText = UIUtil.CreateText(valueBox.transform, "", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(90f, 56f), 28, TextAnchor.MiddleCenter);
        Text unused;
        UIUtil.CreateButton(right, "–", new Vector2(0f, 0.5f), new Vector2(140f, 0f), new Vector2(64f, 56f), SegOff, false, 36, out unused)
            .onClick.AddListener(() => { set(Mathf.Clamp(get() - step, min, max)); Changed(); });
        UIUtil.CreateButton(right, "+", new Vector2(1f, 0.5f), new Vector2(-34f, 0f), new Vector2(64f, 56f), SegOff, false, 36, out unused)
            .onClick.AddListener(() => { set(Mathf.Clamp(get() + step, min, max)); Changed(); });

        // Slider: track, fill and handle
        var sliderRect = UIUtil.CreateRect(right, "Slider", new Vector2(0f, 0.5f), new Vector2(390f, 0f), new Vector2(330f, 40f));
        var track = UIUtil.CreateStretch(sliderRect, "Track").gameObject.AddComponent<Image>();
        track.color = new Color(0.22f, 0.24f, 0.28f, 1f);
        var fillArea = UIUtil.CreateStretch(sliderRect, "FillArea");
        fillArea.offsetMin = new Vector2(0f, 14f);
        fillArea.offsetMax = new Vector2(0f, -14f);
        var fill = UIUtil.CreateStretch(fillArea, "Fill").gameObject.AddComponent<Image>();
        fill.color = new Color(0.55f, 0.57f, 0.62f, 1f);
        var handleArea = UIUtil.CreateStretch(sliderRect, "HandleArea");
        var handle = UIUtil.CreateRect(handleArea, "Handle", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(10f, 44f));
        var handleImg = handle.gameObject.AddComponent<Image>();
        handleImg.color = Color.white;
        var slider = sliderRect.gameObject.AddComponent<UnityEngine.UI.Slider>();
        slider.fillRect = (RectTransform)fill.transform;
        slider.handleRect = handle;
        slider.targetGraphic = handleImg;
        slider.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
        slider.minValue = min;
        slider.maxValue = max;
        slider.wholeNumbers = false;
        bool updating = false;
        slider.onValueChanged.AddListener(v =>
        {
            if (updating)
                return;
            v = Mathf.Round(v / step) * step;
            set(Mathf.Clamp(v, min, max));
            valueText.text = get().ToString(format);
        });
        // Save when the finger lifts (not on every drag step).
        var trigger = sliderRect.gameObject.AddComponent<SliderSaver>();
        trigger.onRelease = Changed;
        refreshers.Add(() =>
        {
            updating = true;
            slider.value = get();
            updating = false;
            valueText.text = get().ToString(format);
        });
    }

    private void ButtonRow(string label, string button, UnityAction action)
    {
        RectTransform right;
        LabeledRow(label, out right);
        Text unused;
        UIUtil.CreateButton(right, button, new Vector2(1f, 0.5f), new Vector2(-150f, 0f), new Vector2(300f, 56f), new Color(0.75f, 0.25f, 0.2f, 1f), false, 28, out unused)
            .onClick.AddListener(action);
    }

    /// <summary>Four big cards like the reference: tap-to-aim, hip fire, automatic, custom.</summary>
    private void FireModeCards()
    {
        var row = Row(250f);
        var bg = row.gameObject.AddComponent<Image>();
        bg.color = RowColor;
        Diamond(row, new Vector2(0f, 1f), new Vector2(34f, -40f));
        var t = UIUtil.CreateText(row, "ATEŞ ETME MODU", new Vector2(0f, 1f), new Vector2(64f, -40f), new Vector2(500f, 50f), 30, TextAnchor.MiddleLeft);
        t.rectTransform.pivot = new Vector2(0f, 0.5f);

        string[] icons = { "ADS", "+", "AUTO", "• • •" };
        var frames = new Image[4];
        for (int i = 0; i < 4; i++)
        {
            int index = i;
            Text label;
            // Yellow frame behind the card shows which mode is selected.
            var frame = UIUtil.CreateImage(row, "Frame", new Vector2(0.5f, 0f), new Vector2(-330f + i * 220f, 95f), new Vector2(213f, 168f), Theme.Accent, false);
            frame.raycastTarget = false;
            frames[i] = frame;
            var b = UIUtil.CreateButton(row, "", new Vector2(0.5f, 0f), new Vector2(-330f + i * 220f, 95f), new Vector2(205f, 160f), new Color(0.2f, 0.22f, 0.27f, 1f), false, 20, out label);
            b.onClick.AddListener(() => { GameSettings.FirePreset = index; Changed(); ShowPage(page); });
            var name = UIUtil.CreateText(b.transform, GameSettings.FirePresetNames[i], new Vector2(0.5f, 1f), new Vector2(0f, -22f), new Vector2(200f, 40f), 20, TextAnchor.MiddleCenter);
            name.fontStyle = FontStyle.Bold;
            var icon = UIUtil.CreateText(b.transform, icons[i], new Vector2(0.5f, 0.5f), new Vector2(0f, -14f), new Vector2(200f, 90f), i < 2 ? 64 : 40, TextAnchor.MiddleCenter);
            icon.color = new Color(1f, 1f, 1f, 0.8f);
        }
        refreshers.Add(() =>
        {
            for (int i = 0; i < frames.Length; i++)
                frames[i].enabled = i == GameSettings.FirePreset;
        });
    }
}

/// <summary>Calls back when a slider is released (saves once instead of on every drag step).</summary>
public class SliderSaver : MonoBehaviour, UnityEngine.EventSystems.IPointerUpHandler
{
    public System.Action onRelease;

    public void OnPointerUp(UnityEngine.EventSystems.PointerEventData eventData)
    {
        if (onRelease != null)
            onRelease();
    }
}
