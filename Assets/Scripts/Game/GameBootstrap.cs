using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Entry point. Created automatically when any scene loads, so the game needs no editor setup:
/// builds the island, managers, UI, player, then opens the lobby.
/// </summary>
public class GameBootstrap : MonoBehaviour
{
    public static GameBootstrap Instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        if (FindObjectOfType<GameBootstrap>() == null)
            new GameObject("GameBootstrap").AddComponent<GameBootstrap>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        ErrorReporter.Install();   // first, so start-up errors are caught too

        Application.targetFrameRate = 60;
        Screen.sleepTimeout = SleepTimeout.NeverSleep;
        Input.simulateMouseWithTouches = false;
        Input.multiTouchEnabled = true;

        EnsureEventSystem();
    }

    /// <summary>Builds everything over a few frames behind the loading screen, then shows the title.
    /// Each step is guarded: an error is reported (hata modu) and start-up carries on.</summary>
    private System.Collections.IEnumerator Start()
    {
        var titleScreen = TitleScreen.Create();
        titleScreen.SetProgress(0.05f, "Harita verisi yükleniyor...");
        yield return null;
        yield return null;
        Step(() => MapData.Load());
        titleScreen.SetProgress(0.3f, "Çekmeköy kuruluyor...");
        yield return null;
        Step(World.Build);
        titleScreen.SetProgress(0.75f, "Oyuncular hazırlanıyor...");
        yield return null;

        GameManager manager = null;
        PlayerController player = null;
        Step(() =>
        {
            manager = new GameObject("GameManager").AddComponent<GameManager>();
            player = new GameObject("Player").AddComponent<PlayerController>();
            manager.RegisterPlayer(player);
        });
        Step(() => PostFx.Setup(player != null ? player.playerCamera : null));
        Step(Grass.Create);
        Step(GameSettings.Load);
        titleScreen.SetProgress(0.95f, "Lobi açılıyor...");
        yield return null;
        Step(() => { if (manager != null) manager.JoinLobby(); });
        titleScreen.SetProgress(1f, "Hazır");
        yield return new WaitForSecondsRealtime(0.4f);
        titleScreen.ShowTitle(player);
    }

    private static void Step(System.Action action)
    {
        try
        {
            action();
        }
        catch (System.Exception e)
        {
            Debug.LogException(e);
        }
    }

    private static void EnsureEventSystem()
    {
        if (FindObjectOfType<EventSystem>() != null)
            return;
        var es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>();
        es.AddComponent<StandaloneInputModule>();
    }
}
