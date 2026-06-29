using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Coordinates the Step 4 auto-fill flow:
/// 1. After the pills are poured out of the bottle, the auto-fill panel is shown.
/// 2. When the player clicks the MULAI button, each poured capsule plays its
///    open -> fill -> close animation in sequence.
/// </summary>
public class Step4AutoFillManager : MonoBehaviour
{
    [Header("Panel")]
    [Tooltip("Panel_AutoFillCapsule - hidden until the pills are poured.")]
    [SerializeField] private GameObject autoFillPanel;
    [Tooltip("Hide the panel automatically once the MULAI button is pressed.")]
    [SerializeField] private bool hidePanelOnStart = true;

    [Header("Start Button")]
    [Tooltip("Button_StartAutoFill (MULAI). onClick is wired in code at runtime.")]
    [SerializeField] private Button startButton;

    [Header("Animation")]
    [Tooltip("Stagger before starting each next capsule's animation (they overlap). Legacy auto-fill only.")]
    [SerializeField] private float delayBetweenCapsules = 0.15f;

    [Header("Sequential Pour")]
    [Tooltip("Drives the player-driven one-by-one capsule pour. When set, MULAI starts this instead of the legacy auto-fill.")]
    [SerializeField] private CapsuleFillingSequenceManager sequenceManager;

    [Header("Optional Checklist Hook")]
    [SerializeField] private Step4ChecklistManager checklistManager;

    private readonly List<CapsuleAutoFill> _capsules = new List<CapsuleAutoFill>();
    private bool _sequenceStarted;

    private void Awake()
    {
        if (autoFillPanel != null)
            autoFillPanel.SetActive(false);
    }

    private void OnEnable()
    {
        if (startButton != null)
            startButton.onClick.AddListener(OnStartClicked);
    }

    private void OnDisable()
    {
        if (startButton != null)
            startButton.onClick.RemoveListener(OnStartClicked);
    }

    /// <summary>
    /// Called by PillBottleController once the pills have finished pouring.
    /// Registers the poured capsules and shows the auto-fill panel.
    /// </summary>
    public void OnPillsPoured(IEnumerable<GameObject> pouredPills)
    {
        _capsules.Clear();

        // Snapshot the incoming collection first so we never enumerate a list that
        // another system might modify while we iterate.
        if (pouredPills != null)
        {
            var snapshot = new List<GameObject>(pouredPills);
            foreach (var pill in snapshot)
            {
                if (pill == null) continue;
                var cap = pill.GetComponent<CapsuleAutoFill>();
                if (cap != null)
                    _capsules.Add(cap);
            }
        }

        if (checklistManager != null)
            checklistManager.CheckCapsulesReady();

        // Always show the panel once pills are out, regardless of how many
        // capsules were registered.
        if (autoFillPanel != null)
            autoFillPanel.SetActive(true);
    }

    public void OnStartClicked()
    {
        if (_sequenceStarted) return;
        _sequenceStarted = true;

        if (hidePanelOnStart && autoFillPanel != null)
            autoFillPanel.SetActive(false);

        if (checklistManager != null)
            checklistManager.CheckAutoFillStarted();

        // New flow: hand the collected capsules to the sequential player-driven pour.
        if (sequenceManager != null)
        {
            sequenceManager.BeginSequence(_capsules);
            return;
        }

        // Legacy fallback: auto-play every capsule (kept for compatibility).
        StartCoroutine(PlayAllCapsules());
    }

    private IEnumerator PlayAllCapsules()
    {
        // Fallback: if no capsules were registered (e.g. wiring relied on scene
        // discovery), find any active capsules in the scene.
        if (_capsules.Count == 0)
        {
            var found = Object.FindObjectsByType<CapsuleAutoFill>(FindObjectsSortMode.None);
            _capsules.AddRange(found);
        }

        // Work over a stable snapshot so we never enumerate a list that could be
        // modified by another system while we yield.
        var capsules = new List<CapsuleAutoFill>(_capsules);

        // Start each capsule with a small stagger so they animate together
        // (looks lively and finishes quickly even with many capsules).
        foreach (var capsule in capsules)
        {
            if (capsule == null) continue;
            capsule.Play();
            if (delayBetweenCapsules > 0f)
                yield return new WaitForSeconds(delayBetweenCapsules);
        }

        // Wait until every capsule has finished its open/fill/close cycle.
        bool anyPlaying = true;
        while (anyPlaying)
        {
            anyPlaying = false;
            foreach (var capsule in capsules)
            {
                if (capsule != null && capsule.IsPlaying) { anyPlaying = true; break; }
            }
            yield return null;
        }

        if (checklistManager != null)
            checklistManager.CheckAllCapsulesFilled();
    }
}
