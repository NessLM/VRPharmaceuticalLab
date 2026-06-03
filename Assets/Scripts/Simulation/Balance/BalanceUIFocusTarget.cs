using UnityEngine;

/// <summary>
/// Marker component. Attach to timbanganNeraca (or a child with a Collider)
/// to designate it as a valid focus target for the balance panel toggle.
///
/// When the controller ray hits a Collider whose hierarchy contains this
/// component, pressing B will open/close the balance lesson panel.
/// </summary>
public class BalanceUIFocusTarget : MonoBehaviour { }
