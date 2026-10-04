using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// "HUD DÜZENLE": drag the touch controls where your thumbs want them and change their size.
/// Works on a preview of every control (also the ones only shown in the plane or near a vehicle);
/// KAYDET writes the layout to HudLayout and applies it to the real controls.
/// </summary>
public class HudEditorScreen : MonoBehaviour
{
    private class Item
    {
        public TouchControls.HudControl control;
        public RectTransform rect;
        public Image image;
        public float scale;
    }

    private readonly List<Item> items = new List<Item>();
    private Item selected;
    private RectTransform area;
    private Text selectedText, sizeText;

    private static readonly Color Back = new Color(0.04f, 0.06f, 0.08f, 0.94f);
    private static readonly Color BarColor = new Color(0.12f, 0.14f, 0.17f, 0.97f);
    private static readonly Color SelectColor = new Color(1f, 0.82f, 0.2f, 0.75f);

    /// <summary>Opens the editor on top of everything in the given canvas. Returns null if the controls are not built yet.</summary>
    public static HudEditorScreen Open(Transform canvas)
    {
        var tc = TouchControls.Instance;
        if (tc == null || tc.HudControls.Count == 0)
            return null;
        var rect = UIUtil.CreateStretch(canvas, "HudEditor");
        rect.SetAsLastSibling();
        rect.gameObject.AddComponent<Image>().color = Back;
        var screen = rect.gameObject.AddComponent<HudEditorScreen>();
        screen.Build(tc);
        return screen;
    }

    private void Build(TouchControls tc)
    {
        // Faint grid to help line things up.
        area = UIUtil.CreateStretch(transform, "Area");
        for (int i = 1; i < 8; i++)
        {
            var v = UIUtil.CreateImage(area, "GridV", new Vector2(i / 8f, 0.5f), Vector2.zero, new Vector2(2f, 2000f), new Color(1f, 1f, 1f, 0.04f), false);
            v.raycastTarget = false;
        }
        for (int i = 1; i < 4; i++)
        {
            var h = UIUtil.CreateImage(area, "GridH", new Vector2(0.5f, i / 4f), Vector2.zero, new Vector2(4000f, 2f), new Color(1f, 1f, 1f, 0.04f), false);
            h.raycastTarget = false;
        }

        foreach (var c in tc.HudControls)
        {
            var e = HudLayout.Get(c.key);
            Color col = c.color;
            col.a = Mathf.Max(col.a, 0.35f);
            var img = UIUtil.CreateImage(area, c.key, c.anchor, c.basePos + e.offset, c.size, col, c.round);
            var label = UIUtil.CreateText(img.transform, c.label, new Vector2(0.5f, 0.5f), Vector2.zero, c.size, c.size.y < 80f ? 18 : 22, TextAnchor.MiddleCenter);
            label.fontStyle = FontStyle.Bold;
            var item = new Item { control = c, rect = img.rectTransform, image = img, scale = e.scale };
            img.rectTransform.localScale = new Vector3(item.scale, item.scale, 1f);
            var drag = img.gameObject.AddComponent<HudDragHandle>();
            drag.editor = this;
            drag.index = items.Count;
            items.Add(item);
        }

        // Toolbar at the top centre.
        var bar = UIUtil.CreateImage(transform, "Bar", new Vector2(0.5f, 1f), new Vector2(0f, -62f), new Vector2(1500f, 100f), BarColor, false);
        var b = bar.transform;
        Text unused;
        var title = UIUtil.CreateText(b, "HUD DÜZENLE", new Vector2(0f, 0.5f), new Vector2(150f, 0f), new Vector2(260f, 60f), 34, TextAnchor.MiddleLeft);
        title.fontStyle = FontStyle.Bold;
        selectedText = UIUtil.CreateText(b, "Bir düğmeyi sürükle", new Vector2(0f, 0.5f), new Vector2(440f, 0f), new Vector2(300f, 60f), 24, TextAnchor.MiddleLeft);
        selectedText.color = Theme.TextDim;

        UIUtil.CreateButton(b, "–", new Vector2(0.5f, 0.5f), new Vector2(-40f, 0f), new Vector2(70f, 64f), BarColor * 1.6f, false, 40, out unused)
            .onClick.AddListener(() => Resize(-0.1f));
        sizeText = UIUtil.CreateText(b, "100%", new Vector2(0.5f, 0.5f), new Vector2(50f, 0f), new Vector2(100f, 60f), 28, TextAnchor.MiddleCenter);
        UIUtil.CreateButton(b, "+", new Vector2(0.5f, 0.5f), new Vector2(140f, 0f), new Vector2(70f, 64f), BarColor * 1.6f, false, 40, out unused)
            .onClick.AddListener(() => Resize(0.1f));

        UIUtil.CreateButton(b, "SIFIRLA", new Vector2(1f, 0.5f), new Vector2(-540f, 0f), new Vector2(170f, 64f), new Color(0.35f, 0.38f, 0.44f, 1f), false, 24, out unused)
            .onClick.AddListener(ResetLayout);
        UIUtil.CreateButton(b, "İPTAL", new Vector2(1f, 0.5f), new Vector2(-350f, 0f), new Vector2(170f, 64f), new Color(0.35f, 0.38f, 0.44f, 1f), false, 24, out unused)
            .onClick.AddListener(Close);
        var save = UIUtil.CreateButton(b, "KAYDET", new Vector2(1f, 0.5f), new Vector2(-130f, 0f), new Vector2(220f, 64f), Theme.Accent, false, 28, out unused);
        unused.color = new Color(0.1f, 0.1f, 0.1f);
        save.onClick.AddListener(Save);
        Select(null);
    }

