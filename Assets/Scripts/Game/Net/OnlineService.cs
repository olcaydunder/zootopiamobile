using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Talks to the game server's HTTP side (Server/orchestrator.py): the player account, matchmaking
/// (quick match, private room, find a room by its 6-digit code), friends, blocking, reports and bug
/// reports. The server's address comes from Server/endpoint.json in the GitHub repo, so it can change
/// without a new APK (the last good address is remembered for when GitHub can't be reached).
/// Every request after registration carries the account (X-ZM-Id / X-ZM-Secret).
/// </summary>
public class OnlineService : MonoBehaviour
{
    public const string EndpointUrl = "https://raw.githubusercontent.com/olcaydunder/zootopiamobile/main/Server/endpoint.json";

    [System.Serializable]
    private class EndpointJson
    {
        public string host = "";
        public int port = 8080;
    }

    [System.Serializable]
    public class MatchInfo
    {
        public bool ok;
        public int port;
        public string code = "";
        public string mode = "";
        public string kind = "";
        public string map = "";
        public string state = "";
        public int players;
        public string version = "";
        public string error = "";
        public bool banned;
        /// <summary>Filled in here: where to connect.</summary>
        public string host = "";
    }

    [System.Serializable]
    private class RegisterAnswer
    {
        public bool ok;
        public string id = "";
        public string secret = "";
        public string error = "";
    }

    [System.Serializable]
    public class Person
    {
        public string id = "";
        public string name = "";
        public bool online;
        public string status = "";   // "", "lobby", "room", "match"
        public string room = "";
        public string mode = "";
    }

    [System.Serializable]
    public class SocialView
    {
        public bool ok;
        public string error = "";
        public string message = "";
        public bool banned;
        public Person me;
        public Person[] friends = new Person[0];
        public Person[] incoming = new Person[0];
        public Person[] outgoing = new Person[0];
        public Person[] invites = new Person[0];
        public Person[] blocked = new Person[0];
        public int unread;       // unread messages
        public int gifts;        // gifts waiting to be opened
        public int followers;
        public int following;
    }

    /// <summary>A player in search results and follow lists.</summary>
    [System.Serializable]
    public class Player
    {
        public string id = "";
        public string name = "";
        public bool online;
        public bool friend;
        public bool followed;     // I follow them
        public bool followsMe;
        public int followers;
    }

    /// <summary>Answers that can carry an error message (filled in here when the request failed).</summary>
    public interface IAnswer
    {
        void Fail(string message);
    }

    [System.Serializable]
    public class PlayersView : IAnswer
    {
        public void Fail(string message) { ok = false; error = message; }
        public bool ok;
        public string error = "";
        public Player[] people = new Player[0];
        public Player[] following = new Player[0];
        public Player[] followers = new Player[0];
    }

    [System.Serializable]
    public class TopEntry
    {
        public string id = "";
        public string name = "";
        public int kills, wins, matches;
    }

    [System.Serializable]
    public class TopView : IAnswer
    {
        public void Fail(string message) { ok = false; error = message; }
        public bool ok;
        public string error = "";
        public TopEntry[] top = new TopEntry[0];
        public int myRank, myKills;
    }

    [System.Serializable]
    public class ChatThread
    {
        public string id = "";
        public string name = "";
        public bool online;
        public string last = "";
        public bool mine;
        public long time;
        public int unread;
    }

    [System.Serializable]
    public class Message
    {
        public long id;
        public bool mine;
        public string text = "";
        public long time;
    }

    [System.Serializable]
    public class ChatView : IAnswer
    {
        public void Fail(string message) { ok = false; error = message; }
        public bool ok;
        public string error = "";
        public ChatThread[] threads = new ChatThread[0];
        public Player with;
        public bool canTalk;
        public Message[] messages = new Message[0];
    }

    [System.Serializable]
    public class Gift
    {
        public long id;
        public string from = "";
        public string name = "";
        public string kind = "";    // credits, box, skin, camo
        public string item = "";
        public int amount;
        public string note = "";
        public long time;
    }

    [System.Serializable]
    public class GiftsView : IAnswer
    {
        public void Fail(string message) { ok = false; error = message; }
        public bool ok;
        public string error = "";
        public string message = "";
        public Gift[] gifts = new Gift[0];
        public Gift claimed;
    }

