using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// "Hata modu": catches every error, exception and warning, gives each one a short stable code
/// (e.g. ZM-X-3F2A), runs start-up system checks (models, shaders, effects), keeps a log file that
/// survives a crash, and shows an on-screen console with a COPY button so the full report can be
/// pasted to the developer. Turned on from Ayarlar; errors are always recorded.
/// </summary>
public class ErrorReporter : MonoBehaviour
{
    public class Entry
    {
        public string code;
        public string type;        // HATA, İSTİSNA, UYARI, KONTROL
        public string message;
        public string stack;
        public string hint;
        public int count;
        public float firstTime;
        public bool previousSession;
    }

    public static ErrorReporter Instance;
    public static bool DebugMode;

    private readonly List<Entry> entries = new List<Entry>();
    private readonly Dictionary<string, Entry> byCode = new Dictionary<string, Entry>();
    private readonly Queue<string[]> pending = new Queue<string[]>();
    private readonly object queueLock = new object();
    private string logPath;
    private int errorCount;
    private bool panelOpen;
    private bool checksDone;

    // FPS
    private float fpsTimer;
    private int fpsFrames;
    private float fps;
    private float lowFpsTime;

    // UI
    private Canvas canvas;
    private GameObject badge;
    private Text badgeText;
    private Text fpsText;
    private GameObject panel;
    private Text listText;
    private RectTransform listContent;
    private Text panelTitle;
    private Text copiedText;
    private float copiedUntil;

    public int ErrorCount { get { return errorCount; } }

    /// <summary>Created first, before anything else, so start-up errors are caught too.</summary>
    public static void Install()
    {
        if (Instance != null)
            return;
        var go = new GameObject("ErrorReporter");
        DontDestroyOnLoad(go);
        Instance = go.AddComponent<ErrorReporter>();
    }

    private void Awake()
    {
        DebugMode = PlayerPrefs.GetInt("zm_debug", 0) == 1;
        logPath = Path.Combine(Application.persistentDataPath, "zm_hata.log");
        LoadPreviousSession();
        Application.logMessageReceivedThreaded += OnLog;
        BuildUI();
        try
        {
            File.WriteAllText(logPath, "== Oturum " + System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " ==\n");
        }
        catch { }
    }

    private void OnDestroy()
    {
        Application.logMessageReceivedThreaded -= OnLog;
    }

    public static void SetDebugMode(bool on)
    {
        DebugMode = on;
        PlayerPrefs.SetInt("zm_debug", on ? 1 : 0);
        PlayerPrefs.Save();
        if (Instance != null)
            Instance.RefreshBadge();
    }

    // ----- Capture -----

    private void OnLog(string condition, string stackTrace, LogType type)
    {
        if (type == LogType.Log)
            return;
        lock (queueLock)
        {
            if (pending.Count < 200)
                pending.Enqueue(new[] { condition ?? "", stackTrace ?? "", type.ToString() });
        }
    }

    private static string TypeName(string unityType)
    {
        switch (unityType)
        {
            case "Exception": return "İSTİSNA";
            case "Error":
            case "Assert": return "HATA";
            default: return "UYARI";
        }
    }

    private static string TypeLetter(string typeName)
    {
        switch (typeName)
        {
            case "İSTİSNA": return "X";
            case "HATA": return "E";
            case "KONTROL": return "C";
            default: return "W";
        }
    }

    /// <summary>Stable 4-hex code from the message without numbers (so "index 3" and "index 7" match).</summary>
    private static string MakeCode(string typeName, string message)
    {
        var sb = new StringBuilder();
        string firstLine = message.Split('\n')[0];
        foreach (char c in firstLine)
            if (!char.IsDigit(c))
                sb.Append(c);
        uint h = 2166136261;
        foreach (char c in sb.ToString())
        {
            h ^= c;
            h *= 16777619;
        }
        return "ZM-" + TypeLetter(typeName) + "-" + (h & 0xFFFF).ToString("X4");
    }

