using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Validasi gerus/aduk di mortar berbasis GERAKAN nyata (bukan timer).
///
/// Saat aktif, jika stamper yang dipegang bergerak di dalam zona mortar, Progress01
/// naik sebanding jarak gerakan. SalepProcedureManager menganggap step mixing selesai
/// saat Progress01 mencapai 1. Ini memenuhi syarat "user benar-benar melakukan mixing,
/// bukan sekadar menyentuh".
/// </summary>
[RequireComponent(typeof(Collider))]
[DisallowMultipleComponent]
public sealed class SalepMortarMixZone : MonoBehaviour
{
    [Header("Stamper")]
    [Tooltip("Transform stamper. Kosong = auto cari object bernama 'Stamper'.")]
    [SerializeField] private Transform stamperTransform;

    [Tooltip("Wajib stamper sedang dipegang.")]
    [SerializeField] private bool requireHeld = true;

    [Header("Tuning")]
    [Tooltip("Total jarak gerakan stamper (meter) untuk menyelesaikan satu fase mixing.")]
    [SerializeField] private float requiredTravelMeters = 1.2f;

    [Tooltip("Gerakan di bawah ini (meter/frame) diabaikan sebagai noise.")]
    [SerializeField] private float minMovePerFrame = 0.0005f;

    [Header("Debug (read-only)")]
    [SerializeField] private float accumulatedTravel;
    [SerializeField] private float progress01;
    [SerializeField] private bool active;
    [SerializeField] private bool stamperInside;

    private Vector3 lastStamperPos;
    private bool hasLastPos;

    public float Progress01 => Mathf.Clamp01(progress01);
    public bool IsActive => active;

    private void Awake()
    {
        Collider col = GetComponent<Collider>();
        if (col != null)
            col.isTrigger = true;

        if (stamperTransform == null)
        {
            GameObject stamper = GameObject.Find("Stamper");
            if (stamper != null)
                stamperTransform = stamper.transform;
        }
    }

    public void SetActive(bool value)
    {
        active = value;
        hasLastPos = false;
        stamperInside = false;
    }

    public void ResetZone()
    {
        accumulatedTravel = 0f;
        progress01 = 0f;
        hasLastPos = false;
        stamperInside = false;
    }

    public void ConfigureStamper(Transform stamper)
    {
        if (stamper != null)
            stamperTransform = stamper;
    }

    private void OnTriggerStay(Collider other)
    {
        if (!active || stamperTransform == null)
            return;

        if (!IsStamper(other))
            return;

        if (requireHeld && !IsHeld(stamperTransform.gameObject))
        {
            hasLastPos = false;
            return;
        }

        stamperInside = true;

        Vector3 currentPos = stamperTransform.position;
        if (!hasLastPos)
        {
            lastStamperPos = currentPos;
            hasLastPos = true;
            return;
        }

        float moved = Vector3.Distance(currentPos, lastStamperPos);
        lastStamperPos = currentPos;

        if (moved < minMovePerFrame)
            return;

        accumulatedTravel += moved;
        progress01 = Mathf.Clamp01(accumulatedTravel / Mathf.Max(0.05f, requiredTravelMeters));
    }

    private void OnTriggerExit(Collider other)
    {
        if (IsStamper(other))
        {
            stamperInside = false;
            hasLastPos = false;
        }
    }

    private bool IsStamper(Collider other)
    {
        if (stamperTransform == null || other == null)
            return false;

        Transform t = other.transform;
        while (t != null)
        {
            if (t == stamperTransform)
                return true;
            t = t.parent;
        }
        return false;
    }

    private static bool IsHeld(GameObject go)
    {
        XRGrabInteractable grab = go.GetComponentInParent<XRGrabInteractable>();
        return grab != null && grab.isSelected;
    }
}