    private static OnlineService runner, socialRunner;
    /// <summary>This runner's requests in flight (matchmaking ones can be cancelled, social ones are not touched).</summary>
    private readonly List<UnityWebRequest> active = new List<UnityWebRequest>();
    private static string host = "";
    private static int apiPort = 8080;
    private static float endpointTime = -1000f;
    private static bool registering;
    private bool registeringHere;   // this runner's Register() is the one in flight

    /// <summary>The latest friends / invites / blocked list (refreshed by <see cref="RefreshSocial"/>).</summary>
    public static SocialView Social;
    public static float SocialTime = -1000f;
    /// <summary>Set when the server says this account is banned (shown instead of playing).</summary>
    public static string BanMessage = "";

    public static string AccountId { get { return PlayerPrefs.GetString("zm_acc_id", ""); } }
    private static string AccountSecret { get { return PlayerPrefs.GetString("zm_acc_secret", ""); } }
    public static bool HasAccount { get { return AccountId.Length > 0 && AccountSecret.Length > 0; } }

    /// <summary>Account ids are shown as "ABC-123" (friend code).</summary>
    public static string FriendCode(string id)
    {
        return id != null && id.Length == 6 ? id.Substring(0, 3) + "-" + id.Substring(3) : id ?? "";
    }

    private static OnlineService Runner
    {
        get
        {
            if (runner == null)
                runner = new GameObject("OnlineService").AddComponent<OnlineService>();
            return runner;
        }
    }

    private static OnlineService SocialRunner
    {
        get
        {
            if (socialRunner == null)
                socialRunner = new GameObject("OnlineSocial").AddComponent<OnlineService>();
            return socialRunner;
        }
    }

    // ----- Matchmaking -----

    public static void Quick(MatchMode mode, System.Action<MatchInfo> done)
    {
        Runner.StartCoroutine(Runner.Match("POST", "/quick?mode=" + NetProtocol.ModeName(mode) + "&map=" + MapCatalog.Current, done));
    }

    public static void CreateRoom(MatchMode mode, System.Action<MatchInfo> done)
    {
        Runner.StartCoroutine(Runner.Match("POST", "/room/create?mode=" + NetProtocol.ModeName(mode) + "&map=" + MapCatalog.Current, done));
    }

    public static void FindRoom(string code, System.Action<MatchInfo> done)
    {
        Runner.StartCoroutine(Runner.Match("GET", "/room/" + code + "?x=1", done));
    }

    /// <summary>Stops matchmaking requests (the friends list keeps working).</summary>
    public static void CancelAll()
    {
        if (runner == null)
            return;
        runner.StopAllCoroutines();
        foreach (var r in runner.active)
        {
            try { r.Abort(); r.Dispose(); } catch (System.Exception) { }
        }
        runner.active.Clear();
    }

    private IEnumerator Match(string method, string path, System.Action<MatchInfo> done)
    {
        long status = 0;
        string text = null, error = null;
        yield return Request(method, path, null, (s, t, e) => { status = s; text = t; error = e; });
        MatchInfo info = null;
        if (!string.IsNullOrEmpty(text))
        {
            try { info = JsonUtility.FromJson<MatchInfo>(text); } catch (System.Exception) { }
        }
        if (info == null)
            info = new MatchInfo { ok = false, error = error ?? ("Sunucu yanıt vermedi (" + status + ")") };
        else if (!info.ok && string.IsNullOrEmpty(info.error))
            info.error = "Sunucu hatası (" + status + ")";
        if (info.banned)
            BanMessage = info.error;
        info.host = host;
        done(info);
    }

    // ----- Social -----

    /// <summary>Friends, requests, invites and blocked players; also tells the server where we are (lobby / room code).</summary>
    public static void RefreshSocial(string status, string room, MatchMode mode, System.Action<SocialView> done)
    {
        string body = "{\"status\":\"" + status + "\",\"room\":\"" + Safe(room) + "\",\"mode\":\"" + NetProtocol.ModeName(mode) + "\"}";
        SocialRunner.StartCoroutine(SocialRunner.SocialCall("/friends", body, done));
    }

