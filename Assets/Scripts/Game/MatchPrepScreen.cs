using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Which primary weapon (and model) the player starts each match with.</summary>
public static class Loadout
{
    public static readonly WeaponType[] Primaries = { WeaponType.Rifle, WeaponType.SMG, WeaponType.Shotgun, WeaponType.Sniper };
    public static readonly string[] PrimaryNames = { "TAARRUZ TÜFEĞİ", "HAFİF MAKİNELİ", "POMPALI", "KESKİN NİŞANCI" };

    /// <summary>Index into Primaries.</summary>
    public static int Primary
    {
        get { return Mathf.Clamp(PlayerPrefs.GetInt("zm_primary", 0), 0, Primaries.Length - 1); }
        set { PlayerPrefs.SetInt("zm_primary", value); PlayerPrefs.Save(); }
    }

    public static WeaponType PrimaryType { get { return Primaries[Primary]; } }

    public static WeaponData PrimaryWeapon()
    {
        return Gunsmith.Apply(Gunsmith.BaseWeapon(PrimaryType));
    }
}

/// <summary>
/// "HAZIRLIK" screen between BAŞLAT and the match: pick a character (the 3D character in the
/// lobby changes at once), the primary weapon you drop in with, and the model of each gun.
/// The middle stays clear so the character and the clinic behind show through.
/// </summary>
public class MatchPrepScreen : MonoBehaviour
{
    private MatchMode mode;
    private readonly List<Image> charButtons = new List<Image>();
    private readonly List<Text> charNames = new List<Text>();
    private readonly List<Image> weaponButtons = new List<Image>();
    private readonly List<Text> weaponNames = new List<Text>();
    private RectTransform primarySkinRow, pistolSkinRow;
    private Text charTitle, charRole, weaponTitle, modeText;
    private ScrollRect charScroll;
    private readonly Image[] statFill = new Image[6];
    private readonly Text[] statValue = new Text[6];

    public static MatchPrepScreen Create(Transform canvas)
    {
        var rect = UIUtil.CreateStretch(canvas, "MatchPrep");
        var s = rect.gameObject.AddComponent<MatchPrepScreen>();
        s.Build();
        rect.gameObject.SetActive(false);
        return s;
    }

