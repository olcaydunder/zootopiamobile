using UnityEngine;

/// <summary>
/// PlayerController part: swimming. Deeper than SwimDepth the player floats at the surface,
/// moves slower, cannot shoot or use items, and climbs out again where the water gets shallow.
/// </summary>
public partial class PlayerController
{
    /// <summary>Height of the sea surface (World.BuildWater).</summary>
    public const float WaterY = -0.15f;
    /// <summary>Water deeper than this (surface to ground) means swimming.</summary>
    public const float SwimDepth = 1.35f;
    public float swimSpeed = 2.4f;
    public float swimSprintSpeed = 3.4f;

    private bool isSwimming;
    private float strokeTimer;

    public bool IsSwimming { get { return isSwimming; } }

    public static float WaterDepthAt(Vector3 p)
    {
        return WaterY - World.HeightAt(p.x, p.z);
    }

    /// <summary>Starts or stops swimming from the water depth here. Returns true while swimming.</summary>
    private bool CheckSwim()
    {
        float depth = WaterDepthAt(transform.position);
        if (!isSwimming && depth > SwimDepth)
            StartSwim();
        else if (isSwimming && depth < SwimDepth - 0.25f)
            StopSwim();
        return isSwimming;
    }

    private void StartSwim()
    {
        isSwimming = true;
        if (isCrouching)
            SetCrouch(false);
        ClearScope();
        aimingDownSights = false;
        isSprinting = false;
        velocity = Vector3.zero;
        currentWeapon.gameObject.SetActive(false);
        rig.pose = RigPose.Swim;
        rig.aiming = false;
        Sfx.Play(SoundBank.Land, 0.3f, 0.7f);
    }

    private void StopSwim()
    {
        if (!isSwimming)
            return;
        isSwimming = false;
        rig.pose = RigPose.Normal;
        if (!isDowned && !isDead)
            currentWeapon.gameObject.SetActive(true);
    }

    /// <summary>Surface swimming: horizontal movement, the body held at the surface.</summary>
    private void UpdateSwim(IPlayerInput tc)
    {
        Vector2 input = MoveInput(tc);
        bool fast = tc != null && tc.SprintHeld && input.y > 0.4f;
        Vector3 move = Vector3.ClampMagnitude(transform.right * input.x + transform.forward * input.y, 1f);
        float speed = fast ? swimSprintSpeed : swimSpeed;

        // Stay inside the map (the sea ring goes far beyond the play area).
        float limit = World.MapSize * 0.5f - 6f;
        Vector3 next = transform.position + move * speed * Time.deltaTime;
        if (Mathf.Abs(next.x) > limit || Mathf.Abs(next.z) > limit)
            move = Vector3.zero;

        controller.Move((move * speed + Vector3.up * FloatVelocity()) * Time.deltaTime);
        rig.crouched = false;

        if (move.sqrMagnitude > 0.05f)
        {
            strokeTimer += Time.deltaTime * (fast ? 1.4f : 1f);
            if (strokeTimer > 0.9f)
            {
                strokeTimer = 0f;
                Sfx.Play(SoundBank.Whoosh, 0.12f, Random.Range(1.3f, 1.6f));
            }
        }
    }

    /// <summary>Vertical speed that brings the body to floating height (also used while knocked down in water).</summary>
    private float FloatVelocity()
    {
        float target = WaterY - 0.15f;
        velocity.y = 0f;
        return Mathf.Clamp((target - transform.position.y) * 4f, -3f, 3f);
    }
}
