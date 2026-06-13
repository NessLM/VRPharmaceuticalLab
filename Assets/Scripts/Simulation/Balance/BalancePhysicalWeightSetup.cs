using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Scene-level setup for the balance weights in VRLabSimulation.
/// Keeps the editable objects in the hierarchy, then normalizes their physics at runtime.
/// </summary>
public class BalancePhysicalWeightSetup : MonoBehaviour
{
    [Header("Targets")]
    [SerializeField] private string[] nameContains = { "Weight_", "anakTimbangan" };
    [SerializeField] private bool includeAllSceneWeightObjects = true;

    [Header("Runtime Components")]
    [SerializeField] private bool addRigidbodyWhenMissing = true;
    [SerializeField] private bool addColliderWhenMissing = true;
    [SerializeField] private bool addGrabInteractableWhenMissing = true;
    [SerializeField] private bool addMassSourceWhenMissing = true;

    [Header("Free Physics")]
    [SerializeField] private bool useGravityWhenFree = true;
    [SerializeField] private bool keepKinematicWhenReleased = false;
    [SerializeField] private float defaultPhysicsMassKg = 0.05f;
    [SerializeField] private Vector3 fallbackColliderSize = new Vector3(0.04f, 0.025f, 0.04f);
    [SerializeField] private float minimumWorldGrabSize = 0.06f;

    private void Awake()
    {
        ApplySetup();
    }

    private void Start()
    {
        ApplySetup();
    }

    [ContextMenu("Apply Weight Setup")]
    public void ApplySetup()
    {
        HashSet<GameObject> processedObjects = new HashSet<GameObject>();
        SetupWeightsInRoot(transform, processedObjects);

        if (!includeAllSceneWeightObjects)
            return;

        Transform[] sceneTransforms = Resources.FindObjectsOfTypeAll<Transform>();
        foreach (Transform sceneTransform in sceneTransforms)
        {
            if (sceneTransform == null || sceneTransform.gameObject == null)
                continue;

            if (!sceneTransform.gameObject.scene.IsValid())
                continue;

            TrySetupWeight(sceneTransform, processedObjects);
        }
    }

    private void SetupWeightsInRoot(Transform root, HashSet<GameObject> processedObjects)
    {
        if (root == null)
            return;

        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        foreach (Transform child in children)
        {
            if (child == null || child == root)
                continue;

            TrySetupWeight(child, processedObjects);
        }
    }

    private void TrySetupWeight(Transform candidate, HashSet<GameObject> processedObjects)
    {
        if (candidate == null || candidate.gameObject == null)
            return;

        if (!IsWeightCandidate(candidate.name))
            return;

        if (processedObjects != null && !processedObjects.Add(candidate.gameObject))
            return;

        SetupWeight(candidate.gameObject);
    }

    private void SetupWeight(GameObject weightObject)
    {
        if (weightObject == null)
            return;

        EnsureCollider(weightObject);
        NormalizeColliders(weightObject);

        Rigidbody rb = weightObject.GetComponent<Rigidbody>();
        if (rb == null && addRigidbodyWhenMissing)
            rb = weightObject.AddComponent<Rigidbody>();

        if (rb != null)
        {
            rb.mass = Mathf.Max(0.001f, defaultPhysicsMassKg);
            rb.isKinematic = keepKinematicWhenReleased;
            rb.useGravity = useGravityWhenFree && !keepKinematicWhenReleased;
            rb.linearDamping = 2f;
            rb.angularDamping = 2f;
            rb.collisionDetectionMode = keepKinematicWhenReleased
                ? CollisionDetectionMode.ContinuousSpeculative
                : CollisionDetectionMode.ContinuousDynamic;
        }

        XRGrabInteractable grabInteractable = weightObject.GetComponent<XRGrabInteractable>();
        if (grabInteractable == null && addGrabInteractableWhenMissing)
            grabInteractable = weightObject.AddComponent<XRGrabInteractable>();

        if (grabInteractable != null)
            ConfigureGrabInteractable(weightObject, grabInteractable);

        WeightItem weightItem = weightObject.GetComponent<WeightItem>();
        if (weightItem != null)
        {
            weightItem.ConfigureReleasedPhysics(useGravityWhenFree, keepKinematicWhenReleased);
            return;
        }

        if (!addMassSourceWhenMissing)
            return;

        BalanceMassSource massSource = weightObject.GetComponent<BalanceMassSource>();
        if (massSource == null)
            massSource = weightObject.AddComponent<BalanceMassSource>();

        if (TryParseGrams(weightObject.name, out float grams))
            massSource.Grams = grams;
    }

