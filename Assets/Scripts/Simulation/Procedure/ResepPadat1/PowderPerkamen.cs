using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Lives on the powder-filled perkamen (PerkamenDosePrefab and its runtime clones).
/// Exposes the state the Step 4 pour sequence needs: whether the paper holds powder,
/// whether the player is holding it, how far it is tilted, and where powder pours from.
/// </summary>
public class PowderPerkamen : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Grab interactable on this perkamen. Auto-resolved from this object if left empty.")]
    [SerializeField] private XRGrabInteractable grab;
    [Tooltip("The powder mesh (Bubuk_Dose_Visual) that is active when this perkamen carries powder.")]
    [SerializeField] private GameObject powderVisual;
    [Tooltip("Point the powder pours from. Defaults to this transform if left empty.")]
    [SerializeField] private Transform pourOrigin;

    /// <summary>
    /// True while the perkamen carries pourable powder. Computed from the powder
    /// mesh's active state so it stays correct even when Step 3 activates the
    /// powder visual directly (via PerkamenDoseReceiver) without calling SetPowder.
    /// </summary>
    public bool HasPowder => powderVisual != null && powderVisual.activeSelf;

    /// <summary>True while the player is grabbing this perkamen.</summary>
    public bool IsHeld => grab != null && grab.isSelected;

    /// <summary>World point the powder pours from.</summary>
    public Transform PourOrigin => pourOrigin != null ? pourOrigin : transform;

    /// <summary>Angle (degrees) between this perkamen's up axis and world up. Higher = more tilted.</summary>
    public float TiltAngle => Vector3.Angle(transform.up, Vector3.up);

    private void Awake()
    {
        if (grab == null)
            grab = GetComponent<XRGrabInteractable>();

        if (pourOrigin == null)
            pourOrigin = transform;
    }

    /// <summary>Show/hide the powder mesh (and therefore <see cref="HasPowder"/>).</summary>
    public void SetPowder(bool on)
    {
        if (powderVisual != null)
            powderVisual.SetActive(on);
    }
}
