using UnityEngine;

/// <summary>
/// Keeps a world-space label facing the camera. Put it on any TextMeshPro (3D)
/// object that should always be readable.
/// </summary>
public class Billboard : MonoBehaviour
{
    [Tooltip("Leave empty to use Camera.main.")]
    public Transform target;

    [Tooltip("Keep the label upright instead of tilting with the camera.")]
    public bool lockUpright = true;

    private void LateUpdate()
    {
        if (target == null)
        {
            if (Camera.main == null) return;
            target = Camera.main.transform;
        }

        Vector3 dir = transform.position - target.position;
        if (lockUpright) dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return;

        transform.rotation = Quaternion.LookRotation(dir);
    }
}
