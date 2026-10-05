using UnityEngine;

/// <summary>
/// Message numbers and field packing shared by the phone and the game server.
/// Every message starts with its id byte. "Pos" is a position packed into 3 x 16 bits (5 cm steps),
/// "Yaw" an angle in one byte. Bump <see cref="Version"/> whenever a message changes.
/// </summary>
public static class NetProtocol
{
    public const int Version = 6;   // 3: the hello carries the map; 4: room chat and emotes; 5: 5v5; 6: masks, throw kinds, new modes
    public const int MaxHumans = 16;
    public const float TickInterval = 0.05f;            // 20 movement / snapshot messages per second
    public const int NoEntity = 0xFFFF;                 // zone, fall, left the game
    public const int HitGrenade = 100;                  // C_Hit / S_Kill weapon code for grenades
    public const int HowLeft = 200;                     // S_Kill: the player left the match

    // Phone -> server
    public const byte C_State = 1;      // ushort seq, Pos, Yaw, sbyte pitch, byte flags, byte weapon+1, byte health, byte armor
    public const byte C_Shot = 2;       // Pos end, byte weapon+1
    public const byte C_Hit = 3;        // ushort target, ushort damage*10, byte weapon (or HitGrenade), byte head
    public const byte C_Died = 4;       // ushort killer, byte how (weapon+1 / HitGrenade, from the last S_Damage)
    public const byte C_Pickup = 5;     // ushort loot id
    public const byte C_Door = 6;       // ushort door, bool open, Pos from
    public const byte C_Grenade = 7;    // Pos, float vx vy vz, byte ThrowKind
    public const byte C_Start = 8;      // (private room leader) start now
    public const byte C_Voice = 9;      // voice frame: byte seq, short predictor, byte index, ADPCM bytes (unreliable)
    public const byte C_Chat = 10;      // string text (room chat; in a match: team chat)
    public const byte C_Emote = 11;     // byte emote (NetChat.Emotes)

    // Server -> phone
    public const byte S_Lobby = 20;       // byte phase, float seconds left, byte mode, bool private, ushort leader, string code, byte n {ushort id, string name, string account}
    public const byte S_Entities = 21;    // byte n {ushort id, byte team, bool bot, string name, string skin, string parachute, string account, string mask}
    public const byte S_MatchStart = 22;  // ushort you, byte team, byte mode, Pos plane start, Pos plane end, float zx zz zr, ushort doors, byte teams
    public const byte S_Loot = 23;        // ushort n {ushort id, byte type, Pos}
    public const byte S_LootTaken = 24;   // ushort id, bool yours (answer to C_Pickup: only then the item is given)
    public const byte S_LootGone = 25;    // ushort id
    public const byte S_Snap = 26;        // see NetServer.SendSnapshots
    public const byte S_Shot = 27;        // ushort shooter, Pos end, byte weapon+1
    public const byte S_Damage = 28;      // ushort damage*10, ushort attacker, Pos from, bool head, byte how
    public const byte S_Kill = 29;        // ushort killer, ushort victim, byte how
    public const byte S_Door = 30;        // ushort door, bool open, Pos from
    public const byte S_Grenade = 31;     // ushort thrower, Pos, float vx vy vz, byte ThrowKind
    public const byte S_Placement = 32;   // byte place, byte teams
    public const byte S_MatchEnd = 33;    // byte winner team (255 none), string names
    public const byte S_Toast = 34;       // string
    public const byte S_Voice = 35;       // ushort speaker, then the C_Voice payload after its id byte
    public const byte S_Chat = 36;        // ushort id, string name, string account, string text
    public const byte S_Emote = 37;       // ushort id, string name, byte emote
    public const byte S_Respawn = 38;     // arena: ushort id, float server time, Pos (the phone with that id spawns there)
    public const byte S_Score = 39;       // arena: byte n {ushort score per server team}, float seconds left, byte points {sbyte owner, sbyte progress*100}

    // Entity flags (C_State and snapshots)
    public const int F_Dead = 1, F_Plane = 2, F_Freefall = 4, F_Parachute = 8, F_Crouch = 16, F_Swim = 32, F_Aim = 64;
    public const int F_Air = F_Plane | F_Freefall | F_Parachute;

    /// <summary>Largest voice payload accepted (40 ms of 8 kHz ADPCM = 160 bytes + header).</summary>
    public const int MaxVoiceBytes = 220;

    // Lobby phases
    public const int LobbyWaiting = 0, LobbyCountdown = 1;

