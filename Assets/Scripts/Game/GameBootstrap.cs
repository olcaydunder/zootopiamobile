using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

/// <summary>
/// Entry point. Created automatically when any scene loads, so the game needs no editor setup:
/// builds the map, managers, UI, player, then opens the lobby. Changing the map reloads the scene
/// and builds everything again for the new one (<see cref="SwitchMap"/>).
/// </summary>
public class GameBootstrap : MonoBehaviour
{
    public static GameBootstrap Instance;

    /// <summary>A map change is under way (the scene is reloading).</summary>
    public static bool Switching { get; private set; }
    private static bool skipTitle;
    private static System.Action afterSwitch;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        if (FindObjectOfType<GameBootstrap>() == null)
            new GameObject("GameBootstrap").AddComponent<GameBootstrap>();
    }

    private NetGame.ServerArgs serverArgs;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        serverArgs = NetGame.ParseServerArgs();
        if (serverArgs != null)
        {
            // Dedicated server (Linux, -batchmode -nographics): no screens, a steady 30 updates a second.
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 30;
            Application.runInBackground = true;
            AudioListener.volume = 0f;
            return;
        }
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
        if (serverArgs != null)
        {
            yield return ServerStart();
            yield break;
        }
        MapCatalog.Current = MapCatalog.Selected;
        var titleScreen = TitleScreen.Create();
        titleScreen.SetProgress(0.05f, "Harita verisi yükleniyor...");
        yield return null;
        yield return null;
        Step(() => MapData.Load());
        titleScreen.SetProgress(0.3f, MapCatalog.CurrentInfo.name + " kuruluyor...");
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
        if (skipTitle)
        {
            // Back from a map change: straight to the lobby, then whatever asked for the change (joining a room).
            skipTitle = false;
            titleScreen.Dismiss(player);
            Switching = false;
            var then = afterSwitch;
            afterSwitch = null;
            yield return null;
            if (then != null)
                Step(then);
        }
        else
            titleScreen.ShowTitle(player);
    }

    /// <summary>
    /// Plays on another map from now on: saves the choice and rebuilds the whole world for it (the scene is
    /// reloaded behind the loading screen). <paramref name="then"/> runs in the lobby once the new map is ready.
    /// Only from the lobby, never during a match.
    /// </summary>
    public static void SwitchMap(string id, System.Action then = null)
    {
        if (Switching || !MapCatalog.IsValid(id))
            return;
        MapCatalog.Select(id);
        if (id == MapCatalog.Current && MapData.Loaded)
        {
            if (then != null)
                then();
            return;
        }
        Switching = true;
        skipTitle = true;
        afterSwitch = then;
        OnlineService.CancelAll();
        if (NetClient.Instance != null)
            NetClient.Instance.Leave();
        SceneManager.sceneLoaded += OnReloaded;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private static void OnReloaded(Scene scene, LoadSceneMode mode)
    {
        SceneManager.sceneLoaded -= OnReloaded;
        // The old world is gone: forget everything static that pointed into it.
        Step(MapData.Unload);
        Step(World.Release);
        Step(Door.All.Clear);
        Step(UpgradeStation.ClearAll);
        Step(AbilityFx.ClearAll);
        Step(VehicleSpawns.ClearPads);
        Step(Marks.Clear);
        Step(TankDrop.Active.Clear);
        Step(AirdropCall.Active.Clear);
        MapCatalog.Current = MapCatalog.Selected;
        Resources.UnloadUnusedAssets();
        new GameObject("GameBootstrap").AddComponent<GameBootstrap>();
    }

    /// <summary>Game server: the same world as the phones, the game systems, then the network.</summary>
    private System.Collections.IEnumerator ServerStart()
    {
        Debug.Log("[Sunucu] başlıyor, sürüm " + NetGame.BuildVersion);
        yield return null;
        Step(() => MapData.Load());
        yield return null;
        Step(World.Build);
        yield return null;
        GameManager manager = null;
        Step(() => manager = new GameObject("GameManager").AddComponent<GameManager>());
        yield return null;
        Debug.Log("[Sunucu] dünya hazır: " + Door.All.Count + " kapı");
        Step(() => NetServer.Create(serverArgs));
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
