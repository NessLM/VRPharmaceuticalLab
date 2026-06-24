using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// Kembalikan SendokTanduk (HornSpoon) ke posisi awalnya saat pengguna kehilangan/mencari
/// sendok. Mirip BalanceWeightResetter: cache pose awal di Awake, lalu ResetSpoon() melepas
/// grab, menol-kan velocity, set kinematic, teleport ke home, lalu pulihkan setting fisika.
/// Pasang pada GameObject mana saja (mis. di samping tombol reset timbangan) dan tunjuk
/// spoon-nya, atau biarkan auto-resolve via FindFirstObjectByType&lt;HornSpoon&gt;.
/// </summary>
[DisallowMultipleComponent]
public sealed class HornSpoonResetter : MonoBehaviour
{
    [Header("Target (auto-resolve jika kosong)")]
    [SerializeField] private HornSpoon spoon;

    [Header("Opsi")]
    [Tooltip("Kosongkan isi bubuk/krim pada sendok saat reset.")]
    [SerializeField] private bool clearPowderOnReset = true;

    private Transform spoonTransform;
    private Transform originalParent;
    private Vector3 homePosition;
    private Quaternion homeRotation;
    private Vector3 homeScale;
    private Rigidbody rb;
    private bool originalKinematic;
    private bool originalUseGravity;
    private bool originalDetectCollisions;
    private bool captured;

    private void Awake()
    {
        ResolveSpoon();
        CaptureHome();
    }

    private void ResolveSpoon()
    {
        if (spoon == null)
            spoon = FindFirstObjectByType<HornSpoon>(FindObjectsInactive.Include);
    }

    private void CaptureHome()
    {
        if (spoon == null)
            return;

        spoonTransform = spoon.transform;
        originalParent = spoonTransform.parent;
        homePosition = spoonTransform.position;
        homeRotation = spoonTransform.rotation;
        homeScale = spoonTransform.localScale;

        rb = spoon.GetComponent<Rigidbody>();
        originalKinematic = rb != null && rb.isKinematic;
        originalUseGravity = rb != null && rb.useGravity;
        originalDetectCollisions = rb == null || rb.detectCollisions;

        captured = true;
    }

    /// <summary>Wire tombol UI ke method ini untuk mengembalikan sendok ke posisi awal.</summary>
    public void ResetSpoon()
    {
        if (spoon == null)
        {
            ResolveSpoon();
            CaptureHome();
        }

        if (!captured || spoonTransform == null)
            return;

        StopAllCoroutines();
        StartCoroutine(ResetRoutine());
    }

    private IEnumerator ResetRoutine()
    {
        var grab = spoon.GetComponent<XRGrabInteractable>();
        ForceRelease(grab);

        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
            rb.detectCollisions = false;
        }

        spoonTransform.SetParent(originalParent, true);
        spoonTransform.position = homePosition;
        spoonTransform.rotation = homeRotation;
        spoonTransform.localScale = homeScale;

        if (clearPowderOnReset)
            spoon.ClearPowder();

        yield return new WaitForFixedUpdate();

        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.detectCollisions = originalDetectCollisions;
            rb.isKinematic = originalKinematic;
            rb.useGravity = originalUseGravity;
        }
    }

    private void ForceRelease(XRGrabInteractable grab)
    {
        if (grab == null || !grab.isSelected)
            return;

        XRInteractionManager manager = grab.interactionManager;
        if (manager == null)
            return;

        var selecting = new List<IXRSelectInteractor>(grab.interactorsSelecting);
        foreach (IXRSelectInteractor interactor in selecting)
            manager.SelectExit(interactor, grab);
    }
}