using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Opening a gift box, full screen: the box sits in a glow with turning light rays; every tap shakes it,
/// cracks of light open on it and sparks fly; after 4–5 taps the lid blows off in a flash and the rewards
/// come out one by one as cards (rarity colours, a bigger show for epic and better). TOPLA closes it.
/// The rewards are already given when this opens; it only shows them.
/// </summary>
public class CrateOpening : MonoBehaviour
{
    private Shop.CrateDef crate;
    private List<GrantedReward> rewards;
    private System.Action done;
    private string heading;

    private RectTransform boxRoot, body, lid, rays, glow, cardsRoot;
    private RawImage raysImg, glowImg;
    private Image flash, bg;
    private Text tapText, titleText;
    private readonly List<Image> dots = new List<Image>();
    private readonly List<Image> cracks = new List<Image>();
    private readonly List<Spark> sparks = new List<Spark>();
    private GameObject collectButton;
    private int taps, needed;
    private float shake, punch, glowLevel, t;
    private bool opened;

    private class Spark
    {
        public Image img;
        public Vector2 vel;
        public float life, max;
    }

    private static Texture2D raysTex, glowTex;

    /// <summary>Shows the opening of <paramref name="crateId"/> with rewards that were already given.</summary>
    public static CrateOpening Show(string crateId, List<GrantedReward> given, string title, System.Action closed)
    {
        var go = new GameObject("CrateOpening");
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 400;
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 1f;
        go.AddComponent<GraphicRaycaster>();
        var c = go.AddComponent<CrateOpening>();
        c.crate = Shop.Crate(crateId);
        c.rewards = given ?? new List<GrantedReward>();
        c.done = closed;
        c.heading = title;
        c.needed = Random.Range(4, 6);
        c.Build(go.transform);
        return c;
    }

    // ----- Textures -----

    private static Texture2D Rays()
    {
        if (raysTex != null)
            return raysTex;
        const int n = 256;
        var px = new Color32[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = (x + 0.5f) / n - 0.5f, dy = (y + 0.5f) / n - 0.5f;
                float r = Mathf.Sqrt(dx * dx + dy * dy) * 2f;
                float a = Mathf.Atan2(dy, dx);
                float ray = Mathf.Pow(Mathf.Abs(Mathf.Cos(a * 7f)), 14f) + 0.6f * Mathf.Pow(Mathf.Abs(Mathf.Cos(a * 7f + 1.5708f)), 30f);
                float fade = Mathf.Clamp01(1f - r) * Mathf.Clamp01(r * 4f);
                byte v = (byte)(Mathf.Clamp01(ray * fade) * 255f);
                px[y * n + x] = new Color32(255, 255, 255, v);
            }
        raysTex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        raysTex.wrapMode = TextureWrapMode.Clamp;
        raysTex.SetPixels32(px);
        raysTex.Apply();
        return raysTex;
    }

    private static Texture2D Glow()
    {
        if (glowTex != null)
            return glowTex;
        const int n = 128;
        var px = new Color32[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = (x + 0.5f) / n - 0.5f, dy = (y + 0.5f) / n - 0.5f;
                float r = Mathf.Sqrt(dx * dx + dy * dy) * 2f;
                float a = Mathf.Clamp01(1f - r);
                px[y * n + x] = new Color32(255, 255, 255, (byte)(a * a * 255f));
            }
        glowTex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        glowTex.wrapMode = TextureWrapMode.Clamp;
        glowTex.SetPixels32(px);
        glowTex.Apply();
        return glowTex;
    }

    // ----- Building -----

