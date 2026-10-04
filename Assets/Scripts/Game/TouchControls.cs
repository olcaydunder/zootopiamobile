using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// On-screen mobile controls: floating joystick on the left, drag-to-look on the right
/// (also while holding the fire button), fire buttons on both sides and action buttons.
/// Buttons that only make sense in some situations (jumping out of the plane, entering a jeep)
/// are shown by the UI depending on the player's state.
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

    private bool jumpQueued, crouchQueued, reloadQueued, medkitQueued;
    private bool drinkQueued, grenadeQueued, swapQueued, vehicleQueued, airQueued, aimQueued, doorQueued;
    private Image aimImage;

    private Canvas canvas;
    private RectTransform root;
    private RectTransform stickBase;
    private RectTransform stickKnob;
    private Vector2 stickHome;
    private const float StickRadius = 110f;

    private HoldButton rightFire;
    private HoldButton leftFire;
    private RectTransform rightFireRect;
    private Vector2 rightFireHome;
    private bool lookIsFireFinger;
    private int fireFinger = -1;
    private Vector2 fireFollowStart;
    private CanvasGroup group;
    private Image sprintImage;
    private Text medkitLabel, drinkLabel, grenadeLabel, swapLabel, vehicleLabel, airLabel;
    private GameObject combatGroup;
    private GameObject swapButton;
    private GameObject vehicleButton;
    private GameObject doorButton;
    private Text doorLabel;
    private GameObject airButton;

    private int moveFinger = -1;
    private int lookFinger = -1;
    private Vector2 moveStartScreen;
    private Vector2 lastLookScreen;

    private static readonly Color ButtonColor = new Color(1f, 1f, 1f, 0.22f);
    private static readonly Color FireColor = new Color(0.95f, 0.3f, 0.25f, 0.55f);
    private static readonly Color ItemColor = new Color(0.3f, 0.85f, 0.4f, 0.4f);

    public bool ConsumeJump() { bool v = jumpQueued; jumpQueued = false; return v; }
    public bool ConsumeCrouch() { bool v = crouchQueued; crouchQueued = false; return v; }
    public bool ConsumeReload() { bool v = reloadQueued; reloadQueued = false; return v; }
    public bool ConsumeMedkit() { bool v = medkitQueued; medkitQueued = false; return v; }
    public bool ConsumeDrink() { bool v = drinkQueued; drinkQueued = false; return v; }
    public bool ConsumeGrenade() { bool v = grenadeQueued; grenadeQueued = false; return v; }
    public bool ConsumeSwap() { bool v = swapQueued; swapQueued = false; return v; }
    public bool ConsumeVehicle() { bool v = vehicleQueued; vehicleQueued = false; return v; }
    public bool ConsumeAirAction() { bool v = airQueued; airQueued = false; return v; }
    public bool ConsumeDoor() { bool v = doorQueued; doorQueued = false; return v; }
    public bool ConsumeAim() { bool v = aimQueued; aimQueued = false; return v; }

    public void Build(Canvas parentCanvas)
    {
        Instance = this;
        canvas = parentCanvas;
        root = (RectTransform)transform;
        group = gameObject.AddComponent<CanvasGroup>();

        Text unused;

        // Movement stick (floats to wherever the left thumb lands).
        stickHome = new Vector2(250f, 250f);
        stickBase = UIUtil.CreateImage(root, "StickBase", Vector2.zero, stickHome, new Vector2(StickRadius * 2f, StickRadius * 2f), new Color(1f, 1f, 1f, 0.15f), true).rectTransform;
        stickBase.GetComponent<Image>().raycastTarget = false;
        stickKnob = UIUtil.CreateImage(stickBase, "StickKnob", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(100f, 100f), new Color(1f, 1f, 1f, 0.45f), true).rectTransform;
        stickKnob.GetComponent<Image>().raycastTarget = false;

        var sprint = UIUtil.CreateButton(root, "KOŞ", Vector2.zero, new Vector2(110f, 440f), new Vector2(110f, 110f), ButtonColor, true, 22, out unused);
        sprintImage = sprint.GetComponent<Image>();
        sprint.onClick.AddListener(() =>
        {
            SprintOn = !SprintOn;
            sprintImage.color = SprintOn ? new Color(1f, 0.85f, 0.2f, 0.55f) : ButtonColor;
        });

        // Everything used while on foot lives in one group that is hidden in the air / in a jeep.
        combatGroup = UIUtil.CreateStretch(root, "CombatButtons").gameObject;
        var g = combatGroup.transform;

        var fireR = UIUtil.CreateButton(g, "ATEŞ", new Vector2(1f, 0f), new Vector2(-230f, 260f), new Vector2(200f, 200f), FireColor, true, 30, out unused);
        rightFire = fireR.gameObject.AddComponent<HoldButton>();
        rightFireRect = (RectTransform)fireR.transform;
        rightFireHome = rightFireRect.anchoredPosition;
        var fireL = UIUtil.CreateButton(g, "ATEŞ", Vector2.zero, new Vector2(250f, 560f), new Vector2(130f, 130f), FireColor, true, 22, out unused);
        leftFire = fireL.gameObject.AddComponent<HoldButton>();

        var jump = UIUtil.CreateButton(g, "ZIPLA", new Vector2(1f, 0f), new Vector2(-470f, 130f), new Vector2(130f, 130f), ButtonColor, true, 22, out unused);
        jump.onClick.AddListener(() => jumpQueued = true);
        var crouch = UIUtil.CreateButton(g, "EĞİL", new Vector2(1f, 0f), new Vector2(-470f, 300f), new Vector2(120f, 120f), ButtonColor, true, 22, out unused);
        crouch.onClick.AddListener(() => crouchQueued = true);
        var reload = UIUtil.CreateButton(g, "DOLDUR", new Vector2(1f, 0f), new Vector2(-230f, 500f), new Vector2(130f, 130f), ButtonColor, true, 20, out unused);
        reload.onClick.AddListener(() => reloadQueued = true);
        var grenade = UIUtil.CreateButton(g, "BOMBA", new Vector2(1f, 0f), new Vector2(-660f, 300f), new Vector2(115f, 115f), new Color(0.35f, 0.5f, 0.25f, 0.5f), true, 18, out grenadeLabel);
        grenade.onClick.AddListener(() => grenadeQueued = true);

        var aim = UIUtil.CreateButton(g, "NİŞAN", new Vector2(1f, 0f), new Vector2(-660f, 470f), new Vector2(115f, 115f), ButtonColor, true, 20, out unused);
        aim.onClick.AddListener(() => aimQueued = true);
        aimImage = aim.GetComponent<Image>();

        var swap = UIUtil.CreateButton(g, "DEĞİŞ", new Vector2(0.5f, 0f), new Vector2(0f, 330f), new Vector2(260f, 56f), ButtonColor, false, 20, out swapLabel);
        swap.onClick.AddListener(() => swapQueued = true);
        swapButton = swap.gameObject;

        var medkit = UIUtil.CreateButton(g, "İLK YARDIM", new Vector2(0.5f, 0f), new Vector2(-260f, 70f), new Vector2(200f, 90f), ItemColor, false, 20, out medkitLabel);
        medkit.onClick.AddListener(() => medkitQueued = true);
        var drink = UIUtil.CreateButton(g, "İÇECEK", new Vector2(0.5f, 0f), new Vector2(260f, 70f), new Vector2(200f, 90f), new Color(0.95f, 0.45f, 0.75f, 0.4f), false, 20, out drinkLabel);
        drink.onClick.AddListener(() => drinkQueued = true);

        // Context buttons.
        var vehicle = UIUtil.CreateButton(root, "BİN", new Vector2(1f, 0f), new Vector2(-470f, 480f), new Vector2(150f, 90f), new Color(0.2f, 0.5f, 1f, 0.6f), false, 26, out vehicleLabel);
        vehicle.onClick.AddListener(() => vehicleQueued = true);
        vehicleButton = vehicle.gameObject;

        var doorBtn = UIUtil.CreateButton(root, "KAPI", new Vector2(1f, 0f), new Vector2(-470f, 590f), new Vector2(150f, 80f), new Color(0.55f, 0.4f, 0.25f, 0.65f), false, 24, out doorLabel);
        doorBtn.onClick.AddListener(() => doorQueued = true);
        doorButton = doorBtn.gameObject;

        var airBtn = UIUtil.CreateButton(root, "ATLA", new Vector2(1f, 0f), new Vector2(-280f, 330f), new Vector2(240f, 240f), new Color(1f, 0.75f, 0.15f, 0.7f), true, 40, out airLabel);
        airBtn.onClick.AddListener(() => airQueued = true);
        airButton = airBtn.gameObject;

        // Everything the player may move or resize in the HUD editor.
        RegisterHud("stick", "HAREKET", stickBase);
        RegisterHud("sprint", "KOŞ", sprint);
        RegisterHud("fireR", "ATEŞ", fireR);
        RegisterHud("fireL", "SOL ATEŞ", fireL);
        RegisterHud("jump", "ZIPLA", jump);
        RegisterHud("crouch", "EĞİL", crouch);
        RegisterHud("reload", "DOLDUR", reload);
        RegisterHud("grenade", "BOMBA", grenade);
        RegisterHud("aim", "NİŞAN", aim);
        RegisterHud("swap", "DEĞİŞ", swap);
        RegisterHud("medkit", "İLK YARDIM", medkit);
        RegisterHud("drink", "İÇECEK", drink);
        RegisterHud("vehicle", "BİN / İN", vehicle);
        RegisterHud("air", "ATLA", airBtn);
        RegisterHud("door", "KAPI", doorBtn);

        vehicleButton.SetActive(false);
        airButton.SetActive(false);
        doorButton.SetActive(false);
        foreach (var feel in GetComponentsInChildren<ButtonFeel>(true))
            feel.silent = true;   // in-game controls: squash + buzz, no click sound
        ApplySettings();
    }

    /// <summary>Applies the control options from the settings screen.</summary>
    public void ApplySettings()
    {
        ApplyLayout();
        if (leftFire != null)
            leftFire.gameObject.SetActive(GameSettings.LeftFireButton);
        if (group != null)
            group.alpha = GameSettings.ButtonOpacity;
        if (stickBase != null && moveFinger == -1)
            stickBase.anchoredPosition = stickHome;
        if (rightFireRect != null)
            rightFireRect.anchoredPosition = rightFireHome;
    }

    /// <summary>Called by the UI every frame to show only the buttons that make sense right now.</summary>
    public void UpdateContext(PlayerController player, bool vehicleNearby)
    {
        bool onFoot = player.state == PlayerState.Ground && !player.isDowned && !player.IsSwimming;
        if (combatGroup.activeSelf != onFoot)
            combatGroup.SetActive(onFoot);

        bool showAir = player.state == PlayerState.Plane || player.state == PlayerState.Freefall;
        if (airButton.activeSelf != showAir)
            airButton.SetActive(showAir);
        if (showAir)
            airLabel.text = player.state == PlayerState.Plane ? "ATLA" : "PARAŞÜT";

        bool showVehicle = player.state == PlayerState.Driving || (onFoot && vehicleNearby);
        if (vehicleButton.activeSelf != showVehicle)
            vehicleButton.SetActive(showVehicle);
        if (showVehicle)
            vehicleLabel.text = player.state == PlayerState.Driving ? "İN" : "BİN";

        Door door = onFoot ? Door.Nearest(player.transform.position, PlayerController.DoorReach) : null;
        bool showDoor = door != null;
        if (doorButton.activeSelf != showDoor)
            doorButton.SetActive(showDoor);
        if (showDoor)
            doorLabel.text = door.IsOpen ? "KAPAT" : "AÇ";

        if (aimImage != null)
            aimImage.color = player.aimingDownSights ? new Color(1f, 0.85f, 0.2f, 0.6f) : ButtonColor;

        // Knocked down: only the joystick works.
        if (player.isDowned && combatGroup.activeSelf)
            combatGroup.SetActive(false);

        medkitLabel.text = "İLK YARDIM x" + player.inventory.medkits;
        drinkLabel.text = "İÇECEK x" + player.inventory.drinks;
        grenadeLabel.text = "BOMBA\nx" + player.inventory.grenades;
        bool hasOther = player.HasOtherWeapon;
        if (swapButton.activeSelf != hasOther)
            swapButton.SetActive(hasOther);
        if (hasOther)
            swapLabel.text = "Değiş: " + player.OtherWeaponName;
    }

    // ----- Custom layout (HUD editor) -----

    /// <summary>A touch control as built by default; the HUD editor draws its preview from this.</summary>
    public class HudControl
    {
        public string key, label;
        public RectTransform rect;
        public Vector2 anchor, basePos, size;
        public Color color;
        public bool round;
    }

    private readonly List<HudControl> hudControls = new List<HudControl>();
    public List<HudControl> HudControls { get { return hudControls; } }

    private void RegisterHud(string key, string label, Component c)
    {
        var rect = (RectTransform)c.transform;
        var img = c.GetComponent<Image>();
        hudControls.Add(new HudControl
        {
            key = key,
            label = label,
            rect = rect,
            anchor = rect.anchorMin,
            basePos = rect.anchoredPosition,
            size = rect.sizeDelta,
            color = img != null ? img.color : ButtonColor,
            round = img != null && img.sprite != null
        });
    }

    /// <summary>Moves and resizes the controls to the saved custom layout.</summary>
    public void ApplyLayout()
    {
        foreach (var c in hudControls)
        {
            var e = HudLayout.Get(c.key);
            Vector2 pos = c.basePos + e.offset;
            c.rect.localScale = new Vector3(e.scale, e.scale, 1f);
            if (c.key == "stick")
                stickHome = pos;          // the floating stick returns here
            else if (c.key == "fireR")
                rightFireHome = pos;      // the following fire button returns here
            else
                c.rect.anchoredPosition = pos;
        }
    }

    public void ResetState()
    {
        Move = Vector2.zero;
        LookDelta = Vector2.zero;
        moveFinger = -1;
        lookFinger = -1;
        lookIsFireFinger = false;
        fireFinger = -1;
        if (rightFireRect != null)
            rightFireRect.anchoredPosition = rightFireHome;
        jumpQueued = crouchQueued = reloadQueued = medkitQueued = false;
        drinkQueued = grenadeQueued = swapQueued = vehicleQueued = airQueued = aimQueued = doorQueued = false;
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

    private static readonly List<RaycastResult> uiHits = new List<RaycastResult>();

    /// <summary>Own UI raycast: reliable on the very frame a touch begins.</summary>
    private static bool OverUI(Vector2 screenPos)
    {
        var es = EventSystem.current;
        if (es == null)
            return false;
        uiHits.Clear();
        es.RaycastAll(new PointerEventData(es) { position = screenPos }, uiHits);
        return uiHits.Count > 0;
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
                    if (OverUI(t.position))
                    {
                        // Holding the right fire button also aims (drag your thumb while shooting).
                        if (rightFireRect != null && rightFireRect.gameObject.activeInHierarchy &&
                            RectTransformUtility.RectangleContainsScreenPoint(rightFireRect, t.position, null))
                        {
                            fireFinger = t.fingerId;
                            fireFollowStart = t.position;
                            if (lookFinger == -1 && GameSettings.FireButtonLook)
                            {
                                lookFinger = t.fingerId;
                                lastLookScreen = t.position;
                                lookIsFireFinger = true;
                            }
                        }
                        break;
                    }

                    bool nearStick = GameSettings.JoystickMode == 1 ||
                        (t.position - RectTransformUtility.WorldToScreenPoint(null, stickBase.position)).magnitude < StickRadius * 1.6f * scale;
                    if (t.position.x < Screen.width * 0.4f && moveFinger == -1 && nearStick)
                    {
                        moveFinger = t.fingerId;
                        if (GameSettings.JoystickMode == 1)
                        {
                            // Dynamic: the stick appears under the thumb.
                            moveStartScreen = t.position;
                            Vector2 local;
                            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(root, t.position, null, out local))
                                stickBase.anchoredPosition = local - root.rect.min;
                        }
                        else
                        {
                            // Fixed: the stick stays put; direction is measured from its centre.
                            moveStartScreen = RectTransformUtility.WorldToScreenPoint(null, stickBase.position);
                        }
                    }
                    else if (lookFinger == -1)
                    {
                        lookFinger = t.fingerId;
                        lastLookScreen = t.position;
                        lookIsFireFinger = false;
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
                    if (t.fingerId == fireFinger && GameSettings.FireButtonFollow && rightFireRect != null)
                        rightFireRect.anchoredPosition = rightFireHome + Vector2.ClampMagnitude((t.position - fireFollowStart) / scale, 150f);
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
                        lookIsFireFinger = false;
                    }
                    if (t.fingerId == fireFinger)
                    {
                        fireFinger = -1;
                        if (rightFireRect != null)
                            rightFireRect.anchoredPosition = rightFireHome;
                    }
                    break;
            }
        }
    }
}