    public static void AddFriend(string code, System.Action<SocialView> done) { SocialPost("/friends/add", code, null, done); }
    public static void AcceptFriend(string id, System.Action<SocialView> done) { SocialPost("/friends/accept", id, null, done); }
    public static void RemoveFriend(string id, System.Action<SocialView> done) { SocialPost("/friends/remove", id, null, done); }
    public static void DismissInvite(string id, System.Action<SocialView> done) { SocialPost("/friends/dismiss", id, null, done); }
    public static void Block(string id, System.Action<SocialView> done) { SocialPost("/block", id, null, done); }
    public static void Unblock(string id, System.Action<SocialView> done) { SocialPost("/unblock", id, null, done); }

    public static void Invite(string id, string room, MatchMode mode, System.Action<SocialView> done)
    {
        SocialPost("/friends/invite", id, ",\"room\":\"" + Safe(room) + "\",\"mode\":\"" + NetProtocol.ModeName(mode) + "\"", done);
    }

    /// <summary>reason: cheat, abuse, voice, name, afk, other.</summary>
    public static void ReportPlayer(string id, string reason, string text, string match, System.Action<SocialView> done)
    {
        SocialPost("/report/player", id, ",\"reason\":\"" + Safe(reason) + "\",\"text\":\"" + Json(text) + "\",\"match\":\"" + Safe(match) + "\"", done);
    }

    // ----- Following, search, messages, gifts -----

    public static void Search(string text, System.Action<PlayersView> done)
    {
        Api("/search", "{\"q\":\"" + Json(text) + "\"}", done);
    }

    public static void Follow(string id, bool follow, System.Action<PlayersView> done)
    {
        Api(follow ? "/follow" : "/unfollow", "{\"id\":\"" + Safe(id) + "\"}", done);
    }

    public static void Follows(System.Action<PlayersView> done) { Api("/follows", "{}", done); }

    public static void Threads(System.Action<ChatView> done) { Api("/msg/threads", "{}", done); }

    /// <summary>The conversation with a player (only messages after <paramref name="after"/> when given); marks it read.</summary>
    public static void Thread(string id, long after, System.Action<ChatView> done)
    {
        Api("/msg/thread", "{\"id\":\"" + Safe(id) + "\",\"after\":" + after + "}", done);
    }

    public static void SendMessage(string id, string text, System.Action<ChatView> done)
    {
        Api("/msg/send", "{\"id\":\"" + Safe(id) + "\",\"text\":\"" + Json(text) + "\"}", done);
    }

    /// <summary>kind: credits (amount), box (item = crate id), skin (item = skin), camo (item = "w:id", "v:id" or "p:id").</summary>
    public static void SendGift(string id, string kind, string item, int amount, string note, System.Action<GiftsView> done)
    {
        Api("/gift/send", "{\"id\":\"" + Safe(id) + "\",\"kind\":\"" + Safe(kind) + "\",\"item\":\"" + Json(item) +
            "\",\"amount\":" + amount + ",\"note\":\"" + Json(note) + "\"}", done);
    }

    public static void GiftInbox(System.Action<GiftsView> done) { Api("/gift/inbox", "{}", done); }

    /// <summary>Today's best players (most eliminations, online and against bots) and where this player is.</summary>
    public static void TopToday(System.Action<TopView> done) { Api("/stats/top", "{}", done); }

    /// <summary>A finished match for today's leaderboard (fire and forget).</summary>
    public static void PostMatchStats(int kills, bool won)
    {
        Api<SocialAnswer>("/stats/match", "{\"kills\":" + Mathf.Clamp(kills, 0, 99) + ",\"won\":" + (won ? "true" : "false") + "}", null);
    }

    [System.Serializable]
    private class SocialAnswer : IAnswer
    {
        public void Fail(string message) { ok = false; error = message; }
        public bool ok;
        public string error = "";
    }

    public static void ClaimGift(long giftId, System.Action<GiftsView> done)
    {
        Api("/gift/claim", "{\"gift\":" + giftId + "}", done);
    }

    /// <summary>A social request answered with a view of type T (ok / error fields); never cancelled by CancelAll.</summary>
    private static void Api<T>(string path, string body, System.Action<T> done) where T : class, IAnswer, new()
    {
        SocialRunner.StartCoroutine(SocialRunner.ApiCall(path, body, done));
    }

