using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// "GÜNÜN EN İYİ OYUNCULARI": today's five players with the most eliminations (from the server, online matches and
/// matches against bots), shown on the right while a match is being prepared or players are searched for.
/// The list is cached and refreshed at most once a minute.
/// </summary>
public class TopPlayersPanel : MonoBehaviour
{
    private static OnlineService.TopView cache;
    private static float cacheTime = -1000f;
    private static bool loading, failed;
    private static int revision;

    private readonly Image[] rows = new Image[5];
    private readonly Text[] names = new Text[5], kills = new Text[5], wins = new Text[5], initials = new Text[5];
    private Text emptyText, mineText;
    private int shownRevision = -1;

    private static readonly Color[] RowColors =
    {
        new Color(0.98f, 0.72f, 0.12f, 0.96f), new Color(0.62f, 0.7f, 0.86f, 0.96f), new Color(0.93f, 0.5f, 0.16f, 0.96f),
        new Color(0.13f, 0.26f, 0.66f, 0.94f), new Color(0.13f, 0.26f, 0.66f, 0.94f)
    };
    private static readonly Color[] AvatarColors =
    {
        new Color(0.85f, 0.25f, 0.25f), new Color(0.2f, 0.6f, 0.35f), new Color(0.25f, 0.45f, 0.9f), new Color(0.6f, 0.3f, 0.8f),
        new Color(0.9f, 0.55f, 0.15f), new Color(0.15f, 0.65f, 0.7f)
    };

    /// <summary>Fetches the list in the background if it is older than a minute.</summary>
    public static void Refresh()
    {
        if (loading || Time.realtimeSinceStartup - cacheTime < 60f || NetGame.IsServer)
            return;
        loading = true;
        OnlineService.TopToday(v =>
        {
            loading = false;
            cacheTime = Time.realtimeSinceStartup;
            failed = v == null || !v.ok;
            if (!failed)
                cache = v;
            revision++;
        });
    }

    /// <summary>After a match: the next look fetches the new standings.</summary>
    public static void Invalidate() { cacheTime = -1000f; }

    /// <summary>Builds the panel (anchored to the right edge of <paramref name="parent"/>).</summary>
    public static TopPlayersPanel Create(Transform parent, Vector2 position)
    {
        var rect = UIUtil.CreateRect(parent, "TopPlayers", new Vector2(1f, 0.5f), position, new Vector2(460f, 640f));
        var panel = rect.gameObject.AddComponent<TopPlayersPanel>();
        panel.Build(rect);
        return panel;
    }

    private void Build(RectTransform rect)
    {
        var title = UIUtil.CreateText(rect, "GÜNÜN EN İYİ OYUNCULARI", new Vector2(0.5f, 1f), new Vector2(0f, -28f), new Vector2(460f, 50f), 30, TextAnchor.MiddleCenter);
        title.fontStyle = FontStyle.Bold;
        for (int i = 0; i < rows.Length; i++)
        {
            float y = -100f - i * 104f;
            var row = UIUtil.CreateImage(rect, "Row" + i, new Vector2(0.5f, 1f), new Vector2(10f, y), new Vector2(430f, 94f), RowColors[i], false);
            row.raycastTarget = false;
            rows[i] = row;
            var rt = row.transform;
            // rank medal
            var medal = UIUtil.CreateImage(rt, "Medal", new Vector2(0f, 0.5f), new Vector2(-6f, 14f), new Vector2(52f, 52f), i < 3 ? Color.Lerp(RowColors[i], Color.white, 0.25f) : new Color(0.3f, 0.45f, 0.85f), true);
            medal.raycastTarget = false;
            var num = UIUtil.CreateText(medal.transform, (i + 1).ToString(), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(52f, 52f), 28, TextAnchor.MiddleCenter);
            num.fontStyle = FontStyle.Bold;
            // avatar: the name's first letter on a colour
            var av = UIUtil.CreateImage(rt, "Avatar", new Vector2(0f, 0.5f), new Vector2(70f, 0f), new Vector2(70f, 70f), new Color(0.5f, 0.5f, 0.5f), false);
            av.raycastTarget = false;
            initials[i] = UIUtil.CreateText(av.transform, "", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(70f, 70f), 40, TextAnchor.MiddleCenter);
            initials[i].fontStyle = FontStyle.Bold;
            names[i] = UIUtil.CreateText(rt, "", new Vector2(0f, 0.5f), new Vector2(250f, 20f), new Vector2(300f, 40f), 28, TextAnchor.MiddleLeft);
            names[i].fontStyle = FontStyle.Bold;
            var kl = UIUtil.CreateText(rt, "ELEME", new Vector2(0f, 0.5f), new Vector2(148f, -22f), new Vector2(80f, 30f), 18, TextAnchor.MiddleLeft);
            kl.color = new Color(1f, 1f, 1f, 0.75f);
            kills[i] = UIUtil.CreateText(rt, "", new Vector2(0f, 0.5f), new Vector2(240f, -22f), new Vector2(100f, 36f), 26, TextAnchor.MiddleLeft);
            kills[i].fontStyle = FontStyle.Bold;
            Icons.Create(rt, "trophy", new Vector2(0f, 0.5f), new Vector2(312f, -22f), new Vector2(30f, 30f)).raycastTarget = false;
            wins[i] = UIUtil.CreateText(rt, "", new Vector2(0f, 0.5f), new Vector2(378f, -22f), new Vector2(90f, 36f), 24, TextAnchor.MiddleLeft);
            wins[i].fontStyle = FontStyle.Bold;
        }
        emptyText = UIUtil.CreateText(rect, "", new Vector2(0.5f, 1f), new Vector2(0f, -300f), new Vector2(430f, 200f), 26, TextAnchor.MiddleCenter);
        emptyText.horizontalOverflow = HorizontalWrapMode.Wrap;
        mineText = UIUtil.CreateText(rect, "", new Vector2(0.5f, 0f), new Vector2(0f, 14f), new Vector2(440f, 34f), 22, TextAnchor.MiddleCenter);
        mineText.color = Theme.Accent;
        mineText.fontStyle = FontStyle.Bold;
        Show();
    }

    private void OnEnable()
    {
        Refresh();
        shownRevision = -1;
    }

    private void Update()
    {
        if (shownRevision != revision)
            Show();
    }

    private void Show()
    {
        shownRevision = revision;
        var list = cache != null ? cache.top : null;
        int n = list != null ? Mathf.Min(list.Length, rows.Length) : 0;
        for (int i = 0; i < rows.Length; i++)
        {
            bool on = i < n;
            rows[i].gameObject.SetActive(on);
            if (!on)
                continue;
            var e = list[i];
            string nm = string.IsNullOrEmpty(e.name) ? "Oyuncu" : e.name;
            names[i].text = nm;
            initials[i].text = nm.Substring(0, 1).ToUpper();
            initials[i].transform.parent.GetComponent<Image>().color = AvatarColors[Mathf.Abs(e.id.GetHashCode()) % AvatarColors.Length];
            kills[i].text = e.kills.ToString("N0");
            wins[i].text = e.wins.ToString("N0");
        }
        if (n == 0)
            emptyText.text = cache == null && failed ? "Sıralama şu an alınamadı.\nİnternet bağlantını kontrol et." :
                             loading || cache == null ? "Sıralama yükleniyor..." : "Bugün henüz sıralama yok.\nMaçı bitir, listeye ilk sen gir!";
        else
            emptyText.text = "";
        if (cache != null && cache.myRank > 0)
            mineText.text = "Sen: " + cache.myRank + ". sıra  •  " + cache.myKills + " eleme";
        else
            mineText.text = "";
    }
}