    private void Build()
    {
        var t = transform;
        Text unused;

        // Side shades so the panels read well over the 3D scene.
        var shadeL = UIUtil.CreateImage(t, "ShadeL", new Vector2(0f, 0.5f), new Vector2(330f, 0f), new Vector2(660f, 2400f), new Color(0f, 0f, 0f, 0.45f), false);
        shadeL.raycastTarget = false;
        var shadeR = UIUtil.CreateImage(t, "ShadeR", new Vector2(1f, 0.5f), new Vector2(-330f, 0f), new Vector2(660f, 2400f), new Color(0f, 0f, 0f, 0.45f), false);
        shadeR.raycastTarget = false;

        // Header
        var back = UIUtil.CreateButton(t, "<", new Vector2(0f, 1f), new Vector2(80f, -70f), new Vector2(84f, 84f), Theme.Accent, false, 54, out unused);
        unused.color = new Color(0.1f, 0.1f, 0.1f);
        unused.GetComponent<Shadow>().enabled = false;
        back.onClick.AddListener(Back);
        var title = UIUtil.CreateText(t, "HAZIRLIK", new Vector2(0f, 1f), new Vector2(330f, -70f), new Vector2(400f, 80f), 52, TextAnchor.MiddleLeft);
        title.fontStyle = FontStyle.Bold;
        modeText = UIUtil.CreateText(t, "", new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(800f, 60f), 30, TextAnchor.MiddleCenter);
        modeText.color = Theme.Accent;

        // ---- Left: characters
        var left = UIUtil.CreateRect(t, "Characters", new Vector2(0f, 0.5f), new Vector2(330f, -20f), new Vector2(600f, 860f));
        var ct = UIUtil.CreateText(left, "KARAKTER", new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(560f, 50f), 34, TextAnchor.MiddleLeft);
        ct.fontStyle = FontStyle.Bold;
        // Scrolling 2-column grid (there are more characters than fit the panel).
        var charView = UIUtil.CreateRect(left, "CharView", new Vector2(0.5f, 0.5f), new Vector2(0f, 30f), new Vector2(590f, 660f));
        charView.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.01f);   // catches drags between buttons
        charView.gameObject.AddComponent<RectMask2D>();
        int charRows = (ModelLibrary.ShopSkins.Length + 1) / 2;
        var charContent = UIUtil.CreateRect(charView, "Content", new Vector2(0.5f, 1f), Vector2.zero, new Vector2(590f, charRows * 120f));
        charContent.pivot = new Vector2(0.5f, 1f);
        charContent.anchoredPosition = Vector2.zero;
        charScroll = charView.gameObject.AddComponent<ScrollRect>();
        charScroll.content = charContent;
        charScroll.viewport = charView;
        charScroll.horizontal = false;
        charScroll.vertical = true;
        charScroll.movementType = ScrollRect.MovementType.Clamped;
        charScroll.scrollSensitivity = 40f;
        for (int i = 0; i < ModelLibrary.ShopSkins.Length; i++)
        {
            int index = i;
            float x = (i % 2 == 0) ? -145f : 145f;
            float y = -60f - (i / 2) * 120f;
            Text label;
            var b = UIUtil.CreateButton(charContent, "", new Vector2(0.5f, 1f), new Vector2(x, y), new Vector2(280f, 106f), Theme.Panel, false, 24, out label);
            b.onClick.AddListener(() => SelectCharacter(index));
            var accent = UIUtil.CreateImage(b.transform, "Accent", new Vector2(0f, 0.5f), new Vector2(4f, 0f), new Vector2(8f, 106f), Theme.Accent, false);
            accent.raycastTarget = false;
            var n = UIUtil.CreateText(b.transform, ModelLibrary.ShopNames[i], new Vector2(0.5f, 0.5f), new Vector2(0f, 14f), new Vector2(260f, 44f), 28, TextAnchor.MiddleCenter);
            n.fontStyle = FontStyle.Bold;
            var r = UIUtil.CreateText(b.transform, ModelLibrary.ShopRoles[i], new Vector2(0.5f, 0.5f), new Vector2(0f, -24f), new Vector2(260f, 30f), 20, TextAnchor.MiddleCenter);
            r.color = Theme.TextDim;
            charButtons.Add(b.GetComponent<Image>());
            charNames.Add(n);
        }
        charTitle = UIUtil.CreateText(left, "", new Vector2(0.5f, 0f), new Vector2(0f, 90f), new Vector2(560f, 50f), 36, TextAnchor.MiddleLeft);
        charTitle.fontStyle = FontStyle.Bold;
        charTitle.color = Theme.Accent;
        charRole = UIUtil.CreateText(left, "", new Vector2(0.5f, 0f), new Vector2(0f, 48f), new Vector2(560f, 40f), 24, TextAnchor.MiddleLeft);
        charRole.color = Theme.TextDim;

        // ---- Right: weapons
        var right = UIUtil.CreateRect(t, "Weapons", new Vector2(1f, 0.5f), new Vector2(-330f, -20f), new Vector2(600f, 860f));
        var wt = UIUtil.CreateText(right, "BİRİNCİL SİLAH", new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(560f, 50f), 34, TextAnchor.MiddleLeft);
        wt.fontStyle = FontStyle.Bold;
        for (int i = 0; i < Loadout.Primaries.Length; i++)
        {
            int index = i;
            float x = (i % 2 == 0) ? -145f : 145f;
            float y = 320f - (i / 2) * 92f;
            Text label;
            var b = UIUtil.CreateButton(right, Loadout.PrimaryNames[i], new Vector2(0.5f, 0.5f), new Vector2(x, y), new Vector2(280f, 80f), Theme.Panel, false, 24, out label);
            b.onClick.AddListener(() => SelectPrimary(index));
            weaponButtons.Add(b.GetComponent<Image>());
            weaponNames.Add(label);
        }
        weaponTitle = UIUtil.CreateText(right, "", new Vector2(0.5f, 0.5f), new Vector2(0f, 175f), new Vector2(560f, 44f), 30, TextAnchor.MiddleLeft);
        weaponTitle.fontStyle = FontStyle.Bold;
        weaponTitle.color = Theme.Accent;

