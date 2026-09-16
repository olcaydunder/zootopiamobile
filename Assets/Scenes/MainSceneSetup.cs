using UnityEngine;

public class MainSceneSetup : MonoBehaviour
{
    private void Start()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.JoinLobby(MatchMode.Solo);
    }
}
