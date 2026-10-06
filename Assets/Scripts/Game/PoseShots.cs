using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// Pose test (development tool): started with "-poseshots &lt;folder&gt;" on a desktop build, it builds a small studio
/// instead of the game, puts characters through every stance (ready, aiming up/level/down, walking, running,
/// strafing, crouching, prone, falling and landing, every weapon class, the lobby showcase), photographs each from
/// several sides (including the game's own over-the-shoulder and aim cameras) into PNGs and quits.
/// "-posetune a.json[,b.json...]" overrides <see cref="RigTune"/> values (several files: every case is shot with each,
/// file names tagged t0, t1...), "-poseskins a,b" and "-posecases x,y" pick a subset, "-poseres 480x360" the picture size.
/// </summary>
public class PoseShots : MonoBehaviour
{
    public static bool Active { get; private set; }

    private string outDir;
    private string[] skins = { "SoldierMale", "SwatOperator", "FemaleSpecops" };
    private HashSet<string> onlyCases;
    private Camera cam;
    private Texture2D grab;
    private int W = 640, H = 480;
    private string[] tunes = { null };
    private int shotIndex;

    private class Case
    {
        public string name;
        public WeaponType gun = WeaponType.Rifle;
        public string gunSkin = "AK47";
        public bool aim, crouch, showcase, noGun;
        public RigPose pose = RigPose.Normal;
        public float pitch;               // + = looking down
        public Vector3 move;              // local velocity (x right, z forward), m/s
        public bool fall;                 // dropped from 4 m, photographed in the air
        public bool land;                 // (with fall) photographed just after touching down
        public string[] views = { "front", "side" };
        public int extraFrames;           // also photographed this many frames later (a second step of a cycle)
    }

