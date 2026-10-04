using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Start-up: a loading screen (the game logo over the chosen map, a progress bar and tips) while the
/// map is built, then a title screen ("DOKUNARAK BAŞLA") over a slow aerial flight around the map's
/// landmark, then the lobby. After a map change the title is skipped (<see cref="Dismiss"/>).
/// </summary>
public class TitleScreen : MonoBehaviour
{
    private static readonly string[] Tips =
    {
        "İpucu: Dürbünlü silahlarla nişan alınca tam ekran dürbün açılır.",
        "İpucu: Ayarlar > Hassasiyet'ten jiroskopla nişan almayı açabilirsin.",
        "İpucu: Girilebilen dükkanlarda daha iyi silahlar bulunur.",
        "İpucu: Mavi duvarın dışında kalma, her aşamada daha çok can yakar.",
        "İpucu: Silah Atölyesi'nde aparatlarla silahını güçlendir.",
        "İpucu: Kliniğin içinde ilk yardım çantası bulabilirsin.",
        "İpucu: Lobideki HARİTA düğmesiyle Ekşioğlu, Senir Kasabası ve Fırat Üniversitesi arasında geçiş yap.",
        "İpucu: Senir'de Burdur Gölü'ne girersen yüzerek kıyıya çıkabilirsin."
    };

    private CanvasGroup group;
    private GameObject loading, title;
    private RectTransform barFill;
    private Text status, tip, tapText;
    private float progressShown, progressTarget;
    private bool titleActive, leaving;
    private PlayerController player;
    private float orbit;
    private const float BarWidth = 900f;

    public static TitleScreen Create()
    {
        var go = new GameObject("TitleScreen");
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 250;
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 1f;
        go.AddComponent<GraphicRaycaster>();
        var s = go.AddComponent<TitleScreen>();
        s.group = go.AddComponent<CanvasGroup>();
        s.Build(go.transform);
        return s;
    }

    private static Texture2D Gradient(Color top, Color bottom, bool stripes)
    {
        const int w = 64, h = 256;
        var px = new Color32[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                Color c = Color.Lerp(bottom, top, y / (float)(h - 1));
                if (stripes && ((x + y) / 6) % 9 == 0)
                    c = Color.Lerp(c, new Color(1f, 0.82f, 0.18f, c.a), 0.06f);
                px[y * w + x] = c;
            }
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.SetPixels32(px);
        tex.Apply();
        return tex;
    }

    /// <summary>The game logo (Resources/UI/Logo.png, drawn by Tools/make_logo.py); text if it is missing.</summary>
    private void Logo(Transform parent, Vector2 pos, float width)
    {
        var tex = Resources.Load<Texture2D>("UI/Logo");
        if (tex != null)
        {
            var r = UIUtil.CreateRect(parent, "LogoImage", new Vector2(0.5f, 0.5f), pos, new Vector2(width, width * tex.height / tex.width));
            var img = r.gameObject.AddComponent<RawImage>();
            img.texture = tex;
            img.raycastTarget = false;
            return;
        }
        float scale = width / 1100f;
        var t1 = UIUtil.CreateText(parent, "ZOOTOPIA", new Vector2(0.5f, 0.5f), pos + new Vector2(0f, 40f * scale), new Vector2(1400f, 200f), Mathf.RoundToInt(170 * scale), TextAnchor.MiddleCenter);
        t1.fontStyle = FontStyle.Bold;
        var t2 = UIUtil.CreateText(parent, "M O B I L E", new Vector2(0.5f, 0.5f), pos + new Vector2(0f, -85f * scale), new Vector2(1200f, 90f), Mathf.RoundToInt(64 * scale), TextAnchor.MiddleCenter);
        t2.fontStyle = FontStyle.Bold;
        t2.color = Theme.Accent;
    }

