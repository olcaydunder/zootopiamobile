using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Where things go on each gun model, measured from the meshes (Tools: side views with markers), in the weapon's
/// space: +Z towards the muzzle, +Y up, +X right, origin at the grip, metres. Used to fit attachments flush to the
/// gun (on the rail, under the handguard, in line with the barrel, below the magazine, at the stock) and to put the
/// character's hands on it (grip and support hand).
/// </summary>
public class GunAnchors
{
    public float cx, w;                                   // centre line, receiver half width
    public float muzzleY, muzzleZ, barrelR, barrelStart;  // bore axis at the muzzle; where the bare barrel starts
    public float railY, railZ, railLen;                   // optic mount (top of the rail / receiver)
    public bool scope;                                    // the model has its own scope (axis height, ends, radius)
    public float scopeY, scopeBack, scopeFront, scopeR;
    public float underY, underZ;                          // underside of the handguard (grips, bipod)
    public float sideX, sideY, sideZ;                     // right side of the handguard (laser)
    public bool hasMag;                                   // a box magazine (else: where one would go)
    public float magTopY, magTopZ, magBotY, magBotZ, magDepth, magThick;
    public bool hasStock;                                 // its own stock: butt plate and comb line
    public float buttZ, buttLo, buttHi, combY0, combY1, combZ0, combZ1, stockW;
    public float attachY, attachZ;                        // no stock: where one would attach (receiver / grip rear)
    public float gripY, gripZ, gripTilt, gripW, gripD, gripH;   // pistol grip centre, backward lean (degrees), size
    public float supportY, supportZ;                      // where the other hand holds it

    public Vector3 Grip { get { return new Vector3(cx, gripY, gripZ); } }
    public Vector3 Support { get { return new Vector3(cx, supportY, supportZ); } }
    public Vector3 Muzzle { get { return new Vector3(cx, muzzleY, muzzleZ); } }
    /// <summary>Middle of the butt plate (the shoulder point); guns without a stock: the rear of the receiver.</summary>
    public Vector3 Butt { get { return hasStock ? new Vector3(cx, (buttLo + buttHi) * 0.5f, buttZ) : new Vector3(cx, attachY, attachZ); } }