    private Entry Add(string typeName, string message, string stack, string hint, string fixedCode)
    {
        string code = fixedCode ?? MakeCode(typeName, message);
        Entry e;
        if (byCode.TryGetValue(code, out e))
        {
            e.count++;
            return e;
        }
        e = new Entry
        {
            code = code,
            type = typeName,
            message = message,
            stack = stack,
            hint = hint,
            count = 1,
            firstTime = Time.realtimeSinceStartup
        };
        byCode[code] = e;
        entries.Add(e);
        if (typeName != "UYARI")
            errorCount++;

        try
        {
            File.AppendAllText(logPath, "[" + code + "] " + typeName + ": " + message + "\n" + Trim(stack, 6) + "\n");
        }
        catch { }

        RefreshBadge();
        if (panelOpen)
            RefreshPanel();
        return e;
    }

    /// <summary>Device details, this session's errors and the end of the error log, for a bug report.</summary>
    public static string RecentLog(int maxChars)
    {
        var sb = new StringBuilder();
        sb.Append("Cihaz: ").Append(SystemInfo.deviceModel).Append(" | ").Append(SystemInfo.operatingSystem)
          .Append(" | GPU ").Append(SystemInfo.graphicsDeviceName).Append(" | RAM ").Append(SystemInfo.systemMemorySize).Append(" MB")
          .Append(" | ekran ").Append(Screen.width).Append('x').Append(Screen.height).Append('\n');
        if (Instance != null)
        {
            sb.Append("FPS ").Append(Mathf.RoundToInt(Instance.fps)).Append(" | hata sayısı ").Append(Instance.errorCount).Append('\n');
            int start = Mathf.Max(0, Instance.entries.Count - 40);
            for (int i = start; i < Instance.entries.Count; i++)
            {
                var e = Instance.entries[i];
                sb.Append('[').Append(e.code).Append("] ").Append(e.type).Append(" x").Append(e.count).Append(": ").Append(e.message).Append('\n');
                if (!string.IsNullOrEmpty(e.stack))
                    sb.Append(Trim(e.stack, 6)).Append('\n');
            }
            try
            {
                if (File.Exists(Instance.logPath))
                {
                    string file = File.ReadAllText(Instance.logPath);
                    sb.Append("--- zm_hata.log ---\n").Append(file.Length > maxChars / 2 ? file.Substring(file.Length - maxChars / 2) : file);
                }
            }
            catch { }
        }
        string all = sb.ToString();
        return all.Length > maxChars ? all.Substring(all.Length - maxChars) : all;
    }

    /// <summary>Records a failed system check with a fixed code and a Turkish explanation.</summary>
    public static void Check(string code, string message, string hint)
    {
        if (Instance != null)
            Instance.Add("KONTROL", message, "", hint, code);
    }

    private void LoadPreviousSession()
    {
        try
        {
            if (!File.Exists(logPath))
                return;
            foreach (var line in File.ReadAllLines(logPath))
            {
                if (!line.StartsWith("[ZM-"))
                    continue;
                int close = line.IndexOf(']');
                if (close < 0)
                    continue;
                string code = line.Substring(1, close - 1);
                string rest = line.Substring(close + 1).Trim();
                if (byCode.ContainsKey(code))
                    continue;
                var e = new Entry { code = code, type = "ÖNCEKİ", message = rest, stack = "", count = 1, previousSession = true };
                byCode[code] = e;
                entries.Add(e);
            }
        }
        catch { }
    }

    private static string Trim(string stack, int lines)
    {
        if (string.IsNullOrEmpty(stack))
            return "";
        var parts = stack.Split('\n');
        var sb = new StringBuilder();
        for (int i = 0; i < parts.Length && i < lines; i++)
            if (parts[i].Trim().Length > 0)
                sb.Append("    ").Append(parts[i].Trim()).Append('\n');
        return sb.ToString();
    }

    // ----- System checks -----

