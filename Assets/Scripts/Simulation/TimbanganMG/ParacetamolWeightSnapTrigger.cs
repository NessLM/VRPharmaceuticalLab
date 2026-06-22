using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class ParacetamolWeightSnapTrigger : MonoBehaviour
{
    [SerializeField] private Transform target3g;
    [SerializeField] private Transform target500mg;
    [SerializeField] private BalanceScaleVisual scaleVisual;

    private bool has3g = false;
    private bool has500mg = false;

    private void OnTriggerEnter(Collider other)
    {
        XRGrabInteractable grab = other.GetComponentInParent<XRGrabInteractable>();
        if (grab == null) return;

        Debug.Log("Yang masuk trigger Para: " + grab.gameObject.name + " | Tag: " + grab.tag);

        if (grab.CompareTag("Weight_Para_3g"))
        {
            if (has3g)
                return;

            has3g = true;
            SnapWeight(grab, target3g);
            Debug.Log("Anak timbangan Paracetamol 3g masuk.");
        }
        else if (grab.CompareTag("Weight_Para_500mg"))
        {
            if (has500mg)
                return;

            has500mg = true;
            SnapWeight(grab, target500mg);
            Debug.Log("Anak timbangan Paracetamol 500mg masuk.");
        }
        else
        {
            ReturnWrongWeight(grab);
            return;
        }

        if (has3g && has500mg)
        {
            Debug.Log("Anak timbangan Paracetamol lengkap.");

            if (scaleVisual != null)
                scaleVisual.SetRightDown();
        }
    }

    private void SnapWeight(XRGrabInteractable grab, Transform target)
    {
        if (target == null)
        {
            Debug.LogWarning("Target snap Paracetamol belum diisi.");
            return;
        }

        grab.enabled = false;

        Rigidbody rb = grab.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.useGravity = false;
            rb.isKinematic = true;
        }

        grab.transform.SetParent(target, false);
        grab.transform.localPosition = Vector3.zero;
        grab.transform.localRotation = Quaternion.identity;
    }

    private void ReturnWrongWeight(XRGrabInteractable grab)
    {
        ReturnToStartPosition returner = grab.GetComponent<ReturnToStartPosition>();

        if (returner != null)
            returner.ReturnToStart();
        else
            Debug.LogWarning(grab.gameObject.name + " salah dan tidak punya ReturnToStartPosition.");
    }
}