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
    [SerializeField] private bool includeAllSceneRigidbodies = true;
    [SerializeField] private XRGrabInteractable[] additionalInteractables;
    [SerializeField] private Transform[] additionalTransforms;

    [Header("Runtime Objects")]
    [SerializeField] private bool destroyGrabInteractablesCreatedAfterCapture = true;

    private readonly List<TransformState> initialStates = new();
    private readonly HashSet<int> capturedInstanceIds = new();
    private readonly HashSet<int> capturedGameObjectIds = new();
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
        public Collider[] colliders;
        public bool[] colliderEnabled;
        public bool[] colliderIsTrigger;
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
        capturedGameObjectIds.Clear();

        Transform[] sceneTransforms =
            FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Transform sceneTransform in sceneTransforms)
        {
            if (sceneTransform != null && sceneTransform.gameObject.scene.IsValid())
                capturedGameObjectIds.Add(sceneTransform.gameObject.GetInstanceID());
        }

        Dictionary<Transform, XRGrabInteractable> unique = new();

        if (includeAllSceneGrabInteractables)
        {
            XRGrabInteractable[] sceneGrabs =
                FindObjectsByType<XRGrabInteractable>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            foreach (XRGrabInteractable grab in sceneGrabs)
            {
                if (IsSceneObject(grab))
                    unique[grab.transform] = grab;
            }
        }

        if (includeAllSceneRigidbodies)
        {
            Rigidbody[] sceneBodies =
                FindObjectsByType<Rigidbody>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            foreach (Rigidbody body in sceneBodies)
            {
                if (IsSceneObject(body) && !unique.ContainsKey(body.transform))
                    unique.Add(body.transform, body.GetComponent<XRGrabInteractable>());
            }
        }

        if (additionalInteractables != null)
        {
            foreach (XRGrabInteractable grab in additionalInteractables)
            {
                if (IsSceneObject(grab))
                    unique[grab.transform] = grab;
            }
        }

        if (additionalTransforms != null)
        {
            foreach (Transform additionalTransform in additionalTransforms)
            {
                if (additionalTransform != null &&
                    additionalTransform.gameObject.scene.IsValid() &&
                    !unique.ContainsKey(additionalTransform))
                {
                    unique.Add(
                        additionalTransform,
                        additionalTransform.GetComponent<XRGrabInteractable>());
                }
            }
        }

        foreach (KeyValuePair<Transform, XRGrabInteractable> entry in unique)
        {
            Transform target = entry.Key;
            XRGrabInteractable grab = entry.Value;
            Rigidbody body = target.GetComponent<Rigidbody>();
            Collider[] colliders = target.GetComponentsInChildren<Collider>(true);
            bool[] colliderEnabled = new bool[colliders.Length];
            bool[] colliderIsTrigger = new bool[colliders.Length];

            for (int i = 0; i < colliders.Length; i++)
            {
                colliderEnabled[i] = colliders[i] != null && colliders[i].enabled;
                colliderIsTrigger[i] = colliders[i] != null && colliders[i].isTrigger;
            }

            initialStates.Add(new TransformState
            {
                grab = grab,
                transform = target,
                parent = target.parent,
                worldPosition = target.position,
                worldRotation = target.rotation,
                localScale = target.localScale,
                activeSelf = target.gameObject.activeSelf,
                grabEnabled = grab == null || grab.enabled,
                rigidbody = body,
                isKinematic = body != null && body.isKinematic,
                useGravity = body != null && body.useGravity,
                detectCollisions = body == null || body.detectCollisions,
                colliders = colliders,
                colliderEnabled = colliderEnabled,
                colliderIsTrigger = colliderIsTrigger
            });

            if (grab != null)
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
            if (state.transform == null)
                continue;

            if (state.grab != null)
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
            if (state.grab != null)
                state.grab.enabled = state.grabEnabled;

            if (state.colliders != null)
            {
                for (int i = 0; i < state.colliders.Length; i++)
                {
                    Collider collider = state.colliders[i];
                    if (collider == null)
                        continue;

                    collider.isTrigger = state.colliderIsTrigger[i];
                    collider.enabled = state.colliderEnabled[i];
                }
            }

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
            if (!IsSceneObject(grab) ||
                capturedInstanceIds.Contains(grab.GetInstanceID()) ||
                capturedGameObjectIds.Contains(grab.gameObject.GetInstanceID()))
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
