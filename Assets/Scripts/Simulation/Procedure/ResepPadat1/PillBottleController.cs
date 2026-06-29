using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class PillBottleController : MonoBehaviour
{
    [Header("Lid Animation")]
    [SerializeField] private Transform lid;
    [SerializeField] private Transform lidOpenPoint;
    [SerializeField] private float lidMoveDuration = 0.8f;
    [SerializeField] private float lidLiftHeight = 0.25f;
    [SerializeField] private float lidHiddenDelay = 0.2f;

    [Header("Bottle Grab")]
    [SerializeField] private XRGrabInteractable bottleGrab;
    [SerializeField] private Rigidbody bottleRigidbody;

    [Header("Pill Spawning")]
    [SerializeField] private GameObject pillPrefab;
    [SerializeField] private Transform pillSpawnPoint;
    [SerializeField] private int requiredPillCount = 3;
    [SerializeField] private float spawnDelay = 0.25f;

    [Header("Pour Detection")]
    [SerializeField] private float pourAngleThreshold = 80f;

    [Header("Step 4 Auto Fill")]
    [Tooltip("Notified once the pills have finished pouring so it can show the auto-fill panel.")]
    [SerializeField] private Step4AutoFillManager autoFillManager;

    [Header("Step 4 Checklist")]
    [Tooltip("Notified the moment the bottle lid is opened so 'Buka botol kapsul' turns to [OK].")]
    [SerializeField] private Step4ChecklistManager checklistManager;

    [Header("Release Behaviour")]
    [Tooltip("If true, the bottle keeps gravity and stays where it is dropped instead of returning to its start position.")]
    [SerializeField] private bool dropAndStayOnRelease = true;

    [Header("Spawned Pill Settling")]
    [Tooltip("After a pill tumbles out, freeze it in place after this delay so it stays put and is stable for the auto-fill animation.")]
    [SerializeField] private float pillSettleDelay = 1.2f;

    private readonly List<GameObject> spawnedPills = new List<GameObject>();

private Vector3 bottleStartPosition;
private Quaternion bottleStartRotation;
private Coroutine returnRoutine;

    private bool isOpened = false;
    private bool isGrabbed = false;
    private bool isPouring = false;
    private bool hasPoured = false;

    private void Start()
    {

        bottleStartPosition = transform.position;
bottleStartRotation = transform.rotation;
        if (bottleGrab != null)
        {
            bottleGrab.enabled = false;

            bottleGrab.selectEntered.AddListener(OnBottleGrabbed);
            bottleGrab.selectExited.AddListener(OnBottleReleased);
        }

        // Awal game: botol diam dulu, tidak jatuh saat menu muncul.
        if (bottleRigidbody != null)
        {
            bottleRigidbody.useGravity = false;
            bottleRigidbody.isKinematic = true;
        }
    }

    private void OnDestroy()
    {
        if (bottleGrab != null)
        {
            bottleGrab.selectEntered.RemoveListener(OnBottleGrabbed);
            bottleGrab.selectExited.RemoveListener(OnBottleReleased);
        }
    }

    private void Update()
    {
        if (!isOpened || !isGrabbed || hasPoured || isPouring)
            return;

        float angle = Vector3.Angle(pillSpawnPoint.forward, Vector3.down);

        if (angle <= pourAngleThreshold)
        {
            StartCoroutine(SpawnPills());
        }
    }

    public void OpenBottle()
    {
        if (isOpened)
            return;

        isOpened = true;
        StartCoroutine(OpenBottleRoutine());
    }

    private IEnumerator OpenBottleRoutine()
    {
        if (lid != null && lidOpenPoint != null)
        {
            Vector3 startPosition = lid.position;
            Quaternion startRotation = lid.rotation;

            Vector3 liftPosition = startPosition + Vector3.up * lidLiftHeight;

            float elapsed = 0f;

            while (elapsed < lidMoveDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / lidMoveDuration;

                lid.position = Vector3.Lerp(startPosition, liftPosition, t);
                lid.rotation = startRotation;

                yield return null;
            }

            lid.position = liftPosition;

            lid.gameObject.SetActive(false);
            yield return new WaitForSeconds(lidHiddenDelay);

            lid.SetParent(null, true);

            lid.position = lidOpenPoint.position;
            lid.rotation = lidOpenPoint.rotation;
            lid.gameObject.SetActive(true);
        }

        if (bottleGrab != null)
        {
            bottleGrab.enabled = true;
        }

        // Setelah botol terbuka: physics botol aktif.
        if (bottleRigidbody != null)
        {
            bottleRigidbody.isKinematic = false;
            bottleRigidbody.useGravity = true;
        }

        Debug.Log("Botol terbuka. Botol sekarang bisa digrab.");

        if (checklistManager != null)
            checklistManager.CheckBottleOpened();
    }

  private void OnBottleGrabbed(SelectEnterEventArgs args)
{
    if (bottleRigidbody != null)
    {
        bottleRigidbody.linearVelocity = Vector3.zero;
        bottleRigidbody.angularVelocity = Vector3.zero;
        bottleRigidbody.useGravity = false;
        bottleRigidbody.isKinematic = true;
    }

    StartCoroutine(EnablePourAfterDelay());
}
    private IEnumerator EnablePourAfterDelay()
    {
        yield return new WaitForSeconds(0.5f);
        isGrabbed = true;
    }

    private void OnBottleReleased(SelectExitEventArgs args)
{
    isGrabbed = false;

    if (returnRoutine != null)
    {
        StopCoroutine(returnRoutine);
        returnRoutine = null;
    }

    if (dropAndStayOnRelease)
    {
        // Let the (now opened) bottle fall under gravity and stay where it lands.
        if (bottleRigidbody != null)
        {
            bottleRigidbody.isKinematic = false;
            bottleRigidbody.useGravity = true;
        }
        return;
    }

    returnRoutine = StartCoroutine(ReturnBottleToStart());
}

private IEnumerator ReturnBottleToStart()
{
    if (bottleRigidbody != null)
    {
        bottleRigidbody.linearVelocity = Vector3.zero;
        bottleRigidbody.angularVelocity = Vector3.zero;
        bottleRigidbody.useGravity = false;
        bottleRigidbody.isKinematic = true;
    }

    Vector3 startPosition = transform.position;
    Quaternion startRotation = transform.rotation;

    float duration = 0.5f;
    float elapsed = 0f;

    while (elapsed < duration)
    {
        elapsed += Time.deltaTime;
        float t = elapsed / duration;

        transform.position = Vector3.Lerp(startPosition, bottleStartPosition, t);
        transform.rotation = Quaternion.Slerp(startRotation, bottleStartRotation, t);

        yield return null;
    }

    transform.position = bottleStartPosition;
    transform.rotation = bottleStartRotation;
}   

    private IEnumerator SpawnPills()
    {
        isPouring = true;
        spawnedPills.Clear();

        for (int i = 0; i < requiredPillCount; i++)
        {
            SpawnOnePill();
            yield return new WaitForSeconds(spawnDelay);
        }

        hasPoured = true;
        isPouring = false;

        Debug.Log("Pil keluar sesuai jumlah resep.");

        // Pills are out -> show the auto-fill panel for the capsule animation step.
        // Pass a snapshot copy so the manager never enumerates the live list.
        if (autoFillManager != null)
        {
            autoFillManager.OnPillsPoured(new List<GameObject>(spawnedPills));
        }
    }

    private void SpawnOnePill()
    {
        if (pillPrefab == null || pillSpawnPoint == null)
        {
            Debug.LogWarning("Pill Prefab atau Pill Spawn Point belum diisi.");
            return;
        }

        GameObject newPill = Instantiate(
            pillPrefab,
            pillSpawnPoint.position,
            pillSpawnPoint.rotation
        );

        newPill.SetActive(true);
        spawnedPills.Add(newPill);

        Rigidbody rb = newPill.GetComponent<Rigidbody>();
        if (rb != null)
        {
            // Make sure the pill actually falls under gravity. A tiny nudge frees it
            // from the bottle mouth; a little damping keeps it from drifting away.
            rb.isKinematic = false;
            rb.useGravity = true;
            rb.linearDamping = 0.3f;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.AddForce(pillSpawnPoint.forward * 0.04f, ForceMode.Impulse);
            StartCoroutine(SettlePill(rb));
        }
    }

    private IEnumerator SettlePill(Rigidbody rb)
    {
        // Let the pill fall and actually land before freezing it. Freezing on a
        // fixed timer (the old behaviour) caught pills still in mid-air, making
        // them look like they were floating/flying. Instead we wait until the pill
        // is resting (low velocity / asleep) and only then lock it in place.
        if (rb == null)
            yield break;

        // Give it a moment to leave the bottle mouth first.
        yield return new WaitForSeconds(Mathf.Max(0.2f, pillSettleDelay * 0.25f));

        float maxWait = 6f;
        float elapsed = 0f;
        while (rb != null && elapsed < maxWait)
        {
            XRGrabInteractable held = rb.GetComponent<XRGrabInteractable>();
            if (held != null && held.isSelected)
                yield break; // player picked it up - leave physics alone

            if (rb.IsSleeping() || rb.linearVelocity.magnitude < 0.04f)
                break; // it has come to rest on the surface

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (rb == null)
            yield break;

        XRGrabInteractable grabNow = rb.GetComponent<XRGrabInteractable>();
        if (grabNow != null && grabNow.isSelected)
            yield break;

        // Lock the rested pill so it stays put and is stable for the next step.
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.useGravity = false;
        rb.isKinematic = true;
    }
}