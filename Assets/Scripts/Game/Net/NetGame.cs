using UnityEngine;

/// <summary>
/// Online state shared by the game systems, and the small hooks they call. Offline play never
/// touches the network: every hook returns at once unless this is the game server or an online match.
/// </summary>
public static class NetGame
{
    /// <summary>Running as the dedicated game server (Linux build started with -server).</summary>
    public static bool IsServer { get; private set; }

    /// <summary>This phone is playing an online match right now.</summary>
    public static bool InOnlineMatch
    {
        get { return NetClient.Instance != null && NetClient.Instance.InMatch; }
    }

    /// <summary>Server or online match: systems not synced yet (abilities, vehicles, tokens) stay off.</summary>
    public static bool Online
    {
        get { return IsServer || InOnlineMatch; }
    }

    private static string version;

    /// <summary>Build version (commit) written by the CI build into Resources/zm_version.txt; "dev" otherwise.
    /// The phone and the server must have the same one.</summary>
    public static string BuildVersion
    {
        get
        {
            if (version == null)
            {
                var asset = Resources.Load<TextAsset>("zm_version");
                version = asset != null ? asset.text.Trim() : "";
                if (version.Length == 0)
                    version = "dev";
            }
            return version;
        }
    }

    public static bool VersionsMatch(string a, string b)
    {
        return a == b || a == "dev" || b == "dev";
    }

    // ----- Server start-up -----

    public sealed class ServerArgs
    {
        public int port = 7777;
        public string code = "000000";
        public MatchMode mode = MatchMode.Solo;
        public bool privateRoom;
        public string api = "";
    }

    /// <summary>Reads "-server -port 7777 -code 123456 -mode solo -matchType quick -api http://..." (null when not a server).</summary>
    public static ServerArgs ParseServerArgs()
    {
        string[] raw = System.Environment.GetCommandLineArgs();
        bool server = false;
        var a = new ServerArgs();
        for (int i = 0; i < raw.Length; i++)
        {
            string key = raw[i];
            string value = i + 1 < raw.Length ? raw[i + 1] : "";
            switch (key)
            {
                case "-server": server = true; break;
                case "-port": int.TryParse(value, out a.port); break;
                case "-code": a.code = value; break;
                case "-mode": a.mode = NetProtocol.ParseMode(value); break;
                case "-matchType": a.privateRoom = value == "private"; break;
                case "-api": a.api = value; break;
            }
        }
        if (!server)
            return null;
        IsServer = true;
        return a;
    }

    // ----- Hooks called by the game systems -----

    /// <summary>A gun fired (tracer end point): the server relays bot shots, the phone its own.</summary>
    public static void WeaponFired(WeaponController weapon, Vector3 end)
    {
        if (IsServer)
        {
            if (NetServer.Instance != null)
                NetServer.Instance.OnWeaponFired(weapon, end);
        }
        else if (weapon.playerOwned && InOnlineMatch)
            NetClient.Instance.SendShot(weapon, end);
    }

    public static void GrenadeThrown(Vector3 position, Vector3 velocity, IDamageable owner)
    {
        if (IsServer)
        {
            if (NetServer.Instance != null)
                NetServer.Instance.OnGrenadeThrown(position, velocity, owner);
        }
        else if (owner is PlayerController && InOnlineMatch)
            NetClient.Instance.SendGrenade(position, velocity);
    }

    /// <summary>The local player opened or closed a door.</summary>
    public static void LocalDoor(Door door, Vector3 from)
    {
        if (InOnlineMatch)
            NetClient.Instance.SendDoor(door, from);
    }

    /// <summary>A door moved on the server (a bot walked through, or a player's request).</summary>
    public static void DoorChanged(Door door, Vector3 from)
    {
        if (IsServer && NetServer.Instance != null)
            NetServer.Instance.OnDoorChanged(door, from);
    }

    /// <summary>The local player took a loot crate.</summary>
    public static void LootTaken(int id)
    {
        if (InOnlineMatch)
            NetClient.Instance.SendPickup(id);
    }

    /// <summary>A crate appeared on the server after the start (a death crate).</summary>
    public static void CrateAdded(int id, LootType type, Vector3 position)
    {
        if (IsServer && NetServer.Instance != null)
            NetServer.Instance.OnCrateAdded(id, type, position);
    }
}
