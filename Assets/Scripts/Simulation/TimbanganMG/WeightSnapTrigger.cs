using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class WeightSnapTrigger : MonoBehaviour
{
    [SerializeField] private Transform snapPoint;

    private bool hasSnapped = false;

    private void OnTriggerEnter(Collider other)
    {
        XRGrabInteractable grab = other.GetComponentInParent<XRGrabInteractable>();

        if (grab == null)
            return;

        if (hasSnapped)
        {
            ReturnWrongWeight(grab);
            return;
        }

        if (!grab.CompareTag("Weight_CTM"))
        {
            ReturnWrongWeight(grab);
            return;
        }

        hasSnapped = true;

        Transform weightObject = grab.transform;

        weightObject.position = snapPoint.position;
        weightObject.rotation = snapPoint.rotation;

        Rigidbody rb = weightObject.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.useGravity = false;
            rb.isKinematic = true;
        }

        grab.enabled = false;

        Debug.Log("Anak timbangan CTM berhasil diletakkan.");
    }

    private void ReturnWrongWeight(XRGrabInteractable grab)
{
    ReturnToStartPosition returner = grab.GetComponent<ReturnToStartPosition>();

    if (returner != null)
    {
        returner.ReturnToStart();
    }
    else
    {
        Debug.LogWarning(grab.gameObject.name + " tidak punya ReturnToStartPosition.");
    }
}
}