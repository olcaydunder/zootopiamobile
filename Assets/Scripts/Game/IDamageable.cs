using UnityEngine;

/// <summary>
/// Anything that can be shot: the local player and the bots.
/// Team 0 is always the player's team. attackerTeam -1 means the safe zone (ignores armor).
/// </summary>
public interface IDamageable
{
    int Team { get; }
    bool IsDead { get; }
    bool IsAirborne { get; }   // in the plane, skydiving or parachuting
    string DisplayName { get; }
    Vector3 AimPoint { get; }
    Transform transform { get; }

    /// <returns>true if this hit eliminated the target.</returns>
    bool TakeDamage(float amount, int attackerTeam);
}
