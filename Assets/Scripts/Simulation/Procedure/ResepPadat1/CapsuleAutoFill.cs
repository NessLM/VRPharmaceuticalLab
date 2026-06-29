using System.Collections;
using UnityEngine;

/// <summary>
/// Drives the "open -> fill -> close" animation of a single capsule pill.
/// Lives on the pill template (and therefore every spawned clone). The cap
/// (Capsule_Cap) lerps away from the body (Capsule_Body) along a configurable
/// local offset, an optional fill visual scales up inside the body, then the
/// cap lerps back to its closed position.
/// </summary>
public class CapsuleAutoFill : MonoBehaviour
{
    [Header("Capsule Parts")]
    [Tooltip("The cap half that lifts off when the capsule opens (Capsule_Cap).")]
    [SerializeField] private Transform capsuleCap;
    [Tooltip("The body half that stays in place and receives the fill (Capsule_Body).")]
    [SerializeField] private Transform capsuleBody;

    [Header("Open Motion (local space of the cap's parent)")]
    [Tooltip("How far / which direction the cap moves from its closed local position when opening.")]
    [SerializeField] private Vector3 openLocalOffset = new Vector3(0f, 1f, 0f);
    [SerializeField] private float openDuration = 0.5f;
    [SerializeField] private float closeDuration = 0.5f;
    [SerializeField] private float fillDuration = 0.6f;
    [SerializeField] private float holdAfterClose = 0.2f;

    [Header("Fill Visual (optional)")]
    [Tooltip("Object shown/scaled while the capsule is open to represent powder being filled. Optional.")]
    [SerializeField] private Transform fillVisual;
    [Tooltip("Local scale the fill visual grows to during the fill phase.")]
    [SerializeField] private Vector3 fillTargetScale = Vector3.one;

    private Vector3 _capClosedLocalPos;
    private bool _capturedClosed;
    private bool _isPlaying;

    public bool IsPlaying => _isPlaying;

    /// <summary>True while the cap is lifted off the body (mouth open).</summary>
    public bool IsOpen { get; private set; }

    /// <summary>World position of the capsule mouth where powder should enter.</summary>
    public Vector3 MouthPosition => capsuleBody != null ? capsuleBody.position : transform.position;

    private void Awake()
    {
        CaptureClosedState();
    }

    private void CaptureClosedState()
    {
        if (_capturedClosed) return;
        if (capsuleCap != null)
        {
            _capClosedLocalPos = capsuleCap.localPosition;
            _capturedClosed = true;
        }
        if (fillVisual != null)
        {
            fillVisual.localScale = Vector3.zero;
            fillVisual.gameObject.SetActive(false);
        }
    }

    /// <summary>Plays the full open -> fill -> close sequence once.</summary>
    public void Play()
    {
        if (_isPlaying) return;
        CaptureClosedState();
        StartCoroutine(PlayRoutine());
    }

    private IEnumerator PlayRoutine()
    {
        _isPlaying = true;

        if (capsuleCap == null)
        {
            _isPlaying = false;
            yield break;
        }

        Vector3 closedPos = _capClosedLocalPos;
        Vector3 openPos = closedPos + openLocalOffset;

        // OPEN: lift the cap off the body
        yield return LerpCapLocal(closedPos, openPos, openDuration);

        // FILL: grow the optional fill visual inside the open body
        if (fillVisual != null)
        {
            fillVisual.gameObject.SetActive(true);
            yield return LerpScale(fillVisual, Vector3.zero, fillTargetScale, fillDuration);
        }
        else
        {
            yield return new WaitForSeconds(fillDuration);
        }

        // CLOSE: bring the cap back to seal the capsule
        yield return LerpCapLocal(openPos, closedPos, closeDuration);

        if (holdAfterClose > 0f)
            yield return new WaitForSeconds(holdAfterClose);

        _isPlaying = false;
    }

    /// <summary>
    /// Step 1 of the player-driven pour: lift the cap off the body so powder can
    /// be poured into the open mouth. Sets <see cref="IsOpen"/> when finished.
    /// </summary>
    public void OpenCapsule()
    {
        CaptureClosedState();
        if (capsuleCap == null) return;
        StopAllCoroutines();
        StartCoroutine(OpenRoutine());
    }

    private IEnumerator OpenRoutine()
    {
        _isPlaying = true;

        Vector3 closedPos = _capClosedLocalPos;
        Vector3 openPos = closedPos + openLocalOffset;

        yield return LerpCapLocal(capsuleCap.localPosition, openPos, openDuration);

        IsOpen = true;
        _isPlaying = false;
    }

    /// <summary>
    /// Step 2 of the player-driven pour: grow the fill visual (if any) to show
    /// the powder settling inside, then slide the cap back to seal the capsule.
    /// </summary>
    public void FillAndClose()
    {
        CaptureClosedState();
        if (capsuleCap == null) return;
        StopAllCoroutines();
        StartCoroutine(FillAndCloseRoutine());
    }

    private IEnumerator FillAndCloseRoutine()
    {
        _isPlaying = true;

        Vector3 closedPos = _capClosedLocalPos;

        // FILL: grow the optional fill visual inside the open body.
        if (fillVisual != null)
        {
            fillVisual.gameObject.SetActive(true);
            yield return LerpScale(fillVisual, Vector3.zero, fillTargetScale, fillDuration);
        }
        else
        {
            yield return new WaitForSeconds(fillDuration);
        }

        // CLOSE: bring the cap back to seal the capsule.
        yield return LerpCapLocal(capsuleCap.localPosition, closedPos, closeDuration);

        IsOpen = false;

        if (holdAfterClose > 0f)
            yield return new WaitForSeconds(holdAfterClose);

        _isPlaying = false;
    }

    private IEnumerator LerpCapLocal(Vector3 from, Vector3 to, float duration)
    {
        if (duration <= 0f)
        {
            capsuleCap.localPosition = to;
            yield break;
        }

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, t / duration);
            capsuleCap.localPosition = Vector3.Lerp(from, to, k);
            yield return null;
        }
        capsuleCap.localPosition = to;
    }

    private IEnumerator LerpScale(Transform target, Vector3 from, Vector3 to, float duration)
    {
        if (duration <= 0f)
        {
            target.localScale = to;
            yield break;
        }

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, t / duration);
            target.localScale = Vector3.Lerp(from, to, k);
            yield return null;
        }
        target.localScale = to;
    }
}
