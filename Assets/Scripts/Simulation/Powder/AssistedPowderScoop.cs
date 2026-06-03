using UnityEngine;

/// <summary>
/// Retired Phase 2 helper. XR-native scoop now uses ScoopBottleTarget and SpoonScoopActivator.
/// </summary>
[DisallowMultipleComponent]
public sealed class AssistedPowderScoop : MonoBehaviour
{
    private void Awake()
    {
        enabled = false;
    }

    private void OnEnable()
    {
        enabled = false;
    }
}
