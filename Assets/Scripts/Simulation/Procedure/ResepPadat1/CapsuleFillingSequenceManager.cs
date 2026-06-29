using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Drives the Step 4 FULLY AUTOMATIC capsule filling sequence:
/// 1. The 10 poured capsules slide into a vertical row of slots in front of the perkamen.
/// 2. One-by-one and with no player input: a capsule opens, a powder-carrying perkamen
///    automatically rises above the open mouth (leaving a visible gap), powder particles
///    fall in, the capsule fills and closes itself, the paper returns down, then the filled
///    capsule slides onto its perkamen row point. Spacing is applied between each capsule.
/// 3. When all capsules are filled, the checklist is completed and the simulation advances.
/// </summary>
public class CapsuleFillingSequenceManager : MonoBehaviour
{
    [Header("Layout Anchors")]
    [Tooltip("Vertical capsule slots (in front of the perkamen) the empty capsules line up at.")]
    [SerializeField] private Transform[] capsuleSlots;
    [Tooltip("Where each filled capsule ends up (the perkamen row points).")]
    [SerializeField] private Transform[] destinationPoints;

    [Header("Motion")]
    [Tooltip("Duration of a capsule sliding to its slot / destination.")]
    [SerializeField] private float capsuleMoveDuration = 0.6f;

    [Header("Automatic Pour")]
    [Tooltip("Vertical gap (jarak) the perkamen rises above the open capsule mouth so the powder is visibly seen falling.")]
    [SerializeField] private float pourGap = 0.12f;
    [Tooltip("How far the perkamen tilts (degrees around local forward) while pouring.")]
    [SerializeField] private float pourTiltAngle = 70f;
    [Tooltip("Duration of the perkamen rising into the pour pose above the capsule.")]
    [SerializeField] private float perkamenRiseDuration = 0.5f;
    [Tooltip("How long powder particles fall into the capsule.")]
    [SerializeField] private float pourDuration = 0.7f;
    [Tooltip("Duration of the perkamen returning down to its rest pose after pouring.")]
    [SerializeField] private float perkamenReturnDuration = 0.4f;
    [Tooltip("Pause after each capsule is placed before starting the next one (spacing).")]
    [SerializeField] private float spacingBetweenCapsules = 0.35f;

    [Header("References")]
    [SerializeField] private PowderPourFX pourFX;
    [SerializeField] private Step4ChecklistManager checklistManager;
    [SerializeField] private ResepPadat1StepManager stepManager;
    [Tooltip("Step the simulation advances to once all capsules are filled.")]
    [SerializeField] private int nextStep = 5;

    private readonly List<CapsuleAutoFill> _capsules = new List<CapsuleAutoFill>();
    private List<PowderPerkamen> _perkamens = new List<PowderPerkamen>();
    private bool _running;

    /// <summary>Index of the capsule currently being filled (for inspection / testing).</summary>
    public int CurrentIndex { get; private set; } = -1;

    /// <summary>True while the sequence coroutine is active.</summary>
    public bool IsRunning => _running;

    /// <summary>
    /// Begin the sequential filling for the given capsules (collected when the pills poured).
    /// </summary>
    public void BeginSequence(IEnumerable<CapsuleAutoFill> capsules)
    {
        if (_running) return;

        _capsules.Clear();
        if (capsules != null)
        {
            foreach (var c in capsules)
            {
                if (c != null)
                    _capsules.Add(c);
            }
        }

        // Fallback: discover capsules in the scene if none were passed in.
        if (_capsules.Count == 0)
        {
            var found = Object.FindObjectsByType<CapsuleAutoFill>(FindObjectsSortMode.None);
            _capsules.AddRange(found);
        }

        _perkamens = new List<PowderPerkamen>(
            Object.FindObjectsByType<PowderPerkamen>(FindObjectsSortMode.None));

        _running = true;
        StartCoroutine(RunSequence());
    }

