using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// On-screen mobile controls: floating joystick on the left, drag-to-look on the right,
/// fire buttons on both sides and action buttons.
/// </summary>
public class TouchControls : MonoBehaviour
{
    public static TouchControls Instance;

    public Vector2 Move { get; private set; }
    public Vector2 LookDelta { get; private set; }   // canvas units moved this frame
    public bool SprintOn { get; private set; }

    public bool FireHeld
    {
        get { return (rightFire != null && rightFire.Held) || (leftFire != null && leftFire.Held); }
    }

    private bool jumpQueued;
    private bool crouchQueued;
    private bool reloadQueued;
    private bool medkitQueued;

    private Canvas canvas;
    private RectTransform root;
    private RectTransform stickBase;
    private RectTransform stickKnob;
    private Vector2 stickHome;
    private const float StickRadius = 110f;

    private HoldButton rightFire;
    private HoldButton leftFire;
    private Image sprintImage;
    private Text medkitLabel;

    private int moveFinger = -1;
    private int lookFinger = -1;
    private Vector2 moveStartScreen;
    private Vector2 lastLookScreen;

    private static readonly Color ButtonColor = new Color(1f, 1f, 1f, 0.22f);
    private static readonly Color FireColor = new Color(0.95f, 0.3f, 0.25f, 0.55f);

    public bool ConsumeJump() { bool v = jumpQueued; jumpQueued = false; return v; }
    public bool ConsumeCrouch() { bool v = crouchQueued; crouchQueued = false; return v; }
    public bool ConsumeReload() { bool v = reloadQueued; reloadQueued = false; return v; }
    public bool ConsumeMedkit() { bool v = medkitQueued; medkitQueued = false; return v; }

    public void Build(Canvas parentCanvas)
    {
        Instance = this;
        canvas = parentCanvas;
        root = (RectTransform)transform;

        Text unused;

        // Movement stick (floats to wherever the left thumb lands).
        stickHome = new Vector2(250f, 250f);
        stickBase = UIUtil.CreateImage(root, "StickBase", Vector2.zero, stickHome, new Vector2(StickRadius * 2f, StickRadius * 2f), new Color(1f, 1f, 1f, 0.15f), true).rectTransform;
        stickBase.GetComponent<Image>().raycastTarget = false;
        stickKnob = UIUtil.CreateImage(stickBase, "StickKnob", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(100f, 100f), new Color(1f, 1f, 1f, 0.45f), true).rectTransform;
        stickKnob.GetComponent<Image>().raycastTarget = false;

        // Fire buttons.
        var fireR = UIUtil.CreateButton(root, "ATEŞ", new Vector2(1f, 0f), new Vector2(-230f, 260f), new Vector2(200f, 200f), FireColor, true, 30, out unused);
        rightFire = fireR.gameObject.AddComponent<HoldButton>();
        var fireL = UIUtil.CreateButton(root, "ATEŞ", new Vector2(0f, 0f), new Vector2(250f, 560f), new Vector2(130f, 130f), FireColor, true, 22, out unused);
        leftFire = fireL.gameObject.AddComponent<HoldButton>();

        // Actions.
        UIUtil.CreateButton(root, "ZIPLA", new Vector2(1f, 0f), new Vector2(-470f, 130f), new Vector2(130f, 130f), ButtonColor, true, 22, out unused)
            .onClick.AddListener(() => jumpQueued = true);
        UIUtil.CreateButton(root, "EĞİL", new Vector2(1f, 0f), new Vector2(-470f, 300f), new Vector2(120f, 120f), ButtonColor, true, 22, out unused)
            .onClick.AddListener(() => crouchQueued = true);
        UIUtil.CreateButton(root, "DOLDUR", new Vector2(1f, 0f), new Vector2(-230f, 500f), new Vector2(130f, 130f), ButtonColor, true, 20, out unused)
            .onClick.AddListener(() => reloadQueued = true);

        var medkit = UIUtil.CreateButton(root, "İLK YARDIM", new Vector2(0.5f, 0f), new Vector2(260f, 70f), new Vector2(200f, 90f), new Color(0.3f, 0.85f, 0.4f, 0.4f), false, 20, out medkitLabel);
        medkit.onClick.AddListener(() => medkitQueued = true);

        var sprint = UIUtil.CreateButton(root, "KOŞ", new Vector2(0f, 0f), new Vector2(110f, 440f), new Vector2(110f, 110f), ButtonColor, true, 22, out unused);
        sprintImage = sprint.GetComponent<Image>();
        sprint.onClick.AddListener(() =>
        {
            SprintOn = !SprintOn;
            sprintImage.color = SprintOn ? new Color(1f, 0.85f, 0.2f, 0.55f) : ButtonColor;
        });
    }

    public void SetMedkitCount(int count)
    {
        if (medkitLabel != null)
            medkitLabel.text = "İLK YARDIM x" + count;
    }

    public void ResetState()
    {
        Move = Vector2.zero;
        LookDelta = Vector2.zero;
        moveFinger = -1;
        lookFinger = -1;
        jumpQueued = crouchQueued = reloadQueued = medkitQueued = false;
        SprintOn = false;
        if (sprintImage != null)
            sprintImage.color = ButtonColor;
        if (stickBase != null)
        {
            stickBase.anchoredPosition = stickHome;
            stickKnob.anchoredPosition = Vector2.zero;
        }
    }

    private void OnDisable()
    {
        ResetState();
    }

    private void Update()
    {
        LookDelta = Vector2.zero;
        float scale = canvas != null ? canvas.scaleFactor : 1f;
        if (scale <= 0f)
            scale = 1f;

        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch t = Input.GetTouch(i);

            switch (t.phase)
            {
                case TouchPhase.Began:
                    if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(t.fingerId))
                        break;

                    if (t.position.x < Screen.width * 0.4f && moveFinger == -1)
                    {
                        moveFinger = t.fingerId;
                        moveStartScreen = t.position;
                        Vector2 local;
                        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(root, t.position, null, out local))
                            stickBase.anchoredPosition = local - root.rect.min;
                    }
                    else if (lookFinger == -1)
                    {
                        lookFinger = t.fingerId;
                        lastLookScreen = t.position;
                    }
                    break;

                case TouchPhase.Moved:
                case TouchPhase.Stationary:
                    if (t.fingerId == moveFinger)
                    {
                        Vector2 delta = (t.position - moveStartScreen) / scale;
                        Vector2 clamped = Vector2.ClampMagnitude(delta, StickRadius);
                        stickKnob.anchoredPosition = clamped;
                        Move = clamped / StickRadius;
                    }
                    else if (t.fingerId == lookFinger)
                    {
                        LookDelta += (t.position - lastLookScreen) / scale;
                        lastLookScreen = t.position;
                    }
                    break;

                case TouchPhase.Ended:
                case TouchPhase.Canceled:
                    if (t.fingerId == moveFinger)
                    {
                        moveFinger = -1;
                        Move = Vector2.zero;
                        stickBase.anchoredPosition = stickHome;
                        stickKnob.anchoredPosition = Vector2.zero;
                    }
                    else if (t.fingerId == lookFinger)
                    {
                        lookFinger = -1;
                    }
                    break;
            }
        }
    }
}