    private void Build(Transform t)
    {
        // ---- Loading: the chosen map from above (dimmed, slowly zooming), the logo, the map's name.
        loading = UIUtil.CreateStretch(t, "Loading").gameObject;
        var bg = loading.AddComponent<RawImage>();
        bg.texture = Gradient(new Color(0.09f, 0.12f, 0.17f, 1f), new Color(0.02f, 0.03f, 0.05f, 1f), true);
        var map = MapCatalog.CurrentInfo;
        var preview = MapCatalog.Preview(map.id);
        if (preview != null)
        {
            var mapRect = UIUtil.CreateRect(loading.transform, "MapPicture", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1920f, 1080f));
            var mapImg = mapRect.gameObject.AddComponent<RawImage>();
            mapImg.texture = preview;
            mapImg.color = new Color(1f, 1f, 1f, 0.32f);
            mapImg.raycastTarget = false;
            var fit = mapRect.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fit.aspectRatio = 1f;
            mapRect.localRotation = Quaternion.Euler(0f, 0f, -8f);
            mapRect.gameObject.AddComponent<KenBurns>();
            var shade = UIUtil.CreateStretch(loading.transform, "Shade").gameObject.AddComponent<RawImage>();
            shade.texture = Vignette();
            shade.raycastTarget = false;
        }
        Logo(loading.transform, new Vector2(0f, 150f), 1150f);
        var mapName = UIUtil.CreateText(loading.transform, map.name, new Vector2(0.5f, 0.5f), new Vector2(0f, -55f), new Vector2(1200f, 64f), 48, TextAnchor.MiddleCenter);
        mapName.fontStyle = FontStyle.Bold;
        mapName.color = Theme.Accent;
        var sub = UIUtil.CreateText(loading.transform, map.place.ToUpper() + "  •  SAVAŞ ALANI", new Vector2(0.5f, 0.5f), new Vector2(0f, -110f), new Vector2(1200f, 44f), 30, TextAnchor.MiddleCenter);
        sub.color = Theme.TextDim;

        UIUtil.CreateImage(loading.transform, "BarBack", new Vector2(0.5f, 0f), new Vector2(0f, 190f), new Vector2(BarWidth + 8f, 18f), new Color(1f, 1f, 1f, 0.12f), false);
        barFill = UIUtil.CreateImage(loading.transform, "BarFill", new Vector2(0.5f, 0f), new Vector2(-BarWidth * 0.5f, 190f), new Vector2(0f, 10f), Theme.Accent, false).rectTransform;
        barFill.pivot = new Vector2(0f, 0.5f);
        status = UIUtil.CreateText(loading.transform, "", new Vector2(0.5f, 0f), new Vector2(0f, 235f), new Vector2(900f, 44f), 28, TextAnchor.MiddleCenter);
        tip = UIUtil.CreateText(loading.transform, Tips[Random.Range(0, Tips.Length)], new Vector2(0.5f, 0f), new Vector2(0f, 120f), new Vector2(1500f, 40f), 26, TextAnchor.MiddleCenter);
        tip.color = Theme.TextDim;
        var credit = UIUtil.CreateText(loading.transform, UIManager.ProducerCredit, new Vector2(0.5f, 0f), new Vector2(0f, 50f), new Vector2(900f, 36f), 24, TextAnchor.MiddleCenter);
        credit.color = new Color(1f, 1f, 1f, 0.45f);

        // ---- Title (over the 3D city): cinematic camera shots, letterbox, animated logo
        title = UIUtil.CreateStretch(t, "Title").gameObject;
        var vignette = title.AddComponent<RawImage>();
        vignette.texture = Vignette();
        var tap = title.AddComponent<Button>();
        tap.targetGraphic = vignette;
        tap.transition = Selectable.Transition.None;
        tap.onClick.AddListener(Leave);

        // Drifting embers over everything.
        for (int i = 0; i < 26; i++)
        {
            var e = UIUtil.CreateImage(title.transform, "Ember", new Vector2(0.5f, 0f), Vector2.zero, Vector2.one * Random.Range(4f, 9f), new Color(1f, 0.75f, 0.35f, 0f), true);
            e.raycastTarget = false;
            embers.Add(new Ember { img = e, x = Random.Range(-1000f, 1000f), y = Random.Range(0f, 1100f), speed = Random.Range(25f, 70f), phase = Random.value * 10f, alpha = Random.Range(0.25f, 0.7f) });
        }

