using UnityEngine;

/// <summary>
/// Drops a random item from a loot table when an enemy dies.
/// Attach to any enemy prefab and populate the item pool.
/// </summary>
public class ItemDropper : MonoBehaviour
{
    [Header("Drop Settings")]
    [Tooltip("Chance to drop an item when killed (0–1).")]
    [Range(0f, 1f)]
    public float dropChance = 0.35f;

    [Tooltip("ItemDataSO assets that may be dropped.")]
    public ItemDataSO[] lootTable;

    [Tooltip("Prefab with an ItemPickup component and a SpriteRenderer.")]
    public GameObject itemPickupPrefab;

    /// <summary>
    /// Call from Enemy.Die(). Will randomly spawn an item at the given position.
    /// </summary>
    public void TryDrop(Vector3 position)
    {
        if (lootTable == null || lootTable.Length == 0) return;
        if (itemPickupPrefab == null) return;

        if (Random.value > dropChance) return;

        ItemDataSO chosen = lootTable[Random.Range(0, lootTable.Length)];

        GameObject go = Instantiate(itemPickupPrefab, position, Quaternion.identity);
        ItemPickup pickup = go.GetComponent<ItemPickup>();
        if (pickup != null)
            pickup.itemData = chosen;
    }
}