    private IEnumerator ApiCall<T>(string path, string body, System.Action<T> done) where T : class, IAnswer, new()
    {
        long status = 0;
        string text = null, error = null;
        yield return Request("POST", path, body, (s2, t, e) => { status = s2; text = t; error = e; });
        T view = null;
        if (!string.IsNullOrEmpty(text))
        {
            try { view = JsonUtility.FromJson<T>(text); } catch (System.Exception) { }
        }
        if (view == null)
        {
            view = new T();
            view.Fail(error ?? ("Sunucu yanıt vermedi (" + status + ")"));
        }
        if (done != null)
            done(view);
    }

    public static void BugReport(string text, string log, System.Action<SocialView> done)
    {
        string body = "{\"text\":\"" + Json(text) + "\",\"log\":\"" + Json(log) + "\",\"device\":\"" + Json(SystemInfo.deviceModel + " / " + SystemInfo.operatingSystem) +
                      "\",\"version\":\"" + Json(NetGame.BuildVersion + " " + Application.version) + "\"}";
        SocialRunner.StartCoroutine(SocialRunner.SocialCall("/bug", body, done));
    }

    private static void SocialPost(string path, string id, string extra, System.Action<SocialView> done)
    {
        string body = "{\"id\":\"" + Safe(id) + "\"" + (extra ?? "") + "}";
        SocialRunner.StartCoroutine(SocialRunner.SocialCall(path, body, done));
    }

    private IEnumerator SocialCall(string path, string body, System.Action<SocialView> done)
    {
        long status = 0;
        string text = null, error = null;
        yield return Request("POST", path, body, (s, t, e) => { status = s; text = t; error = e; });
        SocialView view = null;
        if (!string.IsNullOrEmpty(text))
        {
            try { view = JsonUtility.FromJson<SocialView>(text); } catch (System.Exception) { }
        }
        if (view == null)
            view = new SocialView { ok = false, error = error ?? ("Sunucu yanıt vermedi (" + status + ")") };
        if (view.banned)
            BanMessage = view.error;
        if (view.ok && view.me != null && !string.IsNullOrEmpty(view.me.id) && view.friends != null)
        {
            Social = view;
            SocialTime = Time.realtimeSinceStartup;
        }
        if (done != null)
            done(view);
    }

    // ----- Plumbing -----

    private static string Safe(string s)
    {
        var sb = new System.Text.StringBuilder();
        foreach (char c in s ?? "")
            if (char.IsLetterOrDigit(c) || c == '-' || c == '_')
                sb.Append(c);
        return sb.ToString();
    }

    private static string Json(string s)
    {
        var sb = new System.Text.StringBuilder();
        foreach (char c in s ?? "")
        {
            switch (c)
            {
                case '"': sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': break;
                case '\t': sb.Append("\\t"); break;
                default:
                    if (c < ' ')
                        sb.Append(' ');
                    else
                        sb.Append(c);
                    break;
            }
        }
        return sb.ToString();
    }

    private IEnumerator ResolveHost()
    {
        if (!string.IsNullOrEmpty(host) && Time.realtimeSinceStartup - endpointTime < 300f)
            yield break;
        var get = UnityWebRequest.Get(EndpointUrl + "?t=" + System.DateTime.UtcNow.Ticks / 600000000L);
        get.timeout = 8;
        active.Add(get);
        yield return get.SendWebRequest();
        active.Remove(get);
        if (get.result == UnityWebRequest.Result.Success)
        {
            try
            {
                var ep = JsonUtility.FromJson<EndpointJson>(get.downloadHandler.text);
                if (ep != null && !string.IsNullOrEmpty(ep.host))
                {
                    host = ep.host.Trim();
                    apiPort = ep.port > 0 ? ep.port : 8080;
                    endpointTime = Time.realtimeSinceStartup;
                    PlayerPrefs.SetString("zm_server", host + ":" + apiPort);
                }
            }
            catch (System.Exception) { }
        }
        get.Dispose();
        if (string.IsNullOrEmpty(host))
        {
            string saved = PlayerPrefs.GetString("zm_server", "");
            int colon = saved.LastIndexOf(':');
            if (colon > 0)
            {
                host = saved.Substring(0, colon);
                int.TryParse(saved.Substring(colon + 1), out apiPort);
            }
        }
    }

