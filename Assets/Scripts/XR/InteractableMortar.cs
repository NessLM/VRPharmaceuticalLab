using DG.Tweening;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using EPOOutline;

[RequireComponent(typeof(XRGrabInteractable), typeof(Outlinable))]
public class InteractableMortar : MonoBehaviour
{
    Outlinable outlinable;
    XRGrabInteractable xrGrab;
    [SerializeField] GameObject infoPanel;
    Rigidbody rb;
    Vector3 startPos;
    Vector3 startRot;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
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

        outlinable = GetComponent<Outlinable>();
        outlinable.enabled = false;

        startPos = transform.position;
        startRot = transform.eulerAngles;

        ShowInfoPanel(false);
    }

    private void OnActivated(ActivateEventArgs arg0)
    {
        // toggle panel manual dengan tombol trigger
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
        Debug.Log("Release");
        rb.isKinematic = true;

        // panel hilang otomatis saat dilepas
        ShowInfoPanel(false);

        transform.DOMove(startPos, 1).SetEase(Ease.OutCubic);
        transform.DORotate(startRot, 1).SetEase(Ease.OutCubic);
    }

    private void OnGrab(SelectEnterEventArgs arg0)
    {
        Debug.Log("Grab");
        rb.isKinematic = false;

        // panel muncul otomatis saat digrab
        ShowInfoPanel(true);

        outlinable.enabled = false;
    }

    void ShowInfoPanel(bool isEnable)
    {
        if (infoPanel != null)
        {
            infoPanel.SetActive(isEnable);

            // kalau panel diaktifkan, isi dulu datanya
            if (isEnable)
            {
                var dataHandler = infoPanel.GetComponent<InteractableMortarDataHandler>();
                if (dataHandler != null)
                {
                    dataHandler.Initialize();
                }
            }
        }
    }
}