    private static readonly Dictionary<string, GunAnchors> table = new Dictionary<string, GunAnchors>
    {
        { "Models/Guns/Rifle", new GunAnchors { cx = 0f, w = 0.016f, muzzleY = 0.096f, muzzleZ = 0.634f, barrelR = 0.0085f, barrelStart = 0.42f, railY = 0.136f, railZ = 0.1f, railLen = 0.2f, scope = false, scopeY = 0f, scopeBack = 0f, scopeFront = 0f, scopeR = 0f, underY = 0.069f, underZ = 0.35f, sideX = 0.014f, sideY = 0.105f, sideZ = 0.33f, hasMag = true, magTopY = 0.05f, magTopZ = 0.155f, magBotY = -0.112f, magBotZ = 0.235f, magDepth = 0.058f, magThick = 0.022f, hasStock = true, buttZ = -0.266f, buttLo = -0.019f, buttHi = 0.09f, combY0 = 0.084f, combY1 = 0.094f, combZ0 = -0.25f, combZ1 = -0.08f, stockW = 0.026f, attachY = 0f, attachZ = 0f, gripY = -0.012f, gripZ = -0.006f, gripTilt = 22f, gripW = 0.022f, gripD = 0.034f, gripH = 0.07f, supportY = 0.069f, supportZ = 0.33f } },
        { "Models/Guns/Rifle_AK19", new GunAnchors { cx = -0.009f, w = 0.021f, muzzleY = 0.0365f, muzzleZ = 0.674f, barrelR = 0.0105f, barrelStart = 0.4f, railY = 0.081f, railZ = 0.17f, railLen = 0.3f, scope = false, scopeY = 0f, scopeBack = 0f, scopeFront = 0f, scopeR = 0f, underY = 0.014f, underZ = 0.32f, sideX = 0.021f, sideY = 0.046f, sideZ = 0.32f, hasMag = true, magTopY = -0.005f, magTopZ = 0.145f, magBotY = -0.176f, magBotZ = 0.195f, magDepth = 0.055f, magThick = 0.024f, hasStock = true, buttZ = -0.2755f, buttLo = -0.086f, buttHi = 0.045f, combY0 = 0.045f, combY1 = 0.054f, combZ0 = -0.25f, combZ1 = -0.12f, stockW = 0.032f, attachY = 0f, attachZ = 0f, gripY = -0.055f, gripZ = 0f, gripTilt = 22f, gripW = 0.028f, gripD = 0.036f, gripH = 0.08f, supportY = 0.012f, supportZ = 0.32f } },
        { "Models/Guns/Rifle_AR15", new GunAnchors { cx = 0.003f, w = 0.024f, muzzleY = 0.065f, muzzleZ = 0.545f, barrelR = 0.0085f, barrelStart = 0.47f, railY = 0.104f, railZ = 0.08f, railLen = 0.2f, scope = false, scopeY = 0f, scopeBack = 0f, scopeFront = 0f, scopeR = 0f, underY = 0.033f, underZ = 0.33f, sideX = 0.036f, sideY = 0.065f, sideZ = 0.33f, hasMag = true, magTopY = -0.035f, magTopZ = 0.135f, magBotY = -0.17f, magBotZ = 0.165f, magDepth = 0.07f, magThick = 0.024f, hasStock = true, buttZ = -0.315f, buttLo = -0.045f, buttHi = 0.093f, combY0 = 0.088f, combY1 = 0.093f, combZ0 = -0.3f, combZ1 = -0.14f, stockW = 0.044f, attachY = 0f, attachZ = 0f, gripY = -0.06f, gripZ = -0.035f, gripTilt = 27f, gripW = 0.026f, gripD = 0.035f, gripH = 0.09f, supportY = 0.033f, supportZ = 0.31f } },
        { "Models/Guns/SMG", new GunAnchors { cx = 0f, w = 0.035f, muzzleY = 0.1345f, muzzleZ = 0.4917f, barrelR = 0.0105f, barrelStart = 0.43f, railY = 0.207f, railZ = 0.15f, railLen = 0.3f, scope = false, scopeY = 0f, scopeBack = 0f, scopeFront = 0f, scopeR = 0f, underY = 0.104f, underZ = 0.33f, sideX = 0.035f, sideY = 0.15f, sideZ = 0.33f, hasMag = true, magTopY = 0.03f, magTopZ = 0.19f, magBotY = -0.158f, magBotZ = 0.206f, magDepth = 0.066f, magThick = 0.03f, hasStock = false, buttZ = 0f, buttLo = 0f, buttHi = 0f, combY0 = 0f, combY1 = 0f, combZ0 = 0f, combZ1 = 0f, stockW = 0.05f, attachY = 0.165f, attachZ = -0.108f, gripY = -0.03f, gripZ = -0.022f, gripTilt = 22f, gripW = 0.036f, gripD = 0.045f, gripH = 0.1f, supportY = 0.104f, supportZ = 0.31f } },
        { "Models/Guns/Shotgun", new GunAnchors { cx = 0f, w = 0.025f, muzzleY = 0.085f, muzzleZ = 0.8603f, barrelR = 0.016f, barrelStart = 0.37f, railY = 0.105f, railZ = 0.23f, railLen = 0.22f, scope = false, scopeY = 0f, scopeBack = 0f, scopeFront = 0f, scopeR = 0f, underY = 0.01f, underZ = 0.67f, sideX = 0.025f, sideY = 0.036f, sideZ = 0.71f, hasMag = false, magTopY = 0.014f, magTopZ = 0.262f, magBotY = -0.085f, magBotZ = 0.266f, magDepth = 0.06f, magThick = 0.03f, hasStock = false, buttZ = 0f, buttLo = 0f, buttHi = 0f, combY0 = 0f, combY1 = 0f, combZ0 = 0f, combZ1 = 0f, stockW = 0.04f, attachY = 0.03f, attachZ = -0.025f, gripY = -0.035f, gripZ = -0.045f, gripTilt = 40f, gripW = 0.04f, gripD = 0.045f, gripH = 0.08f, supportY = 0.01f, supportZ = 0.67f } },
        { "Models/Guns/Sniper", new GunAnchors { cx = 0f, w = 0.013f, muzzleY = 0.044f, muzzleZ = 0.8638f, barrelR = 0.011f, barrelStart = 0.48f, railY = 0.063f, railZ = 0.15f, railLen = 0.14f, scope = true, scopeY = 0.089f, scopeBack = -0.03f, scopeFront = 0.31f, scopeR = 0.024f, underY = 0f, underZ = 0.38f, sideX = 0.012f, sideY = 0.025f, sideZ = 0.4f, hasMag = false, magTopY = -0.006f, magTopZ = 0.175f, magBotY = -0.07f, magBotZ = 0.18f, magDepth = 0.06f, magThick = 0.026f, hasStock = true, buttZ = -0.336f, buttLo = -0.13f, buttHi = 0.021f, combY0 = 0.015f, combY1 = 0.021f, combZ0 = -0.32f, combZ1 = -0.08f, stockW = 0.044f, attachY = 0f, attachZ = 0f, gripY = -0.025f, gripZ = -0.045f, gripTilt = 30f, gripW = 0.034f, gripD = 0.04f, gripH = 0.06f, supportY = 0f, supportZ = 0.36f } },
        { "Models/Guns/Sniper_Shadow", new GunAnchors { cx = -0.014f, w = 0.032f, muzzleY = 0.05f, muzzleZ = 0.9126f, barrelR = 0.017f, barrelStart = 0.63f, railY = 0.094f, railZ = 0.17f, railLen = 0.26f, scope = false, scopeY = 0f, scopeBack = 0f, scopeFront = 0f, scopeR = 0f, underY = 0.01f, underZ = 0.5f, sideX = 0.032f, sideY = 0.045f, sideZ = 0.5f, hasMag = true, magTopY = 0f, magTopZ = 0.205f, magBotY = -0.108f, magBotZ = 0.2f, magDepth = 0.105f, magThick = 0.032f, hasStock = true, buttZ = -0.297f, buttLo = -0.107f, buttHi = 0.069f, combY0 = 0.07f, combY1 = 0.074f, combZ0 = -0.24f, combZ1 = -0.08f, stockW = 0.06f, attachY = 0f, attachZ = 0f, gripY = -0.08f, gripZ = -0.03f, gripTilt = 25f, gripW = 0.03f, gripD = 0.04f, gripH = 0.09f, supportY = 0.01f, supportZ = 0.45f } },
        { "Models/Guns/Pistol", new GunAnchors { cx = 0f, w = 0.02f, muzzleY = 0.0735f, muzzleZ = 0.2758f, barrelR = 0.011f, barrelStart = 0.2758f, railY = 0.092f, railZ = 0.035f, railLen = 0.08f, scope = false, scopeY = 0f, scopeBack = 0f, scopeFront = 0f, scopeR = 0f, underY = 0.028f, underZ = 0.18f, sideX = 0.02f, sideY = 0.06f, sideZ = 0.2f, hasMag = true, magTopY = 0.02f, magTopZ = 0.005f, magBotY = -0.053f, magBotZ = -0.014f, magDepth = 0.045f, magThick = 0.034f, hasStock = false, buttZ = 0f, buttLo = 0f, buttHi = 0f, combY0 = 0f, combY1 = 0f, combZ0 = 0f, combZ1 = 0f, stockW = 0.036f, attachY = 0.03f, attachZ = -0.044f, gripY = -0.014f, gripZ = -0.012f, gripTilt = 15f, gripW = 0.034f, gripD = 0.042f, gripH = 0.055f, supportY = -0.03f, supportZ = -0.005f } },
        { "Models/Guns/Pistol_Engraved", new GunAnchors { cx = 0f, w = 0.011f, muzzleY = 0.022f, muzzleZ = 0.158f, barrelR = 0.008f, barrelStart = 0.158f, railY = 0.036f, railZ = 0.03f, railLen = 0.08f, scope = false, scopeY = 0f, scopeBack = 0f, scopeFront = 0f, scopeR = 0f, underY = 0f, underZ = 0.11f, sideX = 0.011f, sideY = 0.02f, sideZ = 0.11f, hasMag = true, magTopY = -0.005f, magTopZ = -0.012f, magBotY = -0.09f, magBotZ = -0.035f, magDepth = 0.03f, magThick = 0.022f, hasStock = false, buttZ = 0f, buttLo = 0f, buttHi = 0f, combY0 = 0f, combY1 = 0f, combZ0 = 0f, combZ1 = 0f, stockW = 0.03f, attachY = 0.005f, attachZ = -0.045f, gripY = -0.045f, gripZ = -0.025f, gripTilt = 18f, gripW = 0.03f, gripD = 0.04f, gripH = 0.08f, supportY = -0.045f, supportZ = -0.01f } },
        { "Models/Guns/Pistol_Flame", new GunAnchors { cx = 0f, w = 0.019f, muzzleY = 0.035f, muzzleZ = 0.24f, barrelR = 0.012f, barrelStart = 0.24f, railY = 0.052f, railZ = 0.03f, railLen = 0.08f, scope = false, scopeY = 0f, scopeBack = 0f, scopeFront = 0f, scopeR = 0f, underY = 0.001f, underZ = 0.19f, sideX = 0.019f, sideY = 0.025f, sideZ = 0.19f, hasMag = true, magTopY = -0.03f, magTopZ = 0.005f, magBotY = -0.119f, magBotZ = -0.004f, magDepth = 0.055f, magThick = 0.033f, hasStock = false, buttZ = 0f, buttLo = 0f, buttHi = 0f, combY0 = 0f, combY1 = 0f, combZ0 = 0f, combZ1 = 0f, stockW = 0.036f, attachY = -0.015f, attachZ = -0.045f, gripY = -0.06f, gripZ = 0f, gripTilt = 10f, gripW = 0.034f, gripD = 0.055f, gripH = 0.08f, supportY = -0.07f, supportZ = 0f } },
    };

    /// <summary>Anchors of a gun model (Resources path, e.g. "Models/Guns/Rifle"); null for unknown models.</summary>
    public static GunAnchors For(string modelPath)
    {
        GunAnchors a;
        return modelPath != null && table.TryGetValue(modelPath, out a) ? a : null;
    }

    public static GunAnchors For(WeaponData data)
    {
        return data != null ? For(ModelLibrary.GunPath(data.weaponType, data.modelSkin)) : null;
    }
}
