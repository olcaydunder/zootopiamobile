#if UNITY_ANDROID && !UNITY_EDITOR
using GooglePlayGames;
using GooglePlayGames.BasicApi;
#endif
using UnityEngine;

/// <summary>
/// Sign-in with Google Play Games. Play Games signs the player in by itself when the game starts (no screen of ours);
/// then the phone asks Google for a one-time server code and the game server (POST /account/google, sent from here) ties this phone's
/// online account to the player's Google account. On a new phone, or after reinstalling, the same Google account finds
/// the old online account again (player code, friends, messages, gifts).
/// The player's progress (level, Kredi, items) is still kept on the phone only.
/// Ids: PlayConfig.PlayGamesAppId / PlayGamesWebClientId. Phones only, never on the game server.
/// </summary>
public class PlayGamesLogin : MonoBehaviour
{
    private static PlayGamesLogin instance;
    private static bool busy;

    /// <summary>Signed in to Play Games on this phone right now.</summary>
    public static bool SignedIn { get; private set; }
    /// <summary>The Play Games name of the signed-in player ("" when not signed in).</summary>
    public static string GoogleName { get; private set; }
    /// <summary>This phone's online account is tied to the signed-in Google account.</summary>
    public static bool Linked
    {
        get
        {
            string pid = PlayerPrefs.GetString("zm_google_pid", "");
            return SignedIn && pid.Length > 0 && pid == currentPid && PlayerPrefs.GetString("zm_google_acc", "") == OnlineService.AccountId;
        }
    }
    /// <summary>Shown in settings: what happened last ("" nothing to say).</summary>
    public static string Status { get; private set; }
    public static bool Busy { get { return busy; } }

    private static string currentPid = "";

    static PlayGamesLogin()
    {
        GoogleName = "";
        Status = "";
    }

    public static bool Available
    {
        get { return Application.platform == RuntimePlatform.Android && !NetGame.IsServer; }
    }

