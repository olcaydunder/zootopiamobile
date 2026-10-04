using UnityEngine;

/// <summary>PlayerController part: vaulting over low obstacles (walls, fences, crates) with the jump button.</summary>
public partial class PlayerController
{
    public const float VaultMinHeight = 0.45f;
    public const float VaultMaxHeight = 1.45f;
    public const float VaultDuration = 0.48f;

    private bool isVaulting;
    private float vaultTime;
    private Vector3 vaultStart, vaultPeak, vaultEnd;

    public bool IsVaulting { get { return isVaulting; } }

    private float FeetOffset { get { return controller.height * 0.5f - controller.center.y; } }

    /// <summary>
    /// Looks for a low obstacle right in front: blocked at knee height, clear above it, with a top
    /// between VaultMinHeight and VaultMaxHeight and room to land behind it. Starts the vault if so.
    /// </summary>
    private bool TryVault()
    {
        if (!controller.isGrounded || isDowned || state != PlayerState.Ground)
            return false;
        Vector3 fwd = transform.forward;
        fwd.y = 0f;
        if (fwd.sqrMagnitude < 0.01f)
            return false;
        fwd.Normalize();
        float feetY = transform.position.y - FeetOffset;
        Vector3 feet = new Vector3(transform.position.x, feetY, transform.position.z);
        const QueryTriggerInteraction q = QueryTriggerInteraction.Ignore;

        // Something to vault: blocked at knee height within reach.
        RaycastHit wall;
        if (!Physics.Raycast(feet + Vector3.up * 0.4f, fwd, out wall, 1.3f, Physics.DefaultRaycastLayers, q))
            return false;
        if (Vector3.Dot(wall.normal, -fwd) < 0.5f)
            return false;   // glancing hit along a wall

        // Low enough: the space just above the maximum height is free.
        if (Physics.Raycast(feet + Vector3.up * (VaultMaxHeight + 0.25f), fwd, wall.distance + 0.6f, Physics.DefaultRaycastLayers, q))
            return false;

        // Find the obstacle's top just past its face.
        Vector3 probe = wall.point + fwd * 0.2f;
        probe.y = feetY + VaultMaxHeight + 0.2f;
        RaycastHit top;
        if (!Physics.Raycast(probe, Vector3.down, out top, VaultMaxHeight + 0.2f, Physics.DefaultRaycastLayers, q))
            return false;
        float height = top.point.y - feetY;
        if (height < VaultMinHeight || height > VaultMaxHeight)
            return false;

        // Clear over the top for the body, and ground to land on behind it.
        float clearY = top.point.y + 0.35f;
        Vector3 over = new Vector3(feet.x, clearY, feet.z);
        RaycastHit block;
        float overDist = wall.distance + 1.6f;
        if (Physics.Raycast(over, fwd, out block, overDist, Physics.DefaultRaycastLayers, q))
            return false;
        Vector3 landProbe = wall.point + fwd * 1.3f;
        landProbe.y = clearY;
        float groundY;
        RaycastHit land;
        if (Physics.Raycast(landProbe, Vector3.down, out land, 6f, Physics.DefaultRaycastLayers, q))
            groundY = land.point.y;
        else
            groundY = World.HeightAt(landProbe.x, landProbe.z);
        if (groundY < feetY - 3f)
            return false;   // a drop, not a vault
        if (groundY > top.point.y + 0.05f)
            groundY = top.point.y;   // thick block: land on top of it

        float offset = FeetOffset;
        vaultStart = transform.position;
        vaultPeak = new Vector3(top.point.x, top.point.y + offset + 0.15f, top.point.z) - fwd * 0.1f;
        vaultEnd = new Vector3(landProbe.x, groundY + offset + 0.02f, landProbe.z);
        vaultTime = 0f;
        isVaulting = true;
        aimingDownSights = false;
        isSprinting = false;
        velocity = Vector3.zero;
        controller.enabled = false;
        rig.crouched = true;
        Sfx.Play(SoundBank.Footstep, 0.3f, 0.8f);
        return true;
    }

    /// <summary>Moves along a curve over the obstacle; ends on the ground behind it.</summary>
    private void UpdateVault()
    {
        vaultTime += Time.deltaTime;
        float t = Mathf.Clamp01(vaultTime / VaultDuration);
        float s = t * t * (3f - 2f * t);
        Vector3 a = Vector3.Lerp(vaultStart, vaultPeak, s);
        Vector3 b = Vector3.Lerp(vaultPeak, vaultEnd, s);
        transform.position = Vector3.Lerp(a, b, s);
        if (t >= 1f)
            EndVault();
    }

    private void EndVault()
    {
        if (!isVaulting)
            return;
        isVaulting = false;
        controller.enabled = true;
        rig.crouched = isCrouching;
        Sfx.Play(SoundBank.Land, 0.15f, 1.1f);
    }
}