    private IEnumerator RunSequence()
    {
        int count = _capsules.Count;
        if (capsuleSlots != null) count = Mathf.Min(count, capsuleSlots.Length);

        // 1. Slide every capsule into its vertical slot.
        for (int i = 0; i < count; i++)
        {
            CapsuleAutoFill capsule = _capsules[i];
            if (capsule == null) continue;

            Transform slot = capsuleSlots != null && i < capsuleSlots.Length ? capsuleSlots[i] : null;
            if (slot != null)
                yield return MoveTransform(capsule.transform, slot.position, slot.rotation, capsuleMoveDuration);
        }

        // 2. Fill each capsule one-by-one, FULLY AUTOMATICALLY (no player input).
        for (int i = 0; i < count; i++)
        {
            CapsuleAutoFill capsule = _capsules[i];
            if (capsule == null) continue;

            CurrentIndex = i;

            Transform dest = destinationPoints != null && i < destinationPoints.Length
                ? destinationPoints[i]
                : null;

            // Pick the powder-carrying perkamen nearest to this capsule's destination
            // so each capsule uses a different one. Remove it from the pool.
            PowderPerkamen perkamen = TakeNearestPerkamen(dest != null ? dest.position : capsule.MouthPosition);

            // Open the current capsule and wait for the cap to finish lifting.
            capsule.OpenCapsule();
            while (capsule.IsPlaying)
                yield return null;

            if (perkamen != null)
            {
                // Remember the perkamen's rest pose so we can send the paper back down.
                Vector3 restPos = perkamen.transform.position;
                Quaternion restRot = perkamen.transform.rotation;

                // Rise above the open mouth, leaving a visible gap so powder is seen falling.
                Vector3 mouth = capsule.MouthPosition;
                Vector3 pourPos = mouth + Vector3.up * pourGap;
                Quaternion pourRot = perkamen.transform.rotation * Quaternion.AngleAxis(pourTiltAngle, Vector3.forward);
                yield return MoveTransform(perkamen.transform, pourPos, pourRot, perkamenRiseDuration);

                // Pour: emit falling powder particles into the capsule mouth.
                float poured = 0f;
                while (poured < pourDuration)
                {
                    if (pourFX != null)
                        pourFX.EmitAt(perkamen.PourOrigin.position, mouth);
                    poured += Time.deltaTime;
                    yield return null;
                }
                if (pourFX != null)
                    pourFX.StopEmitting();
                perkamen.SetPowder(false);

                // Fill (grow visual) then close the capsule, and wait for it to finish.
                capsule.FillAndClose();
                while (capsule.IsPlaying)
                    yield return null;

                // Send the (now empty) paper back down to its rest pose.
                yield return MoveTransform(perkamen.transform, restPos, restRot, perkamenReturnDuration);
            }
            else
            {
                // No perkamen available: just fill and close the capsule.
                capsule.FillAndClose();
                while (capsule.IsPlaying)
                    yield return null;
            }

            // Move the filled capsule onto its perkamen row point.
            if (dest != null)
                yield return MoveTransform(capsule.transform, dest.position, dest.rotation, capsuleMoveDuration);

            // Spacing between each capsule.
            yield return new WaitForSeconds(spacingBetweenCapsules);
        }

        CurrentIndex = -1;
        _running = false;

        if (checklistManager != null)
            checklistManager.CheckAllCapsulesFilled();

        if (stepManager != null)
            stepManager.SetStep(nextStep);

        Debug.Log("[CapsuleFillingSequenceManager] Semua kapsul terisi. Lanjut ke step " + nextStep + ".");
    }

    /// <summary>
    /// Returns (and removes from the pool) the powder-carrying perkamen nearest to
    /// the given world point, so each capsule consumes a different one.
    /// </summary>
    private PowderPerkamen TakeNearestPerkamen(Vector3 worldPoint)
    {
        if (_perkamens == null) return null;

        int bestIndex = -1;
        float bestDist = float.MaxValue;
        for (int i = 0; i < _perkamens.Count; i++)
        {
            PowderPerkamen p = _perkamens[i];
            if (p == null) continue;
            if (!p.HasPowder) continue;

            float dist = Vector3.Distance(p.transform.position, worldPoint);
            if (dist < bestDist)
            {
                bestDist = dist;
                bestIndex = i;
            }
        }

        if (bestIndex < 0) return null;

        PowderPerkamen chosen = _perkamens[bestIndex];
        _perkamens.RemoveAt(bestIndex);
        return chosen;
    }

    /// <summary>
    /// Kinematically slide a transform to a target pose using a SmoothStep lerp.
    /// </summary>
    private IEnumerator MoveTransform(Transform t, Vector3 endPos, Quaternion endRot, float duration)
    {
        if (t == null)
            yield break;

        Rigidbody rb = t.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.useGravity = false;
            rb.isKinematic = true;
        }

        // Disable the transform's colliders during transit so it does not trip any
        // snap/scoop/destroy trigger volumes it sweeps through on the way. Re-enabled
        // on arrival so it can still rest/be interacted with.
        Collider[] cols = t.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < cols.Length; i++)
        {
            if (cols[i] != null) cols[i].enabled = false;
        }

        Vector3 startPos = t.position;
        Quaternion startRot = t.rotation;

        if (duration > 0f)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float k = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                t.SetPositionAndRotation(
                    Vector3.Lerp(startPos, endPos, k),
                    Quaternion.Slerp(startRot, endRot, k));
                yield return null;
            }
        }

        t.SetPositionAndRotation(endPos, endRot);

        // Restore colliders now that the transform has arrived.
        for (int i = 0; i < cols.Length; i++)
        {
            if (cols[i] != null) cols[i].enabled = true;
        }
    }
}