        // stats
        for (int i = 0; i < 6; i++)
        {
            float y = 120f - i * 40f;
            var n = UIUtil.CreateText(right, Gunsmith.StatNames[i], new Vector2(0.5f, 0.5f), new Vector2(-190f, y), new Vector2(180f, 34f), 20, TextAnchor.MiddleLeft);
            n.color = Theme.TextDim;
            UIUtil.CreateImage(right, "Bar", new Vector2(0.5f, 0.5f), new Vector2(60f, y), new Vector2(300f, 10f), new Color(1f, 1f, 1f, 0.12f), false).raycastTarget = false;
            var fill = UIUtil.CreateImage(right, "Fill", new Vector2(0.5f, 0.5f), new Vector2(-90f, y), new Vector2(0f, 10f), Color.white, false);
            fill.rectTransform.pivot = new Vector2(0f, 0.5f);
            fill.raycastTarget = false;
            statFill[i] = fill;
            statValue[i] = UIUtil.CreateText(right, "", new Vector2(0.5f, 0.5f), new Vector2(250f, y), new Vector2(70f, 34f), 22, TextAnchor.MiddleRight);
        }

        var st = UIUtil.CreateText(right, "SİLAH MODELİ", new Vector2(0.5f, 0.5f), new Vector2(0f, -150f), new Vector2(560f, 40f), 24, TextAnchor.MiddleLeft);
        st.color = Theme.TextDim;
        primarySkinRow = UIUtil.CreateRect(right, "PrimarySkins", new Vector2(0.5f, 0.5f), new Vector2(0f, -200f), new Vector2(560f, 64f));
        var pt = UIUtil.CreateText(right, "TABANCA MODELİ", new Vector2(0.5f, 0.5f), new Vector2(0f, -258f), new Vector2(560f, 40f), 24, TextAnchor.MiddleLeft);
        pt.color = Theme.TextDim;
        pistolSkinRow = UIUtil.CreateRect(right, "PistolSkins", new Vector2(0.5f, 0.5f), new Vector2(0f, -308f), new Vector2(560f, 64f));