    private void RunChecks()
    {
        checksDone = true;

        if (ModelLibrary.Prefab(ModelLibrary.CharacterPath(ModelLibrary.PlayerSkin)) == null)
            Check("ZM-C-01", "Karakter modeli yüklenemedi (Resources/Models/Characters).", "Model dosyaları build'e girmemiş; basit şekiller kullanılıyor.");
        else if (ModelLibrary.Clips(ModelLibrary.CharacterPath(ModelLibrary.PlayerSkin)).Length == 0)
            Check("ZM-C-06", "Karakter animasyonları bulunamadı.", "FBX içindeki animasyonlar içe aktarılmamış; karakterler hareketsiz durur.");

        if (ModelLibrary.Prefab(ModelLibrary.GunPath(WeaponType.Rifle)) == null)
            Check("ZM-C-02", "Silah modeli yüklenemedi (Resources/Models/Guns).", "Kutu şeklinde silahlar gösteriliyor.");

        string[] shaders = { "Zootopia/Terrain", "Zootopia/Water", "Zootopia/Sky", "Zootopia/Grass", "Zootopia/Camo", "Zootopia/Facade", "Zootopia/Road", "Standard" };
        foreach (var name in shaders)
        {
            var s = Shader.Find(name);
            if (s == null)
                Check("ZM-C-03", "Shader bulunamadı: " + name, "Shader build'e girmemiş; o yüzey pembe görünür.");
            else if (!s.isSupported)
                Check("ZM-C-04", "Shader bu telefonda desteklenmiyor: " + name, "Grafik kalitesini Düşük yapmayı dene.");
        }

        foreach (var skin in ModelLibrary.ShopSkins)
            if (ModelLibrary.Prefab(ModelLibrary.CharacterPath(skin)) == null)
                Check("ZM-C-12", "Karakter modeli eksik: " + skin, "O karakter basit şekillerle gösterilir.");
        foreach (var w in Gunsmith.Weapons)
            foreach (var gs in ModelLibrary.GunSkins(w))
                if (!string.IsNullOrEmpty(gs) && ModelLibrary.Prefab(ModelLibrary.GunPath(w) + "_" + gs) == null)
                    Check("ZM-C-13", "Silah modeli eksik: " + w + " " + gs, "Standart model kullanılıyor.");

        foreach (var icon in new[] { "rank_00", "rank_21", "token_revive", "currency", "rarity_frame" })
            if (Icons.Get(icon) == null)
                Check("ZM-C-14", "Arayüz ikonu eksik: " + icon, "Rütbe/ödül ekranlarında boş kutu görünür.");

        if (!MapData.Loaded)
            Check("ZM-C-10", MapCatalog.CurrentInfo.name + " harita verisi yüklenemedi (Resources/Map/" + MapCatalog.Current + ").", "Yedek ada haritası kullanılıyor.");
        if (CityBuilder.LastError != null)
            Check("ZM-C-11", "Şehir kurulurken hata: " + CityBuilder.LastError, "Bazı binalar/yollar eksik olabilir.");

        if (PostFx.Failed)
            Check("ZM-C-05", "Post-processing başlatılamadı.", "Bloom/renk efektleri kapalı; oyun yine çalışır.");
        if (!SystemInfo.supportsInstancing)
            Check("ZM-C-07", "GPU instancing desteklenmiyor.", "Çimenler kapalı.");
        if (SystemInfo.systemMemorySize > 0 && SystemInfo.systemMemorySize < 2500)
            Check("ZM-C-08", "Düşük RAM: " + SystemInfo.systemMemorySize + " MB.", "Grafik kalitesi Düşük önerilir.");
    }

    // ----- Report -----

    public string BuildReport()
    {
        var sb = new StringBuilder();
        sb.Append("ZOOTOPIA MOBILE HATA RAPORU\n");
        sb.Append("Sürüm: ").Append(Application.version).Append("  Unity: ").Append(Application.unityVersion).Append('\n');
        sb.Append("Cihaz: ").Append(SystemInfo.deviceModel).Append("  OS: ").Append(SystemInfo.operatingSystem).Append('\n');
        sb.Append("GPU: ").Append(SystemInfo.graphicsDeviceName).Append(" (").Append(SystemInfo.graphicsDeviceType).Append(", ")
          .Append(SystemInfo.graphicsMemorySize).Append(" MB)  RAM: ").Append(SystemInfo.systemMemorySize).Append(" MB\n");
        sb.Append("Ekran: ").Append(Screen.width).Append('x').Append(Screen.height)
          .Append("  Kalite: ").Append(GameSettings.QualityNames[GameSettings.Quality])
          .Append("  FPS: ").Append(Mathf.RoundToInt(fps)).Append('\n');
        sb.Append("Kayıt sayısı: ").Append(entries.Count).Append('\n').Append('\n');
        foreach (var e in entries)
        {
            sb.Append('[').Append(e.code).Append("] ").Append(e.type);
            if (e.count > 1)
                sb.Append(" x").Append(e.count);
            sb.Append('\n').Append(e.message).Append('\n');
            if (!string.IsNullOrEmpty(e.hint))
                sb.Append("  -> ").Append(e.hint).Append('\n');
            if (!string.IsNullOrEmpty(e.stack))
                sb.Append(Trim(e.stack, 8));
            sb.Append('\n');
        }
        return sb.ToString();
    }