    public void Select(int index)
    {
        Select(index >= 0 && index < items.Count ? items[index] : null);
    }

    private void Select(Item item)
    {
        if (selected != null)
            selected.image.color = Tint(selected.control.color);
        selected = item;
        if (selected != null)
        {
            selected.image.color = SelectColor;
            selected.rect.SetAsLastSibling();
            selectedText.text = selected.control.label;
            selectedText.color = Color.white;
        }
        else
        {
            selectedText.text = "Bir düğmeyi sürükle";
            selectedText.color = Theme.TextDim;
        }
        sizeText.text = selected != null ? Mathf.RoundToInt(selected.scale * 100f) + "%" : "–";
    }

    private static Color Tint(Color c)
    {
        c.a = Mathf.Max(c.a, 0.35f);
        return c;
    }

    /// <summary>Moves the selected control by a screen-space drag delta, kept inside the screen.</summary>
    public void Drag(int index, Vector2 screenDelta)
    {
        if (index < 0 || index >= items.Count)
            return;
        var item = items[index];
        if (item != selected)
            Select(item);
        var canvas = GetComponentInParent<Canvas>();
        float f = canvas != null && canvas.scaleFactor > 0f ? canvas.scaleFactor : 1f;
        item.rect.anchoredPosition += screenDelta / f;
        Clamp(item);
    }

    private void Clamp(Item item)
    {
        Rect r = area.rect;
        Vector2 half = item.control.size * item.scale * 0.5f;
        Vector2 anchorPoint = new Vector2(item.control.anchor.x * r.width, item.control.anchor.y * r.height);
        Vector2 centre = anchorPoint + item.rect.anchoredPosition;
        centre.x = Mathf.Clamp(centre.x, half.x, Mathf.Max(half.x, r.width - half.x));
        centre.y = Mathf.Clamp(centre.y, half.y, Mathf.Max(half.y, r.height - half.y - 110f));   // stay below the toolbar
        item.rect.anchoredPosition = centre - anchorPoint;
    }

    private void Resize(float step)
    {
        if (selected == null)
            return;
        selected.scale = Mathf.Clamp(Mathf.Round((selected.scale + step) * 10f) / 10f, HudLayout.MinScale, HudLayout.MaxScale);
        selected.rect.localScale = new Vector3(selected.scale, selected.scale, 1f);
        Clamp(selected);
        sizeText.text = Mathf.RoundToInt(selected.scale * 100f) + "%";
    }

    private void ResetLayout()
    {
        foreach (var item in items)
        {
            item.scale = 1f;
            item.rect.localScale = Vector3.one;
            item.rect.anchoredPosition = item.control.basePos;
        }
        Select(null);
    }

    private void Save()
    {
        HudLayout.ResetAll();
        foreach (var item in items)
            HudLayout.Set(item.control.key, item.rect.anchoredPosition - item.control.basePos, item.scale);
        HudLayout.Save();
        if (TouchControls.Instance != null)
            TouchControls.Instance.ApplySettings();
        Close();
    }

    private void Close()
    {
        Destroy(gameObject);
    }
}

/// <summary>Drag handle on one control preview in the HUD editor.</summary>
public class HudDragHandle : MonoBehaviour, IPointerDownHandler, IDragHandler
{
    public HudEditorScreen editor;
    public int index;

    public void OnPointerDown(PointerEventData e) { editor.Select(index); }
    public void OnDrag(PointerEventData e) { editor.Drag(index, e.delta); }
}
