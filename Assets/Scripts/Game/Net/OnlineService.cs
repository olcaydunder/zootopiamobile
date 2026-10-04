using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Talks to the matchmaker on the game server (Server/orchestrator.py) over HTTP:
/// quick match, create a private room, find a room by its 6-digit code.
/// The server's address comes from Server/endpoint.json in the GitHub repo, so it can change
/// without a new APK (the last good address is remembered for when GitHub can't be reached).
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
        public string state = "";
        public int players;
        public string version = "";
        public string error = "";
        /// <summary>Filled in here: where to connect.</summary>
        public string host = "";
    }

    private static OnlineService runner;
    private static string host = "";
    private static int apiPort = 8080;
    private static float endpointTime = -1000f;

    private static OnlineService Runner
    {
        get
        {
            if (runner == null)
                runner = new GameObject("OnlineService").AddComponent<OnlineService>();
            return runner;
        }
    }

    public static void Quick(MatchMode mode, System.Action<MatchInfo> done)
    {
        Runner.StartCoroutine(Runner.Call("POST", "/quick?mode=" + NetProtocol.ModeName(mode), done));
    }

    public static void CreateRoom(MatchMode mode, System.Action<MatchInfo> done)
    {
        Runner.StartCoroutine(Runner.Call("POST", "/room/create?mode=" + NetProtocol.ModeName(mode), done));
    }

    public static void FindRoom(string code, System.Action<MatchInfo> done)
    {
        Runner.StartCoroutine(Runner.Call("GET", "/room/" + code + "?x=1", done));
    }

    public static void CancelAll()
    {
        if (runner != null)
            runner.StopAllCoroutines();
    }

    private IEnumerator Call(string method, string path, System.Action<MatchInfo> done)
    {
        // 1) Where is the server? (refreshed every few minutes)
        if (string.IsNullOrEmpty(host) || Time.realtimeSinceStartup - endpointTime > 300f)
        {
            var get = UnityWebRequest.Get(EndpointUrl + "?t=" + System.DateTime.UtcNow.Ticks / 600000000L);
            get.timeout = 8;
            yield return get.SendWebRequest();
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
            if (string.IsNullOrEmpty(host))
            {
                done(new MatchInfo { ok = false, error = "Oyun sunucusu henüz ayarlanmadı (ya da internet yok)" });
                yield break;
            }
        }

        // 2) Ask the matchmaker.
        string url = "http://" + host + ":" + apiPort + path + "&version=" + UnityWebRequest.EscapeURL(NetGame.BuildVersion);
        var req = new UnityWebRequest(url, method);
        req.downloadHandler = new DownloadHandlerBuffer();
        if (method == "POST")
            req.uploadHandler = new UploadHandlerRaw(new byte[0]);
        req.timeout = 12;
        yield return req.SendWebRequest();

        MatchInfo info = null;
        string text = req.downloadHandler != null ? req.downloadHandler.text : "";
        if (!string.IsNullOrEmpty(text))
        {
            try
            {
                info = JsonUtility.FromJson<MatchInfo>(text);
            }
            catch (System.Exception) { }
        }
        if (info == null)
        {
            info = new MatchInfo { ok = false };
            info.error = req.result == UnityWebRequest.Result.ConnectionError
                ? "Sunucuya ulaşılamadı. İnternet bağlantını kontrol et."
                : "Sunucu yanıt vermedi (" + req.responseCode + ")";
            endpointTime = -1000f;   // look the address up again next time
        }
        else if (!info.ok && string.IsNullOrEmpty(info.error))
            info.error = "Sunucu hatası (" + req.responseCode + ")";
        info.host = host;
        req.Dispose();
        done(info);
    }
}
