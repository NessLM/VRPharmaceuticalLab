using UnityEngine;

/// <summary>
/// Stamper (pestle) that detects proximity to a MortarController and reports
/// grinding progress based on its physical movement while inside the mortar.
/// Attach to: Stamper/Pestle GameObject.
/// Set stamperTip to a child Transform at the base of the stamper.
/// </summary>
public class StamperController : MonoBehaviour
{
    [Header("Detection")]
    [Tooltip("Child Transform at the base/tip of the stamper.")]
    [SerializeField] private Transform stamperTip;
    [SerializeField] private float detectionRadius = 0.09f;
    [SerializeField] private LayerMask mortarLayerMask = ~0;

    [Header("Grinding Calculation")]
    [Tooltip("Grinding units added per Unity unit of stamper movement.")]
    [SerializeField] private float grindingRatePerUnit = 8f;
    [Tooltip("Minimum movement per frame to count as grinding (prevents drift noise).")]
    [SerializeField] private float movementThreshold = 0.002f;

    private MortarController currentMortar;
    private Vector3 lastPosition;

    private void Start()
    {
        lastPosition = transform.position;
    }

    private void Update()
    {
        if (stamperTip == null) return;

        // Detect nearby MortarController
        currentMortar = null;
        Collider[] hits = Physics.OverlapSphere(stamperTip.position, detectionRadius, mortarLayerMask);
        foreach (var hit in hits)
        {
            var mortar = hit.GetComponent<MortarController>();

            if (mortar == null)
                mortar = hit.GetComponentInParent<MortarController>();

            if (mortar == null)
                mortar = hit.GetComponentInChildren<MortarController>();

            if (mortar != null)
            {
                currentMortar = mortar;
                break;
            }
        }

        // Calculate movement and report to mortar
        float movementDelta = Vector3.Distance(transform.position, lastPosition);
        lastPosition = transform.position;

        if (currentMortar != null && movementDelta > movementThreshold)
        {
            float grindAmount = movementDelta * grindingRatePerUnit;
            currentMortar.AddGrindingProgress(grindAmount);
        }
    }

    /// <summary>Returns true if the stamper is currently inside a mortar.</summary>
    public bool IsInsideMortar => currentMortar != null;

    /// <summary>Returns the MortarController currently being used, if any.</summary>
    public MortarController CurrentMortar => currentMortar;

    private void OnDrawGizmosSelected()
    {
        if (stamperTip == null) return;
        Gizmos.color = IsInsideMortar ? Color.green : Color.gray;
        Gizmos.DrawWireSphere(stamperTip.position, detectionRadius);
    }
}