        letterTop = UIUtil.CreateImage(title.transform, "BarTop", new Vector2(0.5f, 1f), new Vector2(0f, 0f), new Vector2(4000f, 0f), Color.black, false).rectTransform;
        letterTop.pivot = new Vector2(0.5f, 1f);
        letterBottom = UIUtil.CreateImage(title.transform, "BarBottom", new Vector2(0.5f, 0f), new Vector2(0f, 0f), new Vector2(4000f, 0f), Color.black, false).rectTransform;
        letterBottom.pivot = new Vector2(0.5f, 0f);
        letterTop.GetComponent<Image>().raycastTarget = false;
        letterBottom.GetComponent<Image>().raycastTarget = false;

        logoRoot = UIUtil.CreateRect(title.transform, "Logo", new Vector2(0.5f, 0.5f), new Vector2(0f, 200f), new Vector2(1600f, 400f));
        logoGroup = logoRoot.gameObject.AddComponent<CanvasGroup>();
        Logo(logoRoot, new Vector2(0f, 30f), 1100f);
        var tagline = UIUtil.CreateText(logoRoot, "B A T T L E   R O Y A L E", new Vector2(0.5f, 0.5f), new Vector2(0f, -140f), new Vector2(1200f, 50f), 30, TextAnchor.MiddleCenter);
        tagline.color = new Color(1f, 1f, 1f, 0.7f);
        sweep = UIUtil.CreateImage(logoRoot, "Sweep", new Vector2(0.5f, 0.5f), new Vector2(-900f, 30f), new Vector2(90f, 420f), new Color(1f, 1f, 1f, 0.12f), false).rectTransform;
        sweep.localRotation = Quaternion.Euler(0f, 0f, -18f);
        sweep.GetComponent<Image>().raycastTarget = false;

        // Tap to start, in a framed pill.
        var pill = UIUtil.CreateImage(title.transform, "Start", new Vector2(0.5f, 0f), new Vector2(0f, 205f), new Vector2(560f, 92f), new Color(0f, 0f, 0f, 0.55f), false);
        pill.raycastTarget = false;
        pillFrame = pill.gameObject.AddComponent<Outline>();
        pillFrame.effectColor = Theme.Accent;
        pillFrame.effectDistance = new Vector2(2f, -2f);
        tapText = UIUtil.CreateText(pill.transform, "DOKUNARAK BAŞLA", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(560f, 92f), 40, TextAnchor.MiddleCenter);
        tapText.fontStyle = FontStyle.Bold;
        var info = UIUtil.CreateText(title.transform, map.name + ", " + map.place + "  •  Çevrimiçi ve botlarla Battle Royale", new Vector2(0.5f, 0f), new Vector2(0f, 135f), new Vector2(1400f, 40f), 26, TextAnchor.MiddleCenter);
        info.color = Theme.TextDim;

        // Player card (bottom-left) and version / credit (bottom-right).
        var card = Theme.Box(title.transform, "Card", new Vector2(0f, 0f), new Vector2(290f, 80f), new Vector2(520f, 110f), new Color(0f, 0f, 0f, 0.55f), true);
        cardIcon = Icons.Create(card.transform, Icons.Rank(0), new Vector2(0f, 0.5f), new Vector2(62f, 0f), new Vector2(88f, 88f));
        cardName = UIUtil.CreateText(card.transform, "", new Vector2(0f, 0.5f), new Vector2(300f, 18f), new Vector2(380f, 44f), 32, TextAnchor.MiddleLeft);
        cardName.fontStyle = FontStyle.Bold;
        cardRank = UIUtil.CreateText(card.transform, "", new Vector2(0f, 0.5f), new Vector2(300f, -20f), new Vector2(380f, 34f), 22, TextAnchor.MiddleLeft);
        cardRank.color = Theme.Accent;
        var ver = UIUtil.CreateText(title.transform, "v" + Application.version + "   •   " + UIManager.ProducerCredit, new Vector2(1f, 0f), new Vector2(-320f, 60f), new Vector2(600f, 34f), 22, TextAnchor.MiddleRight);
        ver.color = new Color(1f, 1f, 1f, 0.5f);

