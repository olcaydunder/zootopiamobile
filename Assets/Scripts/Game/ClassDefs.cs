using UnityEngine;

public enum PlayerClass
{
    Medic = 0,      // Sahra Hekimi
    K9 = 1,         // K9 Eğitmeni
    Teleport = 2,   // Işınlayıcı
    Scout = 3,      // Gözcü
    Shield = 4,     // Kalkan Ustası
    Engineer = 5,   // Mühendis
    Airborne = 6,   // Paraşütçü
    Shadow = 7      // Gölge
}

/// <summary>One battle royale class: an active ability on the SINIF button and an always-on passive.</summary>
public class ClassDef
{
    public PlayerClass type;
    public string name;
    public string icon;
    public string ability;
    public string abilityInfo;
    public string passive;
    public string level2;
    public float cooldown;   // seconds
    public Color color;
}

/// <summary>The class catalogue and the player's chosen class (Teçhizat).</summary>
public static class ClassDefs
{
    public static readonly ClassDef[] All =
    {
        new ClassDef { type = PlayerClass.Medic, name = "Sahra Hekimi", icon = "class_medic", ability = "Şifa Alanı",
            abilityInfo = "6 m çapta bir şifa alanı kurar: 8 sn boyunca sen ve takımın saniyede 8 can kazanır.",
            passive = "İlk yardım %30 fazla can verir, enerji içeceği 2 kat hızlı işler, yerden %50 hızlı kaldırılırsın.", level2 = "12 sn sürer ve zırhı da onarır.",
            cooldown = 60f, color = new Color(0.25f, 0.8f, 0.45f) },
        new ClassDef { type = PlayerClass.K9, name = "K9 Eğitmeni", icon = "class_k9", ability = "Köpeği Salıver",
            abilityInfo = "Köpek, baktığın yönde 40 m içindeki en yakın düşmana koşar, 6 sn haritada işaretler ve ısırır (20 hasar).",
            passive = "25 m içindeki düşman ayak seslerini yön oku olarak görürsün.", level2 = "İki köpek, 10 sn işaret.",
            cooldown = 45f, color = new Color(0.85f, 0.55f, 0.25f) },
        new ClassDef { type = PlayerClass.Teleport, name = "Işınlayıcı", icon = "class_teleport", ability = "Faz Kayması",
            abilityInfo = "Baktığın yöne 12 m anında ışınlanırsın (duvarın içine değil, önündeki boşluğa).",
            passive = "Işınlandıktan sonra 3 sn %20 daha hızlı koşarsın.", level2 = "İki şarj.",
            cooldown = 30f, color = new Color(0.2f, 0.8f, 0.9f) },
        new ClassDef { type = PlayerClass.Scout, name = "Gözcü", icon = "class_scout", ability = "Kartal Gözü",
            abilityInfo = "Bir kartal havalanır: 60 m içindeki düşmanlar 8 sn haritada görünür.",
            passive = "İşaretli düşmanlara %10 fazla hasar.", level2 = "90 m çap.",
            cooldown = 50f, color = new Color(0.95f, 0.78f, 0.2f) },
        new ClassDef { type = PlayerClass.Shield, name = "Kalkan Ustası", icon = "class_shield", ability = "Mobil Siper",
            abilityInfo = "Önüne 2,5 m genişliğinde kurşun geçirmez bir kalkan duvarı kurar (400 can, 15 sn).",
            passive = "Patlayıcı hasarı %25 daha az.", level2 = "700 canlı kalkan.",
            cooldown = 45f, color = new Color(0.3f, 0.5f, 0.95f) },
        new ClassDef { type = PlayerClass.Engineer, name = "Mühendis", icon = "class_engineer", ability = "Taret",
            abilityInfo = "15 sn boyunca 30 m içindeki düşmanlara ateş eden otomatik bir taret kurar.",
            passive = "Araç kullanırken aldığın hasar %30 daha az.", level2 = "25 sn çalışır.",
            cooldown = 75f, color = new Color(0.95f, 0.5f, 0.2f) },
        new ClassDef { type = PlayerClass.Airborne, name = "Paraşütçü", icon = "class_airborne", ability = "Fırlatıcı",
            abilityInfo = "Seni 35 m havaya fırlatır, paraşütle süzülerek yer değiştirirsin.",
            passive = "Serbest düşüş %15 daha hızlı.", level2 = "50 m yükseklik.",
            cooldown = 60f, color = new Color(0.45f, 0.75f, 1f) },
        new ClassDef { type = PlayerClass.Shadow, name = "Gölge", icon = "class_shadow", ability = "Sis Perdesi",
            abilityInfo = "6 sn neredeyse görünmez olursun; ateş edersen bozulur.",
            passive = "Adımların sessizdir.", level2 = "9 sn sürer.",
            cooldown = 55f, color = new Color(0.6f, 0.4f, 0.9f) },
    };

    public static ClassDef Get(PlayerClass c) { return All[Mathf.Clamp((int)c, 0, All.Length - 1)]; }

    /// <summary>The class the player takes into matches.</summary>
    public static PlayerClass Selected
    {
        get { return (PlayerClass)Mathf.Clamp(PlayerPrefs.GetInt("zm_class", 0), 0, All.Length - 1); }
        set { PlayerPrefs.SetInt("zm_class", (int)value); PlayerPrefs.Save(); }
    }
}
