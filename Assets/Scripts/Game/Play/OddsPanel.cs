using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The chances of every random reward (boxes, the wheel, the weekly draw), shown before buying as Google Play's
/// payments policy asks for random items: a dimmed screen with a list and a close button.
/// </summary>
public static class OddsPanel
{
    public static void Show(Transform anyUi, string title, List<string> lines)
    {
        var canvas = anyUi.GetComponentInParent<Canvas>();
        Transform root = canvas != null ? canvas.rootCanvas.transform : anyUi;
        var dim = UIUtil.CreateStretch(root, "Odds");
        var dimImg = dim.gameObject.AddComponent<Image>();
        dimImg.color = new Color(0f, 0f, 0f, 0.75f);
        var mid = new Vector2(0.5f, 0.5f);
        float h = Mathf.Min(900f, 200f + lines.Count * 44f);
        var panel = UIUtil.CreateImage(dim, "Panel", mid, Vector2.zero, new Vector2(1100f, h), new Color(0.07f, 0.08f, 0.11f, 0.98f), false);
        var o = panel.gameObject.AddComponent<Outline>();
        o.effectColor = new Color(1f, 0.82f, 0.18f, 0.6f);
        o.effectDistance = new Vector2(3f, -3f);
        var t = UIUtil.CreateText(panel.transform, title, new Vector2(0.5f, 1f), new Vector2(0f, -50f), new Vector2(1000f, 60f), 38, TextAnchor.MiddleCenter);
        t.fontStyle = FontStyle.Bold;
        t.color = Theme.Accent;
        var body = UIUtil.CreateText(panel.transform, string.Join("\n", lines.ToArray()), new Vector2(0.5f, 1f), new Vector2(0f, -110f - (h - 230f) * 0.5f),
                                     new Vector2(1000f, h - 230f), 26, TextAnchor.UpperLeft);
        body.horizontalOverflow = HorizontalWrapMode.Wrap;
        body.verticalOverflow = VerticalWrapMode.Truncate;
        Text label;
        var close = UIUtil.CreateButton(panel.transform, "KAPAT", new Vector2(0.5f, 0f), new Vector2(0f, 60f), new Vector2(300f, 70f), Theme.Accent, false, 30, out label);
        label.color = new Color(0.1f, 0.1f, 0.1f);
        label.GetComponent<Shadow>().enabled = false;
        close.onClick.AddListener(() => Object.Destroy(dim.gameObject));
        dim.SetAsLastSibling();
    }

    public static string Percent(float p)
    {
        float v = p * 100f;
        return "%" + (v >= 10f ? v.ToString("0.#") : v >= 1f ? v.ToString("0.0#") : v.ToString("0.0##"));
    }
}
