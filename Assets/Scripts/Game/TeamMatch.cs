using UnityEngine;

/// <summary>
/// The arena modes: a small arena cut from the map (MapCatalog arena: a circle the blue wall keeps everyone
/// in), everyone respawning 5 seconds after death. 5v5 (Takım Ölüm Maçı): first team to 40 kills. Hakimiyet:
/// 5v5, held capture points score (ArenaObjectives). Herkes Tek: eight players, each their own team, first to
/// 20 kills. Soygun: 5v5, money bags carried from the vault to a team's base (ArenaObjectives). Otherwise the
/// one ahead when the time is up wins. Offline the phone runs it; online the game server does and sends
/// S_Score / S_Respawn.
/// </summary>
public static class TeamMatch
{
    public const int Size = 5;
    public const int FfaPlayers = 8;
    public const float RespawnDelay = 5f;

    public static MatchMode Mode = MatchMode.Team5;
    /// <summary>Score per team (Herkes Tek: per player, team = player).</summary>
    public static readonly int[] Score = new int[FfaPlayers];
    public static Vector3 Center;
    public static float Radius = 80f;
    public static readonly Vector3[] Spawns = new Vector3[2];
    public static readonly Vector3[] FfaSpawns = new Vector3[FfaPlayers];
    private static float endsAt;

    public static int ScoreToWin { get { return GoalFor(Mode); } }
    public static float Duration { get { return DurationFor(Mode); } }
    public static int Teams { get { return Mode == MatchMode.FreeForAll ? FfaPlayers : 2; } }
    public static bool FreeForAll { get { return Mode == MatchMode.FreeForAll; } }

    public static int GoalFor(MatchMode m)
    {
        switch (m)
        {
            case MatchMode.Domination: return 200;
            case MatchMode.FreeForAll: return 20;
            case MatchMode.Heist: return 5;
            default: return 40;
        }
    }

    public static float DurationFor(MatchMode m)
    {
        return m == MatchMode.FreeForAll || m == MatchMode.Heist ? 420f : 480f;
    }

    /// <summary>The arena of the current map and the spawn sides.</summary>
    public static void Setup(MatchMode mode)
    {
        Mode = mode;
        var info = MapCatalog.CurrentInfo;
        Center = new Vector3(info.arena.x, 0f, info.arena.y);
        Center.y = World.HeightAt(Center.x, Center.z);
        Radius = info.arenaRadius;
        for (int i = 0; i < Score.Length; i++)
            Score[i] = 0;
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
        // Herkes Tek: eight places around the arena.
        for (int i = 0; i < FfaPlayers; i++)
        {
            float a = i * Mathf.PI * 2f / FfaPlayers;
            FfaSpawns[i] = Center + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * Radius * 0.65f;
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

    /// <summary>Where a player of <paramref name="team"/> comes back (ground level, a few metres around the team's side;
    /// Herkes Tek: the free place furthest from the others).</summary>
    public static Vector3 SpawnPoint(int team)
    {
        Vector3 at;
        if (FreeForAll)
        {
            at = FfaSpawns[Mathf.Abs(team) % FfaPlayers];
            var gm = GameManager.Instance;
            if (gm != null)
            {
                float best = -1f;
                foreach (var s in FfaSpawns)
                {
                    float nearest = 1e9f;
                    foreach (var c in gm.Combatants)
                    {
                        if (c == null || c.IsDead || c.Team == team)
                            continue;
                        nearest = Mathf.Min(nearest, (c.transform.position - s).sqrMagnitude);
                    }
                    nearest *= Random.Range(0.8f, 1.2f);
                    if (nearest > best)
                    {
                        best = nearest;
                        at = s;
                    }
                }
            }
        }
        else
            at = Spawns[Mathf.Clamp(team, 0, 1)];
        Vector3 p = World.RandomOpenPoint(at, FreeForAll ? 6f : 9f);
        return new Vector3(p.x, World.GroundHeight(p.x, p.z), p.z);
    }

    public static float TimeLeft
    {
        get { return Mathf.Max(0f, endsAt - Time.time); }
        set { endsAt = Time.time + value; }
    }

    /// <summary>Winner: the team (Herkes Tek: player) ahead, or -1 for a draw.</summary>
    public static int Winner
    {
        get
        {
            int best = -1, bestScore = -1;
            bool tie = false;
            for (int i = 0; i < Teams; i++)
            {
                if (Score[i] > bestScore)
                {
                    best = i;
                    bestScore = Score[i];
                    tie = false;
                }
                else if (Score[i] == bestScore)
                    tie = true;
            }
            return tie ? -1 : best;
        }
    }

    public static bool Over
    {
        get
        {
            if (TimeLeft <= 0f)
                return true;
            for (int i = 0; i < Teams; i++)
                if (Score[i] >= ScoreToWin)
                    return true;
            return false;
        }
    }

    /// <summary>Herkes Tek: the best score of everyone but <paramref name="team"/>.</summary>
    public static int BestOther(int team)
    {
        int best = 0;
        for (int i = 0; i < Teams; i++)
            if (i != team)
                best = Mathf.Max(best, Score[i]);
        return best;
    }

    /// <summary>Herkes Tek: 1 + how many are ahead of <paramref name="team"/>.</summary>
    public static int Place(int team)
    {
        int place = 1;
        for (int i = 0; i < Teams; i++)
            if (i != team && Score[i] > Score[team])
                place++;
        return place;
    }

    public static string TimeText
    {
        get
        {
            int s = Mathf.CeilToInt(TimeLeft);
            return (s / 60) + ":" + (s % 60).ToString("00");
        }
    }
}
