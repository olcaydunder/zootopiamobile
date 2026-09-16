using UnityEngine;

public class SafeZoneController : MonoBehaviour
{
    public float radius = 60f;
    public float minRadius = 12f;
    public float shrinkRate = 0.8f;
    public Vector3 center = Vector3.zero;
    public bool active;

    public void Init(float startRadius, float shrinkAmount)
    {
        radius = startRadius;
        shrinkRate = shrinkAmount;
        center = Vector3.zero;
        active = true;
    }

    private void Update()
    {
        if (!active)
            return;

        radius = Mathf.Max(minRadius, radius - shrinkRate * Time.deltaTime);

        if (PlayerController.LocalPlayer != null)
        {
            float distance = Vector3.Distance(PlayerController.LocalPlayer.transform.position, center);
            if (distance > radius)
                PlayerController.LocalPlayer.TakeDamage(12f * Time.deltaTime, true);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(center, radius);
    }
}
