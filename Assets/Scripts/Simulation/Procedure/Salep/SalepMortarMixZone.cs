using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Validasi gerus/aduk di mortar berbasis GERAKAN nyata (bukan timer).
///
/// CATATAN: versi lama mengandalkan trigger collider zona ini, tetapi collider tersebut
/// ikut skala mortar (~45x) sehingga posisinya MELESET dari mangkuk (stamper tidak pernah
/// masuk trigger → progress mandek). Versi ini meniru StamperController: deteksi kedekatan
/// ujung stamper ke mortar via OverlapSphere, lalu akumulasi jarak gerak ujung stamper
/// saat berada di dalam mortar dan sedang dipegang. Tidak bergantung pada collider zona.
/// </summary>
[DisallowMultipleComponent]
public sealed class SalepMortarMixZone : MonoBehaviour
{
    [Header("Stamper")]
    [Tooltip("Transform stamper. Kosong = auto cari object bernama 'Stamper'.")]
    [SerializeField] private Transform stamperTransform;

    [Tooltip("Ujung stamper (child 'StamperTip'). Kosong = auto-resolve.")]
    [SerializeField] private Transform stamperTip;

    [Tooltip("Wajib stamper sedang dipegang.")]
    [SerializeField] private bool requireHeld = true;

    [Header("Deteksi kedekatan ke mortar (meniru StamperController)")]
    [Tooltip("Radius OverlapSphere dari ujung stamper untuk mendeteksi MortarController.")]
    [SerializeField] private float detectionRadius = 0.09f;
    [SerializeField] private LayerMask mortarLayerMask = ~0;

    [Header("Tuning")]
    [Tooltip("Total jarak gerakan ujung stamper (meter) untuk menyelesaikan satu fase mixing.")]
    [SerializeField] private float requiredTravelMeters = 0.9f;

    [Tooltip("Gerakan di bawah ini (meter/frame) diabaikan sebagai noise.")]
    [SerializeField] private float minMovePerFrame = 0.0008f;

    [Header("Debug (read-only)")]
    [SerializeField] private float accumulatedTravel;
    [SerializeField] private float progress01;
    [SerializeField] private bool active;
    [SerializeField] private bool stamperInside;

    private Vector3 lastTipPos;
    private bool hasLastPos;

    public float Progress01 => Mathf.Clamp01(progress01);
    public bool IsActive => active;
    public bool StamperInside => stamperInside;

    private void Awake()
    {
        // Collider zona lama (kalau ada) tidak lagi dipakai untuk deteksi; matikan agar
        // tidak memicu trigger tak terduga pada sistem lain.
        Collider col = GetComponent<Collider>();
        if (col != null)
            col.enabled = false;

        ResolveStamper();
    }

    private void ResolveStamper()
    {
        if (stamperTransform == null)
        {
            GameObject stamper = GameObject.Find("Stamper");
            if (stamper != null)
                stamperTransform = stamper.transform;
        }

        if (stamperTip == null && stamperTransform != null)
        {
            Transform tip = FindChildByName(stamperTransform, "StamperTip");
            stamperTip = tip != null ? tip : stamperTransform;
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
        {
            stamperTransform = stamper;
            stamperTip = null;
        }
        ResolveStamper();
    }

    private void Update()
    {
        if (!active)
            return;

        if (stamperTip == null)
            ResolveStamper();

        Transform tip = stamperTip != null ? stamperTip : stamperTransform;
        if (tip == null)
            return;

        if (requireHeld && !IsHeld(stamperTransform != null ? stamperTransform.gameObject : tip.gameObject))
        {
            hasLastPos = false;
            stamperInside = false;
            return;
        }

        // Apakah ujung stamper berada di dalam mortar? (sama seperti StamperController)
        stamperInside = IsInsideMortar(tip.position);
        if (!stamperInside)
        {
            hasLastPos = false;
            return;
        }

        Vector3 currentPos = tip.position;
        if (!hasLastPos)
        {
            lastTipPos = currentPos;
            hasLastPos = true;
            return;
        }

        float moved = Vector3.Distance(currentPos, lastTipPos);
        lastTipPos = currentPos;

        if (moved < minMovePerFrame)
            return;

        accumulatedTravel += moved;
        progress01 = Mathf.Clamp01(accumulatedTravel / Mathf.Max(0.05f, requiredTravelMeters));
    }

    private bool IsInsideMortar(Vector3 tipPosition)
    {
        Collider[] hits = Physics.OverlapSphere(tipPosition, detectionRadius, mortarLayerMask);
        foreach (Collider hit in hits)
        {
            if (hit == null)
                continue;
            if (hit.GetComponentInParent<MortarController>() != null)
                return true;
        }
        return false;
    }

    private static Transform FindChildByName(Transform root, string childName)
    {
        if (root == null)
            return null;
        Transform[] all = root.GetComponentsInChildren<Transform>(true);
        foreach (Transform t in all)
            if (t != null && t.name == childName)
                return t;
        return null;
    }

    private static bool IsHeld(GameObject go)
    {
        XRGrabInteractable grab = go.GetComponentInParent<XRGrabInteractable>();
        return grab != null && grab.isSelected;
    }
}
