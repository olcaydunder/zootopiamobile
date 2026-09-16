using System.Collections;
using UnityEngine;

public class Matchmaker : MonoBehaviour
{
    public MatchMode selectedMode = MatchMode.Solo;
    public int queueCount;
    private bool matching;

    public void BeginMatching(MatchMode mode)
    {
        selectedMode = mode;
        matching = true;
        StartCoroutine(QueueRoutine());
    }

    private IEnumerator QueueRoutine()
    {
        queueCount = 0;
        while (matching)
        {
            queueCount += Random.Range(1, 5);
            yield return new WaitForSeconds(0.8f);

            if (queueCount >= 24)
            {
                matching = false;
                GameManager.Instance.BeginRound();
                yield break;
            }
        }
    }
}
