using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class PlasticBagTrigger : MonoBehaviour
{
    [SerializeField] private Step5Manager step5Manager;

    private XRGrabInteractable grab;
    private bool alreadyOpened = false;

    private void Awake()
    {
        grab = GetComponent<XRGrabInteractable>();

        if (grab != null)
            grab.selectEntered.AddListener(OnGrabbed);
    }

    private void OnDestroy()
    {
        if (grab != null)
            grab.selectEntered.RemoveListener(OnGrabbed);
    }

    private void OnGrabbed(SelectEnterEventArgs args)
    {
        if (alreadyOpened)
            return;

        alreadyOpened = true;

        Debug.Log("Plastik berhasil diambil.");

        if (step5Manager != null)
            step5Manager.StartStep5();
    }
}