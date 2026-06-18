using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

[RequireComponent(typeof(XRSimpleInteractable))]
public class StackPerkamenDispenser : MonoBehaviour
{
    [Header("Prefab yang akan muncul")]
    public XRGrabInteractable singlePerkamenPrefab;

    [Header("Tempat spawn perkamen")]
    public Transform spawnPoint;

    [Header("Setting")]
    public float spawnCooldown = 0.35f;

    [Header("Safety")]
    [Tooltip("0 berarti tidak dibatasi. Isi 2-4 untuk mencegah clone perkamen menumpuk terus di scene.")]
    [SerializeField] private int maxLiveSpawned = 0;

    private XRSimpleInteractable stackInteractable;
    private XRInteractionManager interactionManager;
    private bool busy;
    private Coroutine spawnRoutine;
    private readonly List<XRGrabInteractable> liveSpawned = new List<XRGrabInteractable>();

    private void Awake()
    {
        stackInteractable = GetComponent<XRSimpleInteractable>();
        interactionManager = stackInteractable.interactionManager;

        if (interactionManager == null)
            interactionManager = FindFirstObjectByType<XRInteractionManager>();
    }

    private void OnEnable()
    {
        if (stackInteractable == null)
            stackInteractable = GetComponent<XRSimpleInteractable>();

        stackInteractable.selectEntered.AddListener(OnStackSelected);
    }

    private void OnDisable()
    {
        if (stackInteractable != null)
            stackInteractable.selectEntered.RemoveListener(OnStackSelected);

        if (spawnRoutine != null)
        {
            StopCoroutine(spawnRoutine);
            spawnRoutine = null;
        }

        busy = false;
    }

    private void OnStackSelected(SelectEnterEventArgs args)
    {
        if (busy)
            return;

        if (singlePerkamenPrefab == null)
        {
            Debug.LogWarning("Single Perkamen Prefab belum diisi.");
            return;
        }

        PruneLiveSpawned();

        if (maxLiveSpawned > 0 && liveSpawned.Count >= maxLiveSpawned)
        {
            Debug.Log("[StackPerkamen] Batas perkamen aktif tercapai.", this);
            return;
        }

        spawnRoutine = StartCoroutine(SpawnRoutine());
    }

    private IEnumerator SpawnRoutine()
    {
        busy = true;

        Transform point = spawnPoint != null ? spawnPoint : transform;

        XRGrabInteractable newPerkamen = Instantiate(
            singlePerkamenPrefab,
            point.position,
            point.rotation
        );

        PreparePerkamen(newPerkamen);
        liveSpawned.Add(newPerkamen);

        yield return new WaitForSeconds(Mathf.Max(0.01f, spawnCooldown));

        busy = false;
        spawnRoutine = null;
    }

    private void PreparePerkamen(XRGrabInteractable perkamen)
    {
        if (perkamen == null)
            return;

        perkamen.gameObject.SetActive(true);
        perkamen.enabled = true;
        perkamen.transform.SetParent(null, true);

        TrySetTag(perkamen.gameObject, "Perkamen");

        if (interactionManager != null && perkamen.interactionManager == null)
            perkamen.interactionManager = interactionManager;

        Rigidbody rb = perkamen.GetComponent<Rigidbody>();
        if (rb == null)
            rb = perkamen.gameObject.AddComponent<Rigidbody>();

        rb.isKinematic = false;
        rb.useGravity = true;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.linearDamping = 1f;
        rb.angularDamping = 1f;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        perkamen.throwOnDetach = false;
        perkamen.forceGravityOnDetach = false;

        RefreshInteractableColliders(perkamen);

        Transform dropTrigger = perkamen.transform.Find("CTM_DropTrigger");
        if (dropTrigger != null)
            dropTrigger.gameObject.SetActive(false);

        Debug.Log("Single perkamen muncul dan siap digrab manual.");
    }

    private void RefreshInteractableColliders(XRGrabInteractable perkamen)
    {
        if (perkamen == null)
            return;

        perkamen.colliders.Clear();

        Collider[] colliders = perkamen.GetComponentsInChildren<Collider>(false);

        foreach (Collider collider in colliders)
        {
            if (collider == null)
                continue;

            if (collider.isTrigger)
                continue;

            if (!collider.gameObject.activeInHierarchy)
                continue;

            collider.enabled = true;
            perkamen.colliders.Add(collider);
        }

        Debug.Log("Collider grab perkamen aktif: " + perkamen.colliders.Count);
    }

    private void PruneLiveSpawned()
    {
        for (int i = liveSpawned.Count - 1; i >= 0; i--)
        {
            if (liveSpawned[i] == null)
                liveSpawned.RemoveAt(i);
        }
    }

    private void TrySetTag(GameObject target, string tagName)
    {
        if (target == null)
            return;

        try
        {
            target.tag = tagName;
        }
        catch (UnityException)
        {
            Debug.LogWarning($"[StackPerkamen] Tag '{tagName}' belum ada di project.", this);
        }
    }
}