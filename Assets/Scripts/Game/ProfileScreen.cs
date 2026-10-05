using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// PROFİL: who you are (rank, level, KUPA and league, GÜÇ), your statistics (matches, wins, win rate, kills,
/// K/D, best match, head shots, damage, time played) and what you have on (character, gun, mask, armour,
/// equipment, perks). İSİM changes the name; ENVANTER opens the inventory.
/// </summary>
public class ProfileScreen : MonoBehaviour
{
    private System.Action onClose, onRename, onInventory;
    private RectTransform body;

    public static ProfileScreen Create(Transform canvas)
    {
        var rect = UIUtil.CreateStretch(canvas, "Profile");
        rect.gameObject.AddComponent<Image>().color = new Color(0.03f, 0.05f, 0.09f, 0.97f);
        var s = rect.gameObject.AddComponent<ProfileScreen>();
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
        Icons.Create(t, "profile", new Vector2(0f, 1f), new Vector2(190f, -70f), new Vector2(84f, 84f));
        var title = UIUtil.CreateText(t, "PROFİL", new Vector2(0f, 1f), new Vector2(460f, -70f), new Vector2(500f, 80f), 52, TextAnchor.MiddleLeft);
        title.fontStyle = FontStyle.Bold;
        body = UIUtil.CreateRect(t, "Body", new Vector2(0.5f, 0.5f), new Vector2(0f, -50f), new Vector2(1800f, 880f));
    }

    public void Open(System.Action closed, System.Action rename, System.Action inventory)
    {
        onClose = closed;
        onRename = rename;
        onInventory = inventory;
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        Rebuild();
    }

    public void Hide() { gameObject.SetActive(false); }

    private void Close()
    {
        Hide();
        if (onClose != null)
            onClose();
    }