    private void Build(Transform t0)
    {
        var c = new Vector2(0.5f, 0.5f);
        bg = UIUtil.CreateImage(t0, "Dim", c, Vector2.zero, new Vector2(5000f, 3000f), new Color(0.01f, 0.02f, 0.04f, 0f), false);
        var tap = bg.gameObject.AddComponent<Button>();
        tap.transition = Selectable.Transition.None;
        tap.onClick.AddListener(Tap);

        glow = UIUtil.CreateRect(t0, "Glow", c, new Vector2(0f, -10f), new Vector2(1100f, 1100f));
        glowImg = glow.gameObject.AddComponent<RawImage>();
        glowImg.texture = Glow();
        glowImg.raycastTarget = false;
        rays = UIUtil.CreateRect(t0, "Rays", c, new Vector2(0f, -10f), new Vector2(1400f, 1400f));
        raysImg = rays.gameObject.AddComponent<RawImage>();
        raysImg.texture = Rays();
        raysImg.raycastTarget = false;

        titleText = UIUtil.CreateText(t0, heading ?? crate.name, new Vector2(0.5f, 1f), new Vector2(0f, -90f), new Vector2(1600f, 80f), 54, TextAnchor.MiddleCenter);
        titleText.fontStyle = FontStyle.Bold;
        titleText.color = Color.Lerp(crate.color, Color.white, 0.35f);

        boxRoot = UIUtil.CreateRect(t0, "Box", c, new Vector2(0f, -40f), new Vector2(560f, 560f));
        body = Piece(boxRoot, "Crates/" + crate.id + "_body");
        for (int i = 0; i < 7; i++)
        {
            var k = UIUtil.CreateImage(body, "Crack", c, new Vector2(Random.Range(-150f, 150f), Random.Range(-160f, -20f)),
                new Vector2(Random.Range(70f, 150f), 7f), new Color(1f, 0.97f, 0.8f, 0f), false);
            k.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Random.Range(-70f, 70f));
            k.raycastTarget = false;
            cracks.Add(k);
        }
        lid = Piece(boxRoot, "Crates/" + crate.id + "_lid");

        tapText = UIUtil.CreateText(t0, "DOKUN!", c, new Vector2(0f, -390f), new Vector2(900f, 70f), 50, TextAnchor.MiddleCenter);
        tapText.fontStyle = FontStyle.Bold;
        for (int i = 0; i < needed; i++)
        {
            var d = UIUtil.CreateImage(t0, "Dot", c, new Vector2((i - (needed - 1) * 0.5f) * 44f, -450f), new Vector2(24f, 24f), new Color(1f, 1f, 1f, 0.25f), true);
            d.raycastTarget = false;
            dots.Add(d);
        }

        cardsRoot = UIUtil.CreateRect(t0, "Cards", c, new Vector2(0f, -20f), new Vector2(1800f, 500f));

        Text label;
        var collect = UIUtil.CreateButton(t0, "TOPLA", new Vector2(0.5f, 0f), new Vector2(0f, 110f), new Vector2(420f, 110f), Theme.Accent, false, 48, out label);
        label.color = new Color(0.1f, 0.08f, 0.02f);
        label.GetComponent<Shadow>().enabled = false;
        collect.onClick.AddListener(Close);
        collectButton = collect.gameObject;
        collectButton.SetActive(false);

