using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Sending a gift (paid from your Kredi, delivered by the server to the friend's HEDİYELER inbox):
/// from a store item ("who gets it?": friends and players you follow each other with), or from a
/// friend ("what do you send?": Kredi, a gift box, or a character / camo picked in the store).
/// </summary>
public class GiftPanel : MonoBehaviour
{
    private static readonly int[] CreditAmounts = { 100, 250, 500, 1000 };

    private Text title, info, status;
    private RectTransform list;
    private System.Action refreshed;
    private string kind = "", item = "", what = "", personId = "", personName = "";
    private int price;

    private static GiftPanel Make(string heading)
    {
        var go = new GameObject("GiftPanel");
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 350;
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 1f;
        go.AddComponent<GraphicRaycaster>();
        var p = go.AddComponent<GiftPanel>();
        p.Build(go.transform, heading);
        return p;
    }

    private void Build(Transform t, string heading)
    {
        var c = new Vector2(0.5f, 0.5f);
        var dim = UIUtil.CreateImage(t, "Dim", c, Vector2.zero, new Vector2(5000f, 3000f), new Color(0f, 0f, 0f, 0.75f), false);
        dim.gameObject.AddComponent<Button>().onClick.AddListener(Close);
        var box = UIUtil.CreateImage(t, "Box", c, Vector2.zero, new Vector2(1180f, 860f), new Color(0.07f, 0.1f, 0.15f, 0.99f), false).transform;
        Icons.Create(box, "gift", new Vector2(0f, 1f), new Vector2(80f, -70f), new Vector2(96f, 96f));
        title = UIUtil.CreateText(box, heading, new Vector2(0.5f, 1f), new Vector2(40f, -60f), new Vector2(980f, 64f), 42, TextAnchor.MiddleLeft);
        title.fontStyle = FontStyle.Bold;
        info = UIUtil.CreateText(box, "", new Vector2(0.5f, 1f), new Vector2(40f, -112f), new Vector2(980f, 40f), 26, TextAnchor.MiddleLeft);
        info.color = Theme.TextDim;
        Text label;
        var close = UIUtil.CreateButton(box, "X", new Vector2(1f, 1f), new Vector2(-60f, -60f), new Vector2(76f, 76f), Theme.PanelLight, false, 36, out label);
        close.onClick.AddListener(Close);
        list = UIUtil.CreateRect(box, "List", new Vector2(0.5f, 1f), new Vector2(0f, -170f), new Vector2(1100f, 10f));
        list.pivot = new Vector2(0.5f, 1f);
        status = UIUtil.CreateText(box, "", new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(1100f, 44f), 26, TextAnchor.MiddleCenter);
        status.color = Theme.Accent;
    }

    private void Close()
    {
        Destroy(gameObject);
        if (refreshed != null)
            refreshed();
    }

    private void Clear()
    {
        for (int i = list.childCount - 1; i >= 0; i--)
        {
            var ch = list.GetChild(i);
            ch.SetParent(null, false);   // gone from the list at once (Destroy waits for the frame's end)
            Destroy(ch.gameObject);
        }
    }

    private static ProfileData Profile { get { return GameManager.Instance.profile; } }

    // ----- From an item: pick who gets it -----

    public static void ForItem(string kind, string item, string what, int price, System.Action refreshed)
    {
        var p = Make("HEDİYE ET: " + what);
        p.kind = kind;
        p.item = item;
        p.what = what;
        p.price = price;
        p.refreshed = refreshed;
        p.info.text = price.ToString("N0") + " Kredi senden düşer  •  sende " + Profile.coins.ToString("N0") +
                      "  •  arkadaşların ve karşılıklı takipleştiklerin";
        p.ShowPeople();
    }

    private void ShowPeople()
    {
        Clear();
        Row("Liste yükleniyor...", null, null);
        var people = new List<KeyValuePair<string, string>>();
        var seen = new HashSet<string>();
        var social = OnlineService.Social;
        if (social != null)
            foreach (var f in social.friends)
                if (seen.Add(f.id))
                    people.Add(new KeyValuePair<string, string>(f.id, f.name));
        OnlineService.Follows(view =>
        {
            if (this == null)
                return;
            if (view.ok)
                foreach (var f in view.following)
                    if (f.followsMe && seen.Add(f.id))
                        people.Add(new KeyValuePair<string, string>(f.id, f.name));
            Clear();
            if (people.Count == 0)
                Row("Henüz hediye gönderebileceğin kimse yok. Arkadaş ekle ya da karşılıklı takipleş.", null, null);
            int shown = 0;
            foreach (var kv in people)
            {
                if (shown++ >= 6)
                    break;
                var person = kv;
                Row(person.Value, "GÖNDER", () => Send(person.Key, person.Value, kind, item, 0, what, msg =>
                {
                    if (this != null)
                        status.text = msg;
                }));
            }
            if (people.Count > 6)
                Row("+" + (people.Count - 6) + " kişi daha: ARKADAŞLAR ekranından kişiye dokunarak da gönderebilirsin", null, null);
        });
    }

    // ----- From a friend: pick what to send -----

    public static void ForPerson(string id, string name, System.Action refreshed)
    {
        var p = Make("HEDİYE: " + name);
        p.personId = id;
        p.personName = name;
        p.refreshed = refreshed;
        p.info.text = "Sende " + Profile.coins.ToString("N0") + " Kredi  •  hediyeler bedeli senin Kredinden düşer";
        p.ShowChoices();
    }

    private void ShowChoices()
    {
        Clear();
        var c = new Vector2(0.5f, 1f);
        Text label;
        var h1 = UIUtil.CreateText(list, "KREDİ", c, new Vector2(-470f, -30f), new Vector2(200f, 40f), 28, TextAnchor.MiddleLeft);
        h1.color = Theme.TextDim;
        for (int i = 0; i < CreditAmounts.Length; i++)
        {
            int amount = CreditAmounts[i];
            var b = UIUtil.CreateButton(list, amount.ToString("N0"), c, new Vector2(-300f + i * 220f, -90f), new Vector2(200f, 84f), Theme.Panel, false, 32, out label);
            Icons.Create(b.transform, "currency", new Vector2(0f, 0.5f), new Vector2(34f, 0f), new Vector2(44f, 44f));
            b.onClick.AddListener(() => Send(personId, personName, "credits", "", amount, amount.ToString("N0") + " Kredi", msg => status.text = msg));
        }
        var h2 = UIUtil.CreateText(list, "HEDİYE KUTUSU", c, new Vector2(-420f, -170f), new Vector2(300f, 40f), 28, TextAnchor.MiddleLeft);
        h2.color = Theme.TextDim;
        for (int i = 0; i < Shop.Crates.Length; i++)
        {
            var cr = Shop.Crates[i];
            var b = UIUtil.CreateButton(list, "", c, new Vector2((i - (Shop.Crates.Length - 1) * 0.5f) * 214f, -300f), new Vector2(204f, 220f), Theme.Panel, false, 20, out label);
            Icons.Create(b.transform, "crate_" + cr.id, new Vector2(0.5f, 1f), new Vector2(0f, -62f), new Vector2(110f, 110f));
            var n = UIUtil.CreateText(b.transform, cr.name, new Vector2(0.5f, 0f), new Vector2(0f, 72f), new Vector2(196f, 60f), 20, TextAnchor.MiddleCenter);
            n.fontStyle = FontStyle.Bold;
            n.horizontalOverflow = HorizontalWrapMode.Wrap;
            var pr = UIUtil.CreateText(b.transform, cr.price.ToString("N0") + " Kredi", new Vector2(0.5f, 0f), new Vector2(0f, 26f), new Vector2(196f, 30f), 20, TextAnchor.MiddleCenter);
            pr.color = new Color(1f, 0.85f, 0.3f);
            string id = cr.id, nm = cr.name;
            b.onClick.AddListener(() => Send(personId, personName, "box", id, 0, nm, msg => status.text = msg));
        }
        var more = UIUtil.CreateButton(list, "KARAKTER, TEÇHİZAT YA DA KAMUFLAJ SEÇ  ›", c, new Vector2(0f, -470f), new Vector2(760f, 90f), new Color(0.16f, 0.45f, 0.95f, 0.95f), false, 30, out label);
        more.onClick.AddListener(() =>
        {
            string id = personId, nm = personName;
            Destroy(gameObject);
            var gm = GameManager.Instance;
            if (gm != null && gm.uiManager != null)
                gm.uiManager.OpenStoreForGift(id, nm);
        });
    }

    // ----- Sending -----

    /// <summary>Checks the Kredi, sends, and takes the Kredi when the server accepted it.</summary>
    public static void Send(string id, string name, string kind, string item, int amount, string what, System.Action<string> result)
    {
        var p = Profile;
        int cost = Shop.GiftCost(kind, item, amount);
        if (p.coins < cost)
        {
            result("Yetersiz Kredi: " + cost.ToString("N0") + " gerekli (sende " + p.coins.ToString("N0") + ")");
            return;
        }
        result("Gönderiliyor...");
        OnlineService.SendGift(id, kind, item, amount, "", view =>
        {
            if (!view.ok)
            {
                result(view.error);
                return;
            }
            p.coins -= cost;
            p.Save();
            UiSound.Confirm();
            result(what + " → " + name + " gönderildi!");
        });
    }

    private void Row(string text, string button, System.Action action)
    {
        float yy = -55f - list.childCount * 100f;
        var bg = UIUtil.CreateImage(list, "Row", new Vector2(0.5f, 1f), new Vector2(0f, yy), new Vector2(1080f, 88f), Theme.Panel, false);
        var t = UIUtil.CreateText(bg.transform, text, new Vector2(0f, 0.5f), new Vector2(380f, 0f), new Vector2(720f, 80f), 28, TextAnchor.MiddleLeft);
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        if (button != null)
        {
            t.fontStyle = FontStyle.Bold;
            Text label;
            var b = UIUtil.CreateButton(bg.transform, button, new Vector2(1f, 0.5f), new Vector2(-130f, 0f), new Vector2(220f, 70f), Theme.Accent, false, 28, out label);
            label.color = new Color(0.1f, 0.08f, 0.02f);
            label.GetComponent<Shadow>().enabled = false;
            b.onClick.AddListener(() => action());
        }
    }
}
