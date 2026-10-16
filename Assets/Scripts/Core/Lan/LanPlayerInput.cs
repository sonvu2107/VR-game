using UnityEngine;

/// <summary>Inputs for player two are set only by the Host's authenticated connection.</summary>
public sealed class LanPlayerInput : MonoBehaviour
{
    public LanInputFrame Frame { get; private set; }
    public void Apply(LanInputFrame frame)
    {
        LanInputFrame merged = Frame;
        merged.MergeEdges(frame);
        Frame = merged;
    }
    public void ResetInput() => Frame = default;
    private void LateUpdate()
    {
        LanInputFrame frame = Frame;
        frame.ClearEdges();
        Frame = frame;
    }
}
