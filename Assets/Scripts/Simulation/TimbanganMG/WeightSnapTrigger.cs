using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class WeightSnapTrigger : MonoBehaviour
{
    [SerializeField] private Transform snapPoint;
    [SerializeField] private BalanceScaleVisual scaleVisual;

    private bool hasSnapped = false;

    private void OnTriggerEnter(Collider other)
    {
        XRGrabInteractable grab = other.GetComponentInParent<XRGrabInteractable>();

        if (grab == null)
            return;

        if (hasSnapped)
            return;

        if (!grab.CompareTag("Weight_CTM"))
        {
            ReturnWrongWeight(grab);
            return;
        }

        hasSnapped = true;

        Transform weightObject = grab.transform;

        grab.enabled = false;

        Rigidbody rb = weightObject.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.useGravity = false;
            rb.isKinematic = true;
        }

        weightObject.SetParent(snapPoint, false);
        weightObject.localPosition = Vector3.zero;
        weightObject.localRotation = Quaternion.identity;

        if (scaleVisual != null)
            scaleVisual.SetRightDown();

        Debug.Log("Anak timbangan CTM berhasil snap ke Plate_Right_Target.");
    }

    private void ReturnWrongWeight(XRGrabInteractable grab)
    {
        ReturnToStartPosition returner = grab.GetComponent<ReturnToStartPosition>();

        if (returner != null)
            returner.ReturnToStart();
        else
            Debug.LogWarning(grab.gameObject.name + " tidak punya ReturnToStartPosition.");
    }
}