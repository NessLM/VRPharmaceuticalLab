using UnityEngine;

/// <summary>
/// Retired contained-grain helper kept only for compatibility with the current scene.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class ContainedPowderVisual : MonoBehaviour
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
