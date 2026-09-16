using System.Collections.Generic;
using UnityEngine;

public class LootSystem : MonoBehaviour
{
    public List<GameObject> lootCrates = new List<GameObject>();

    public void SpawnLoot()
    {
        foreach (var item in lootCrates)
        {
            if (item != null)
                Destroy(item);
        }
        lootCrates.Clear();

        for (int i = 0; i < 22; i++)
        {
            var crate = GameObject.CreatePrimitive(PrimitiveType.Cube);
            crate.name = "SupplyCrate";
            crate.transform.position = new Vector3(Random.Range(-32f, 32f), 0.8f, Random.Range(-32f, 32f));
            crate.transform.localScale = new Vector3(1.2f, 0.8f, 1.2f);
            crate.GetComponent<Renderer>().material.color = Color.yellow;
            crate.tag = "Loot";
            lootCrates.Add(crate);
        }
    }

    public void TryCollectLoot(PlayerController player)
    {
        if (player == null)
            return;

        foreach (var item in lootCrates)
        {
            if (item == null)
                continue;

            if (Vector3.Distance(player.transform.position, item.transform.position) < 2f)
            {
                player.inventory.medkits++;
                player.armor = Mathf.Min(100f, player.armor + 20f);
                Destroy(item);
            }
        }
    }
}
