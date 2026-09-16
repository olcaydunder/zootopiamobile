using UnityEngine;

public class BotAgent : MonoBehaviour
{
    public float health = 100f;
    public float armor = 35f;
    public float moveSpeed = 4.2f;
    public float attackRange = 20f;
    public bool isDead;

    public CharacterController controller;
    public WeaponController weapon;
    private Vector3 moveDir;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        if (controller == null)
            controller = gameObject.AddComponent<CharacterController>();

        var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        body.transform.SetParent(transform, false);
        body.transform.localScale = new Vector3(0.8f, 1f, 0.8f);
        body.GetComponent<Renderer>().material.color = new Color(1f, 0.45f, 0.2f);

        var weaponObj = new GameObject("BotWeapon");
        weaponObj.transform.SetParent(transform, false);
        weaponObj.transform.localPosition = new Vector3(0.5f, 0.0f, 1f);

        weapon = weaponObj.AddComponent<WeaponController>();
        weapon.Initialize(WeaponData.CreateRifle());

        if (GameManager.Instance != null)
            GameManager.Instance.RegisterBot(this);
    }

    private void Update()
    {
        if (GameManager.Instance == null || GameManager.Instance.currentState != GameState.InGame || isDead)
            return;

        if (PlayerController.LocalPlayer == null)
            return;

        Vector3 targetPos = PlayerController.LocalPlayer.transform.position;
        Vector3 dir = targetPos - transform.position;
        dir.y = 0f;
        float distance = dir.magnitude;

        if (distance > 0.1f)
        {
            if (distance > attackRange)
            {
                moveDir = dir.normalized;
                controller.Move(moveDir * moveSpeed * Time.deltaTime);
            }
            else
            {
                Vector3 fireDir = (targetPos - transform.position).normalized;
                if (weapon != null)
                    weapon.TryFire(transform.position + Vector3.up, fireDir, out _);
            }
        }
    }

    public void TakeDamage(float amount, bool armorFirst)
    {
        if (isDead)
            return;

        if (armorFirst && armor > 0f)
        {
            float absorbed = Mathf.Min(armor, amount * 0.7f);
            armor -= absorbed;
            amount -= absorbed;
        }

        health -= amount;
        if (health <= 0f)
        {
            health = 0f;
            isDead = true;
            gameObject.SetActive(false);
        }
    }
}
