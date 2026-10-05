using UnityEngine;

/// <summary>
/// The maps the game ships with (baked by Tools/build_map.py into Resources/Map/&lt;id&gt;/), the one the
/// player picked in the lobby (saved) and the one the world is built from right now.
/// </summary>
public static class MapCatalog
{
    public class Info
    {
        public string id;
        public string name;          // EKŞİOĞLU
        public string place;         // Çekmeköy, İstanbul
        public string blurb;         // one line for the selection card
        public string signTop;       // big line on the landmark's sign
        public string signBottom;    // small line under it
        public Color32 signColor;
        public bool signCross;       // the clinic's white medical cross
        public bool sea;             // an island in the sea (otherwise land, or a lake, runs on past the edge)
        public Vector2 arena;        // 5v5: centre of the small arena (x, z)
        public float arenaRadius = 80f;
        public string arenaName;
    }

    public static readonly Info[] All =
    {
        new Info
        {
            id = "eksioglu", name = "EKŞİOĞLU", place = "Çekmeköy, İstanbul",
            blurb = "Kliniğin mahallesi: apartmanlar, dar sokaklar, denizle çevrili ada.",
            signTop = "ZOOTOPIA", signBottom = "VETERİNER KLİNİĞİ 7/24",
            signColor = new Color32(22, 110, 60, 255), signCross = true, sea = true,
            arena = new Vector2(100f, -120f), arenaRadius = 78f, arenaName = "Apartmanlar"
        },
        new Info
        {
            id = "senir", name = "SENİR KASABASI", place = "Keçiborlu, Isparta",
            blurb = "Burdur Gölü kıyısından arkadaki dağa kadar bütün kasaba: bahçeli evler, tarlalar, ormanlık yamaç.",
            signTop = "SENİR", signBottom = "KASABAMIZA HOŞ GELDİNİZ",
            signColor = new Color32(24, 78, 140, 255), signCross = false,
            arena = new Vector2(-250f, -60f), arenaRadius = 80f, arenaName = "Kasaba sokakları"
        },
        new Info
        {
            id = "firat", name = "FIRAT ÜNİVERSİTESİ", place = "Rektörlük Kampüsü, Elazığ",
            blurb = "Rektörlük, fakülteler, yurtlar ve kampüs yolları; çevresi tepelerle kapalı.",
            signTop = "REKTÖRLÜK", signBottom = "FIRAT ÜNİVERSİTESİ",
            signColor = new Color32(118, 28, 44, 255), signCross = false,
            arena = new Vector2(-110f, 140f), arenaRadius = 80f, arenaName = "Fakülteler"
        },
    };

    public const string DefaultId = "eksioglu";
    private const string PrefKey = "zm_map";
    private static string current;

    public static bool IsValid(string id)
    {
        return Get(id) != null;
    }

    public static Info Get(string id)
    {
        if (string.IsNullOrEmpty(id))
            return null;
        foreach (var m in All)
            if (m.id == id)
                return m;
        return null;
    }

    /// <summary>The map the player picked in the lobby (used for bot matches and matchmaking).</summary>
    public static string Selected
    {
        get
        {
            string id = PlayerPrefs.GetString(PrefKey, DefaultId);
            return IsValid(id) ? id : DefaultId;
        }
    }

    public static void Select(string id)
    {
        if (!IsValid(id))
            return;
        PlayerPrefs.SetString(PrefKey, id);
        PlayerPrefs.Save();
    }

    /// <summary>The map the world is built from (the server's -map argument, otherwise the player's pick).</summary>
    public static string Current
    {
        get { return current ?? Selected; }
        set { current = IsValid(value) ? value : DefaultId; }
    }

    public static Info CurrentInfo { get { return Get(Current) ?? All[0]; } }

    public static int IndexOf(string id)
    {
        for (int i = 0; i < All.Length; i++)
            if (All[i].id == id)
                return i;
        return 0;
    }

    /// <summary>Turkish capitals whatever the phone's language ("Keçiborlu" → "KEÇİBORLU", "Elazığ" → "ELAZIĞ").</summary>
    public static string TrUpper(string s)
    {
        var sb = new System.Text.StringBuilder(s.Length);
        foreach (char c in s)
            sb.Append(c == 'i' ? 'İ' : c == 'ı' ? 'I' : char.ToUpperInvariant(c));
        return sb.ToString();
    }

    /// <summary>The selection screen's picture of a map (Resources/Map/&lt;id&gt;/preview.png), or null.</summary>
    public static Texture2D Preview(string id)
    {
        return Resources.Load<Texture2D>("Map/" + id + "/preview");
    }
}
