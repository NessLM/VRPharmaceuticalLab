using UnityEngine;

[RequireComponent(typeof(Collider))]
public class LiquidReceiverZone : MonoBehaviour
{
    [SerializeField] private LiquidContainer targetContainer;
    [SerializeField] private bool allowSameContainer;
    [SerializeField] private bool debugLogs;

    public LiquidContainer TargetContainer => targetContainer;

    private void Reset()
    {
        targetContainer = GetComponentInParent<LiquidContainer>();
        EnsureTriggerCollider();
    }

    private void Awake()
    {
        if (targetContainer == null)
            targetContainer = GetComponentInParent<LiquidContainer>();

        EnsureTriggerCollider();
    }

    private void OnValidate()
    {
        EnsureTriggerCollider();
    }

    public bool CanReceiveFrom(LiquidContainer sourceContainer)
    {
        if (targetContainer == null || sourceContainer == null)
            return false;

        if (!allowSameContainer && targetContainer == sourceContainer)
            return false;

        return targetContainer.AvailableCapacityMl > 0f;
    }

    public float ReceiveFrom(LiquidContainer sourceContainer, float amountMl)
    {
        if (!CanReceiveFrom(sourceContainer) || amountMl <= 0f)
            return 0f;

        float transferredMl = sourceContainer.TryTransferTo(targetContainer, amountMl);

        if (debugLogs && transferredMl > 0f)
            Debug.Log($"{sourceContainer.name} poured {transferredMl:0.###} ml into {targetContainer.name}", this);

        return transferredMl;
    }

    private void EnsureTriggerCollider()
    {
        Collider zoneCollider = GetComponent<Collider>();
        if (zoneCollider != null)
            zoneCollider.isTrigger = true;
    }
}
