using System.Text;

/// <summary>
/// Lobby emotes (the order is part of the protocol: S_Emote sends the index) and the word filter for
/// room chat, the same idea as the account server's (Turkish swearing masked with stars).
/// </summary>
public static class NetChat
{
    /// <summary>Resources/UI/Emotes/&lt;name&gt;.png (drawn by Tools/make_emotes.py).</summary>
    public static readonly string[] Emotes =
        { "laugh", "cool", "angry", "cry", "clown", "poop", "fire", "thumbs", "wave", "chicken", "party", "sleep" };

    public const int MaxChat = 120;

    private static readonly string[] BadRoots =
    {
        "orospu", "siktir", "sikerim", "sikeyim", "siktiğim", "siktigim", "sikim", "sikik", "sikiş", "sikis",
        "yarrak", "yarak", "amına", "amina", "amcık", "amcik", "pezevenk", "kahpe", "ibne", "gavat", "kaltak",
        "şerefsiz", "serefsiz", "puşt", "pust", "yavşak", "yavsak", "götveren", "gotveren"
    };

    private static readonly string[] BadWords = { "amk", "aq", "amq", "mk", "oç", "oc", "piç", "pic", "göt", "got", "sik", "am", "sg", "siktir" };

    private static string Lower(string w)
    {
        var sb = new StringBuilder(w.Length);
        foreach (char c in w)
        {
            switch (c)
            {
                case 'İ': sb.Append('i'); break;
                case 'I': sb.Append('ı'); break;
                default: sb.Append(char.ToLowerInvariant(c)); break;
            }
        }
        return sb.ToString();
    }

    private static bool Bad(string word)
    {
        string low = Lower(word);
        foreach (var b in BadWords)
            if (low == b)
                return true;
        foreach (var r in BadRoots)
            if (low.StartsWith(r, System.StringComparison.Ordinal))
                return true;
        return false;
    }

    /// <summary>A chat line: printable, short, swear words masked.</summary>
    public static string Clean(string text)
    {
        if (string.IsNullOrEmpty(text))
            return "";
        var printable = new StringBuilder();
        foreach (char c in text)
            if (!char.IsControl(c))
                printable.Append(c);
        string s = printable.ToString().Trim();
        if (s.Length > MaxChat)
            s = s.Substring(0, MaxChat);
        var outp = new StringBuilder(s.Length);
        int i = 0;
        while (i < s.Length)
        {
            if (char.IsLetterOrDigit(s[i]))
            {
                int j = i;
                while (j < s.Length && char.IsLetterOrDigit(s[j]))
                    j++;
                string word = s.Substring(i, j - i);
                outp.Append(Bad(word) ? new string('*', word.Length) : word);
                i = j;
            }
            else
                outp.Append(s[i++]);
        }
        return outp.ToString();
    }
}