        fade = UIUtil.CreateImage(title.transform, "Fade", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(5000f, 3000f), Color.black, false);
        fade.raycastTarget = false;
        fade.transform.SetSiblingIndex(0);   // only the city fades between shots, not the logo and buttons
        title.SetActive(false);
    }

    private class Ember
    {
        public Image img;
        public float x, y, speed, phase, alpha;
    }

    private readonly System.Collections.Generic.List<Ember> embers = new System.Collections.Generic.List<Ember>();
    private RectTransform letterTop, letterBottom, logoRoot, sweep;
    private CanvasGroup logoGroup;
    private Outline pillFrame;
    private Image fade, cardIcon;
    private Text cardName, cardRank;
    private float titleTime;
    private int shot = -1;
    private float shotTime;

    /// <summary>Dark edges, clear middle: the city shows through.</summary>
    private static Texture2D Vignette()
    {
        const int n = 128;
        var px = new Color32[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = (x + 0.5f) / n - 0.5f, dy = (y + 0.5f) / n - 0.5f;
                float d = Mathf.Sqrt(dx * dx * 1.2f + dy * dy) * 2f;
                float a = Mathf.Clamp01((d - 0.45f) / 0.75f);
                a = a * a * 0.85f + (y < n * 0.35f ? (0.35f - y / (float)n) * 1.4f : 0f);   // extra shade low down for the text
                px[y * n + x] = new Color32(2, 3, 6, (byte)(Mathf.Clamp01(a) * 255f));
            }
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.SetPixels32(px);
        tex.Apply();
        return tex;
    }

    public void SetProgress(float value, string message)
    {
        progressTarget = Mathf.Clamp01(value);
        status.text = message;
        // Draw the bar right away: the next frames are spent building the city.
        progressShown = Mathf.Max(progressShown, progressTarget * 0.9f);
        barFill.sizeDelta = new Vector2(BarWidth * progressShown, 10f);
    }

    public void ShowTitle(PlayerController p)
    {
        player = p;
        if (player != null)
            player.cinematic = true;
        loading.SetActive(false);
        title.SetActive(true);
        titleActive = true;
        orbit = 0f;
        titleTime = 0f;
        shot = -1;
        var gm = GameManager.Instance;
        if (gm != null)
        {
            var prof = gm.profile;
            cardName.text = prof.playerName;
            cardRank.text = prof.RankName.ToUpper() + "  •  SV " + prof.level;
            Icons.Set(cardIcon, Icons.Rank(prof.RankIndex));
        }
    }

    /// <summary>Back from a map change: no title, the loading screen just fades into the lobby.</summary>
    public void Dismiss(PlayerController p)
    {
        player = p;
        Leave();
    }

    private void Leave()
    {
        if (leaving)
            return;
        leaving = true;
        StartCoroutine(FadeOut());
    }

    private IEnumerator FadeOut()
    {
        Sfx.Play(SoundBank.Reload, 0.4f, 1.3f);
        float t = 0f;
        while (t < 0.5f)
        {
            t += Time.unscaledDeltaTime;
            group.alpha = 1f - t / 0.5f;
            yield return null;
        }
        if (player != null)
            player.cinematic = false;
        Destroy(gameObject);
    }

    private void Update()
    {
        if (!titleActive)
        {
            progressShown = Mathf.MoveTowards(progressShown, progressTarget, Time.unscaledDeltaTime * 1.5f);
            barFill.sizeDelta = new Vector2(BarWidth * progressShown, 10f);
            return;
        }

        float dt = Time.unscaledDeltaTime;
        titleTime += dt;
        float pulse = Mathf.Abs(Mathf.Sin(Time.unscaledTime * 2.2f));
        tapText.color = new Color(1f, 1f, 1f, 0.6f + 0.4f * pulse);
        pillFrame.effectColor = new Color(Theme.Accent.r, Theme.Accent.g, Theme.Accent.b, 0.35f + 0.65f * pulse);

        // Letterbox slides in, logo settles from a slight zoom, a light sweep crosses it now and then.
        float bars = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(titleTime / 1.2f)) * 95f;
        letterTop.sizeDelta = new Vector2(4000f, bars);
        letterBottom.sizeDelta = new Vector2(4000f, bars * 0.6f);
        float k = Mathf.Clamp01((titleTime - 0.3f) / 1.1f);
        logoGroup.alpha = k;
        logoRoot.localScale = Vector3.one * Mathf.Lerp(1.12f, 1f, 1f - (1f - k) * (1f - k));
        float sw = Mathf.Repeat(titleTime - 1.5f, 6f);
        sweep.anchoredPosition = new Vector2(Mathf.Lerp(-900f, 900f, sw / 1.4f), 30f);
        sweep.gameObject.SetActive(titleTime > 1.5f && sw < 1.4f);

        foreach (var e in embers)
        {
            e.y += e.speed * dt;
            if (e.y > 1150f)
            {
                e.y = -20f;
                e.x = Random.Range(-1000f, 1000f);
            }
            e.img.rectTransform.anchoredPosition = new Vector2(e.x + Mathf.Sin(Time.unscaledTime * 0.7f + e.phase) * 30f, e.y);
            float life = Mathf.Clamp01(e.y / 200f) * Mathf.Clamp01((1150f - e.y) / 300f);
            e.img.color = new Color(1f, 0.72f, 0.3f, e.alpha * life * Mathf.Clamp01(titleTime));
        }

        // Camera: a few slow shots around the landmark and over the map, with fades between them.
        var cam = Camera.main;
        if (cam != null && !leaving)
            CameraShots(cam, dt);
    }

    private static readonly float[] ShotLengths = { 9f, 7f, 9f };

    private void CameraShots(Camera cam, float dt)
    {
        if (shot < 0 || shotTime >= ShotLengths[shot])
        {
            shot = (shot + 1) % ShotLengths.Length;
            shotTime = 0f;
        }
        shotTime += dt;
        float len = ShotLengths[shot];
        float u = Mathf.Clamp01(shotTime / len);
        float f = Mathf.Max(Mathf.Clamp01(1f - shotTime / 0.7f), Mathf.Clamp01((shotTime - (len - 0.6f)) / 0.6f));
        if (titleTime < 0.7f)
            f = Mathf.Max(f, 1f - titleTime / 0.7f);
        fade.color = new Color(0f, 0f, 0f, f);

        Vector3 c = World.LobbySpot;
        Vector3 fwd = Quaternion.Euler(0f, World.LobbyYaw, 0f) * Vector3.forward;
        Vector3 right = Vector3.Cross(Vector3.up, fwd);
        switch (shot)
        {
            case 0:   // slow orbit around the landmark
            {
                orbit += dt * 0.05f;
                float a = orbit + 0.6f;
                cam.transform.position = c + new Vector3(Mathf.Cos(a) * 70f, 38f, Mathf.Sin(a) * 70f);
                cam.transform.LookAt(c + Vector3.up * 6f);
                cam.fieldOfView = 50f;
                break;
            }
            case 1:   // low dolly in on the character
            {
                float e = Mathf.SmoothStep(0f, 1f, u);
                cam.transform.position = Vector3.Lerp(c + fwd * 8f + right * 4.5f + Vector3.up * 1.3f, c + fwd * 3.6f + right * 1.6f + Vector3.up * 1.7f, e);
                cam.transform.LookAt(c + Vector3.up * 1.45f);
                cam.fieldOfView = Mathf.Lerp(48f, 40f, e);
                break;
            }
            default:  // high pass over the city
            {
                float e = Mathf.SmoothStep(0f, 1f, u);
                Vector3 from = c - fwd * 160f + right * 120f + Vector3.up * 120f;
                Vector3 to = c - fwd * 60f - right * 140f + Vector3.up * 95f;
                cam.transform.position = Vector3.Lerp(from, to, e);
                cam.transform.LookAt(c + Vector3.up * 10f + fwd * 40f);
                cam.fieldOfView = 55f;
                break;
            }
        }
    }
}

/// <summary>Slow zoom on the loading picture so the screen feels alive.</summary>
public class KenBurns : MonoBehaviour
{
    private float t;

    private void Update()
    {
        t += Time.unscaledDeltaTime;
        transform.localScale = Vector3.one * (1.02f + Mathf.Min(t, 20f) * 0.004f);
    }
}
