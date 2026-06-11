using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class PerkamenSnapTrigger : MonoBehaviour
{
    [SerializeField] private Transform snapPoint;

    private bool hasSnapped = false;

    private void OnTriggerEnter(Collider other)
    {
        if (hasSnapped)
            return;

        if (!other.CompareTag("Perkamen"))
            return;

        hasSnapped = true;

        other.transform.position = snapPoint.position;
        other.transform.rotation = snapPoint.rotation;

        Rigidbody rb = other.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.useGravity = false;
            rb.isKinematic = true;
        }

        XRGrabInteractable grab = other.GetComponent<XRGrabInteractable>();
        if (grab != null)
        {
            grab.enabled = false;
        }

        Debug.Log("Kertas perkamen berhasil diletakkan di piring neraca.");
    }
}