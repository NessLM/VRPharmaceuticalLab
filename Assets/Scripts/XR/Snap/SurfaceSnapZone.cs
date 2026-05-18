using UnityEngine;

public class SurfaceSnapZone : MonoBehaviour
{
    [Header("Surface Reference")]
    public Transform surfacePoint;

    [Header("Snap Settings")]
    public float surfaceOffset = 0.01f;

    public float SurfaceY
    {
        get
        {
            if (surfacePoint != null)
                return surfacePoint.position.y + surfaceOffset;

            return transform.position.y + surfaceOffset;
        }
    }
}