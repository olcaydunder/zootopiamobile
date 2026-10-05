using UnityEngine;

/// <summary>
/// 5v5 team deathmatch: two teams of five (bots fill the empty places) in a small arena cut from the
/// map (MapCatalog arena: a circle the blue wall keeps everyone in), respawning 5 seconds after death at
/// their own side; first team to 40 kills, or the one ahead after 8 minutes, wins. Offline the phone
/// runs it; online the game server does and sends S_Score / S_Respawn.
/// </summary>
public static class TeamMatch
{
    public const int Size = 5;
    public const int ScoreToWin = 40;
    public const float Duration = 480f;
    public const float RespawnDelay = 5f;

    public static readonly int[] Score = new int[2];
    public static Vector3 Center;
    public static float Radius = 80f;
    public static readonly Vector3[] Spawns = new Vector3[2];
    private static float endsAt;

    /// <summary>The arena of the current map and a spawn side for each team.</summary>
    public static void Setup()
    {
        var info = MapCatalog.CurrentInfo;
        Center = new Vector3(info.arena.x, 0f, info.arena.y);
        Center.y = World.HeightAt(Center.x, Center.z);
        Radius = info.arenaRadius;
        Score[0] = Score[1] = 0;
        endsAt = Time.time + Duration;
        // Opposite sides of the arena on open ground: the first direction where both sides are free.
        float best = -1f;
        for (int k = 0; k < 12; k++)
        {
            float a = k * Mathf.PI / 6f;
            var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            float good = Openness(Center + dir * Radius * 0.7f) + Openness(Center - dir * Radius * 0.7f);
            if (good > best)
            {
                best = good;
                Spawns[0] = Center + dir * Radius * 0.7f;
                Spawns[1] = Center - dir * Radius * 0.7f;
            }
        }
    }

    private static float Openness(Vector3 p)
    {
        float score = 0f;
        for (int i = 0; i < 12; i++)
        {
            float a = i * Mathf.PI / 6f;
            float x = p.x + Mathf.Cos(a) * 6f, z = p.z + Mathf.Sin(a) * 6f;
            if (World.IsLand(x, z) && !World.IsBlocked(x, z))
                score += 1f;
        }
        return score - Mathf.Abs(World.HeightAt(p.x, p.z) - Center.y) * 0.1f;
    }

    /// <summary>Where a player of <paramref name="team"/> comes back (ground level, a few metres around the team's side).</summary>
    public static Vector3 SpawnPoint(int team)
    {
        Vector3 p = World.RandomOpenPoint(Spawns[Mathf.Clamp(team, 0, 1)], 9f);
        return new Vector3(p.x, World.GroundHeight(p.x, p.z), p.z);
    }

    public static float TimeLeft
    {
        get { return Mathf.Max(0f, endsAt - Time.time); }
        set { endsAt = Time.time + value; }
    }

    /// <summary>Winner: 0, 1, or -1 for a draw.</summary>
    public static int Winner { get { return Score[0] > Score[1] ? 0 : Score[1] > Score[0] ? 1 : -1; } }

    public static bool Over { get { return Score[0] >= ScoreToWin || Score[1] >= ScoreToWin || TimeLeft <= 0f; } }

    public static string TimeText
    {
        get
        {
            int s = Mathf.CeilToInt(TimeLeft);
            return (s / 60) + ":" + (s % 60).ToString("00");
        }
    }
}
