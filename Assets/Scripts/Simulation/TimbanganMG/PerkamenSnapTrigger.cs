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

        XRGrabInteractable grab = other.GetComponentInParent<XRGrabInteractable>();

        if (grab == null)
            return;

        if (!grab.CompareTag("Perkamen"))
            return;

        hasSnapped = true;

        Transform perkamen = grab.transform;

        grab.enabled = false;

        Rigidbody rb = perkamen.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.useGravity = false;
            rb.isKinematic = true;
        }

        perkamen.SetParent(snapPoint, false);
        perkamen.localPosition = Vector3.zero;
        perkamen.localRotation = Quaternion.identity;

        Debug.Log("Kertas perkamen berhasil snap ke piring kiri.");
    }
}