    // ----- UI -----

    private void BuildUI()
    {
        canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 300;
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 1f;
        gameObject.AddComponent<GraphicRaycaster>();

        var root = canvas.transform;
        fpsText = UIUtil.CreateText(root, "", new Vector2(0.5f, 1f), new Vector2(0f, -18f), new Vector2(600f, 30f), 22, TextAnchor.MiddleCenter);
        fpsText.color = new Color(0.6f, 1f, 0.6f);

        Text label;
        var b = UIUtil.CreateButton(root, "", new Vector2(0.5f, 1f), new Vector2(-430f, -60f), new Vector2(170f, 64f), new Color(0.85f, 0.15f, 0.12f, 0.9f), false, 26, out label);
        b.onClick.AddListener(OpenPanel);
        badge = b.gameObject;
        badgeText = label;

        // Console panel
        var dim = UIUtil.CreateStretch(root, "ErrorPanel");
        dim.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.88f);
        panel = dim.gameObject;
        var c = new Vector2(0.5f, 0.5f);

        panelTitle = UIUtil.CreateText(panel.transform, "HATA MODU", new Vector2(0.5f, 1f), new Vector2(0f, -55f), new Vector2(1600f, 60f), 42, TextAnchor.MiddleCenter);
        panelTitle.fontStyle = FontStyle.Bold;
        panelTitle.color = new Color(1f, 0.84f, 0.2f);

        // Scrollable list
        var viewport = UIUtil.CreateRect(panel.transform, "Viewport", c, new Vector2(0f, 10f), new Vector2(1760f, 780f));
        viewport.gameObject.AddComponent<Image>().color = new Color(0.08f, 0.09f, 0.11f, 1f);
        viewport.gameObject.AddComponent<RectMask2D>();
        listContent = UIUtil.CreateRect(viewport, "Content", new Vector2(0.5f, 1f), Vector2.zero, new Vector2(1720f, 100f));
        listContent.pivot = new Vector2(0.5f, 1f);
        listText = listContent.gameObject.AddComponent<Text>();
        listText.font = UIUtil.DefaultFont;
        listText.fontSize = 26;
        listText.color = Color.white;
        listText.alignment = TextAnchor.UpperLeft;
        listText.horizontalOverflow = HorizontalWrapMode.Wrap;
        listText.verticalOverflow = VerticalWrapMode.Overflow;
        listText.supportRichText = true;
        listText.raycastTarget = false;
        var fitter = listContent.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var scroll = viewport.gameObject.AddComponent<ScrollRect>();
        scroll.content = listContent;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 30f;
        viewport.GetComponent<Image>().raycastTarget = true;

        Text unused;
        UIUtil.CreateButton(panel.transform, "RAPORU KOPYALA", new Vector2(0.5f, 0f), new Vector2(-420f, 60f), new Vector2(380f, 84f), new Color(1f, 0.78f, 0.15f, 1f), false, 30, out unused)
            .onClick.AddListener(CopyReport);
        UIUtil.CreateButton(panel.transform, "TEMİZLE", new Vector2(0.5f, 0f), new Vector2(0f, 60f), new Vector2(300f, 84f), new Color(0.35f, 0.38f, 0.45f, 1f), false, 30, out unused)
            .onClick.AddListener(ClearAll);
        UIUtil.CreateButton(panel.transform, "KAPAT", new Vector2(0.5f, 0f), new Vector2(380f, 60f), new Vector2(300f, 84f), new Color(0.2f, 0.5f, 1f, 1f), false, 30, out unused)
            .onClick.AddListener(ClosePanel);
        copiedText = UIUtil.CreateText(panel.transform, "", new Vector2(0.5f, 0f), new Vector2(-420f, 125f), new Vector2(700f, 40f), 26, TextAnchor.MiddleCenter);
        copiedText.color = new Color(0.5f, 1f, 0.5f);

