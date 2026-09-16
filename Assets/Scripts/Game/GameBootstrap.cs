using UnityEngine;

public class GameBootstrap : MonoBehaviour
{
    public static GameBootstrap Instance;

    private GameManager gameManager;
    private bool initialized;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoCreate()
    {
        if (FindObjectOfType<GameBootstrap>() == null)
        {
            var obj = new GameObject("GameBootstrap");
            obj.AddComponent<GameBootstrap>();
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (!initialized)
            InitializeProject();
    }

    private void InitializeProject()
    {
        initialized = true;

        if (GameManager.Instance == null)
        {
            var managerObj = new GameObject("GameManager");
            managerObj.transform.SetParent(transform);
            gameManager = managerObj.AddComponent<GameManager>();
        }
        else
        {
            gameManager = GameManager.Instance;
        }

        BuildIsland();
        BuildPlayer();
        BuildBots();

        if (GameManager.Instance != null)
            GameManager.Instance.JoinLobby(MatchMode.Solo);
    }

    private void BuildIsland()
    {
        var island = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        island.name = "Island";
        island.transform.position = new Vector3(0f, -2.6f, 0f);
        island.transform.localScale = new Vector3(38f, 2.5f, 38f);
        island.GetComponent<Renderer>().material.color = new Color(0.32f, 0.62f, 0.29f);

        var ocean = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        ocean.name = "Ocean";
        ocean.transform.position = new Vector3(0f, -5.5f, 0f);
        ocean.transform.localScale = new Vector3(46f, 0.8f, 46f);
        ocean.GetComponent<Renderer>().material.color = new Color(0.12f, 0.42f, 0.68f);

        for (int i = 0; i < 30; i++)
        {
            var trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            trunk.name = "TreeTrunk";
            trunk.transform.localScale = new Vector3(0.5f, 2f, 0.5f);
            trunk.transform.position = new Vector3(Random.Range(-30f, 30f), 1.5f, Random.Range(-30f, 30f));
            trunk.GetComponent<Renderer>().material.color = new Color(0.25f, 0.15f, 0.08f);

            var canopy = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            canopy.transform.localScale = new Vector3(1.5f, 1.4f, 1.5f);
            canopy.transform.position = trunk.transform.position + new Vector3(0f, 2f, 0f);
            canopy.GetComponent<Renderer>().material.color = new Color(0.08f, 0.58f, 0.18f);
        }
    }

    private void BuildPlayer()
    {
        if (FindObjectOfType<PlayerController>() != null)
            return;

        var player = new GameObject("Player");
        player.transform.position = new Vector3(0f, 1.2f, 0f);
        player.AddComponent<PlayerController>();
    }

    private void BuildBots()
    {
        if (FindObjectsOfType<BotAgent>().Length > 0)
            return;

        for (int i = 0; i < 14; i++)
        {
            var bot = new GameObject("Bot_" + i);
            bot.transform.position = new Vector3(Random.Range(-18f, 18f), 1.2f, Random.Range(-18f, 18f));
            bot.AddComponent<CharacterController>();
            bot.AddComponent<BotAgent>();
        }
    }
}
