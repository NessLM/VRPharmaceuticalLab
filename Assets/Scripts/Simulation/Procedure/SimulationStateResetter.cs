using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

[DisallowMultipleComponent]
public class SimulationStateResetter : MonoBehaviour
{
    [Header("Capture")]
    [SerializeField] private bool includeAllSceneGrabInteractables = true;
    [SerializeField] private XRGrabInteractable[] additionalInteractables;

    [Header("Runtime Objects")]
    [SerializeField] private bool destroyGrabInteractablesCreatedAfterCapture = true;

    private readonly List<TransformState> initialStates = new();
    private readonly HashSet<int> capturedInstanceIds = new();
    private bool hasCaptured;

    private sealed class TransformState
    {
        public XRGrabInteractable grab;
        public Transform transform;
        public Transform parent;
        public Vector3 worldPosition;
        public Quaternion worldRotation;
        public Vector3 localScale;
        public bool activeSelf;
        public bool grabEnabled;
        public Rigidbody rigidbody;
        public bool isKinematic;
        public bool useGravity;
        public bool detectCollisions;
    }

    private void Awake()
    {
        CaptureInitialState();
    }

    [ContextMenu("Capture Initial State")]
    public void CaptureInitialState()
    {
        initialStates.Clear();
        capturedInstanceIds.Clear();

        HashSet<XRGrabInteractable> unique = new();

        if (includeAllSceneGrabInteractables)
        {
            XRGrabInteractable[] sceneGrabs =
                FindObjectsByType<XRGrabInteractable>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            foreach (XRGrabInteractable grab in sceneGrabs)
            {
                if (IsSceneObject(grab))
                    unique.Add(grab);
            }
        }

        if (additionalInteractables != null)
        {
            foreach (XRGrabInteractable grab in additionalInteractables)
            {
                if (IsSceneObject(grab))
                    unique.Add(grab);
            }
        }

        foreach (XRGrabInteractable grab in unique)
        {
            Transform target = grab.transform;
            Rigidbody body = grab.GetComponent<Rigidbody>();

            initialStates.Add(new TransformState
            {
                grab = grab,
                transform = target,
                parent = target.parent,
                worldPosition = target.position,
                worldRotation = target.rotation,
                localScale = target.localScale,
                activeSelf = target.gameObject.activeSelf,
                grabEnabled = grab.enabled,
                rigidbody = body,
                isKinematic = body != null && body.isKinematic,
                useGravity = body != null && body.useGravity,
                detectCollisions = body == null || body.detectCollisions
            });

            capturedInstanceIds.Add(grab.GetInstanceID());
        }

        hasCaptured = true;
    }

    [ContextMenu("Reset Captured State")]
    public void ResetCapturedState()
    {
        if (!hasCaptured)
            CaptureInitialState();

        DestroyRuntimeGrabInteractables();

        foreach (TransformState state in initialStates)
        {
            if (state.grab == null || state.transform == null)
                continue;

            ForceRelease(state.grab);

            if (state.rigidbody != null)
            {
                state.rigidbody.linearVelocity = Vector3.zero;
                state.rigidbody.angularVelocity = Vector3.zero;
                state.rigidbody.isKinematic = true;
                state.rigidbody.detectCollisions = false;
            }

            state.transform.SetParent(state.parent, true);
            state.transform.SetPositionAndRotation(state.worldPosition, state.worldRotation);
            state.transform.localScale = state.localScale;
            state.transform.gameObject.SetActive(state.activeSelf);
            state.grab.enabled = state.grabEnabled;

            if (state.rigidbody != null)
            {
                state.rigidbody.linearVelocity = Vector3.zero;
                state.rigidbody.angularVelocity = Vector3.zero;
                state.rigidbody.detectCollisions = state.detectCollisions;
                state.rigidbody.isKinematic = state.isKinematic;
                state.rigidbody.useGravity = state.useGravity;
            }
        }
    }

    private void DestroyRuntimeGrabInteractables()
    {
        if (!destroyGrabInteractablesCreatedAfterCapture)
            return;

        XRGrabInteractable[] current =
            FindObjectsByType<XRGrabInteractable>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (XRGrabInteractable grab in current)
        {
            if (!IsSceneObject(grab) || capturedInstanceIds.Contains(grab.GetInstanceID()))
                continue;

            ForceRelease(grab);
            Destroy(grab.gameObject);
        }
    }

    private static bool IsSceneObject(Component component)
    {
        return component != null &&
               component.gameObject != null &&
               component.gameObject.scene.IsValid();
    }

    private static void ForceRelease(XRGrabInteractable grab)
    {
        if (grab == null || !grab.isSelected || grab.interactionManager == null)
            return;

        List<IXRSelectInteractor> interactors = new(grab.interactorsSelecting);
        foreach (IXRSelectInteractor interactor in interactors)
            grab.interactionManager.SelectExit(interactor, grab);
    }
}
