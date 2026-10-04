using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Lobby → HARİTA: the three maps side by side (picture from above, name, place, a line about it).
/// Picking another map rebuilds the world for it (GameBootstrap.SwitchMap) and comes back to the lobby;
/// bot matches, quick matches and new rooms are then played there.
/// </summary>
public class MapSelectScreen : MonoBehaviour
{
    private System.Action onClose;
    private readonly List<Image> frames = new List<Image>();
    private readonly List<GameObject> badges = new List<GameObject>();
    private readonly List<Text> buttonLabels = new List<Text>();
    private GameObject busy;
    private Text busyText;

    public static MapSelectScreen Create(Transform canvas)
    {
        var rect = UIUtil.CreateStretch(canvas, "MapSelect");
        var img = rect.gameObject.AddComponent<Image>();
        img.color = new Color(0.03f, 0.05f, 0.08f, 0.96f);
        var s = rect.gameObject.AddComponent<MapSelectScreen>();
        s.Build();
        rect.gameObject.AddComponent<PopIn>();
        rect.gameObject.SetActive(false);
        return s;
    }

    private void Build()
    {
        var t = transform;
        Text label;
        var back = UIUtil.CreateButton(t, "<", new Vector2(0f, 1f), new Vector2(80f, -70f), new Vector2(84f, 84f), Theme.Accent, false, 54, out label);
        label.color = new Color(0.1f, 0.1f, 0.1f);
        label.GetComponent<Shadow>().enabled = false;
        back.onClick.AddListener(Close);
        var title = UIUtil.CreateText(t, "HARİTA SEÇ", new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(1200f, 80f), 50, TextAnchor.MiddleCenter);
        title.fontStyle = FontStyle.Bold;
        title.color = Theme.Accent;
        var hint = UIUtil.CreateText(t, "Botlu maçlar, hızlı maç ve kurduğun özel odalar seçtiğin haritada oynanır.",
            new Vector2(0.5f, 1f), new Vector2(0f, -130f), new Vector2(1600f, 40f), 26, TextAnchor.MiddleCenter);
        hint.color = Theme.TextDim;

        var maps = MapCatalog.All;
        const float cardW = 540f, cardH = 760f, gap = 44f;
        float x0 = -(maps.Length - 1) * (cardW + gap) * 0.5f;
        for (int i = 0; i < maps.Length; i++)
        {
            var m = maps[i];
            string id = m.id;
            var card = UIUtil.CreateButton(t, "", new Vector2(0.5f, 0.5f), new Vector2(x0 + i * (cardW + gap), -40f), new Vector2(cardW, cardH), Theme.Panel, false, 20, out label);
            card.onClick.AddListener(() => Pick(id));
            var ct = card.transform;
            var frame = card.gameObject.AddComponent<Outline>();
            frame.effectDistance = new Vector2(4f, -4f);
            frames.Add(card.GetComponent<Image>());

            var pic = UIUtil.CreateRect(ct, "Picture", new Vector2(0.5f, 1f), new Vector2(0f, -258f), new Vector2(cardW - 40f, cardW - 40f));
            var raw = pic.gameObject.AddComponent<RawImage>();
            raw.texture = MapCatalog.Preview(id);
            raw.color = raw.texture != null ? Color.white : new Color(0.2f, 0.3f, 0.25f);
            raw.raycastTarget = false;

            var name = UIUtil.CreateText(ct, m.name, new Vector2(0.5f, 0f), new Vector2(0f, 212f), new Vector2(cardW - 30f, 56f), 40, TextAnchor.MiddleCenter);
            name.fontStyle = FontStyle.Bold;
            var place = UIUtil.CreateText(ct, m.place, new Vector2(0.5f, 0f), new Vector2(0f, 168f), new Vector2(cardW - 30f, 36f), 26, TextAnchor.MiddleCenter);
            place.color = Theme.Accent;
            var blurb = UIUtil.CreateText(ct, m.blurb, new Vector2(0.5f, 0f), new Vector2(0f, 107f), new Vector2(cardW - 50f, 76f), 22, TextAnchor.UpperCenter);
            blurb.horizontalOverflow = HorizontalWrapMode.Wrap;
            blurb.color = Theme.TextDim;

            Text bl;
            var pick = UIUtil.CreateButton(ct, "SEÇ", new Vector2(0.5f, 0f), new Vector2(0f, 38f), new Vector2(cardW - 60f, 58f), Theme.Accent, false, 28, out bl);
            pick.onClick.AddListener(() => Pick(id));
            bl.color = new Color(0.1f, 0.08f, 0.02f);
            bl.GetComponent<Shadow>().enabled = false;
            buttonLabels.Add(bl);

            var badge = UIUtil.CreateImage(ct, "Badge", new Vector2(1f, 1f), new Vector2(-92f, -44f), new Vector2(150f, 46f), Theme.Good, false);
            badge.raycastTarget = false;
            var bt = UIUtil.CreateText(badge.transform, "SEÇİLİ", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(150f, 46f), 24, TextAnchor.MiddleCenter);
            bt.fontStyle = FontStyle.Bold;
            bt.color = new Color(0.05f, 0.15f, 0.05f);
            bt.GetComponent<Shadow>().enabled = false;
            badges.Add(badge.gameObject);
        }

        var credit = UIUtil.CreateText(t, "Haritalar: gerçek yükseklik verisi ve © OpenStreetMap katkıcıları (ODbL)", new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(1600f, 34f), 20, TextAnchor.MiddleCenter);
        credit.color = new Color(1f, 1f, 1f, 0.45f);

        var b = UIUtil.CreateStretch(t, "Busy");
        b.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.8f);
        busyText = UIUtil.CreateText(b, "", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1400f, 80f), 44, TextAnchor.MiddleCenter);
        busyText.fontStyle = FontStyle.Bold;
        busy = b.gameObject;
        busy.SetActive(false);
    }

    public void Open(System.Action closed)
    {
        onClose = closed;
        busy.SetActive(false);
        string current = MapCatalog.Current;
        for (int i = 0; i < MapCatalog.All.Length; i++)
        {
            bool sel = MapCatalog.All[i].id == current;
            badges[i].SetActive(sel);
            buttonLabels[i].text = sel ? "BU HARİTADA OYNA" : "SEÇ";
            var outline = frames[i].GetComponent<Outline>();
            outline.effectColor = sel ? Theme.Accent : new Color(1f, 1f, 1f, 0.12f);
        }
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
    }

    private void Pick(string id)
    {
        if (busy.activeSelf)
            return;
        if (id == MapCatalog.Current)
        {
            UiSound.Confirm();
            Close();
            return;
        }
        if (NetClient.Instance != null)
        {
            GameManager.Instance.uiManager.Toast("Önce çevrimiçi odadan çık");
            return;
        }
        UiSound.Confirm();
        busyText.text = MapCatalog.Get(id).name + " yükleniyor...";
        busy.SetActive(true);
        StartCoroutine(SwitchSoon(id));
    }

    private System.Collections.IEnumerator SwitchSoon(string id)
    {
        yield return null;   // let the "yükleniyor" text show before the frame the scene reloads
        yield return null;
        GameBootstrap.SwitchMap(id);
    }

    private void Close()
    {
        gameObject.SetActive(false);
        if (onClose != null)
            onClose();
    }
}
