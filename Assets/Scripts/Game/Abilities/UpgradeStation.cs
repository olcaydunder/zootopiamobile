using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Güçlendirme Noktası: a purple beacon on the map. Standing next to it for 2 seconds raises your class
/// ability to level 2 and fills it. Each player or bot can use each station once.
/// </summary>
public class UpgradeStation : MonoBehaviour
{
    public static readonly List<UpgradeStation> All = new List<UpgradeStation>();
    public const int Count = 5;
    public const float Radius = 3f;
    public const float HoldTime = 2f;

    private readonly HashSet<IAbilityUser> used = new HashSet<IAbilityUser>();
    private Transform icon;
    private Transform beam;
    private float playerHold;

    public float PlayerProgress { get { return playerHold / HoldTime; } }

    public bool UsedBy(IAbilityUser u) { return used.Contains(u); }

    /// <summary>Puts the stations around the play area (called at the start of each round).</summary>
    public static void SpawnAll()
    {
        ClearAll();
        float half = MapData.Loaded ? MapData.PlayHalf : World.IslandRadius * 0.7f;
        float start = Random.Range(0f, 360f);
        for (int i = 0; i < Count; i++)
        {
            float a = (start + i * 360f / Count + Random.Range(-20f, 20f)) * Mathf.Deg2Rad;
            float r = half * (i == 0 ? 0.15f : Random.Range(0.35f, 0.7f));
            Vector3 around = new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
            Vector3 p = World.RandomOpenPoint(around, 30f);
            var go = new GameObject("UpgradeStation");
            go.transform.position = p - Vector3.up * 0.95f;
            var s = go.AddComponent<UpgradeStation>();
            s.Build();
            All.Add(s);
        }
    }

    public static void ClearAll()
    {
        foreach (var s in All)
            if (s != null)
                Destroy(s.gameObject);
        All.Clear();
    }

    private void Build()
    {
        var purple = new Color(0.6f, 0.35f, 1f);
        var metal = MaterialCache.Lit(new Color(0.18f, 0.18f, 0.22f));
        AbilityFx.Primitive(transform, PrimitiveType.Cylinder, new Vector3(0f, 0.1f, 0f), new Vector3(2.2f, 0.1f, 2.2f), metal);
        AbilityFx.Primitive(transform, PrimitiveType.Cylinder, new Vector3(0f, 0.22f, 0f), new Vector3(1.6f, 0.03f, 1.6f), MaterialCache.Lit(purple));
        AbilityFx.Primitive(transform, PrimitiveType.Cube, new Vector3(0f, 0.75f, 0f), new Vector3(0.5f, 1.1f, 0.5f), metal);
        AbilityFx.Primitive(transform, PrimitiveType.Cube, new Vector3(0f, 1.1f, 0.26f), new Vector3(0.36f, 0.3f, 0.02f), MaterialCache.Lit(purple * 1.2f));
        beam = AbilityFx.Primitive(transform, PrimitiveType.Cylinder, new Vector3(0f, 9f, 0f), new Vector3(0.5f, 8f, 0.5f), AbilityFx.Glass(new Color(0.65f, 0.4f, 1f, 0.28f)));

        var iconGo = new GameObject("Icon");
        iconGo.transform.SetParent(transform, false);
        iconGo.transform.localPosition = new Vector3(0f, 2.6f, 0f);
        var sr = iconGo.AddComponent<SpriteRenderer>();
        sr.sprite = Icons.Get("upgrade_station");
        iconGo.transform.localScale = Vector3.one * 0.55f;
        icon = iconGo.transform;
    }

    private void Update()
    {
        // Icon bobs and faces the camera.
        var cam = Camera.main;
        if (cam != null)
            icon.rotation = Quaternion.LookRotation(icon.position - cam.transform.position);
        icon.localPosition = new Vector3(0f, 2.6f + Mathf.Sin(Time.time * 2f) * 0.15f, 0f);
        float k = 0.5f + 0.5f * Mathf.Sin(Time.time * 3f);
        beam.localScale = new Vector3(0.4f + k * 0.15f, 8f, 0.4f + k * 0.15f);

        var gm = GameManager.Instance;
        if (gm == null || gm.currentState != GameState.InGame)
            return;
        foreach (var d in gm.Combatants)
        {
            var u = d as IAbilityUser;
            if (u == null || u.IsDead || u.Ability == null || used.Contains(u))
                continue;
            Vector3 to = u.transform.position - transform.position;
            if (Mathf.Abs(to.y) > 3f)
                continue;
            to.y = 0f;
            bool inside = to.sqrMagnitude < Radius * Radius && u.CanUseAbility;
            if (!u.IsPlayer)
            {
                if (inside)
                    Apply(u);
                continue;
            }
            playerHold = inside ? playerHold + Time.deltaTime : 0f;
            if (playerHold >= HoldTime)
            {
                playerHold = 0f;
                Apply(u);
            }
        }
    }

    private void Apply(IAbilityUser u)
    {
        used.Add(u);
        u.Ability.Upgrade();
        AbilityFx.Flash(u.transform.position, new Color(0.65f, 0.4f, 1f, 0.6f), 3f, 0.5f);
        Sfx.PlayAt(SoundBank.Pickup, transform.position, 1f, 1.3f);
        if (u.IsPlayer)
        {
            var gm = GameManager.Instance;
            if (gm != null && gm.uiManager != null)
                gm.uiManager.Toast(u.Ability.Def.ability.ToUpper() + " SEVİYE 2!");
        }
    }

    /// <summary>The station the player is currently charging at (for the HUD), or null.</summary>
    public static UpgradeStation PlayerCharging()
    {
        foreach (var s in All)
            if (s != null && s.playerHold > 0f)
                return s;
        return null;
    }
}
