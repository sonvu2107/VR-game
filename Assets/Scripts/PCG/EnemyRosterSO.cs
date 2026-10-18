using UnityEngine;

[CreateAssetMenu(menuName = "The Labyrinth/Additional Enemy Roster")]
public sealed class EnemyRosterSO : ScriptableObject
{
    [System.Serializable]
    public sealed class Entry
    {
        public GameObject prefab;
        public int firstLevel = 2, lastLevel = 10;
        public bool introduceInWideRoom;
    }
    public Entry[] entries;
    public GameObject Choose(GameObject original, int level, int combatIndex, int mobIndex, bool wide, SeededRandom random)
    {
        if (entries == null) return original;
        int eligible = 0;
        foreach (var entry in entries)
        {
            if (entry == null || entry.prefab == null || level < entry.firstLevel || level > entry.lastLevel) continue;
            if (level == entry.firstLevel && mobIndex == 1 &&
                (entry.introduceInWideRoom ? wide : combatIndex == 0)) return entry.prefab;
            eligible++;
        }
        if (eligible == 0 || random == null || random.Range(0, 3) != 0) return original;
        int choice = random.Range(0, eligible);
        foreach (var entry in entries)
            if (entry != null && entry.prefab != null && level >= entry.firstLevel && level <= entry.lastLevel && choice-- == 0)
                return entry.prefab;
        return original;
    }
}
