using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Mengunci kemampuan grab Mortar sampai prosedur benar-benar membutuhkannya.
///
/// Masalah yang diatasi: saat pemain meraih Stamper, kadang yang ter-grab malah Mortar
/// (karena Mortar punya collider "Grab Assist" besar yang menutupi area Stamper). Gate ini
/// menonaktifkan XRGrabInteractable + collider grab-assist Mortar secara default, dan hanya
/// mengaktifkannya saat dipanggil oleh procedure manager (mis. langkah menuang/akhir).
/// </summary>
[DisallowMultipleComponent]
public sealed class MortarGrabGate : MonoBehaviour
{
    [Tooltip("XRGrabInteractable Mortar. Auto-resolve dari GameObject ini bila kosong.")]
    [SerializeField] private XRGrabInteractable grab;

    [Tooltip("Collider trigger 'Grab Assist' Mortar (opsional). Auto-resolve dari nama.")]
    [SerializeField] private Collider grabAssistCollider;

    [Tooltip("Apakah Mortar boleh di-grab saat scene mulai (default: tidak).")]
    [SerializeField] private bool grabbableAtStart = false;

    public bool IsGrabbable { get; private set; }

    private void Awake()
    {
        ResolveReferences();
        SetGrabbable(grabbableAtStart);
    }

    private void ResolveReferences()
    {
        if (grab == null)
            grab = GetComponent<XRGrabInteractable>();

        if (grabAssistCollider == null)
        {
            foreach (Collider c in GetComponentsInChildren<Collider>(true))
            {
                if (c != null && c.gameObject.name.IndexOf("Grab Assist", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    grabAssistCollider = c;
                    break;
                }
            }
        }
    }

    /// <summary>Aktif/nonaktifkan kemampuan grab Mortar. Aman dipanggil berulang.</summary>
    public void SetGrabbable(bool value)
    {
        IsGrabbable = value;

        if (grab != null && grab.enabled != value)
            grab.enabled = value;

        // Collider grab-assist hanya berguna saat boleh grab; matikan saat terkunci supaya
        // tidak "mencuri" raih pemain yang sebenarnya menuju Stamper.
        if (grabAssistCollider != null && grabAssistCollider.enabled != value)
            grabAssistCollider.enabled = value;
    }
}