    /// <summary>At start-up: Play Games' own automatic sign-in, then the link with the game server.</summary>
    public static void Init()
    {
        if (instance != null || !Available)
            return;
        var go = new GameObject("PlayGamesLogin");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<PlayGamesLogin>();
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            PlayGamesPlatform.Activate();
            busy = true;
            PlayGamesPlatform.Instance.Authenticate(status => instance.OnAuthenticated(status, false, null));
        }
        catch (System.Exception e)
        {
            busy = false;
            Debug.LogWarning("[PlayGames] start: " + e.Message);
        }
#endif
    }

    /// <summary>The "Google ile giriş" button: the player asked for it, so Play Games may show its sign-in screen.
    /// done(ok, message to show).</summary>
    public static void SignIn(System.Action<bool, string> done)
    {
        if (!Available)
        {
            Finish(done, false, "Google ile giriş yalnızca Android'de var");
            return;
        }
        if (instance == null)
            Init();
        if (busy)
        {
            Finish(done, false, "Giriş sürüyor, biraz bekle");
            return;
        }
#if UNITY_ANDROID && !UNITY_EDITOR
        busy = true;
        Status = "Google'a bağlanılıyor...";
        try
        {
            if (PlayGamesPlatform.Instance.IsAuthenticated())
                instance.LinkWithServer(true, done);
            else
                PlayGamesPlatform.Instance.ManuallyAuthenticate(status => instance.OnAuthenticated(status, true, done));
        }
        catch (System.Exception e)
        {
            busy = false;
            Debug.LogWarning("[PlayGames] sign-in: " + e.Message);
            Status = "Google girişi açılamadı";
            Finish(done, false, Status);
        }
#endif
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    private void OnAuthenticated(SignInStatus status, bool asked, System.Action<bool, string> done)
    {
        if (status != SignInStatus.Success)
        {
            busy = false;
            SignedIn = false;
            // At start-up the player may simply not want Play Games: say nothing then; when they pressed the button, say why.
            Status = asked ? (status == SignInStatus.Canceled ? "Google girişi iptal edildi" : "Google girişi yapılamadı") : "";
            Debug.Log("[PlayGames] sign-in: " + status);
            Finish(done, false, Status);
            return;
        }
        SignedIn = true;
        GoogleName = PlayGamesPlatform.Instance.GetUserDisplayName() ?? "";
        currentPid = PlayGamesPlatform.Instance.GetUserId() ?? "";
        if (!asked && Linked)
        {
            busy = false;   // already tied to this account on an earlier start: nothing to ask the server
            Status = "";
            return;
        }
        LinkWithServer(asked, done);
    }

    private void LinkWithServer(bool asked, System.Action<bool, string> done)
    {
        PlayGamesPlatform.Instance.RequestServerSideAccess(false, code =>
        {
            if (string.IsNullOrEmpty(code))
            {
                busy = false;
                Status = "Google sunucu izni alınamadı";
                Debug.LogWarning("[PlayGames] no server auth code");
                Finish(done, false, asked ? Status : null);
                return;
            }
            StartCoroutine(PostCode(code, answer =>
            {
                busy = false;
                if (answer == null || !answer.ok)
                {
                    Status = answer != null && answer.error.Length > 0 ? answer.error : "Google girişi tamamlanamadı";
                    Finish(done, false, asked ? Status : null);
                    return;
                }
                PlayerPrefs.SetString("zm_google_pid", currentPid);
                PlayerPrefs.SetString("zm_google_acc", OnlineService.AccountId);
                PlayerPrefs.Save();
                Status = "";
                string message = answer.restored
                    ? "Google hesabın bulundu: oyuncu kodun " + OnlineService.FriendCode(OnlineService.AccountId) + " geri geldi"
                    : "Hesabın Google hesabına bağlandı";
                // a message at start-up only when something changed (the old account came back)
                Finish(done, true, asked || answer.restored ? message : null);
            }));
        });
    }
#endif

    /// <summary>Answer of POST /account/google on the game server (Server/orchestrator.py).</summary>
    [System.Serializable]
    private class GoogleAnswer
    {
        public bool ok;
        public string id = "";
        public string secret = "";   // only when this phone gets a key to another account (or a new one)
        public string name = "";
        public bool restored;        // the Google player's old account came back to this phone
        public bool banned;
        public string error = "";
    }

    /// <summary>Sends the server auth code with this phone's account (when it has one). Kept out of OnlineService so
    /// the network code, and with it the online version every installed APK must match, stays the same.</summary>
    private System.Collections.IEnumerator PostCode(string code, System.Action<GoogleAnswer> done)
    {
        var gm = GameManager.Instance;
        string name = gm != null ? gm.profile.playerName : "Oyuncu";
        string body = "{\"code\":\"" + Esc(code) + "\",\"name\":\"" + Esc(name) + "\",\"device\":\"" + Esc(SystemInfo.deviceModel) +
                      "\",\"version\":\"" + Esc(NetGame.BuildVersion) + "\"}";
        string url = OnlineService.WebPage("/account/google");
        GoogleAnswer answer = null;
        bool connectionError;
        using (var req = new UnityEngine.Networking.UnityWebRequest(url, "POST"))
        {
            req.downloadHandler = new UnityEngine.Networking.DownloadHandlerBuffer();
            req.uploadHandler = new UnityEngine.Networking.UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(body));
            req.SetRequestHeader("Content-Type", "application/json");
            if (OnlineService.HasAccount)
            {
                req.SetRequestHeader("X-ZM-Id", OnlineService.AccountId);
                req.SetRequestHeader("X-ZM-Secret", OnlineService.SecretForHello);
            }
            req.timeout = 20;
            yield return req.SendWebRequest();
            connectionError = req.result == UnityEngine.Networking.UnityWebRequest.Result.ConnectionError;
            try { answer = JsonUtility.FromJson<GoogleAnswer>(req.downloadHandler.text); } catch (System.Exception) { }
        }
        if (answer == null)
        {
            done(new GoogleAnswer { error = connectionError ? "Sunucuya ulaşılamadı. İnternet bağlantını kontrol et." : "Google girişi tamamlanamadı" });
            yield break;
        }
        if (answer.ok && answer.id.Length > 0 && answer.secret.Length > 0)
        {
            bool switched = answer.id != OnlineService.AccountId;
            PlayerPrefs.SetString("zm_acc_id", answer.id);
            PlayerPrefs.SetString("zm_acc_secret", answer.secret);
            if (switched)
            {
                OnlineService.Social = null;           // the friends list was the other account's
                OnlineService.SocialTime = -1000f;
                if (gm != null && answer.restored && answer.name.Length >= 2)
                {
                    gm.profile.playerName = answer.name;
                    PlayerPrefs.SetString("zm_name", answer.name);
                }
            }
            PlayerPrefs.Save();
        }
        if (answer.banned)
            OnlineService.BanMessage = answer.error;
        done(answer);
    }

    private static string Esc(string s)
    {
        if (string.IsNullOrEmpty(s))
            return "";
        var sb = new System.Text.StringBuilder(s.Length + 8);
        foreach (char c in s)
        {
            if (c == '"' || c == '\\')
                sb.Append('\\').Append(c);
            else if (c < ' ')
                sb.Append("\\u").Append(((int)c).ToString("x4"));
            else
                sb.Append(c);
        }
        return sb.ToString();
    }

    private static void Finish(System.Action<bool, string> done, bool ok, string message)
    {
        if (done != null)
        {
            done(ok, message ?? "");
            return;
        }
        var gm = GameManager.Instance;
        if (!string.IsNullOrEmpty(message) && gm != null && gm.uiManager != null)
            gm.uiManager.Toast(message);
    }
}
