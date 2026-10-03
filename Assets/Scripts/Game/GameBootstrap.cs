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

        Application.targetFrameRate = 60;
        Screen.sleepTimeout = SleepTimeout.NeverSleep;
        Input.simulateMouseWithTouches = false;
        Input.multiTouchEnabled = true;

        EnsureEventSystem();
        World.Build();

        var manager = new GameObject("GameManager").AddComponent<GameManager>();
        var player = new GameObject("Player").AddComponent<PlayerController>();
        manager.RegisterPlayer(player);
        GameSettings.Load();
        manager.JoinLobby();
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
