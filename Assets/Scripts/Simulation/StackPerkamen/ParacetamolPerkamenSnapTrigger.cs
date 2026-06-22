using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class ParacetamolPerkamenSnapTrigger : MonoBehaviour
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

        Transform paraDropTrigger = perkamen.Find("Paracetamol_DropTrigger");

        if (paraDropTrigger != null)
        {
            paraDropTrigger.gameObject.SetActive(true);
            Debug.Log("Paracetamol_DropTrigger aktif setelah perkamen snap.");
        }
        else
        {
            Debug.LogWarning("Paracetamol_DropTrigger tidak ditemukan di singleperkamen.");
        }

        Debug.Log("Kertas perkamen Paracetamol berhasil snap ke piring kiri.");
    }
}