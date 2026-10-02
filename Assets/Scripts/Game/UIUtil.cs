using UnityEngine;
using UnityEngine.UI;

/// <summary>Small helpers for building the whole UI from code (no prefabs or editor work needed).</summary>
public static class UIUtil
{
    private static Font font;
    private static Sprite circle;

    public static Font DefaultFont
    {
        get
        {
            if (font == null)
            {
                // Unity 2022.2+ ships LegacyRuntime.ttf; Arial.ttf only exists in older versions.
                font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                if (font == null)
                    font = Font.CreateDynamicFontFromOSFont("Arial", 32);
            }
            return font;
        }
    }

    public static Sprite Circle
    {
        get
        {
            if (circle == null)
            {
                const int size = 128;
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
                tex.wrapMode = TextureWrapMode.Clamp;
                float r = size * 0.5f;
                var pixels = new Color32[size * size];
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float dx = x + 0.5f - r;
                        float dy = y + 0.5f - r;
                        float d = Mathf.Sqrt(dx * dx + dy * dy);
                        byte a = (byte)(Mathf.Clamp01(r - d) * 255f);
                        pixels[y * size + x] = new Color32(255, 255, 255, a);
                    }
                }
                tex.SetPixels32(pixels);
                tex.Apply();
                circle = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            }
            return circle;
        }
    }

    public static Material UnlitMaterial(Color color)
    {
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
            shader = Shader.Find("UI/Default");
        var mat = new Material(shader);
        mat.color = color;
        return mat;
    }

    public static RectTransform CreateRect(Transform parent, string name, Vector2 anchor, Vector2 position, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rect = (RectTransform)go.transform;
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    public static RectTransform CreateStretch(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rect = (RectTransform)go.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return rect;
    }

    public static Image CreateImage(Transform parent, string name, Vector2 anchor, Vector2 position, Vector2 size, Color color, bool round)
    {
        var rect = CreateRect(parent, name, anchor, position, size);
        var image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        if (round)
            image.sprite = Circle;
        return image;
    }

    public static Text CreateText(Transform parent, string text, Vector2 anchor, Vector2 position, Vector2 size, int fontSize, TextAnchor alignment)
    {
        var rect = CreateRect(parent, "Text", anchor, position, size);
        var uiText = rect.gameObject.AddComponent<Text>();
        uiText.font = DefaultFont;
        uiText.text = text;
        uiText.fontSize = fontSize;
        uiText.color = Color.white;
        uiText.alignment = alignment;
        uiText.horizontalOverflow = HorizontalWrapMode.Overflow;
        uiText.verticalOverflow = VerticalWrapMode.Overflow;
        uiText.raycastTarget = false;

        var shadow = rect.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.6f);
        shadow.effectDistance = new Vector2(2f, -2f);
        return uiText;
    }

    public static Button CreateButton(Transform parent, string label, Vector2 anchor, Vector2 position, Vector2 size, Color color, bool round, int fontSize, out Text labelText)
    {
        var image = CreateImage(parent, label + "Button", anchor, position, size, color, round);
        var button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;

        labelText = CreateText(image.transform, label, new Vector2(0.5f, 0.5f), Vector2.zero, size, fontSize, TextAnchor.MiddleCenter);
        labelText.fontStyle = FontStyle.Bold;
        return button;
    }
}
