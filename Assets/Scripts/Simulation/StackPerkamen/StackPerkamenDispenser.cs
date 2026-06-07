using System.Collections;
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

    private XRSimpleInteractable stackInteractable;
    private XRInteractionManager interactionManager;
    private bool busy;

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
        stackInteractable.selectEntered.AddListener(OnStackSelected);
    }

    private void OnDisable()
    {
        stackInteractable.selectEntered.RemoveListener(OnStackSelected);
    }

    private void OnStackSelected(SelectEnterEventArgs args)
    {
        if (busy) return;

        if (singlePerkamenPrefab == null)
        {
            Debug.LogWarning("Single Perkamen Prefab belum diisi di StackPerkamenDispenser.");
            return;
        }

        StartCoroutine(SpawnAndGrabRoutine(args.interactorObject));
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

        yield return new WaitForSeconds(spawnCooldown);
        busy = false;
    }

    private void PreparePerkamen(XRGrabInteractable perkamen)
    {
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
}