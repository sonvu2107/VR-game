using UnityEngine;

/// <summary>
/// Global loot table ScriptableObject.
/// Assign to enemy prefabs or to the PrefabPlacer to define which items
/// can drop and their relative weights.
/// </summary>
[CreateAssetMenu(fileName = "LootTable_", menuName = "Items/LootTable")]
public class ItemLootTableSO : ScriptableObject
{
    [System.Serializable]
    public struct LootEntry
    {
        public ItemDataSO item;
        [Min(1)] public int weight;
    }

    public LootEntry[] entries;

    /// <summary>Pick a random item using weighted probability.</summary>
    public ItemDataSO GetRandomItem()
    {
        if (entries == null || entries.Length == 0) return null;

        int totalWeight = 0;
        foreach (var e in entries)
            totalWeight += e.weight;

        int roll = Random.Range(0, totalWeight);
        int cumulative = 0;
        foreach (var e in entries)
        {
            cumulative += e.weight;
            if (roll < cumulative)
                return e.item;
        }

        return entries[^1].item;
    }
}
