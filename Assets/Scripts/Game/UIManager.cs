using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    private Canvas canvas;
    private GameObject lobbyPanel;
    private GameObject hudPanel;
    private GameObject resultPanel;

    private void Start()
    {
        SetupCanvas();
        ShowLobby();
    }

    private void SetupCanvas()
    {
        canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);

        gameObject.AddComponent<GraphicRaycaster>();
    }

    public void ShowLobby()
    {
        if (lobbyPanel == null)
            lobbyPanel = CreatePanel("LobbyPanel", new Vector2(0, 0), new Vector2(520, 430), new Color(0.06f, 0.06f, 0.10f, 0.9f));

        lobbyPanel.SetActive(true);
        if (hudPanel != null) hudPanel.SetActive(false);
        if (resultPanel != null) resultPanel.SetActive(false);

        CreateText(lobbyPanel.transform, "Battle Island Mobile", new Vector2(0, 140), 38, Color.white, true);
        CreateButton(lobbyPanel.transform, "Solo", new Vector2(0, 30), 180, 62, () => GameManager.Instance.StartMatch(MatchMode.Solo));
        CreateButton(lobbyPanel.transform, "Duo", new Vector2(0, -50), 180, 62, () => GameManager.Instance.StartMatch(MatchMode.Duo));
        CreateButton(lobbyPanel.transform, "Squad", new Vector2(0, -130), 180, 62, () => GameManager.Instance.StartMatch(MatchMode.Squad));
    }

    public void ShowBattleHud()
    {
        if (hudPanel == null)
            hudPanel = CreatePanel("BattleHud", new Vector2(0, 0), new Vector2(1280, 720), new Color(0f, 0f, 0f, 0f));

        if (lobbyPanel != null) lobbyPanel.SetActive(false);
        if (resultPanel != null) resultPanel.SetActive(false);
        hudPanel.SetActive(true);

        CreateText(hudPanel.transform, "Health: 100", new Vector2(-520, 280), 22, Color.white, false);
        CreateText(hudPanel.transform, "Armor: 50", new Vector2(-350, 280), 22, Color.white, false);
        CreateText(hudPanel.transform, "Ammo: 30 / 120", new Vector2(200, 280), 22, Color.white, false);
        CreateText(hudPanel.transform, "Zone: 60m", new Vector2(430, 280), 22, Color.white, false);
    }

    public void ShowResultPanel(string message)
    {
        if (resultPanel == null)
            resultPanel = CreatePanel("ResultPanel", new Vector2(0, 0), new Vector2(470, 260), new Color(0.09f, 0.09f, 0.11f, 0.9f));

        resultPanel.SetActive(true);
        if (hudPanel != null) hudPanel.SetActive(false);

        CreateText(resultPanel.transform, message, new Vector2(0, 30), 28, Color.white, true);
        CreateButton(resultPanel.transform, "Restart", new Vector2(0, -50), 180, 60, () => GameManager.Instance.BeginRound());
    }

    private GameObject CreatePanel(string name, Vector2 pos, Vector2 size, Color color)
    {
        var panel = new GameObject(name);
        panel.transform.SetParent(canvas.transform, false);

        var rect = panel.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = pos;

        var image = panel.AddComponent<Image>();
        image.color = color;

        return panel;
    }

    private void CreateText(Transform parent, string text, Vector2 pos, int size, Color color, bool centered)
    {
        var textObj = new GameObject(text + "Text");
        textObj.transform.SetParent(parent, false);

        var rect = textObj.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = pos;
        rect.sizeDelta = new Vector2(600f, 80f);

        var uiText = textObj.AddComponent<Text>();
        uiText.text = text;
        uiText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        uiText.fontSize = size;
        uiText.color = color;
        uiText.alignment = centered ? TextAnchor.MiddleCenter : TextAnchor.MiddleLeft;
    }

    private void CreateButton(Transform parent, string label, Vector2 pos, float width, float height, UnityEngine.Events.UnityAction action)
    {
        var buttonObj = new GameObject(label + "Button");
        buttonObj.transform.SetParent(parent, false);

        var rect = buttonObj.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(width, height);
        rect.anchoredPosition = pos;

        var image = buttonObj.AddComponent<Image>();
        image.color = new Color(0.22f, 0.48f, 1f, 0.95f);

        var button = buttonObj.AddComponent<Button>();
        button.onClick.AddListener(action);

        var labelObj = new GameObject("Label");
        labelObj.transform.SetParent(buttonObj.transform, false);

        var labelRect = labelObj.AddComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        var labelText = labelObj.AddComponent<Text>();
        labelText.text = label;
        labelText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        labelText.fontSize = 22;
        labelText.alignment = TextAnchor.MiddleCenter;
        labelText.color = Color.white;
    }
}