    private void EnsureCollider(GameObject weightObject)
    {
        if (!addColliderWhenMissing || weightObject.GetComponentInChildren<Collider>(true) != null)
            return;

        BoxCollider box = weightObject.AddComponent<BoxCollider>();
        box.isTrigger = false;
        box.size = fallbackColliderSize;
        box.center = Vector3.zero;
    }

    private void NormalizeColliders(GameObject weightObject)
    {
        Collider[] colliders = weightObject.GetComponentsInChildren<Collider>(true);
        foreach (Collider collider in colliders)
        {
            if (collider == null)
                continue;

            collider.enabled = true;
            collider.isTrigger = false;

            if (collider is BoxCollider box)
                EnsureMinimumBoxSize(box);
        }
    }

    private void EnsureMinimumBoxSize(BoxCollider box)
    {
        Vector3 lossyScale = box.transform.lossyScale;
        Vector3 safeScale = new Vector3(
            Mathf.Max(Mathf.Abs(lossyScale.x), 0.0001f),
            Mathf.Max(Mathf.Abs(lossyScale.y), 0.0001f),
            Mathf.Max(Mathf.Abs(lossyScale.z), 0.0001f));

        Vector3 worldSize = Vector3.Scale(box.size, safeScale);
        Vector3 size = box.size;

        if (worldSize.x < minimumWorldGrabSize)
            size.x = minimumWorldGrabSize / safeScale.x;
        if (worldSize.y < minimumWorldGrabSize)
            size.y = minimumWorldGrabSize / safeScale.y;
        if (worldSize.z < minimumWorldGrabSize)
            size.z = minimumWorldGrabSize / safeScale.z;

        box.size = size;
    }

    private void ConfigureGrabInteractable(GameObject weightObject, XRGrabInteractable grabInteractable)
    {
        Collider[] colliders = weightObject.GetComponentsInChildren<Collider>(true);

        grabInteractable.colliders.Clear();
        foreach (Collider collider in colliders)
        {
            if (collider != null && collider.enabled && !collider.isTrigger)
                grabInteractable.colliders.Add(collider);
        }

        grabInteractable.throwOnDetach = false;
    }

    private bool IsWeightCandidate(string objectName)
    {
        if (string.IsNullOrEmpty(objectName) || nameContains == null)
            return false;

        foreach (string marker in nameContains)
        {
            if (!string.IsNullOrEmpty(marker) &&
                objectName.IndexOf(marker, StringComparison.OrdinalIgnoreCase) >= 0 &&
                TryParseGrams(objectName, out _))
                return true;
        }

        return false;
    }

    private bool TryParseGrams(string objectName, out float grams)
    {
        grams = 0f;
        if (string.IsNullOrEmpty(objectName))
            return false;

        string normalized = objectName.Replace(" ", string.Empty);
        int markerIndex = normalized.IndexOf("Weight_", StringComparison.OrdinalIgnoreCase);
        string valueText;

        if (markerIndex >= 0)
        {
            valueText = normalized.Substring(markerIndex + "Weight_".Length);
        }
        else
        {
            markerIndex = normalized.IndexOf("anakTimbangan", StringComparison.OrdinalIgnoreCase);
            if (markerIndex < 0)
                return false;

            valueText = normalized.Substring(markerIndex + "anakTimbangan".Length);
        }

        bool isMilligram = valueText.EndsWith("mg", StringComparison.OrdinalIgnoreCase);
        if (isMilligram)
            valueText = valueText.Substring(0, valueText.Length - 2);
        else if (valueText.EndsWith("g", StringComparison.OrdinalIgnoreCase))
            valueText = valueText.Substring(0, valueText.Length - 1);

        if (!float.TryParse(valueText, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out float parsed))
            return false;

        grams = isMilligram ? parsed / 1000f : parsed;
        return grams > 0f;
    }
}
