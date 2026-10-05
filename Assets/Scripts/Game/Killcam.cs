using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// When the local player is eliminated, the camera flies to whoever shot them and holds on them for about two
/// seconds with a short music sting: a cinematic letterbox, "TARAFINDAN ÖLDÜRÜLDÜN" with the killer's name, their
/// weapon and how much health they have left. PlayerController.LateUpdate hands the camera over while it runs.
/// </summary>
public class Killcam : MonoBehaviour
{
    public const float Duration = 2.4f;
    private const float FlyTime = 0.55f;

    private static Killcam instance;

    private Transform target;
    private Camera cam;
    private bool running;
    private float startTime;
    private Vector3 fromPos;
    private Quaternion fromRot;
    private float fromFov;
    private Vector3 approach;          // from the killer towards where the victim was (horizontal)
    private System.Func<float> healthSource;

    private RectTransform root;
    private CanvasGroup group;
    private RectTransform barTop, barBottom;
    private Text nameText, weaponText, healthText;
    private RawImage weaponPic;
    private Image healthFill, flash;

    /// <summary>True while the killcam drives the camera.</summary>
    public static bool Active { get { return instance != null && instance.running && instance.target != null && Time.unscaledTime - instance.startTime < Duration; } }

    /// <summary>
    /// Starts the killcam on <paramref name="killer"/> (null: nothing happens). <paramref name="health"/> reports the
    /// killer's health 0-1 while it runs (or null).
    /// </summary>
    public static void Begin(Transform killer, Camera cam, string killerName, WeaponData weapon, System.Func<float> health)
    {
        if (killer == null || cam == null || NetGame.IsServer)
            return;
        var gm = GameManager.Instance;
        if (gm == null || gm.uiManager == null)
            return;
        if (instance == null)
        {
            var go = new GameObject("Killcam", typeof(RectTransform));
            go.transform.SetParent(gm.uiManager.transform, false);
            instance = go.AddComponent<Killcam>();
            instance.Build();
        }
        instance.Run(killer, cam, killerName, weapon, health);
    }

    /// <summary>Stops at once (respawn, leaving the match).</summary>
    public static void Stop()
    {
        if (instance == null || !instance.running)
            return;
        instance.running = false;
        instance.target = null;
        instance.root.gameObject.SetActive(false);
        if (instance.cam != null)
        {
            // back to the normal third-person camera (it only sets the local position)
            instance.cam.transform.localRotation = Quaternion.identity;
            instance.cam.fieldOfView = GameSettings.Fov;
        }
    }

    private void Build()
    {
        root = (RectTransform)transform;
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.offsetMin = root.offsetMax = Vector2.zero;
        group = gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;
        var c = new Vector2(0.5f, 0.5f);

        // red flash at the start, then letterbox bars sliding in
        flash = UIUtil.CreateStretch(root, "Flash").gameObject.AddComponent<Image>();
        flash.color = new Color(0.8f, 0.05f, 0.05f, 0f);
        flash.raycastTarget = false;
        barTop = UIUtil.CreateImage(root, "BarTop", new Vector2(0.5f, 1f), Vector2.zero, new Vector2(4000f, 130f), Color.black, false).rectTransform;
        barTop.pivot = new Vector2(0.5f, 1f);
        barBottom = UIUtil.CreateImage(root, "BarBottom", new Vector2(0.5f, 0f), Vector2.zero, new Vector2(4000f, 130f), Color.black, false).rectTransform;
        barBottom.pivot = new Vector2(0.5f, 0f);
        barTop.GetComponent<Image>().raycastTarget = false;
        barBottom.GetComponent<Image>().raycastTarget = false;

        // banner
        var killed = UIUtil.CreateText(barTop, "TARAFINDAN ÖLDÜRÜLDÜN", new Vector2(0.5f, 0f), new Vector2(0f, 96f), new Vector2(1000f, 50f), 36, TextAnchor.MiddleCenter);
        killed.fontStyle = FontStyle.Bold;
        nameText = UIUtil.CreateText(root, "", new Vector2(0.5f, 1f), new Vector2(0f, -190f), new Vector2(1200f, 90f), 70, TextAnchor.MiddleCenter);
        nameText.fontStyle = FontStyle.Bold;
        nameText.color = new Color(1f, 0.28f, 0.22f);

        // killer's weapon card (bottom-left) and health (bottom-right)
        var card = UIUtil.CreateImage(root, "WeaponCard", new Vector2(0f, 0f), new Vector2(250f, 230f), new Vector2(400f, 200f), new Color(0.42f, 0.18f, 0.6f, 0.92f), false);
        card.raycastTarget = false;
        weaponPic = UIUtil.CreateRect(card.transform, "Gun", c, new Vector2(0f, 20f), new Vector2(280f, 140f)).gameObject.AddComponent<RawImage>();   // 2:1 pictures
        weaponPic.raycastTarget = false;
        weaponText = UIUtil.CreateText(card.transform, "", new Vector2(0.5f, 0f), new Vector2(0f, 24f), new Vector2(380f, 36f), 28, TextAnchor.MiddleCenter);
        weaponText.fontStyle = FontStyle.Bold;

        var hp = UIUtil.CreateImage(root, "Health", new Vector2(1f, 0f), new Vector2(-250f, 230f), new Vector2(400f, 110f), new Color(0.05f, 0.06f, 0.08f, 0.85f), false);
        hp.raycastTarget = false;
        var hl = UIUtil.CreateText(hp.transform, "KALAN CANI", new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(380f, 34f), 24, TextAnchor.MiddleCenter);
        hl.color = Theme.TextDim;
        var hbg = UIUtil.CreateImage(hp.transform, "Bg", c, new Vector2(-30f, -16f), new Vector2(300f, 18f), new Color(1f, 1f, 1f, 0.15f), false);
        hbg.raycastTarget = false;
        healthFill = UIUtil.CreateImage(hbg.transform, "Fill", new Vector2(0f, 0.5f), Vector2.zero, new Vector2(300f, 18f), new Color(0.95f, 0.3f, 0.25f), false);
        healthFill.rectTransform.pivot = new Vector2(0f, 0.5f);
        healthFill.raycastTarget = false;
        healthText = UIUtil.CreateText(hp.transform, "", new Vector2(1f, 0.5f), new Vector2(-46f, -16f), new Vector2(80f, 36f), 28, TextAnchor.MiddleRight);
        healthText.fontStyle = FontStyle.Bold;
        root.gameObject.SetActive(false);
    }

