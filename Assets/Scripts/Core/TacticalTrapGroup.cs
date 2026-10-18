using UnityEngine;

// Metadata runtime để kiểm tra đường an toàn, không cần lưu vào scene/prefab.
public sealed class TacticalTrapGroup : MonoBehaviour
{
    public TacticalTrapPattern Pattern { get; private set; }
    public Vector2Int[] Cells { get; private set; }
    public float MinimumClearance { get; private set; }
    public void Initialize(TacticalTrapPattern pattern, Vector2Int[] cells, float clearance)
    { Pattern = pattern; Cells = cells; MinimumClearance = clearance; }
}
