using UnityEngine;

/// <summary>Names, descriptions and properties of the game modes (lobby, mode screen, HUD, results).</summary>
public static class Modes
{
    public static readonly MatchMode[] All =
    {
        MatchMode.Solo, MatchMode.Duo, MatchMode.Squad, MatchMode.Team5, MatchMode.Domination, MatchMode.FreeForAll, MatchMode.Heist
    };

    /// <summary>Short label: "SOLO", "5v5", "HAKİMİYET"…</summary>
    public static string Short(MatchMode m)
    {
        switch (m)
        {
            case MatchMode.Duo: return "DUO";
            case MatchMode.Squad: return "SQUAD";
            case MatchMode.Team5: return "5v5";
            case MatchMode.Domination: return "HAKİMİYET";
            case MatchMode.FreeForAll: return "HERKES TEK";
            case MatchMode.Heist: return "SOYGUN";
            default: return "SOLO";
        }
    }

    public static string Title(MatchMode m)
    {
        switch (m)
        {
            case MatchMode.Team5: return "TAKIM ÖLÜM MAÇI";
            case MatchMode.Domination: return "HAKİMİYET";
            case MatchMode.FreeForAll: return "HERKES TEK";
            case MatchMode.Heist: return "SOYGUN";
            default: return "BATTLE ROYALE  •  " + Short(m);
        }
    }

    public static string Description(MatchMode m)
    {
        switch (m)
        {
            case MatchMode.Duo: return "25 oyuncu, 1 takım arkadaşınla son kalan takım ol";
            case MatchMode.Squad: return "25 oyuncu, 3 takım arkadaşınla son kalan takım ol";
            case MatchMode.Team5: return "5'e 5, yeniden doğarak; " + TeamMatch.GoalFor(m) + " öldürmeye ulaşan takım kazanır";
            case MatchMode.Domination: return "5'e 5, A B C bölgelerini tut; " + TeamMatch.GoalFor(m) + " puana ulaşan takım kazanır";
            case MatchMode.FreeForAll: return "8 oyuncu, herkes herkese karşı; " + TeamMatch.GoalFor(m) + " öldürmeye ulaşan kazanır";
            case MatchMode.Heist: return "5'e 5, kasadaki para çantasını kendi üssüne taşı; " + TeamMatch.GoalFor(m) + " çanta kazandırır";
            default: return "25 oyuncu, paraşütle atla, ganimet topla, son kalan ol";
        }
    }

    public static string Icon(MatchMode m)
    {
        switch (m)
        {
            case MatchMode.Team5: return "mode_tdm";
            case MatchMode.Domination: return "mode_dom";
            case MatchMode.FreeForAll: return "mode_ffa";
            case MatchMode.Heist: return "mode_heist";
            default: return "mode_br";
        }
    }

    public static Color Color(MatchMode m)
    {
        switch (m)
        {
            case MatchMode.Team5: return new Color(1f, 0.35f, 0.3f);
            case MatchMode.Domination: return new Color(0.3f, 0.6f, 1f);
            case MatchMode.FreeForAll: return new Color(0.75f, 0.45f, 1f);
            case MatchMode.Heist: return new Color(0.35f, 0.85f, 0.4f);
            default: return Theme.Accent;
        }
    }

    /// <summary>Respawning matches in a small arena (everything but Battle Royale).</summary>
    public static bool Arena(MatchMode m) { return m == MatchMode.Team5 || m == MatchMode.Domination || m == MatchMode.FreeForAll || m == MatchMode.Heist; }

    /// <summary>Two teams of five.</summary>
    public static bool TwoTeams(MatchMode m) { return m == MatchMode.Team5 || m == MatchMode.Domination || m == MatchMode.Heist; }

    /// <summary>Can be played online on the game server (Soygun is bots only for now).</summary>
    public static bool Online(MatchMode m) { return m != MatchMode.Heist; }
}
