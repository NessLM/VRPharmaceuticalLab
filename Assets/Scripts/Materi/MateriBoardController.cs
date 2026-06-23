using UnityEngine;

/// <summary>
/// MateriBoardController inherits from MateriPanelController to maintain backward compatibility
/// and wire cleanly with existing bootstrap systems (like CairSemiPadatMenuWiring.cs).
/// All panel management, UI references, and teleport logic are handled in the base class.
/// </summary>
[DisallowMultipleComponent]
public sealed class MateriBoardController : Materi.MateriPanelController
{
    // All fields, methods, properties, and event handlers are inherited from MateriPanelController.
}
