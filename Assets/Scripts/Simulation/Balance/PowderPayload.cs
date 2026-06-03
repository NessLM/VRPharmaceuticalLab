using UnityEngine;

/// <summary>
/// Exposes a logical gram value for a powder-containing object placed on the balance scale.
/// WeightingZone detects this component inside its trigger area and adds gramValue to the zone total.
///
/// For objects that use HornSpoon, WeightingZone reads HornSpoon.CurrentAmountMg directly.
/// Use PowderPayload for sample trays or containers that do not have a HornSpoon component.
///
/// Attach to: Powder tray, sample container, or any non-HornSpoon object to be weighed.
/// </summary>
public class PowderPayload : MonoBehaviour
{
    [Header("Payload")]
    [Tooltip("Current powder mass in grams.")]
    [SerializeField] private float gramValue = 0f;

    /// <summary>Current gram value of this powder payload.</summary>
    public float GramValue
    {
        get => gramValue;
        set => gramValue = Mathf.Max(0f, value);
    }

    /// <summary>Sets the gram value converted from milligrams (interop with mg-based systems).</summary>
    public void SetFromMilligrams(float mg) => gramValue = Mathf.Max(0f, mg / 1000f);

    /// <summary>Adds an amount in milligrams to the current gram value.</summary>
    public void AddMilligrams(float mg) => gramValue = Mathf.Max(0f, gramValue + mg / 1000f);

    /// <summary>Resets powder to zero grams.</summary>
    public void Clear() => gramValue = 0f;

#if UNITY_EDITOR
    private void OnValidate() => gramValue = Mathf.Max(0f, gramValue);
#endif
}
