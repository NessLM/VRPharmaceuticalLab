using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Controls pill dispensing from a bottle.
/// Works alongside BottleLid: pills only pour when lid IsOpen.
///
/// Setup:
///   - Assign bottleGrab (XRGrabInteractable on the bottle body).
///   - Assign pillPrefab and pillSpawnPoint.
///   - Optionally assign bottleLid to gate pill dispensing.
/// </summary>
public class PillBottleController : MonoBehaviour
{
    [Header("Bottle Grab")]
    [SerializeField] private XRGrabInteractable bottleGrab;
    [SerializeField] private Rigidbody bottleRigidbody;

    [Header("Lid Reference")]
    [Tooltip("BottleLid component on the cap. Pills only dispense when lid IsOpen. Leave empty to skip check.")]
    [SerializeField] private BottleLid bottleLid;

    [Header("Pill Spawning")]
    [SerializeField] private GameObject pillPrefab;
    [SerializeField] private Transform pillSpawnPoint;
    [SerializeField] private int requiredPillCount = 3;
    [SerializeField] private float spawnDelay = 0.25f;

    [Header("Pour Detection")]
    [Tooltip("Angle from spawnPoint.forward to Vector3.down. Smaller = more tilted needed.")]
    [SerializeField] private float pourAngleThreshold = 80f;

    private Vector3 _bottleStartPosition;
    private Quaternion _bottleStartRotation;
    private Coroutine _returnRoutine;

    private bool _isGrabbed = false;
    private bool _isPouring = false;
    private bool _hasPoured = false;

    private bool LidIsOpen => bottleLid == null || bottleLid.IsOpen;

    private void Start()
    {
        _bottleStartPosition = transform.position;
        _bottleStartRotation = transform.rotation;

        if (bottleRigidbody != null)
        {
            bottleRigidbody.useGravity = false;
            bottleRigidbody.isKinematic = true;
        }

        if (bottleGrab != null)
        {
            bottleGrab.selectEntered.AddListener(OnBottleGrabbed);
            bottleGrab.selectExited.AddListener(OnBottleReleased);
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
        if (!_isGrabbed || !LidIsOpen || _hasPoured || _isPouring || pillSpawnPoint == null)
            return;

        float angle = Vector3.Angle(pillSpawnPoint.forward, Vector3.down);
        if (angle <= pourAngleThreshold)
        {
            StartCoroutine(SpawnPills());
        }
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
        yield return new WaitForSeconds(0.4f);
        _isGrabbed = true;
    }

    private void OnBottleReleased(SelectExitEventArgs args)
    {
        _isGrabbed = false;

        if (_returnRoutine != null)
            StopCoroutine(_returnRoutine);

        _returnRoutine = StartCoroutine(ReturnBottleToStart());
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
        const float duration = 0.5f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            transform.position = Vector3.Lerp(startPosition, _bottleStartPosition, t);
            transform.rotation = Quaternion.Slerp(startRotation, _bottleStartRotation, t);
            yield return null;
        }

        transform.SetPositionAndRotation(_bottleStartPosition, _bottleStartRotation);
    }

    private IEnumerator SpawnPills()
    {
        _isPouring = true;

        for (int i = 0; i < requiredPillCount; i++)
        {
            SpawnOnePill();
            yield return new WaitForSeconds(spawnDelay);
        }

        _hasPoured = true;
        _isPouring = false;
        Debug.Log("[PillBottleController] Pills dispensed.");
    }

    private void SpawnOnePill()
    {
        if (pillPrefab == null || pillSpawnPoint == null)
        {
            Debug.LogWarning("[PillBottleController] pillPrefab or pillSpawnPoint not assigned.");
            return;
        }

        GameObject newPill = Instantiate(pillPrefab, pillSpawnPoint.position, pillSpawnPoint.rotation);
        newPill.SetActive(true);

        Rigidbody rb = newPill.GetComponent<Rigidbody>();
        if (rb != null)
            rb.AddForce(pillSpawnPoint.forward * 0.1f, ForceMode.Impulse);
    }
}
