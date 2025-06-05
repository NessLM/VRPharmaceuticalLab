using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using EPOOutline;

[RequireComponent(typeof(XRGrabInteractable), typeof(Outlinable))]
public class SimulasiPenumbukController : MonoBehaviour
{
    public Transform attachPoint; // Tambahan: untuk menentukan posisi pegang

    Outlinable outlinable;
    XRGrabInteractable xrGrab;
    Rigidbody rb;
    Vector3 startPos;
    Vector3 startRot;

    bool isReturning = false;
    float returnSpeed = 2f;
    float returnProgress = 0f;

    void Start()
    {
        xrGrab = GetComponent<XRGrabInteractable>();

        // Set attach transform jika tersedia
        if (attachPoint != null)
        {
            xrGrab.attachTransform = attachPoint;
        }

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
    }

    void Update()
    {
        if (isReturning)
        {
            returnProgress += Time.deltaTime * returnSpeed;

            transform.position = Vector3.Lerp(transform.position, startPos, returnProgress);
            transform.rotation = Quaternion.Lerp(transform.rotation, Quaternion.Euler(startRot), returnProgress);

            if (returnProgress >= 1f)
            {
                isReturning = false;
                returnProgress = 0f;
                rb.isKinematic = true;
            }
        }
    }

    private void OnActivated(ActivateEventArgs arg0)
    {
        // infoPanel logic removed
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
        isReturning = true;
        returnProgress = 0f;
    }

    private void OnGrab(SelectEnterEventArgs arg0)
    {
        Debug.Log("Grab");
        rb.isKinematic = false;
        isReturning = false;
        outlinable.enabled = false;
    }
}
