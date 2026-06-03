using UnityEngine;

/// <summary>
/// Retired visual helper kept so existing scene references stay valid.
/// </summary>
[DisallowMultipleComponent]
public sealed class PowderFakeMotion : MonoBehaviour
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