    private void Rebuild()
    {
        for (int i = body.childCount - 1; i >= 0; i--)
            Destroy(body.GetChild(i).gameObject);
        var p = GameManager.Instance.profile;
        var c = new Vector2(0.5f, 0.5f);
        Text label;

        // ----- Left: who -----
        var card = Theme.Box(body, "Card", c, new Vector2(-640f, 0f), new Vector2(500f, 860f), Theme.Panel, true).transform;
        Icons.Create(card, Icons.Rank(p.RankIndex), new Vector2(0.5f, 1f), new Vector2(0f, -120f), new Vector2(170f, 170f));
        var name = UIUtil.CreateText(card, p.playerName, new Vector2(0.5f, 1f), new Vector2(0f, -240f), new Vector2(460f, 56f), 40, TextAnchor.MiddleCenter);
        name.fontStyle = FontStyle.Bold;
        var rank = UIUtil.CreateText(card, p.RankName.ToUpper() + "  •  SEVİYE " + p.level, new Vector2(0.5f, 1f), new Vector2(0f, -284f), new Vector2(460f, 34f), 24, TextAnchor.MiddleCenter);
        rank.color = Theme.Accent;
        if (!p.IsMaxLevel)
        {
            UIUtil.CreateImage(card, "XpBg", new Vector2(0.5f, 1f), new Vector2(0f, -318f), new Vector2(400f, 12f), new Color(1f, 1f, 1f, 0.15f), false).raycastTarget = false;
            var xp = UIUtil.CreateImage(card, "Xp", new Vector2(0.5f, 1f), new Vector2(-200f, -318f), new Vector2(400f * Mathf.Clamp01((float)p.xp / Mathf.Max(1, p.XpForNextLevel)), 12f), Theme.Accent, false);
            xp.rectTransform.pivot = new Vector2(0f, 0.5f);
            xp.raycastTarget = false;
            var xt = UIUtil.CreateText(card, p.xp + " / " + p.XpForNextLevel + " XP", new Vector2(0.5f, 1f), new Vector2(0f, -344f), new Vector2(400f, 28f), 18, TextAnchor.MiddleCenter);
            xt.color = Theme.TextDim;
        }
        // KUPA and league
        var trophy = UIUtil.CreateImage(card, "Trophy", new Vector2(0.5f, 1f), new Vector2(0f, -440f), new Vector2(440f, 120f), new Color(0.25f, 0.18f, 0.05f, 0.9f), false);
        trophy.raycastTarget = false;
        Icons.Create(trophy.transform, "trophy", new Vector2(0f, 0.5f), new Vector2(66f, 0f), new Vector2(96f, 96f));
        var tv = UIUtil.CreateText(trophy.transform, p.trophies.ToString("N0"), new Vector2(0f, 0.5f), new Vector2(250f, 16f), new Vector2(260f, 56f), 48, TextAnchor.MiddleLeft);
        tv.fontStyle = FontStyle.Bold;
        tv.color = new Color(1f, 0.85f, 0.3f);
        int li = p.LeagueIndex;
        string next = li + 1 < ProfileData.LeagueAt.Length ? "  •  " + ProfileData.Leagues[li + 1] + " " + ProfileData.LeagueAt[li + 1] : "";
        var lg = UIUtil.CreateText(trophy.transform, p.League + " LİGİ" + next, new Vector2(0f, 0.5f), new Vector2(250f, -28f), new Vector2(300f, 30f), 20, TextAnchor.MiddleLeft);
        lg.color = Theme.TextDim;
        // GÜÇ
        var power = UIUtil.CreateImage(card, "Power", new Vector2(0.5f, 1f), new Vector2(0f, -575f), new Vector2(440f, 110f), new Color(0.18f, 0.1f, 0.28f, 0.9f), false);
        power.raycastTarget = false;
        Icons.Create(power.transform, "power", new Vector2(0f, 0.5f), new Vector2(66f, 0f), new Vector2(90f, 90f));
        var pv = UIUtil.CreateText(power.transform, Gear.Power(p).ToString("N0"), new Vector2(0f, 0.5f), new Vector2(250f, 14f), new Vector2(260f, 56f), 46, TextAnchor.MiddleLeft);
        pv.fontStyle = FontStyle.Bold;
        pv.color = new Color(0.85f, 0.6f, 1f);
        var pl = UIUtil.CreateText(power.transform, "GÜÇ  •  kuşanılanlar ve seviye", new Vector2(0f, 0.5f), new Vector2(250f, -28f), new Vector2(300f, 30f), 20, TextAnchor.MiddleLeft);
        pl.color = Theme.TextDim;
        var rename = UIUtil.CreateButton(card, "İSİM DEĞİŞTİR", new Vector2(0.5f, 0f), new Vector2(-110f, 70f), new Vector2(210f, 80f), Theme.PanelLight, false, 24, out label);
        rename.onClick.AddListener(() => { Hide(); if (onRename != null) onRename(); });
        var inv = UIUtil.CreateButton(card, "ENVANTER  ›", new Vector2(0.5f, 0f), new Vector2(110f, 70f), new Vector2(210f, 80f), new Color(0.3f, 0.2f, 0.45f, 0.95f), false, 24, out label);
        inv.onClick.AddListener(() => { Hide(); if (onInventory != null) onInventory(); });

        // ----- Middle: statistics -----
        var head = UIUtil.CreateText(body, "İSTATİSTİKLER", c, new Vector2(-30f, 400f), new Vector2(660f, 50f), 32, TextAnchor.MiddleLeft);
        head.fontStyle = FontStyle.Bold;
        head.color = Theme.Accent;
        int hours = Mathf.FloorToInt(p.playSeconds / 3600f), minutes = Mathf.FloorToInt(p.playSeconds / 60f) % 60;
        string[,] stats =
        {
            { "MAÇ", p.matches.ToString("N0") },
            { "ZAFER", p.wins.ToString("N0") },
            { "KAZANMA ORANI", "%" + p.WinRate.ToString("0.0") },
            { "ÖLDÜRME", p.totalKills.ToString("N0") },
            { "Ö/Ö ORANI (K/D)", p.KillsPerDeath.ToString("0.00") },
            { "EN ÇOK ÖLDÜRME", p.bestKills.ToString("N0") },
            { "KAFADAN VURUŞ", p.headshots.ToString("N0") },
            { "TOPLAM HASAR", p.damage.ToString("N0") },
            { "OYUN SÜRESİ", hours > 0 ? hours + " sa " + minutes + " dk" : minutes + " dk" },
            { "KUPA", p.trophies.ToString("N0") },
        };
        for (int i = 0; i < stats.GetLength(0); i++)
        {
            float x = -170f + (i % 2) * 340f;
            float y = 320f - (i / 2) * 150f;
            var tile = UIUtil.CreateImage(body, "Stat", c, new Vector2(x, y), new Vector2(326f, 136f), Theme.Panel, false);
            tile.raycastTarget = false;
            var v = UIUtil.CreateText(tile.transform, stats[i, 1], c, new Vector2(0f, 18f), new Vector2(310f, 60f), 44, TextAnchor.MiddleCenter);
            v.fontStyle = FontStyle.Bold;
            var k = UIUtil.CreateText(tile.transform, stats[i, 0], c, new Vector2(0f, -36f), new Vector2(310f, 30f), 20, TextAnchor.MiddleCenter);
            k.color = Theme.TextDim;
        }

        // ----- Right: equipped -----
        var eh = UIUtil.CreateText(body, "KUŞANILANLAR", c, new Vector2(640f, 400f), new Vector2(500f, 50f), 32, TextAnchor.MiddleLeft);
        eh.fontStyle = FontStyle.Bold;
        eh.color = Theme.Accent;
        int skin = System.Array.IndexOf(ModelLibrary.ShopSkins, p.equippedSkin);
        Line(body, 320f, "skin", "KARAKTER", skin >= 0 ? ModelLibrary.ShopNames[skin] : p.equippedSkin);
        var gun = Gunsmith.BaseWeapon(Loadout.PrimaryType);
        Line(body, 230f, "slot_barrel", "ANA SİLAH", gun != null ? gun.weaponName : "-");
        var mask = Gear.EquippedDef(GearSlot.Mask);
        Line(body, 140f, mask != null ? mask.Icon : "gear_k_cat", "MASKE", mask != null ? mask.name : "yok");
        // armour and equipment icons
        GearSlot[] slots = { GearSlot.Head, GearSlot.Body, GearSlot.Legs, GearSlot.Explosive, GearSlot.Tactical, GearSlot.Medical };
        for (int i = 0; i < slots.Length; i++)
        {
            var g = Gear.EquippedDef(slots[i]);
            float x = 470f + (i % 3) * 120f;
            float y = 10f - (i / 3) * 130f;
            if (g != null)
                RewardView.Create(body, new Reward { kind = RewardKind.Gear, id = g.id, amount = 1 }, c, new Vector2(x, y), 104f);
            else
                UIUtil.CreateImage(body, "Empty", c, new Vector2(x, y), new Vector2(104f, 104f), new Color(1f, 1f, 1f, 0.06f), false).raycastTarget = false;
            var n = UIUtil.CreateText(body, g != null ? "SV " + Gear.Level(g.id) : Gear.SlotNames[(int)slots[i]], c, new Vector2(x, y - 64f), new Vector2(118f, 24f), 16, TextAnchor.MiddleCenter);
            n.color = Theme.TextDim;
        }
        var ph = UIUtil.CreateText(body, "GÜÇLENDİRİCİLER", c, new Vector2(640f, -255f), new Vector2(500f, 34f), 22, TextAnchor.MiddleLeft);
        ph.color = Theme.TextDim;
        for (int i = 0; i < Gear.PerkSlots; i++)
        {
            var g = Gear.Find(Gear.Perk(i));
            float x = 470f + i * 120f;
            if (g != null)
            {
                RewardView.Create(body, new Reward { kind = RewardKind.Gear, id = g.id, amount = 1 }, c, new Vector2(x, -330f), 104f);
                InventoryScreen.Stars(body, c, new Vector2(x, -394f), Gear.Level(g.id), g.MaxLevel, 18f);
            }
            else
                UIUtil.CreateImage(body, "Empty", c, new Vector2(x, -330f), new Vector2(104f, 104f), new Color(1f, 1f, 1f, 0.06f), false).raycastTarget = false;
        }
    }

    private static void Line(Transform parent, float y, string icon, string key, string value)
    {
        var c = new Vector2(0.5f, 0.5f);
        var bg = UIUtil.CreateImage(parent, "Line", c, new Vector2(640f, y), new Vector2(500f, 80f), Theme.Panel, false);
        bg.raycastTarget = false;
        Icons.Create(bg.transform, icon, new Vector2(0f, 0.5f), new Vector2(46f, 0f), new Vector2(64f, 64f)).raycastTarget = false;
        var k = UIUtil.CreateText(bg.transform, key, new Vector2(0f, 0.5f), new Vector2(200f, 16f), new Vector2(220f, 26f), 18, TextAnchor.MiddleLeft);
        k.color = Theme.TextDim;
        var v = UIUtil.CreateText(bg.transform, value, new Vector2(0f, 0.5f), new Vector2(260f, -12f), new Vector2(340f, 36f), 26, TextAnchor.MiddleLeft);
        v.fontStyle = FontStyle.Bold;
    }
}
