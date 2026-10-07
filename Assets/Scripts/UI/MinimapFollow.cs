using UnityEngine;

public class MinimapFollow : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private float z = -10f;

    private void LateUpdate()
    {
        if (target == null)
        {
            PlayerMovement player = FindObjectOfType<PlayerMovement>();
            if (player == null) return;
            target = player.transform;
        }

        Vector3 pos = target.position;
        pos.z = z;
        transform.position = pos;
    }
}