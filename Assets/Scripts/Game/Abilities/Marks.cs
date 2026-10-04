using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Enemies revealed by abilities (K9 dog, Gözcü eagle): shown on the minimap and with a marker above
/// their head to the team that marked them, until the mark runs out.
/// </summary>
public static class Marks
{
    private struct Mark { public int team; public float until; }

    private static readonly Dictionary<IDamageable, List<Mark>> marks = new Dictionary<IDamageable, List<Mark>>();
    private static readonly List<IDamageable> scratch = new List<IDamageable>();

    public static void Add(IDamageable target, int byTeam, float seconds)
    {
        if (target == null || target.IsDead)
            return;
        List<Mark> list;
        if (!marks.TryGetValue(target, out list))
        {
            list = new List<Mark>();
            marks[target] = list;
        }
        for (int i = 0; i < list.Count; i++)
            if (list[i].team == byTeam)
            {
                list[i] = new Mark { team = byTeam, until = Mathf.Max(list[i].until, Time.time + seconds) };
                return;
            }
        list.Add(new Mark { team = byTeam, until = Time.time + seconds });
    }

    public static bool IsMarked(IDamageable target, int forTeam)
    {
        List<Mark> list;
        if (target == null || target.IsDead || !marks.TryGetValue(target, out list))
            return false;
        for (int i = 0; i < list.Count; i++)
            if (list[i].team == forTeam && list[i].until > Time.time)
                return true;
        return false;
    }

    /// <summary>Everyone currently marked for a team (reused list — copy it if you keep it).</summary>
    public static List<IDamageable> MarkedFor(int team)
    {
        scratch.Clear();
        foreach (var kv in marks)
            if (IsMarked(kv.Key, team))
                scratch.Add(kv.Key);
        return scratch;
    }

    public static void Clear()
    {
        marks.Clear();
    }
}
