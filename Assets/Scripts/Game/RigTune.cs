using UnityEngine;

/// <summary>
/// The numbers behind the characters' procedural posing (CharacterRig.PoseBody): where the gun sits against the
/// shoulder, how it is held at the ready, where the hands and elbows go, how much the spine follows the aim, the
/// bladed shooting stance. Kept in one place so the pose test (PoseShots, -posetune file.json) can try values
/// without a new build; the defaults here are what the game uses.
/// </summary>
[System.Serializable]
public class RigTune
{
    public static RigTune Current = new RigTune();

    // Long guns: the butt sits in the shoulder pocket (aim space offset from the shoulder joint: right, up, forward).
    public Vector3 pocket = new Vector3(-0.05f, -0.02f, 0.03f);
    public float readyDrop = 0.03f;                 // at the ready the butt drops this much lower
    public float readyPitch = 24f, readyYaw = -14f; // muzzle down / inwards at the ready (degrees)
    public float pistolReadyPitch = 38f, pistolReadyYaw = -6f;
    // Pistol: grip position in front of the shoulders' midpoint (aim space)
    public Vector3 pistolAimGrip = new Vector3(0.02f, -0.04f, 0.42f);
    public Vector3 pistolReadyGrip = new Vector3(0.04f, -0.24f, 0.3f);
    // Hands relative to the grip / support point (gun space)
    public Vector3 rightHandOffset = new Vector3(0f, 0.02f, 0.05f);
    public Vector3 leftHandOffset = new Vector3(0f, 0.035f, 0.03f);
    public Vector3 pistolLeftOffset = new Vector3(-0.045f, -0.03f, -0.01f);
    public float reach = 0.97f, maxSlide = 0.22f;   // short arms: the support hand slides back along the handguard
    // Elbow pole targets from each shoulder (body space: right, up, forward)
    public Vector3 rightPole = new Vector3(0.35f, -0.6f, -0.25f);
    public Vector3 leftPole = new Vector3(-0.3f, -0.6f, -0.1f);
    // Spine and chest share the aim pitch (looking down / up) and undo the hips' turn
    public float spineDown = 0.25f, chestDown = 0.35f, spineUp = 0.35f, chestUp = 0.45f;
    public float hipSpine = 0.55f, hipChest = 0.45f;
    public float crouchLean = 0.25f, kneeMax = 52f;
    // Bladed stance: the chest turns right while a long gun is up (left shoulder forward; twistSpine = the spine's
    // share), the head turns back to the aim
    public float aimTwist = 24f, readyTwist = 12f, twistSpine = 0.4f, headFollow = 0.9f;
    // Head: share of the aim pitch on top of spine and chest; cheek on the stock when aiming a long gun (degrees)
    public float headDown = 0.25f, headUp = 0.15f, cheekTilt = 8f, cheekPitch = 8f;
    // Collarbones: share of the way towards the hand's target (right / left), capped (degrees). The low-poly cartoon
    // characters get clavCartoon times that (their shirts poke through the vests when the collarbones turn).
    public float clavRight = 0.12f, clavLeft = 0.28f, clavMax = 18f, clavCartoon = 0f;
    public Vector3 showcaseAim = new Vector3(4f, -12f, 0f);   // lobby: gun held across the body
    // Reloading: the gun canted (roll) and lifted (degrees), pulled in front of the chest (metres)
    public float reloadRoll = 28f, reloadLift = 10f, reloadPull = 0.07f;
    // Shot kick: back (metres) and muzzle up (degrees), dying away at kickDecay per second
    public float kickBack = 0.035f, kickPitch = 4f, kickDecay = 26f;
    // Prone: body tipped forward, sunk to the ground; up on the elbows, head lifted to look ahead, legs apart
    public float pronePitch = 84f, proneDrop = -0.74f;
    public float proneSpineLift = 10f, proneChestLift = 14f, proneHeadLift = 50f, proneLegSpread = 9f;
    public float proneAimFollow = 0.3f, proneReadyPitch = 6f;
    public Vector3 pronePoleR = new Vector3(0.45f, -1f, 0.15f);
    public Vector3 pronePoleL = new Vector3(-0.45f, -1f, 0.25f);

    /// <summary>Loads overrides from a JSON file (only the fields it names change).</summary>
    public static void Load(string path)
    {
        try
        {
            if (!string.IsNullOrEmpty(path) && System.IO.File.Exists(path))
            {
                var t = new RigTune();
                JsonUtility.FromJsonOverwrite(System.IO.File.ReadAllText(path), t);
                Current = t;
                Debug.Log("[RigTune] loaded " + path);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[RigTune] " + e.Message);
        }
    }
}
