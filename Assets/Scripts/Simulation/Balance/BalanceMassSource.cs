using UnityEngine;

/// <summary>
/// Lightweight data-only component that stores a logical gram value for balance scale calculation.
/// Detected by WeightingZone to compute total mass on a pan.
///
/// Use on objects that DO NOT have a WeightItem component (to avoid double-counting).
/// Typical use cases:
///   - anakTimbangan5/10/20/50/100/200/500 (alongside XRGrabInteractable)
///   - Weight_1g, Weight_2g, Weight_5g from the balance weight set tray
///   - Any interactable weight that needs an educational gram value
///
/// NOTE: Rigidbody.mass is intentionally kept separate (physics only).
///       This script provides the educational/logical gram value.
///
/// Attach to: any object that should contribute a fixed gram value to a WeightingZone.
/// </summary>
public class BalanceMassSource : MonoBehaviour
{
    [Header("Mass Data")]
    [Tooltip("Logical mass in grams used for balance scale calculation. " +
             "NOT related to Rigidbody.mass — that is kept constant for VR physics stability.")]
    [SerializeField] private float grams = 1f;

    /// <summary>Logical mass in grams. Used by WeightingZone for balance computation.</summary>
    public float Grams
    {
        get => grams;
        set => grams = Mathf.Max(0f, value);
    }

#if UNITY_EDITOR
    private void OnValidate() => grams = Mathf.Max(0f, grams);
#endif
}
