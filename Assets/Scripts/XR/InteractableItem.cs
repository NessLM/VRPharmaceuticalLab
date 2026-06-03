using DG.Tweening;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using EPOOutline;

[RequireComponent(typeof(XRGrabInteractable), typeof(Outlinable))]
public class InteractableItem : MonoBehaviour
{
    Outlinable outlinable;
    XRGrabInteractable xrGrab;
    [SerializeField] GameObject infoPanel;
    Rigidbody rb;
    Vector3 startPos;
    Vector3 startRot;

    void Start()
    {
        xrGrab = GetComponent<XRGrabInteractable>();
        xrGrab.hoverEntered.AddListener(OnHoverEnter);
        xrGrab.hoverExited.AddListener(OnHoverExit);
        xrGrab.selectEntered.AddListener(OnGrab);
        xrGrab.selectExited.AddListener(OnRelease);
        xrGrab.activated.AddListener(OnActivated);

        rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        outlinable = GetComponent<Outlinable>();
        outlinable.enabled = false;

        startPos = transform.position;
        startRot = transform.eulerAngles;

        ShowInfoPanel(false);
    }

    private void OnActivated(ActivateEventArgs arg0)
    {
        ShowInfoPanel(!infoPanel.activeSelf);
    }

    private void OnHoverExit(HoverExitEventArgs arg0)
    {
        outlinable.enabled = false;
    }

    private void OnHoverEnter(HoverEnterEventArgs arg0)
    {
        outlinable.enabled = true;
    }

    private void OnRelease(SelectExitEventArgs arg0)
    {
        Debug.Log("[InteractableItem] Released");
        // Kill active tweens before starting new ones
        DOTween.Kill(transform);

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.isKinematic = true;
        rb.useGravity = false;

        transform.DOMove(startPos, 1f).SetEase(Ease.OutCubic);
        transform.DORotate(startRot, 1f).SetEase(Ease.OutCubic);
    }

    private void OnGrab(SelectEnterEventArgs arg0)
    {
        Debug.Log("[InteractableItem] Grabbed");
        // Kill active return tweens when grabbed again
        DOTween.Kill(transform);

        rb.isKinematic = false;
        rb.useGravity = false; // XRI controls movement; gravity causes unwanted drops
        ShowInfoPanel(false);
        outlinable.enabled = false;
    }

    void ShowInfoPanel(bool isEnable)
    {
        if (infoPanel != null)
            infoPanel.SetActive(isEnable);
    }
}