    private void Run(Transform killer, Camera cam, string killerName, WeaponData weapon, System.Func<float> health)
    {
        target = killer;
        this.cam = cam;
        running = true;
        healthSource = health;
        startTime = Time.unscaledTime;
        fromPos = cam.transform.position;
        fromRot = cam.transform.rotation;
        fromFov = cam.fieldOfView;
        approach = fromPos - killer.position;
        approach.y = 0f;
        if (approach.sqrMagnitude < 0.01f)
            approach = killer.forward;
        approach.Normalize();

        nameText.text = string.IsNullOrEmpty(killerName) ? "DÜŞMAN" : killerName.ToUpper();
        if (weapon != null)
        {
            string skin = string.IsNullOrEmpty(weapon.modelSkin) ? weapon.weaponType.ToString() : weapon.weaponType + "_" + weapon.modelSkin;
            var tex = Resources.Load<Texture2D>("UI/Guns/" + skin) ?? Resources.Load<Texture2D>("UI/Guns/" + weapon.weaponType);
            weaponPic.texture = tex;
            weaponPic.enabled = tex != null;
            weaponText.text = string.IsNullOrEmpty(weapon.modelSkin) ? weapon.weaponName : ModelLibrary.GunSkinName(weapon.weaponType, weapon.modelSkin);
        }
        else
        {
            weaponPic.enabled = false;
            weaponText.text = "";
        }
        root.SetAsLastSibling();
        root.gameObject.SetActive(true);
        Sfx.Play(SoundBank.KillcamSting, 0.9f);
    }

    /// <summary>Moves the camera while the killcam runs; false when it isn't running (the normal camera takes over).</summary>
    public static bool Drive(Camera cam)
    {
        if (instance == null || !instance.running)
            return false;
        var k = instance;
        float t = Time.unscaledTime - k.startTime;
        if (k.target == null || t >= Duration || !k.target.gameObject.activeInHierarchy)
        {
            Stop();
            return false;
        }

        // In front of the killer, a little to the side, looking at their chest; a slow push-in after the fly.
        Vector3 chest = k.target.position + Vector3.up * 0.35f;
        Vector3 side = Vector3.Cross(Vector3.up, k.approach);
        float hold = Mathf.Clamp01((t - FlyTime) / (Duration - FlyTime));
        float dist = Mathf.Lerp(2.9f, 2.2f, hold);
        Vector3 want = chest + k.approach * dist + side * 0.7f + Vector3.up * 0.35f;
        RaycastHit hit;
        if (Physics.Linecast(chest, want, out hit, Physics.DefaultRaycastLayers & ~(1 << PlayerController.IgnoreRaycastLayer), QueryTriggerInteraction.Ignore))
            want = chest + (want - chest).normalized * Mathf.Max(0.8f, hit.distance - 0.25f);
        Quaternion look = Quaternion.LookRotation(chest - want, Vector3.up);

        float fly = Mathf.Clamp01(t / FlyTime);
        float e = 1f - Mathf.Pow(1f - fly, 3f);
        cam.transform.position = Vector3.Lerp(k.fromPos, want, e);
        cam.transform.rotation = Quaternion.Slerp(k.fromRot, look, e);
        cam.fieldOfView = Mathf.Lerp(k.fromFov, 38f, e) - hold * 4f;

        // overlay animation
        float inK = Mathf.Clamp01(t / 0.3f), outK = Mathf.Clamp01((Duration - t) / 0.25f);
        k.group.alpha = Mathf.Min(inK, outK);
        k.flash.color = new Color(0.8f, 0.05f, 0.05f, 0.45f * Mathf.Clamp01(1f - t / 0.35f));
        float bar = 130f * Mathf.Min(inK, outK);
        k.barTop.sizeDelta = new Vector2(4000f, bar);
        k.barBottom.sizeDelta = new Vector2(4000f, bar);
        float hp01 = k.healthSource != null ? Mathf.Clamp01(k.healthSource()) : 1f;
        k.healthFill.rectTransform.sizeDelta = new Vector2(300f * hp01, 18f);
        k.healthText.text = Mathf.RoundToInt(hp01 * 100f).ToString();
        return true;
    }
}
