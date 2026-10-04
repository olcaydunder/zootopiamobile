using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Square showing a reward: a rarity-tinted frame with the reward's icon, or a swatch of the camo
/// pattern for camouflage. Used on the reward track, the result screen and the spares list.
/// </summary>
public static class RewardView
{
    public static RectTransform Create(Transform parent, Reward r, Vector2 anchor, Vector2 pos, float size)
    {
        Color rc = Theme.Rarity(r.Rarity);
        var bg = UIUtil.CreateImage(parent, "Reward", anchor, pos, new Vector2(size, size), new Color(rc.r * 0.25f, rc.g * 0.25f, rc.b * 0.25f, 0.95f), false);
        bg.raycastTarget = false;
        var t = bg.transform;

        if (r.IsCamo)
        {
            var camo = Cosmetics.FindAny(r.id);
            if (!string.IsNullOrEmpty(camo.id))
            {
                var sw = UIUtil.CreateRawSwatch(t, WeaponDressing.Pattern(camo), Vector2.zero, new Vector2(size * 0.8f, size * 0.8f));
                sw.uvRect = new Rect(0f, 0f, 0.6f, 0.6f);
                sw.raycastTarget = false;
            }
            // small badge: which collection
            string badge = r.kind == RewardKind.WeaponCamo ? "slot_barrel" : r.kind == RewardKind.VehicleCamo ? "veh_offroad" : "class_airborne";
            var b = Icons.Create(t, badge, new Vector2(1f, 0f), new Vector2(-size * 0.17f, size * 0.15f), new Vector2(size * 0.3f, size * 0.22f));
            b.color = new Color(1f, 1f, 1f, 0.9f);
        }
        else
        {
            Icons.Create(t, r.IconName, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size * 0.78f, size * 0.78f));
        }

        var frame = Icons.Create(t, "rarity_frame", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size, size));
        frame.color = rc;
        frame.preserveAspect = false;
        return (RectTransform)t;
    }
}