        // ---- Bottom: go
        var go = UIUtil.CreateButton(t, "SAVAŞA GİR", new Vector2(0.5f, 0f), new Vector2(0f, 90f), new Vector2(560f, 120f), Theme.Accent, false, 52, out unused);
        unused.color = new Color(0.1f, 0.08f, 0.02f);
        unused.GetComponent<Shadow>().enabled = false;
        go.GetComponent<ButtonFeel>().silent = true;   // plays the confirm sound instead of a click
        go.onClick.AddListener(() =>
        {
            UiSound.Confirm();
            Haptics.Tap(40);
            gameObject.SetActive(false);
            GameManager.Instance.StartMatch(mode);
        });
        var hint = UIUtil.CreateText(t, "Seçimlerin kaydedilir. Aparat ve kamuflaj için Silah Atölyesi'ni kullan.", new Vector2(0.5f, 0f), new Vector2(0f, 175f), new Vector2(1000f, 34f), 22, TextAnchor.MiddleCenter);
        hint.color = Theme.TextDim;
    }

    public void Open(MatchMode matchMode)
    {
        mode = matchMode;
        modeText.text = "BATTLE ROYALE  •  " + matchMode.ToString().ToUpper() + "  •  " + (MapData.Loaded ? MapCatalog.CurrentInfo.name : "ZOOTOPIA ADASI");
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        var p = GameManager.Instance.profile;
        int ci = Mathf.Max(0, System.Array.IndexOf(ModelLibrary.ShopSkins, p.equippedSkin));
        SelectCharacter(ci);
        SelectPrimary(Loadout.Primary);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    private void Back()
    {
        Hide();
        GameManager.Instance.uiManager.ShowLobby();
    }

    private void SelectCharacter(int index)
    {
        var gm = GameManager.Instance;
        string skin = ModelLibrary.ShopSkins[index];
        if (!gm.profile.OwnsSkin(skin))
            gm.profile.BuySkin(skin, 0);
        gm.profile.EquipSkin(skin);
        if (gm.player != null)
        {
            gm.player.ApplySkin(skin);
            gm.player.ShowcaseWeapon(Loadout.PrimaryWeapon());
        }
        for (int i = 0; i < charButtons.Count; i++)
        {
            bool sel = i == index;
            charButtons[i].color = sel ? Theme.Selected : Theme.Panel;
            charNames[i].color = sel ? new Color(0.08f, 0.08f, 0.1f) : Color.white;
            charNames[i].GetComponent<Shadow>().enabled = !sel;
        }
        charTitle.text = ModelLibrary.ShopNames[index].ToUpper();
        ScrollToCharacter(index);
        charRole.text = ModelLibrary.ShopRoles[index];
    }

    /// <summary>Scrolls the character grid so the selected row is visible.</summary>
    private void ScrollToCharacter(int index)
    {
        if (charScroll == null)
            return;
        float contentH = charScroll.content.rect.height, viewH = charScroll.viewport.rect.height;
        if (contentH <= viewH)
            return;
        float rowTop = (index / 2) * 120f, rowBottom = rowTop + 120f;
        float top = (1f - charScroll.verticalNormalizedPosition) * (contentH - viewH);
        if (rowTop < top)
            top = rowTop;
        else if (rowBottom > top + viewH)
            top = rowBottom - viewH;
        charScroll.verticalNormalizedPosition = 1f - Mathf.Clamp01(top / (contentH - viewH));
    }

    private void SelectPrimary(int index)
    {
        Loadout.Primary = index;
        for (int i = 0; i < weaponButtons.Count; i++)
        {
            bool sel = i == index;
            weaponButtons[i].color = sel ? Theme.Selected : Theme.Panel;
            weaponNames[i].color = sel ? new Color(0.08f, 0.08f, 0.1f) : Color.white;
            weaponNames[i].GetComponent<Shadow>().enabled = !sel;
        }
        RefreshWeapon();
    }

    private void RefreshWeapon()
    {
        var data = Loadout.PrimaryWeapon();
        weaponTitle.text = data.weaponName + "  •  " + ModelLibrary.GunSkinName(data.weaponType, data.modelSkin);
        float[] stats = Gunsmith.Stats(data);
        for (int i = 0; i < 6; i++)
        {
            statFill[i].rectTransform.sizeDelta = new Vector2(300f * Mathf.Clamp01(stats[i] / 100f), 10f);
            statValue[i].text = Mathf.RoundToInt(stats[i]).ToString();
        }
        BuildSkinRow(primarySkinRow, data.weaponType);
        BuildSkinRow(pistolSkinRow, WeaponType.Pistol);
        var gm = GameManager.Instance;
        if (gm.player != null)
            gm.player.ShowcaseWeapon(data);
    }

    private void BuildSkinRow(RectTransform row, WeaponType type)
    {
        for (int i = row.childCount - 1; i >= 0; i--)
        {
            var c = row.GetChild(i).gameObject;
            c.SetActive(false);
            Destroy(c);
        }
        string[] skins = ModelLibrary.GunSkins(type);
        string current = ModelLibrary.SelectedGunSkin(type);
        float w = 560f / Mathf.Max(2, skins.Length);
        for (int i = 0; i < skins.Length; i++)
        {
            string skin = skins[i];
            bool sel = skin == current;
            Text label;
            var b = UIUtil.CreateButton(row, ModelLibrary.GunSkinName(type, skin), new Vector2(0f, 0.5f), new Vector2(w * (i + 0.5f), 0f), new Vector2(w - 10f, 60f),
                sel ? Theme.Selected : Theme.PanelLight, false, 24, out label);
            if (sel)
            {
                label.color = new Color(0.08f, 0.08f, 0.1f);
                label.GetComponent<Shadow>().enabled = false;
            }
            b.onClick.AddListener(() =>
            {
                ModelLibrary.SelectGunSkin(type, skin);
                RefreshWeapon();
            });
        }
        if (skins.Length == 1)
        {
            var more = UIUtil.CreateText(row, "Yakında yeni modeller", new Vector2(0f, 0.5f), new Vector2(w * 1.5f, 0f), new Vector2(w - 10f, 60f), 20, TextAnchor.MiddleCenter);
            more.color = Theme.TextDim;
        }
    }
}
