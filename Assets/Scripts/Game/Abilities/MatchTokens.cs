using UnityEngine;

/// <summary>
/// Tokens the player brought into the current match (Teçhizat): at most one of each type, spent from the
/// profile only when actually used.
/// </summary>
public static class MatchTokens
{
    private static readonly bool[] available = new bool[Progression.TokenTypes];

    /// <summary>The revive token can be used until this zone phase starts.</summary>
    public const int ReviveBeforePhase = 4;

    public static void BeginMatch()
    {
        for (int i = 0; i < available.Length; i++)
        {
            var t = (TokenType)i;
            available[i] = Progression.CarryToken(t) && Progression.TokenCount(t) > 0;
        }
    }

    public static bool Available(TokenType t) { return available[(int)t]; }

    /// <summary>Spends the token (profile count - 1). False if it is not available.</summary>
    public static bool Use(TokenType t)
    {
        if (!available[(int)t] || !Progression.UseToken(t))
            return false;
        available[(int)t] = false;
        return true;
    }
}