        panel.SetActive(false);
        RefreshBadge();
    }

    private void RefreshBadge()
    {
        if (badge == null)
            return;
        bool show = errorCount > 0 || (DebugMode && entries.Count > 0);
        badge.SetActive(show && !panelOpen);
        badgeText.text = "HATA " + (errorCount > 0 ? errorCount : entries.Count);
        fpsText.enabled = DebugMode;
    }

    public void OpenPanel()
    {
        panelOpen = true;
        panel.SetActive(true);
        RefreshPanel();
        RefreshBadge();
    }

    private void ClosePanel()
    {
        panelOpen = false;
        panel.SetActive(false);
        RefreshBadge();
    }

    private void ClearAll()
    {
        entries.Clear();
        byCode.Clear();
        errorCount = 0;
        try { File.WriteAllText(logPath, ""); } catch { }
        RefreshPanel();
    }

    private void CopyReport()
    {
        GUIUtility.systemCopyBuffer = BuildReport();
        copiedText.text = "Kopyalandı! Sohbete yapıştırabilirsin.";
        copiedUntil = Time.unscaledTime + 3f;
    }

    private static string ColorFor(string type)
    {
        switch (type)
        {
            case "İSTİSNA": return "#ff5a4a";
            case "HATA": return "#ff8a3a";
            case "KONTROL": return "#ffd54a";
            case "ÖNCEKİ": return "#9aa4b2";
            default: return "#c8d0da";
        }
    }

    private void RefreshPanel()
    {
        panelTitle.text = "HATA MODU  •  " + entries.Count + " kayıt  •  FPS " + Mathf.RoundToInt(fps);
        if (entries.Count == 0)
        {
            listText.text = "\n  Hiç hata yok. Oyun sorunsuz çalışıyor.\n\n  Bir sorun yaşarsan bu ekrana dön, RAPORU KOPYALA'ya bas ve rapor metnini geliştiriciye gönder.";
            return;
        }
        var sb = new StringBuilder();
        foreach (var e in entries)
        {
            sb.Append("<color=").Append(ColorFor(e.type)).Append("><b>").Append(e.code).Append("</b>  ").Append(e.type);
            if (e.count > 1)
                sb.Append("  x").Append(e.count);
            sb.Append("</color>\n").Append(Escape(e.message.Split('\n')[0])).Append('\n');
            if (!string.IsNullOrEmpty(e.hint))
                sb.Append("<color=#9fe39f>-> ").Append(Escape(e.hint)).Append("</color>\n");
            if (!string.IsNullOrEmpty(e.stack))
                sb.Append("<size=20><color=#8a94a3>").Append(Escape(Trim(e.stack, 3))).Append("</color></size>");
            sb.Append('\n');
            if (sb.Length > 12000)
            {
                sb.Append("... (tamamı için RAPORU KOPYALA)");
                break;
            }
        }
        listText.text = sb.ToString();
    }

    private static string Escape(string s)
    {
        return s.Replace("<", "‹").Replace(">", "›");
    }

    private void Update()
    {
        // Drain the thread-safe queue on the main thread.
        while (true)
        {
            string[] item;
            lock (queueLock)
            {
                if (pending.Count == 0)
                    break;
                item = pending.Dequeue();
            }
            Add(TypeName(item[2]), item[0], item[1], null, null);
        }

        if (!checksDone && Time.realtimeSinceStartup > 3f && GameManager.Instance != null)
            RunChecks();

        fpsFrames++;
        fpsTimer += Time.unscaledDeltaTime;
        if (fpsTimer >= 0.5f)
        {
            fps = fpsFrames / fpsTimer;
            fpsFrames = 0;
            fpsTimer = 0f;
            if (DebugMode)
                fpsText.text = "FPS " + Mathf.RoundToInt(fps) + "  •  " + (1000f / Mathf.Max(1f, fps)).ToString("0.0") + " ms  •  " + GameSettings.QualityNames[GameSettings.Quality];

            var gm = GameManager.Instance;
            if (gm != null && gm.currentState == GameState.InGame && fps < 22f)
            {
                lowFpsTime += 0.5f;
                if (lowFpsTime > 15f && !byCode.ContainsKey("ZM-C-09"))
                    Check("ZM-C-09", "Maçta FPS uzun süre düşük (" + Mathf.RoundToInt(fps) + ").", "Ayarlar'dan grafik kalitesini düşür.");
            }
        }

        if (copiedText != null && copiedText.text.Length > 0 && Time.unscaledTime > copiedUntil)
            copiedText.text = "";
    }
}
