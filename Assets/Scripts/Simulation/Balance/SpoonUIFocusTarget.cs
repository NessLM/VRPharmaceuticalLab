using UnityEngine;

/// <summary>
/// Marker component. Attach to sendokTanduk to designate it as a valid focus target
/// for the spoon info panel toggle.
///
/// When the controller ray hits sendokTanduk's Collider OR the player is holding
/// the spoon (XRGrabInteractable selected), pressing B will open/close the spoon panel.
/// </summary>
public class SpoonUIFocusTarget : MonoBehaviour { }