        flash = UIUtil.CreateImage(t0, "Flash", c, Vector2.zero, new Vector2(5000f, 3000f), new Color(1f, 1f, 1f, 0f), false);
        flash.raycastTarget = false;
        Sfx.Play(SoundBank.Whoosh, 0.5f, 0.9f);
    }

    private static RectTransform Piece(RectTransform parent, string resource)
    {
        var r = UIUtil.CreateRect(parent, resource, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(560f, 560f));
        var img = r.gameObject.AddComponent<RawImage>();
        img.texture = Resources.Load<Texture2D>("UI/" + resource);
        img.raycastTarget = false;
        if (img.texture == null)
            img.color = new Color(0f, 0f, 0f, 0f);
        return r;
    }

    // ----- Tapping -----

    private void Tap()
    {
        if (opened)
            return;
        taps++;
        shake = 1f;
        punch = 1f;
        glowLevel = Mathf.Min(1f, glowLevel + 0.22f);
        if (taps - 1 < dots.Count)
            dots[taps - 1].color = Color.Lerp(crate.accent, Color.white, 0.3f);
        for (int i = 0; i < cracks.Count; i++)
            if (i < taps * cracks.Count / Mathf.Max(1, needed - 1))
                cracks[i].color = new Color(1f, 0.97f, 0.8f, 0.9f);
        Burst(10 + taps * 3, 500f + taps * 120f, 0.7f);
        Sfx.Play(SoundBank.Hit, 0.7f, 0.75f + taps * 0.12f);
        Haptics.Tap(25 + taps * 8);
        tapText.text = taps >= needed ? "" : (needed - taps) + " DOKUNUŞ KALDI";
        if (taps >= needed)
            StartCoroutine(Open());
    }

    private void Burst(int count, float speed, float life)
    {
        var c = new Vector2(0.5f, 0.5f);
        for (int i = 0; i < count; i++)
        {
            float a = Random.Range(0f, Mathf.PI * 2f);
            var img = UIUtil.CreateImage(transform, "Spark", c, boxRoot.anchoredPosition + new Vector2(0f, 20f), Vector2.one * Random.Range(8f, 20f),
                Random.value < 0.5f ? Color.white : Color.Lerp(crate.accent, Color.white, 0.4f), Random.value < 0.6f);
            img.raycastTarget = false;
            img.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 90f));
            sparks.Add(new Spark { img = img, vel = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * speed * Random.Range(0.4f, 1.2f), life = life * Random.Range(0.6f, 1.2f), max = life });
        }
    }

    private IEnumerator Open()
    {
        opened = true;
        // wind-up
        float w = 0f;
        while (w < 0.45f)
        {
            w += Time.unscaledDeltaTime;
            shake = 0.6f + w;
            yield return null;
        }
        // boom
        Sfx.Play(SoundBank.Explosion, 0.35f, 1.4f);
        Sfx.Play(SoundBank.Kill, 0.8f, 1.1f);
        Haptics.Tap(120);
        Burst(70, 1300f, 1.1f);
        float f = 0f;
        var lidStart = lid.anchoredPosition;
        while (f < 1.1f)
        {
            f += Time.unscaledDeltaTime;
            flash.color = new Color(1f, 1f, 1f, Mathf.Clamp01(1f - f * 2.2f) * 0.95f);
            lid.anchoredPosition = lidStart + new Vector2(f * 380f, f * 900f - f * f * 300f);
            lid.localRotation = Quaternion.Euler(0f, 0f, -f * 160f);
            float s = Mathf.Lerp(1f, 0.55f, Mathf.Clamp01(f / 0.6f));
            boxRoot.localScale = Vector3.one * s;
            boxRoot.anchoredPosition = Vector2.Lerp(new Vector2(0f, -40f), new Vector2(0f, -330f), Mathf.Clamp01(f / 0.6f));
            yield return null;
        }
        lid.gameObject.SetActive(false);
        foreach (var d in dots)
            d.gameObject.SetActive(false);
        titleText.text = "KAZANDIKLARIN";

        // the rewards, one by one
        int n = rewards.Count;
        float cardW = Mathf.Min(300f, 1700f / Mathf.Max(1, n) - 30f);
        for (int i = 0; i < n; i++)
        {
            float x = (i - (n - 1) * 0.5f) * (cardW + 30f);
            yield return Card(rewards[i], new Vector2(x, 60f), cardW);
            yield return new WaitForSecondsRealtime(0.18f);
        }
        collectButton.SetActive(true);
        collectButton.transform.localScale = Vector3.zero;
        float k = 0f;
        while (k < 0.25f)
        {
            k += Time.unscaledDeltaTime;
            collectButton.transform.localScale = Vector3.one * Back(k / 0.25f);
            yield return null;
        }
        collectButton.transform.localScale = Vector3.one;
    }

    private IEnumerator Card(GrantedReward g, Vector2 pos, float w)
    {
        var r = g.reward;
        string rarity = r.Rarity;
        Color rc = Theme.Rarity(rarity);
        bool big = rarity == "Epik" || rarity == "Efsanevi" || rarity == "Mitik";
        var c = new Vector2(0.5f, 0.5f);
        var root = UIUtil.CreateRect(cardsRoot, "Card", c, pos, new Vector2(w, 420f));
        if (big)
        {
            var rr = UIUtil.CreateRect(root, "Rays", c, new Vector2(0f, 40f), new Vector2(w * 2.2f, w * 2.2f));
            var ri = rr.gameObject.AddComponent<RawImage>();
            ri.texture = Rays();
            ri.color = new Color(rc.r, rc.g, rc.b, 0.55f);
            ri.raycastTarget = false;
            rr.gameObject.AddComponent<Spin>().speed = 25f;
        }
        var panel = UIUtil.CreateImage(root, "Panel", c, Vector2.zero, new Vector2(w, 420f), new Color(rc.r * 0.22f, rc.g * 0.22f, rc.b * 0.22f, 0.96f), false);
        panel.raycastTarget = false;
        var edge = panel.gameObject.AddComponent<Outline>();
        edge.effectColor = rc;
        edge.effectDistance = new Vector2(4f, -4f);
        RewardView.Create(root, r, c, new Vector2(0f, 55f), Mathf.Min(w - 50f, 220f));
        var name = UIUtil.CreateText(root, r.Name, c, new Vector2(0f, -110f), new Vector2(w - 20f, 70f), 26, TextAnchor.MiddleCenter);
        name.fontStyle = FontStyle.Bold;
        name.horizontalOverflow = HorizontalWrapMode.Wrap;
        var rl = UIUtil.CreateText(root, MapCatalog.TrUpper(rarity), c, new Vector2(0f, -160f), new Vector2(w - 20f, 34f), 22, TextAnchor.MiddleCenter);
        rl.color = rc;
        if (g.duplicate)
        {
            var dup = UIUtil.CreateText(root, "Zaten vardı  •  +" + Shop.DuplicateValue(r) + " Kredi", c, new Vector2(0f, -192f), new Vector2(w - 10f, 30f), 18, TextAnchor.MiddleCenter);
            dup.color = Theme.TextDim;
        }
        else if (r.kind != RewardKind.Credits)
        {
            var nw = UIUtil.CreateText(root, "YENİ!", new Vector2(1f, 1f), new Vector2(-50f, -26f), new Vector2(90f, 34f), 24, TextAnchor.MiddleCenter);
            nw.color = Theme.Good;
            nw.fontStyle = FontStyle.Bold;
        }
        Sfx.Play(SoundBank.Pickup, big ? 0.9f : 0.6f, big ? 0.8f : 1.1f);
        if (big)
            Haptics.Tap(60);
        root.localScale = new Vector3(0f, 1f, 1f);
        float k = 0f;
        while (k < 0.32f)
        {
            k += Time.unscaledDeltaTime;
            float e = Back(k / 0.32f);
            root.localScale = new Vector3(e, e, 1f);
            yield return null;
        }
        root.localScale = Vector3.one;
    }

    private static float Back(float x)
    {
        x = Mathf.Clamp01(x);
        const float s = 1.7f;
        x -= 1f;
        return x * x * ((s + 1f) * x + s) + 1f;
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        t += dt;
        bg.color = new Color(0.01f, 0.02f, 0.04f, Mathf.Min(0.9f, t * 3f));
        rays.localRotation = Quaternion.Euler(0f, 0f, -t * (12f + glowLevel * 30f));
        float pulse = 0.5f + 0.5f * Mathf.Sin(t * 3f);
        raysImg.color = new Color(crate.accent.r, crate.accent.g, crate.accent.b, (opened ? 0.75f : 0.18f + glowLevel * 0.45f) * (0.85f + 0.15f * pulse));
        glowImg.color = new Color(crate.color.r, crate.color.g, crate.color.b, (opened ? 0.5f : 0.25f + glowLevel * 0.4f) * (0.8f + 0.2f * pulse));
        glow.localScale = Vector3.one * (1f + glowLevel * 0.25f + 0.04f * pulse);

        if (!opened || shake > 0f)
        {
            float bob = opened ? 0f : Mathf.Sin(t * 2.2f) * 10f;
            shake = Mathf.MoveTowards(shake, 0f, dt * 3.5f);
            punch = Mathf.MoveTowards(punch, 0f, dt * 6f);
            float ang = Mathf.Sin(t * 45f) * 9f * shake;
            boxRoot.localRotation = Quaternion.Euler(0f, 0f, ang);
            if (!opened)
            {
                boxRoot.localScale = Vector3.one * (1f + punch * 0.14f);
                boxRoot.anchoredPosition = new Vector2(Mathf.Sin(t * 60f) * 10f * shake, -40f + bob);
            }
        }
        tapText.color = new Color(1f, 1f, 1f, opened ? 0f : 0.6f + 0.4f * Mathf.Abs(Mathf.Sin(t * 3.5f)));

        for (int i = sparks.Count - 1; i >= 0; i--)
        {
            var s = sparks[i];
            s.life -= dt;
            if (s.life <= 0f || s.img == null)
            {
                if (s.img != null)
                    Destroy(s.img.gameObject);
                sparks.RemoveAt(i);
                continue;
            }
            s.vel *= 1f - dt * 1.8f;
            s.vel.y -= 700f * dt;
            var rt = s.img.rectTransform;
            rt.anchoredPosition += s.vel * dt;
            var col = s.img.color;
            col.a = Mathf.Clamp01(s.life / s.max);
            s.img.color = col;
        }
    }

    private void Close()
    {
        UiSound.Confirm();
        Destroy(gameObject);
        if (done != null)
            done();
    }
}

/// <summary>Turns a UI element slowly (light rays behind special rewards).</summary>
public class Spin : MonoBehaviour
{
    public float speed = 20f;

    private void Update()
    {
        transform.Rotate(0f, 0f, -speed * Time.unscaledDeltaTime);
    }
}