    public static string ModeName(MatchMode mode)
    {
        switch (mode)
        {
            case MatchMode.Duo: return "duo";
            case MatchMode.Squad: return "squad";
            case MatchMode.Team5: return "5v5";
            case MatchMode.Domination: return "dom";
            case MatchMode.FreeForAll: return "ffa";
            case MatchMode.Heist: return "heist";
            default: return "solo";
        }
    }

    public static MatchMode ParseMode(string s)
    {
        switch (s)
        {
            case "duo": return MatchMode.Duo;
            case "squad": return MatchMode.Squad;
            case "5v5": return MatchMode.Team5;
            case "dom": return MatchMode.Domination;
            case "ffa": return MatchMode.FreeForAll;
            case "heist": return MatchMode.Heist;
            default: return MatchMode.Solo;
        }
    }

    public static int TeamSize(MatchMode mode)
    {
        switch (mode)
        {
            case MatchMode.Solo: return 1;
            case MatchMode.FreeForAll: return 1;
            case MatchMode.Duo: return 2;
            case MatchMode.Team5:
            case MatchMode.Domination:
            case MatchMode.Heist: return TeamMatch.Size;
            default: return 4;
        }
    }

    // ----- Packing helpers -----

    public static void Pos(this NetWriter w, Vector3 p)
    {
        w.Short(Mathf.RoundToInt(p.x * 20f));
        w.Short(Mathf.RoundToInt(p.y * 20f));
        w.Short(Mathf.RoundToInt(p.z * 20f));
    }

    public static Vector3 Pos(this NetReader r)
    {
        float x = r.Short() * 0.05f;
        float y = r.Short() * 0.05f;
        float z = r.Short() * 0.05f;
        return new Vector3(x, y, z);
    }

    public static void Yaw(this NetWriter w, float degrees)
    {
        w.Byte(Mathf.RoundToInt(Mathf.Repeat(degrees, 360f) * (256f / 360f)) & 255);
    }

    public static float Yaw(this NetReader r)
    {
        return r.Byte() * (360f / 256f);
    }

    public static void Vec(this NetWriter w, Vector3 v)
    {
        w.Float(v.x);
        w.Float(v.y);
        w.Float(v.z);
    }

    public static Vector3 Vec(this NetReader r)
    {
        float x = r.Float();
        float y = r.Float();
        float z = r.Float();
        if (float.IsNaN(x) || float.IsNaN(y) || float.IsNaN(z) || float.IsInfinity(x) || float.IsInfinity(y) || float.IsInfinity(z))
            throw new NetFormatException();
        return new Vector3(x, y, z);
    }

    /// <summary>Most damage one hit of this weapon can do (head shot, best attachments and class bonus included).</summary>
    public static float MaxHitDamage(int weapon)
    {
        if (weapon == HitGrenade)
            return 170f * 2f;   // the explosive charge with the best levels and perks
        float baseDamage;
        switch ((WeaponType)weapon)
        {
            case WeaponType.Rifle: baseDamage = 20f; break;
            case WeaponType.SMG: baseDamage = 14f; break;
            case WeaponType.Shotgun: baseDamage = 11f; break;
            case WeaponType.Sniper: baseDamage = 85f; break;
            case WeaponType.Pistol: baseDamage = 16f; break;
            default: return 0f;
        }
        return baseDamage * 2f * 1.6f + 1f;
    }

    /// <summary>Furthest a hit of this weapon can land (generous: positions are 50-100 ms old).</summary>
    public static float MaxHitRange(int weapon)
    {
        if (weapon == HitGrenade)
            return 70f;
        float range;
        switch ((WeaponType)weapon)
        {
            case WeaponType.Rifle: range = 80f; break;
            case WeaponType.SMG: range = 40f; break;
            case WeaponType.Shotgun: range = 20f; break;
            case WeaponType.Sniper: range = 150f; break;
            case WeaponType.Pistol: range = 45f; break;
            default: return 0f;
        }
        return range * 1.7f + 10f;
    }
}

/// <summary>
/// Who is dealing the damage that is being applied right now (set around TakeDamage calls by weapons,
/// grenades and the server), so kills can be credited and the hit direction shown.
/// </summary>
public static class HitContext
{
    public static IDamageable Attacker;
    public static Vector3 From;
    public static bool Head;
    public static bool Pierce;       // poison: armour does not help
    public static int Weapon = -1;

    public static void Set(IDamageable attacker, Vector3 from, bool head, int weapon)
    {
        Attacker = attacker;
        From = from;
        Head = head;
        Weapon = weapon;
    }

    public static void Clear()
    {
        Attacker = null;
        Head = false;
        Pierce = false;
        Weapon = -1;
    }
}
