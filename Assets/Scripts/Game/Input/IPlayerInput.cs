using UnityEngine;

/// <summary>
/// One player's control intent for a frame. PlayerController reads only this, never the touch UI
/// or the keyboard directly, so the same controller can later be driven by bots or the network.
/// "Consume" calls return true once per press (a queued tap or a key-down this frame).
/// </summary>
public interface IPlayerInput
{
    /// <summary>Movement stick, clamped to length 1.</summary>
    Vector2 Move { get; }
    /// <summary>Swipe look moved this frame, in canvas units (sensitivity is applied by the controller).</summary>
    Vector2 TouchLookDelta { get; }
    /// <summary>Desktop mouse-look axes this frame (raw), zero on mobile or when not held.</summary>
    Vector2 MouseLook { get; }
    bool SprintHeld { get; }
    /// <summary>Helicopter climb (+1) / descend (-1).</summary>
    float VerticalAxis { get; }
    bool FireHeld { get; }
    /// <summary>BOMBA / TAKTİK held down: aiming a throw (released: thrown).</summary>
    bool GrenadeHeld { get; }
    bool TacticalHeld { get; }

    bool ConsumeJump();
    bool ConsumeCrouch();
    /// <summary>YAT: lie down / get up.</summary>
    bool ConsumeProne();
    bool ConsumeReload();
    bool ConsumeMedkit();
    bool ConsumeDrink();
    bool ConsumeGrenade();
    bool ConsumeTactical();
    bool ConsumeSwap();
    bool ConsumeVehicle();
    bool ConsumeAirAction();
    bool ConsumeAim();
    bool ConsumeDoor();
    bool ConsumeAbility();
    bool ConsumeAirdropToken();
    bool ConsumeBoostToken();
}
