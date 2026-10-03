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

    /// <summary>Builds everything over a few frames behind the loading screen, then shows the title.</summary>
    private System.Collections.IEnumerator Start()
    {
        var titleScreen = TitleScreen.Create();
        titleScreen.SetProgress(0.05f, "Harita verisi yükleniyor...");
        yield return null;
        yield return null;
        MapData.Load();
        titleScreen.SetProgress(0.3f, "Çekmeköy kuruluyor...");
        yield return null;
        World.Build();
        titleScreen.SetProgress(0.75f, "Oyuncular hazırlanıyor...");
        yield return null;

        var manager = new GameObject("GameManager").AddComponent<GameManager>();
        var player = new GameObject("Player").AddComponent<PlayerController>();
        manager.RegisterPlayer(player);
        PostFx.Setup(player.playerCamera);
        Grass.Create();
        GameSettings.Load();
        titleScreen.SetProgress(0.95f, "Lobi açılıyor...");
        yield return null;
        manager.JoinLobby();
        titleScreen.SetProgress(1f, "Hazır");
        yield return new WaitForSecondsRealtime(0.4f);
        titleScreen.ShowTitle(player);
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
