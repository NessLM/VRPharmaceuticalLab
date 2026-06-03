using UnityEngine;

/// <summary>
/// Retired visual helper. Difenhidramin powder now uses InternalPowderMeshVisual only.
/// </summary>
[DisallowMultipleComponent]
public sealed class AnimatedPowderVisual : MonoBehaviour
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
