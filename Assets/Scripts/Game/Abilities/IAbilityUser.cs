using UnityEngine;

/// <summary>
/// What a class ability needs from whoever uses it (the player or a bot): position, aim, team,
/// and a few actions (heal, teleport, launch into the air, stealth, speed boost).
/// </summary>
public interface IAbilityUser
{
    Transform transform { get; }
    int Team { get; }
    bool IsDead { get; }
    bool IsAirborne { get; }
    bool IsPlayer { get; }
    /// <summary>Chest height, where abilities start from.</summary>
    Vector3 AbilityOrigin { get; }
    /// <summary>Where the user is looking (camera for the player, body for bots).</summary>
    Vector3 AimDirection { get; }
    /// <summary>On the ground and free to act (not downed, not driving, not swimming).</summary>
    bool CanUseAbility { get; }
    ClassAbility Ability { get; }

    void HealBy(float health, float armor);
    void TeleportTo(Vector3 feetPosition);
    void LaunchUp(float height);
    void SetStealth(bool on);
    void SpeedBoost(float multiplier, float seconds);
}