    private IEnumerator Send(string method, string path, string body, System.Action<UnityWebRequest> done)
    {
        string url = "http://" + host + ":" + apiPort + path + (path.Contains("?") ? "&" : "?") + "version=" + UnityWebRequest.EscapeURL(NetGame.BuildVersion);
        var req = new UnityWebRequest(url, method);
        req.downloadHandler = new DownloadHandlerBuffer();
        if (method == "POST")
        {
            req.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(body ?? ""));
            req.SetRequestHeader("Content-Type", "application/json");
        }
        if (HasAccount)
        {
            req.SetRequestHeader("X-ZM-Id", AccountId);
            req.SetRequestHeader("X-ZM-Secret", AccountSecret);
        }
        req.timeout = 12;
        active.Add(req);
        yield return req.SendWebRequest();
        active.Remove(req);
        done(req);
        req.Dispose();
    }

    /// <summary>Makes sure there is an account (registers once), then sends the request.
    /// done(status, body text or null, error message or null).</summary>
    private IEnumerator Request(string method, string path, string body, System.Action<long, string, string> done)
    {
        yield return ResolveHost();
        if (string.IsNullOrEmpty(host))
        {
            done(0, null, "Oyun sunucusu henüz ayarlanmadı (ya da internet yok)");
            yield break;
        }
        for (int attempt = 0; attempt < 2; attempt++)
        {
            if (!HasAccount)
            {
                // Registration runs on the social runner, which is never cancelled (no half-done sign-up).
                if (!registering)
                    SocialRunner.StartCoroutine(SocialRunner.Register());
                while (registering)
                    yield return null;
                if (!HasAccount)
                {
                    done(0, null, "Hesap oluşturulamadı. İnternet bağlantını kontrol et.");
                    yield break;
                }
            }
            long status = 0;
            string text = null;
            bool connectionError = false;
            yield return Send(method, path, body, r =>
            {
                status = r.responseCode;
                text = r.downloadHandler != null ? r.downloadHandler.text : null;
                connectionError = r.result == UnityWebRequest.Result.ConnectionError;
            });
            if (status == 401 && text != null && text.Contains("relogin") && attempt == 0)
            {
                // The server does not know this account (new database): make a new one once.
                PlayerPrefs.DeleteKey("zm_acc_id");
                PlayerPrefs.DeleteKey("zm_acc_secret");
                continue;
            }
            if (connectionError)
            {
                endpointTime = -1000f;   // look the address up again next time
                done(status, null, "Sunucuya ulaşılamadı. İnternet bağlantını kontrol et.");
                yield break;
            }
            done(status, text, null);
            yield break;
        }
    }

    /// <summary>A map change reloads the scene and destroys the runners mid-request: don't leave the
    /// sign-up flag set, or every later request would wait for it forever.</summary>
    private void OnDestroy()
    {
        if (registeringHere)
            registering = false;
    }

    private IEnumerator Register()
    {
        if (registering)
            yield break;
        registering = true;
        registeringHere = true;
        var gm = GameManager.Instance;
        string name = gm != null ? gm.profile.playerName : "Oyuncu";
        string body = "{\"name\":\"" + Json(name) + "\",\"device\":\"" + Json(SystemInfo.deviceModel) + "\",\"version\":\"" + Json(NetGame.BuildVersion) + "\"}";
        RegisterAnswer answer = null;
        yield return Send("POST", "/account/register", body, r =>
        {
            if (r.result == UnityWebRequest.Result.Success)
            {
                try { answer = JsonUtility.FromJson<RegisterAnswer>(r.downloadHandler.text); } catch (System.Exception) { }
            }
        });
        registering = false;
        registeringHere = false;
        if (answer != null && answer.ok && answer.id.Length > 0 && answer.secret.Length > 0)
        {
            PlayerPrefs.SetString("zm_acc_id", answer.id);
            PlayerPrefs.SetString("zm_acc_secret", answer.secret);
            PlayerPrefs.Save();
        }
    }

    /// <summary>Sends the current player name to the server (after renaming).</summary>
    public static void SyncName()
    {
        var gm = GameManager.Instance;
        if (gm == null || !HasAccount)
            return;
        string body = "{\"name\":\"" + Json(gm.profile.playerName) + "\",\"version\":\"" + Json(NetGame.BuildVersion) + "\",\"device\":\"" + Json(SystemInfo.deviceModel) + "\"}";
        SocialRunner.StartCoroutine(SocialRunner.SocialCall("/account/hello", body, null));
    }

    /// <summary>For the game server's join check: id + secret go in the UDP hello.</summary>
    public static string SecretForHello { get { return AccountSecret; } }
}
