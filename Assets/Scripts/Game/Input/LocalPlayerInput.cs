using UnityEngine;

/// <summary>
/// The local human player: on-screen touch controls, plus keyboard and mouse for desktop testing.
/// Behaviour matches what PlayerController used to read directly.
/// </summary>
public class LocalPlayerInput : IPlayerInput
{
    private static TouchControls Touch { get { return TouchControls.Instance; } }

    public Vector2 Move
    {
        get
        {
            var tc = Touch;
            Vector2 input = tc != null ? tc.Move : Vector2.zero;
            if (input.sqrMagnitude < 0.01f)
                input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            return Vector2.ClampMagnitude(input, 1f);
        }
    }

    public Vector2 TouchLookDelta
    {
        get { var tc = Touch; return tc != null ? tc.LookDelta : Vector2.zero; }
    }

    public Vector2 MouseLook
    {
        get
        {
            // Desktop testing: hold the right mouse button to look around.
            if (!Application.isMobilePlatform && Input.GetMouseButton(1))
                return new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));
            return Vector2.zero;
        }
    }

    public bool SprintHeld
    {
        get { var tc = Touch; return Input.GetKey(KeyCode.LeftShift) || (tc != null && tc.SprintOn); }
    }

    public bool FireHeld
    {
        get { var tc = Touch; return (!Application.isMobilePlatform && Input.GetMouseButton(0)) || (tc != null && tc.FireHeld); }
    }

    public bool ConsumeJump() { var tc = Touch; return Input.GetKeyDown(KeyCode.Space) || (tc != null && tc.ConsumeJump()); }
    public bool ConsumeCrouch() { var tc = Touch; return Input.GetKeyDown(KeyCode.LeftControl) || Input.GetKeyDown(KeyCode.C) || (tc != null && tc.ConsumeCrouch()); }
    public bool ConsumeReload() { var tc = Touch; return Input.GetKeyDown(KeyCode.R) || (tc != null && tc.ConsumeReload()); }
    public bool ConsumeMedkit() { var tc = Touch; return Input.GetKeyDown(KeyCode.X) || (tc != null && tc.ConsumeMedkit()); }
    public bool ConsumeDrink() { var tc = Touch; return Input.GetKeyDown(KeyCode.V) || (tc != null && tc.ConsumeDrink()); }
    public bool ConsumeGrenade() { var tc = Touch; return Input.GetKeyDown(KeyCode.G) || (tc != null && tc.ConsumeGrenade()); }
    public bool ConsumeSwap() { var tc = Touch; return Input.GetKeyDown(KeyCode.Q) || (tc != null && tc.ConsumeSwap()); }
    public bool ConsumeVehicle() { var tc = Touch; return Input.GetKeyDown(KeyCode.F) || (tc != null && tc.ConsumeVehicle()); }
    public bool ConsumeAirAction() { var tc = Touch; return Input.GetKeyDown(KeyCode.Space) || (tc != null && tc.ConsumeAirAction()); }
    public bool ConsumeDoor() { var tc = Touch; return Input.GetKeyDown(KeyCode.T) || (tc != null && tc.ConsumeDoor()); }
    public bool ConsumeAirdropToken() { var tc = Touch; return Input.GetKeyDown(KeyCode.Alpha1) || (tc != null && tc.ConsumeAirdropToken()); }
    public bool ConsumeBoostToken() { var tc = Touch; return Input.GetKeyDown(KeyCode.Alpha2) || (tc != null && tc.ConsumeBoostToken()); }
    public bool ConsumeAbility() { var tc = Touch; return Input.GetKeyDown(KeyCode.Z) || (tc != null && tc.ConsumeAbility()); }
    public bool ConsumeAim() { var tc = Touch; return Input.GetKeyDown(KeyCode.E) || (tc != null && tc.ConsumeAim()); }
}
