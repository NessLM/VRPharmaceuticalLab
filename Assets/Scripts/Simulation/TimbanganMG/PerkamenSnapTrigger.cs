using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class PerkamenSnapTrigger : MonoBehaviour
{
    [SerializeField] private Transform snapPoint;
    [SerializeField] private bool disableGrabAfterSnap = true;

    private bool hasSnapped = false;
    public bool HasSnapped => hasSnapped;

    private void OnTriggerEnter(Collider other)
    {
        TrySnap(other);
    }

    private void OnTriggerStay(Collider other)
    {
        TrySnap(other);
    }

    private void TrySnap(Collider other)
    {
        if (hasSnapped)
            return;

        XRGrabInteractable grab = other.GetComponentInParent<XRGrabInteractable>();
        GameObject perkamenObject = grab != null ? grab.gameObject : other.gameObject;

        bool isPerkamen = HasPerkamenTag(perkamenObject) ||
                          perkamenObject.name.IndexOf("perkamen", System.StringComparison.OrdinalIgnoreCase) >= 0;

        if (!isPerkamen)
            return;

        if (grab != null && grab.isSelected)
            return;

        hasSnapped = true;

        Transform perkamenTransform = grab != null ? grab.transform : other.transform;
        Transform target = snapPoint != null ? snapPoint : transform;

        perkamenTransform.position = target.position;
        perkamenTransform.rotation = target.rotation;

        Rigidbody rb = perkamenTransform.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.useGravity = false;
            rb.isKinematic = true;
        }

        if (disableGrabAfterSnap && grab != null)
        {
            grab.enabled = false;
        }

        Debug.Log("Kertas perkamen berhasil diletakkan di piring neraca.");
    }

    private bool HasPerkamenTag(GameObject candidate)
    {
        try
        {
            return candidate.CompareTag("Perkamen");
        }
        catch (UnityException)
        {
            return false;
        }
    }
}
