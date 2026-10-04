using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>Press feel for every button: quick squash on touch, spring back, a soft click and a short buzz.</summary>
public class ButtonFeel : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler, IPointerClickHandler
{
    public bool silent;          // in-game controls: no click sound
    private Vector3 baseScale;
    private float current = 1f, target = 1f;
    private bool pressed;

    private void Awake()
    {
        baseScale = transform.localScale;
    }

    private bool Interactable()
    {
        var sel = GetComponent<UnityEngine.UI.Selectable>();
        return sel == null || sel.IsInteractable();
    }

    public void OnPointerDown(PointerEventData e)
    {
        if (!Interactable())
            return;
        pressed = true;
        target = 0.92f;
        Haptics.Tap(silent ? 8 : 12);
    }

    public void OnPointerUp(PointerEventData e)
    {
        pressed = false;
        target = 1f;
    }

    public void OnPointerExit(PointerEventData e)
    {
        if (pressed)
            target = 1f;
    }

    public void OnPointerClick(PointerEventData e)
    {
        if (!silent && Interactable())
            UiSound.Click();
        current = Mathf.Min(current, 0.94f);
    }

    private void OnDisable()
    {
        pressed = false;
        current = target = 1f;
        if (baseScale != Vector3.zero)
            transform.localScale = baseScale;
    }

    private void Update()
    {
        if (Mathf.Abs(current - target) < 0.001f)
            return;
        // Springy return: fast in, slightly slower out.
        current = Mathf.MoveTowards(current, target, Time.unscaledDeltaTime * (target < current ? 1.6f : 0.9f));
        transform.localScale = baseScale * current;
    }
}

/// <summary>Panels fade and settle in when they open.</summary>
public class PopIn : MonoBehaviour
{
    private CanvasGroup group;
    private float t;

    private void OnEnable()
    {
        if (group == null)
        {
            group = GetComponent<CanvasGroup>();
            if (group == null)
                group = gameObject.AddComponent<CanvasGroup>();
        }
        t = 0f;
        group.alpha = 0f;
        transform.localScale = Vector3.one * 0.985f;
    }

    private void Update()
    {
        if (t >= 1f)
            return;
        t = Mathf.Min(1f, t + Time.unscaledDeltaTime / 0.18f);
        float e = 1f - (1f - t) * (1f - t);
        group.alpha = e;
        transform.localScale = Vector3.one * Mathf.Lerp(0.985f, 1f, e);
    }
}

public static class UiSound
{
    private static AudioClip click, confirm;

    public static void Click()
    {
        if (!GameSettings.UiSounds)
            return;
        if (click == null)
            click = SoundBank.MakeUiTone("UiClick", 0.035f, 1900f, 1300f, 110f, 0.32f);
        Sfx.Play(click, 0.5f, Random.Range(0.97f, 1.03f));
    }

    public static void Confirm()
    {
        if (!GameSettings.UiSounds)
            return;
        if (confirm == null)
            confirm = SoundBank.MakeUiTone("UiConfirm", 0.12f, 700f, 1400f, 22f, 0.35f);
        Sfx.Play(confirm, 0.6f, 1f);
    }
}

/// <summary>Short vibrations (Android VibrationEffect), off with Ayarlar > Kontroller > Titreşim.</summary>
public static class Haptics
{
#if UNITY_ANDROID && !UNITY_EDITOR
    private static AndroidJavaObject vibrator;
    private static AndroidJavaClass effectClass;
    private static int sdk;
#endif
    private static bool tried;
    private static float lastTap;

    public static void Tap(int milliseconds)
    {
        if (!GameSettings.Vibration || Time.unscaledTime - lastTap < 0.04f)
            return;
        lastTap = Time.unscaledTime;
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            if (!tried)
            {
                tried = true;
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                    vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
                using (var version = new AndroidJavaClass("android.os.Build$VERSION"))
                    sdk = version.GetStatic<int>("SDK_INT");
            }
            if (vibrator == null)
                return;
            if (sdk >= 26)
            {
                if (effectClass == null)
                    effectClass = new AndroidJavaClass("android.os.VibrationEffect");
                using (var oneShot = effectClass.CallStatic<AndroidJavaObject>("createOneShot", (long)milliseconds, Mathf.Clamp(milliseconds * 6, 40, 255)))
                    vibrator.Call("vibrate", oneShot);
            }
            else
            {
                vibrator.Call("vibrate", (long)milliseconds);
            }
        }
        catch (System.Exception)
        {
            vibrator = null;
        }
#else
        if (tried)
            return;
#endif
    }

    /// <summary>Long buzz (knocked down / eliminated). Also makes Unity add the VIBRATE permission.</summary>
    public static void Long()
    {
#if UNITY_ANDROID || UNITY_IOS
        if (GameSettings.Vibration)
            Handheld.Vibrate();
#endif
    }
}
