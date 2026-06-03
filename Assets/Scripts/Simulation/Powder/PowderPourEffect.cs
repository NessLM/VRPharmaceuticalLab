using UnityEngine;

/// <summary>
/// Retired pour feedback placeholder. Phase 3B keeps Difenhidramin powder contained.
/// </summary>
[DisallowMultipleComponent]
public sealed class PowderPourEffect : MonoBehaviour
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
