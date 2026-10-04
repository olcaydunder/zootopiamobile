using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Battle HUD crosshair. Five shapes (Ayarlar → Nişangâh), each with a thin dark outline so it reads
/// on any background. It is dynamic: it opens up while you move, sprint, jump or keep firing (bloom
/// that settles back), tightens when you aim down sights, and turns red over an enemy.
/// </summary>
public class Crosshair : MonoBehaviour
{
    public static readonly string[] StyleNames = { "KLASİK", "NOKTA", "DAİRE", "T", "ÇAPRAZ" };

    private RectTransform root;
    private Image[] arms = new Image[4];
    private Image dot, ring;
    private int builtStyle = -1;
    private float bloom;
    private float gap = 14f;
    private float lastShot = -10f;

    public static Crosshair Create(Transform hud)
    {
        var rect = UIUtil.CreateRect(hud, "Crosshair", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(200f, 200f));
        var c = rect.gameObject.AddComponent<Crosshair>();
        c.root = rect;
        return c;
    }

    private Image Part(string name, Vector2 size)
    {
        var img = UIUtil.CreateImage(root, name, new Vector2(0.5f, 0.5f), Vector2.zero, size, Color.white, false);
        img.raycastTarget = false;
        var outline = img.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.55f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);
        return img;
    }

    private void Rebuild(int style)
    {
        for (int i = root.childCount - 1; i >= 0; i--)
            Destroy(root.GetChild(i).gameObject);
        builtStyle = style;
        arms = new Image[4];
        dot = null;
        ring = null;
        switch (style)
        {
            case 1:   // dot
                dot = Part("Dot", new Vector2(9f, 9f));
                dot.sprite = UIUtil.Circle;
                break;
            case 2:   // ring + dot
                ring = Part("Ring", new Vector2(44f, 44f));
                ring.sprite = UIUtil.Ring;
                dot = Part("Dot", new Vector2(5f, 5f));
                dot.sprite = UIUtil.Circle;
                break;
            case 4:   // X
                for (int i = 0; i < 4; i++)
                {
                    arms[i] = Part("Arm" + i, new Vector2(14f, 3f));
                    arms[i].rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f + 90f * i);
                }
                break;
            default:  // classic (0) and T (3): lines with a gap, a small centre dot
                for (int i = 0; i < 4; i++)
                {
                    if (style == 3 && i == 1)
                        continue;   // T: no top line
                    bool horizontal = i % 2 == 0;
                    arms[i] = Part("Arm" + i, horizontal ? new Vector2(14f, 3f) : new Vector2(3f, 14f));
                }
                dot = Part("Dot", new Vector2(4f, 4f));
                break;
        }
    }

    /// <summary>The player just fired (adds bloom).</summary>
    public void Kick(float amount)
    {
        bloom = Mathf.Min(1f, bloom + amount);
        lastShot = Time.time;
    }

    public void Tick(PlayerController p, bool enemyUnder)
    {
        int style = Mathf.Clamp(GameSettings.CrosshairStyle, 0, StyleNames.Length - 1);
        if (style != builtStyle)
            Rebuild(style);

        bool show = p != null && !p.isDead && p.state == PlayerState.Ground && !p.IsScoped && !p.lobbyView;
        if (root.gameObject.activeSelf != show)
            root.gameObject.SetActive(show);
        if (!show)
            return;

        // How open: movement, sprint, airborne, weapon spread, recent firing; aiming down sights tightens.
        var w = p.currentWeapon != null ? p.currentWeapon.weaponData : null;
        float spread = w != null ? w.spread : 1.5f;
        float speed = p.controller != null ? new Vector2(p.controller.velocity.x, p.controller.velocity.z).magnitude : 0f;
        float move = Mathf.Clamp01(speed / 6f) * (p.isSprinting ? 1.6f : 1f);
        bool air = p.controller != null && !p.controller.isGrounded;
        bloom = Mathf.MoveTowards(bloom, 0f, Time.deltaTime * (Time.time - lastShot > 0.25f ? 2.5f : 0.6f));
        float target = 7f + spread * 4.5f + move * 16f + (air ? 18f : 0f) + bloom * 22f + (p.isCrouching ? -3f : 0f);
        if (p.aimingDownSights)
            target *= 0.45f;
        gap = Mathf.Lerp(gap, Mathf.Clamp(target, 3f, 70f), Time.deltaTime * 14f);

        Color col = enemyUnder ? new Color(1f, 0.25f, 0.2f, 0.95f) : GameSettings.CrosshairColors[GameSettings.CrosshairColor];
        float len = p.aimingDownSights ? 10f : 13f;
        for (int i = 0; i < 4; i++)
        {
            var a = arms[i];
            if (a == null)
                continue;
            a.color = col;
            var rt = a.rectTransform;
            if (style == 4)
            {
                float d = gap * 0.85f + len * 0.5f;
                float ang = (45f + 90f * i) * Mathf.Deg2Rad;
                rt.anchoredPosition = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * d;
                rt.sizeDelta = new Vector2(len, 3f);
            }
            else
            {
                float d = gap + len * 0.5f;
                Vector2 dir = i == 0 ? Vector2.right : i == 1 ? Vector2.up : i == 2 ? Vector2.left : Vector2.down;
                rt.anchoredPosition = dir * d;
                rt.sizeDelta = i % 2 == 0 ? new Vector2(len, 3f) : new Vector2(3f, len);
            }
        }
        if (dot != null)
            dot.color = col;
        if (ring != null)
        {
            ring.color = col;
            float size = 22f + gap * 1.6f;
            ring.rectTransform.sizeDelta = new Vector2(size, size);
        }
    }
}
