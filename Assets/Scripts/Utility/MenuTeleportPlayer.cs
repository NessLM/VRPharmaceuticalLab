using UnityEngine;

public class MenuTeleportPlayer : MonoBehaviour
{
    [SerializeField] private Transform xrOrigin;
    [SerializeField] private Transform targetPoint;

    public void TeleportFromMenu()
    {
        if (xrOrigin == null || targetPoint == null)
        {
            Debug.LogWarning("XR Origin atau Target Point belum diisi.");
            return;
        }

        xrOrigin.position = targetPoint.position;
        xrOrigin.rotation = targetPoint.rotation;
    }
}
