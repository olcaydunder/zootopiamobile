using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Start-up: a loading screen with a progress bar and tips while the city is built, then a title
/// screen ("DOKUNARAK BAŞLA") over a slow aerial flight around the clinic, then the lobby.
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
        "İpucu: Kliniğin içinde ilk yardım çantası bulabilirsin."
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

    private void Logo(Transform parent, Vector2 pos, float scale)
    {
        var t1 = UIUtil.CreateText(parent, "ZOOTOPIA", new Vector2(0.5f, 0.5f), pos + new Vector2(0f, 40f * scale), new Vector2(1400f, 200f), Mathf.RoundToInt(170 * scale), TextAnchor.MiddleCenter);
        t1.fontStyle = FontStyle.Bold;
        var t2 = UIUtil.CreateText(parent, "M O B I L E", new Vector2(0.5f, 0.5f), pos + new Vector2(0f, -85f * scale), new Vector2(1200f, 90f), Mathf.RoundToInt(64 * scale), TextAnchor.MiddleCenter);
        t2.fontStyle = FontStyle.Bold;
        t2.color = Theme.Accent;
        UIUtil.CreateImage(parent, "LineL", new Vector2(0.5f, 0.5f), pos + new Vector2(-330f * scale, -85f * scale), new Vector2(220f * scale, 4f), Theme.Accent, false).raycastTarget = false;
        UIUtil.CreateImage(parent, "LineR", new Vector2(0.5f, 0.5f), pos + new Vector2(330f * scale, -85f * scale), new Vector2(220f * scale, 4f), Theme.Accent, false).raycastTarget = false;
    }

    private void Build(Transform t)
    {
        // ---- Loading
        loading = UIUtil.CreateStretch(t, "Loading").gameObject;
        var bg = loading.AddComponent<RawImage>();
        bg.texture = Gradient(new Color(0.09f, 0.12f, 0.17f, 1f), new Color(0.02f, 0.03f, 0.05f, 1f), true);
        // Key art (the clinic street + Kasap Leydi), filling the screen without stretching.
        var art = Resources.Load<Texture2D>("UI/KeyArt");
        Vector2 logoPos = new Vector2(0f, 120f);
        if (art != null)
        {
            var artRect = UIUtil.CreateRect(loading.transform, "KeyArt", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1920f, 1080f));
            artRect.gameObject.AddComponent<RawImage>().texture = art;
            var fit = artRect.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fit.aspectRatio = (float)art.width / art.height;
            artRect.gameObject.AddComponent<KenBurns>();
            logoPos = new Vector2(-380f, 170f);
        }
        Logo(loading.transform, logoPos, art != null ? 0.85f : 1f);
        var sub = UIUtil.CreateText(loading.transform, "ÇEKMEKÖY SAVAŞ ALANI", new Vector2(0.5f, 0.5f), logoPos + new Vector2(0f, -150f), new Vector2(900f, 50f), 34, TextAnchor.MiddleCenter);
        sub.color = Theme.TextDim;

        UIUtil.CreateImage(loading.transform, "BarBack", new Vector2(0.5f, 0f), new Vector2(0f, 190f), new Vector2(BarWidth + 8f, 18f), new Color(1f, 1f, 1f, 0.12f), false);
        barFill = UIUtil.CreateImage(loading.transform, "BarFill", new Vector2(0.5f, 0f), new Vector2(-BarWidth * 0.5f, 190f), new Vector2(0f, 10f), Theme.Accent, false).rectTransform;
        barFill.pivot = new Vector2(0f, 0.5f);
        status = UIUtil.CreateText(loading.transform, "", new Vector2(0.5f, 0f), new Vector2(0f, 235f), new Vector2(900f, 44f), 28, TextAnchor.MiddleCenter);
        tip = UIUtil.CreateText(loading.transform, Tips[Random.Range(0, Tips.Length)], new Vector2(0.5f, 0f), new Vector2(0f, 120f), new Vector2(1500f, 40f), 26, TextAnchor.MiddleCenter);
        tip.color = Theme.TextDim;
        var credit = UIUtil.CreateText(loading.transform, UIManager.ProducerCredit, new Vector2(0.5f, 0f), new Vector2(0f, 50f), new Vector2(900f, 36f), 24, TextAnchor.MiddleCenter);
        credit.color = new Color(1f, 1f, 1f, 0.45f);

        // ---- Title (over the 3D city)
        title = UIUtil.CreateStretch(t, "Title").gameObject;
        var shade = title.AddComponent<RawImage>();
        shade.texture = Gradient(new Color(0.02f, 0.03f, 0.05f, 0.55f), new Color(0.01f, 0.01f, 0.02f, 0.95f), false);
        var tap = title.AddComponent<Button>();
        tap.targetGraphic = shade;
        tap.transition = Selectable.Transition.None;
        tap.onClick.AddListener(Leave);
        Logo(title.transform, new Vector2(0f, 230f), 0.9f);
        tapText = UIUtil.CreateText(title.transform, "DOKUNARAK BAŞLA", new Vector2(0.5f, 0f), new Vector2(0f, 190f), new Vector2(900f, 70f), 44, TextAnchor.MiddleCenter);
        tapText.fontStyle = FontStyle.Bold;
        var info = UIUtil.CreateText(title.transform, "Ekşioğlu, Çekmeköy  •  25 oyunculu Battle Royale", new Vector2(0.5f, 0f), new Vector2(0f, 120f), new Vector2(1200f, 40f), 26, TextAnchor.MiddleCenter);
        info.color = Theme.TextDim;
        var ver = UIUtil.CreateText(title.transform, "v" + Application.version + "   •   " + UIManager.ProducerCredit, new Vector2(1f, 0f), new Vector2(-320f, 40f), new Vector2(600f, 34f), 22, TextAnchor.MiddleRight);
        ver.color = new Color(1f, 1f, 1f, 0.45f);
        title.SetActive(false);
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

        tapText.color = new Color(1f, 1f, 1f, 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 2.2f)));

        // Slow aerial orbit around the clinic.
        var cam = Camera.main;
        if (cam != null && !leaving)
        {
            orbit += Time.unscaledDeltaTime * 0.05f;
            Vector3 c = World.LobbySpot;
            float a = orbit + 0.6f;
            cam.transform.position = c + new Vector3(Mathf.Cos(a) * 70f, 38f, Mathf.Sin(a) * 70f);
            cam.transform.LookAt(c + Vector3.up * 6f);
            cam.fieldOfView = 50f;
        }
    }
}

/// <summary>Slow zoom on the loading art so the screen feels alive.</summary>
public class KenBurns : MonoBehaviour
{
    private float t;

    private void Update()
    {
        t += Time.unscaledDeltaTime;
        transform.localScale = Vector3.one * (1.02f + Mathf.Min(t, 20f) * 0.004f);
    }
}