    /// <summary>Called first thing by GameBootstrap; true when this run is a pose test.</summary>
    public static bool TryStart()
    {
        string[] args = System.Environment.GetCommandLineArgs();
        string dir = null, tune = null, skinList = null, caseList = null, res = null;
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == "-poseshots") dir = args[i + 1];
            else if (args[i] == "-posetune") tune = args[i + 1];
            else if (args[i] == "-poseskins") skinList = args[i + 1];
            else if (args[i] == "-posecases") caseList = args[i + 1];
            else if (args[i] == "-poseres") res = args[i + 1];
        }
        if (string.IsNullOrEmpty(dir))
            return false;
        Active = true;
        var go = new GameObject("PoseShots");
        var p = go.AddComponent<PoseShots>();
        p.outDir = dir;
        if (!string.IsNullOrEmpty(tune))
            p.tunes = tune.Split(',');
        int w, h;
        var wh = string.IsNullOrEmpty(res) ? null : res.Split('x');
        if (wh != null && wh.Length == 2 && int.TryParse(wh[0], out w) && int.TryParse(wh[1], out h))
        {
            p.W = w;
            p.H = h;
        }
        if (!string.IsNullOrEmpty(skinList))
            p.skins = skinList.Split(',');
        if (!string.IsNullOrEmpty(caseList))
            p.onlyCases = new HashSet<string>(caseList.Split(','));
        return true;
    }

    private List<Case> Cases()
    {
        var all = new List<Case>
        {
            new Case { name = "ready", views = new[] { "front", "side", "back", "close" } },
            new Case { name = "aim", aim = true, views = new[] { "front", "side", "back", "ads", "left", "close", "top" } },
            new Case { name = "aimup", aim = true, pitch = -30f, views = new[] { "side", "back" } },
            new Case { name = "aimdown", aim = true, pitch = 30f, views = new[] { "side", "back" } },
            new Case { name = "walkaim", aim = true, move = new Vector3(0f, 0f, 2.6f), views = new[] { "side", "front" }, extraFrames = 7 },
            new Case { name = "run", move = new Vector3(0f, 0f, 6.5f), views = new[] { "side", "front", "back" }, extraFrames = 6 },
            new Case { name = "strafe", aim = true, move = new Vector3(2.6f, 0f, 0f), views = new[] { "front", "back" }, extraFrames = 7 },
            new Case { name = "backpedal", aim = true, move = new Vector3(0f, 0f, -2.2f), views = new[] { "side" }, extraFrames = 7 },
            new Case { name = "crouch", crouch = true, views = new[] { "front", "side" } },
            new Case { name = "crouchaim", crouch = true, aim = true, views = new[] { "side", "back" } },
            new Case { name = "crouchwalk", crouch = true, move = new Vector3(0f, 0f, 2.2f), views = new[] { "side" }, extraFrames = 8 },
            new Case { name = "prone", pose = RigPose.Prone, views = new[] { "side", "back", "front" } },
            new Case { name = "proneaim", pose = RigPose.Prone, aim = true, views = new[] { "side", "front", "back" } },
            new Case { name = "crawl", pose = RigPose.Prone, move = new Vector3(0f, 0f, 1.1f), views = new[] { "side", "front" }, extraFrames = 10 },
            new Case { name = "fall", fall = true, views = new[] { "side", "front" } },
            new Case { name = "land", fall = true, land = true, views = new[] { "side" } },
            new Case { name = "pistolready", gun = WeaponType.Pistol, gunSkin = "Magnum", views = new[] { "front", "side" } },
            new Case { name = "pistolaim", gun = WeaponType.Pistol, gunSkin = "Magnum", aim = true, views = new[] { "front", "side", "back" } },
            new Case { name = "smgaim", gun = WeaponType.SMG, gunSkin = "U45", aim = true, views = new[] { "side", "back", "close" } },
            new Case { name = "sniperready", gun = WeaponType.Sniper, gunSkin = "G28", views = new[] { "front", "side" } },
            new Case { name = "sniperaim", gun = WeaponType.Sniper, gunSkin = "G28", aim = true, views = new[] { "side", "back", "close" } },
            new Case { name = "shotgunready", gun = WeaponType.Shotgun, gunSkin = "P870", views = new[] { "front", "side" } },
            new Case { name = "shotgunaim", gun = WeaponType.Shotgun, gunSkin = "P870", aim = true, views = new[] { "side" } },
            new Case { name = "showcase", showcase = true, views = new[] { "lobby" } },
        };
        if (onlyCases == null)
            return all;
        return all.FindAll(c => onlyCases.Contains(c.name));
    }

    private IEnumerator Start()
    {
        Directory.CreateDirectory(outDir);
        Time.captureFramerate = 30;   // every frame advances the animation by exactly 1/30 s, however slow the rendering
        SetupStudio();
        grab = new Texture2D(W, H, TextureFormat.RGB24, false);
        yield return null;
        var log = new List<string>();
        for (int ti = 0; ti < tunes.Length; ti++)
        {
            foreach (var skin in skins)
            {
                RigTune.Current = new RigTune();
                RigTune.Load(tunes[ti]);
                tuneTag = tunes.Length > 1 ? "t" + ti + "_" : "";
                foreach (var c in Cases())
                {
                    string error = null;
                    IEnumerator run = RunCase(c, skin);
                    while (true)
                    {
                        object current;
                        try
                        {
                            if (!run.MoveNext())
                                break;
                            current = run.Current;
                        }
                        catch (System.Exception e)
                        {
                            error = e.ToString();
                            break;
                        }
                        yield return current;
                    }
                    log.Add(tuneTag + skin + " " + c.name + (error != null ? " ERROR " + error : " ok"));
                    Cleanup();
                    yield return null;
                }
            }
        }
        File.WriteAllLines(Path.Combine(outDir, "done.txt"), log.ToArray());
        Application.Quit();
    }

    private void SetupStudio()
    {
        QualitySettings.shadows = ShadowQuality.All;
        QualitySettings.shadowResolution = ShadowResolution.High;
        QualitySettings.shadowDistance = 25f;
        QualitySettings.antiAliasing = 4;
        RenderSettings.fog = false;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.62f, 0.67f, 0.75f);
        RenderSettings.ambientEquatorColor = new Color(0.48f, 0.48f, 0.48f);
        RenderSettings.ambientGroundColor = new Color(0.26f, 0.24f, 0.21f);

        var sun = new GameObject("Sun").AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.intensity = 1.15f;
        sun.color = new Color(1f, 0.96f, 0.9f);
        sun.shadows = LightShadows.Soft;
        sun.shadowStrength = 0.65f;
        sun.transform.rotation = Quaternion.Euler(50f, -35f, 0f);
        RenderSettings.sun = sun;

        // Ground: a 1 m checker (feet sliding and stride length are easy to judge on it).
        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.transform.localScale = new Vector3(6f, 1f, 6f);
        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        tex.SetPixels32(new Color32[] { new Color32(150, 152, 148, 255), new Color32(128, 130, 126, 255), new Color32(128, 130, 126, 255), new Color32(150, 152, 148, 255) });
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Repeat;
        tex.Apply();
        var mat = new Material(MaterialCache.Lit(Color.white));
        mat.mainTexture = tex;
        mat.mainTextureScale = new Vector2(30f, 30f);
        ground.GetComponent<Renderer>().sharedMaterial = mat;

        cam = new GameObject("ShotCamera").AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.56f, 0.62f, 0.7f);
        cam.nearClipPlane = 0.05f;
        cam.farClipPlane = 200f;
        cam.tag = "MainCamera";
    }

    private GameObject actor;
    private string tuneTag = "";
    private readonly HashSet<string> loggedSkins = new HashSet<string>();

    private void Cleanup()
    {
        if (actor != null)
            Destroy(actor);
        actor = null;
    }

    private IEnumerator RunCase(Case c, string skin)
    {
        actor = new GameObject("Actor");
        actor.transform.position = new Vector3(0f, c.fall ? 4.9f : 0.9f, 0f);
        var rig = CharacterRig.Build(actor, new Color(0.2f, 0.45f, 0.85f), new Color(0.25f, 0.27f, 0.3f), new Color(0.93f, 0.78f, 0.63f),
            new Color(0.32f, 0.38f, 0.26f), new Color(0.42f, 0.34f, 0.22f), skin);
        var pivot = new GameObject("Pivot").transform;
        pivot.SetParent(actor.transform, false);
        pivot.localPosition = new Vector3(0f, 0.75f, 0f);
        var weaponGo = new GameObject("Weapon");
        weaponGo.transform.SetParent(pivot, false);
        weaponGo.transform.localPosition = new Vector3(0.32f, -0.22f, 0.55f);
        var wc = weaponGo.AddComponent<WeaponController>();
        var data = Gunsmith.BaseWeapon(c.gun).Clone();
        data.modelSkin = c.gunSkin;
        wc.Initialize(data, null);
        weaponGo.SetActive(!c.noGun);
        rig.weaponHold = weaponGo.transform;
        rig.aimReference = pivot;
        rig.aiming = c.aim;
        rig.crouched = c.crouch;
        rig.showcase = c.showcase;
        rig.pose = c.pose;
        rig.grounded = !c.fall;
        if (loggedSkins.Add(skin + "|" + c.gun + c.gunSkin))
        {
            // which textures the model and the gun ended up with (white models: a texture that did not load)
            var sb = new System.Text.StringBuilder("[PoseShots] materials " + skin + " / " + c.gun + " " + c.gunSkin + ":");
            foreach (var r in actor.GetComponentsInChildren<Renderer>(true))
                foreach (var m in r.sharedMaterials)
                    if (m != null)
                        sb.Append(" ").Append(r.name).Append(":").Append(m.name).Append("=").Append(m.mainTexture != null ? m.mainTexture.name : "-");
            Debug.Log(sb.ToString());
        }

        float vy = 0f;
        const float dt = 1f / 30f;
        if (c.fall)
        {
            // dropped from 4 m: photographed in the air, or (land) a few frames after touching down
            for (int f = 0; f < 200; f++)
            {
                vy -= 9.81f * dt;
                Vector3 p = actor.transform.position + Vector3.up * vy * dt;
                if (p.y <= 0.9f)
                {
                    p.y = 0.9f;
                    rig.grounded = true;
                }
                actor.transform.position = p;
                PlaceCamera(c.views[0], c);
                if (!c.land && f == 15)
                    break;
                if (c.land && rig.grounded)
                    break;
                yield return null;
            }
            for (int f = 0; f < (c.land ? 3 : 0); f++)
                yield return null;
            yield return new WaitForEndOfFrame();
            foreach (var v in c.views)
                Capture(c, skin, v, "");
            yield break;
        }

        const int frames = 50;
        for (int f = 0; f < frames + c.extraFrames; f++)
        {
            pivot.localRotation = Quaternion.Euler(c.pitch, 0f, 0f);
            actor.transform.position += actor.transform.TransformDirection(c.move) * dt;
            PlaceCamera(c.views[0], c);
            yield return new WaitForEndOfFrame();
            bool first = f == frames - 1, second = c.extraFrames > 0 && f == frames - 1 + c.extraFrames;
            if (first || second)
            {
                foreach (var v in c.views)
                    Capture(c, skin, v, first ? "" : "_b");
                PlaceCamera(c.views[0], c);
            }
            yield return null;
        }
    }

    private void PlaceCamera(string view, Case c)
    {
        Vector3 feet = actor.transform.position - Vector3.up * 0.9f;
        Quaternion r = actor.transform.rotation;
        Vector3 pos, look;
        float fov = 35f;
        switch (view)
        {
            case "side":
                pos = feet + r * new Vector3(3.3f, 1.2f, 0.35f);
                look = feet + r * new Vector3(0f, 0.95f, 0.2f);
                break;
            case "left":
                pos = feet + r * new Vector3(-3.3f, 1.2f, 0.35f);
                look = feet + r * new Vector3(0f, 0.95f, 0.2f);
                break;
            case "back":
            {
                // the game's third-person camera: over the right shoulder, looking along the aim
                Vector3 pv = feet + Vector3.up * (0.9f + (c.crouch ? 0.25f : c.pose == RigPose.Prone ? -0.4f : 0.75f));
                Quaternion aim = r * Quaternion.Euler(c.pitch, 0f, 0f);
                pos = pv + aim * new Vector3(0.55f, 0.35f, -3.6f);
                look = pos + aim * Vector3.forward;
                fov = 60f;
                break;
            }
            case "ads":
            {
                Vector3 pv = feet + Vector3.up * 1.65f;
                Quaternion aim = r * Quaternion.Euler(c.pitch, 0f, 0f);
                pos = pv + aim * new Vector3(0.55f, 0.35f, -1.6f);
                look = pos + aim * Vector3.forward;
                fov = 45f;
                break;
            }
            case "close":
                // upper body from the front-right: shoulders, elbows, hands on the gun
                pos = feet + r * new Vector3(1.0f, 1.55f, 1.45f);
                look = feet + r * new Vector3(0.05f, 1.3f, 0.15f);
                fov = 38f;
                break;
            case "top":
                // from above and behind: how the gun lines up with the shoulder and the eye
                pos = feet + r * new Vector3(0.35f, 3.2f, -1.2f);
                look = feet + r * new Vector3(0.1f, 1.45f, 0.35f);
                fov = 32f;
                break;
            case "lobby":
                // the lobby camera (PlayerController.LateUpdate): in front, chest height, long lens
                pos = feet + r * new Vector3(0f, 1.3f, 4.4f);
                look = feet + Vector3.up * 1.0f;
                fov = 32f;
                break;
            default:   // front three-quarter, from the character's right
                pos = feet + r * new Vector3(1.7f, 1.35f, 2.7f);
                look = feet + r * new Vector3(0f, 1.0f, 0.1f);
                break;
        }
        if (c.pose == RigPose.Prone && view != "back")
        {
            pos.y = feet.y + Mathf.Min(pos.y - feet.y, 0.9f);
            look.y = feet.y + 0.3f;
        }
        cam.transform.position = pos;
        cam.transform.LookAt(look);
        cam.fieldOfView = fov;
    }

    private void Capture(Case c, string skin, string view, string tag)
    {
        PlaceCamera(view, c);
        var rt = RenderTexture.GetTemporary(W, H, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default, 4);
        cam.targetTexture = rt;
        cam.Render();
        RenderTexture.active = rt;
        grab.ReadPixels(new Rect(0, 0, W, H), 0, 0);
        grab.Apply();
        RenderTexture.active = null;
        cam.targetTexture = null;
        RenderTexture.ReleaseTemporary(rt);
        string file = string.Format("{0:000}_{5}{1}_{2}_{3}{4}.ppm", shotIndex++, skin, c.name, view, tag, tuneTag);
        File.WriteAllBytes(Path.Combine(outDir, file), Ppm(grab));
    }

    /// <summary>Binary PPM (the project has no image-encoding module; converted to PNG afterwards).</summary>
    private static byte[] Ppm(Texture2D t)
    {
        Color32[] px = t.GetPixels32();
        byte[] head = System.Text.Encoding.ASCII.GetBytes("P6\n" + t.width + " " + t.height + "\n255\n");
        var bytes = new byte[head.Length + t.width * t.height * 3];
        System.Array.Copy(head, bytes, head.Length);
        int o = head.Length;
        for (int y = t.height - 1; y >= 0; y--)   // texture rows go bottom-up, PPM top-down
        {
            for (int x = 0; x < t.width; x++)
            {
                Color32 c = px[y * t.width + x];
                bytes[o++] = c.r;
                bytes[o++] = c.g;
                bytes[o++] = c.b;
            }
        }
        return bytes;
    }
}
