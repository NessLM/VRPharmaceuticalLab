using UnityEngine;

[RequireComponent(typeof(Collider))]
public class MortarWaterIntakeZone : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private MortarController mortar;
    [SerializeField] private Transform receiverPoint;

    [Header("Detection")]
    [SerializeField] private float gizmoRadius = 0.12f;

    [Header("Debug")]
    [SerializeField] private bool debugLogs;
    [SerializeField] private float lastAcceptedMl;

    private Collider triggerCollider;

    // Dimatikan selama prosedur SALEP supaya tuangan Etanol tidak terserap ke sistem air
    // Sirup (yang bisa memunculkan visual mixture sirup di mortar). Dipulihkan saat reset.
    private bool suppressed;

    public Transform ReceiverPoint => receiverPoint != null ? receiverPoint : transform;

    /// <summary>Matikan/aktifkan penerimaan cairan (dipakai Salep agar tidak bentrok Sirup).</summary>
    public void SetSuppressed(bool value)
    {
        suppressed = value;
    }

    private void Awake()
    {
        EnsureCollider();

        if (mortar == null)
            mortar = GetComponentInParent<MortarController>();

        if (receiverPoint == null)
            receiverPoint = transform;
    }

    public void Configure(MortarController targetMortar, Transform targetPoint)
    {
        mortar = targetMortar;
        receiverPoint = targetPoint != null ? targetPoint : transform;
        EnsureCollider();
    }

    public bool CanReceiveFrom(LiquidContainer source)
    {
        if (suppressed || mortar == null || source == null || source.IsEmpty)
            return false;

        return mortar.RemainingWaterMlForCurrentPhase > 0.001f;
    }

    public float ReceiveFrom(LiquidContainer source, float requestedMl)
    {
        if (!CanReceiveFrom(source) || requestedMl <= 0f)
            return 0f;

        float acceptedMl = mortar.AddWaterMl(Mathf.Min(requestedMl, source.CurrentMl));
        if (acceptedMl <= 0.001f)
            return 0f;

        source.RemoveLiquid(acceptedMl);
        lastAcceptedMl = acceptedMl;

        if (debugLogs)
            Debug.Log($"[MortarWaterIntakeZone] Received {acceptedMl:0.###} ml from {source.name}. Mortar water = {mortar.CurrentWaterMl:0.###} ml", this);

        return acceptedMl;
    }

    private void EnsureCollider()
    {
        triggerCollider = GetComponent<Collider>();
        if (triggerCollider == null)
        {
            SphereCollider sphere = gameObject.AddComponent<SphereCollider>();
            sphere.radius = 0.0027f;
            triggerCollider = sphere;
        }

        triggerCollider.isTrigger = true;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        gizmoRadius = Mathf.Max(0.01f, gizmoRadius);
    }

    private void OnDrawGizmosSelected()
    {
        Transform point = receiverPoint != null ? receiverPoint : transform;

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(point.position, gizmoRadius);
    }
#endif
}
