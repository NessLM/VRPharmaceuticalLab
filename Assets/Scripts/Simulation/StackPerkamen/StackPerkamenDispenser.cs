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
    public float spawnCooldown = 0.15f;
    public bool forceGrabAfterSpawn = true;

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
        {
            interactionManager = FindFirstObjectByType<XRInteractionManager>();
        }
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
            Debug.LogWarning("Single Perkamen Prefab belum diisi di StackPerkamenDispenser.");
            return;
        }

        PruneLiveSpawned();
        if (maxLiveSpawned > 0 && liveSpawned.Count >= maxLiveSpawned)
        {
            Debug.Log("[StackPerkamen] Batas perkamen aktif tercapai. Pakai perkamen yang sudah muncul dulu.", this);
            return;
        }

        spawnRoutine = StartCoroutine(SpawnAndGrabRoutine(args.interactorObject));
    }

    private IEnumerator SpawnAndGrabRoutine(IXRSelectInteractor interactor)
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

        yield return null;

        if (forceGrabAfterSpawn && interactionManager != null && interactor != null)
        {
            if (stackInteractable.isSelected)
            {
                interactionManager.SelectExit(interactor, stackInteractable);
            }

            yield return null;

            if (newPerkamen != null && newPerkamen.isActiveAndEnabled)
            {
                interactionManager.SelectEnter(interactor, newPerkamen);
            }
        }

        yield return new WaitForSeconds(Mathf.Max(0.01f, spawnCooldown));
        busy = false;
        spawnRoutine = null;
    }

    private void PreparePerkamen(XRGrabInteractable perkamen)
    {
        if (perkamen == null)
            return;

        perkamen.transform.SetParent(null, true);
        TrySetTag(perkamen.gameObject, "Perkamen");

        Rigidbody rb = perkamen.GetComponent<Rigidbody>();

        if (rb == null)
        {
            rb = perkamen.gameObject.AddComponent<Rigidbody>();
        }

        rb.useGravity = false;
        rb.isKinematic = false;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.linearDamping = 8f;
        rb.angularDamping = 8f;

        perkamen.throwOnDetach = false;

        if (perkamen.GetComponent<PerkamenNoGravity>() == null)
        {
            perkamen.gameObject.AddComponent<PerkamenNoGravity>();
        }
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
            Debug.LogWarning($"[StackPerkamen] Tag '{tagName}' belum ada di project. Snap tetap pakai nama object sebagai fallback.", this);
        }
    }
}
