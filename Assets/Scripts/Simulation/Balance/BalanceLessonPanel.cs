using UnityEngine;
using UnityEngine.Events;
using TMPro;

/// <summary>
/// World-space educational UI panel for the neraca (balance scale) simulation.
/// Reads from VirtualWeightSelector and PowderDepositZone to show live mass data,
/// remaining target, balance status, and step-by-step instructions.
///
/// Lesson flow:
///   Step 1 — PendingWeightSelection: user picks anak timbangan via VirtualWeightSelector panel.
///   Step 2 — PendingPowder: target accepted; user scoops powder from Difenhidramin.
///   Step 3 — Pouring: user pours powder onto left pan.
///   Step 4 — Balanced: left mass matches right target within tolerance.
///
/// Attach to: LessonPanel (child of BalanceLessonCanvas).
/// Wire: balanceController, virtualWeightSelector, powderDepositZone, and all TMP_Text fields.
/// </summary>
public class BalanceLessonPanel : MonoBehaviour
{
    // ──────────────────────────── References ────────────────────────────

    [Header("Balance References")]
    [SerializeField] private MG_BalanceController balanceController;
    [SerializeField] private VirtualWeightSelector virtualWeightSelector;
    [SerializeField] private PowderDepositZone powderDepositZone;

    [Header("Legacy Zone Fallbacks (optional)")]
    [SerializeField] private WeightingZone leftZone;
    [SerializeField] private WeightingZone rightZone;

    // ──────────────────────────── UI Texts ────────────────────────────

    [Header("UI Texts")]
    [SerializeField] private TMP_Text targetMassText;
    [SerializeField] private TMP_Text leftMassText;
    [SerializeField] private TMP_Text differenceText;
    [SerializeField] private TMP_Text remainingText;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private TMP_Text instructionText;

    // ──────────────────────────── Colors ────────────────────────────

    [Header("Status Colors")]
    [SerializeField] private Color balancedColor   = new Color(0.22f, 0.85f, 0.22f, 1f);
    [SerializeField] private Color warningColor    = new Color(0.95f, 0.75f, 0.10f, 1f);
    [SerializeField] private Color imbalancedColor = new Color(0.90f, 0.28f, 0.10f, 1f);
    [SerializeField] private Color neutralColor    = Color.white;

    // ──────────────────────────── Optional Indicators ────────────────────────────

    [Header("Optional Visual Indicators")]
    [SerializeField] private GameObject balancedIndicator;
    [SerializeField] private GameObject imbalancedIndicator;

    // ──────────────────────────── Events ────────────────────────────

    [Header("Events")]
    public UnityEvent onBalanceAchieved;
    public UnityEvent onLessonReset;

    // ──────────────────────────── Instruction Strings ────────────────────────────

    private const string Instr_SelectWeight =
        "Langkah 1: Pilih anak timbangan\ndi panel kanan, lalu tekan \"Terima\".";
    private const string Instr_ScoopPowder =
        "Langkah 2: Ambil bubuk Difenhidramin\ndengan sendok tanduk.";
    private const string Instr_PourPowder =
        "Langkah 3: Tuang bubuk ke piring kiri.\nUlangi sampai neraca seimbang.";
    private const string Instr_Balanced =
        "Neraca seimbang! Bubuk siap dipindahkan ke mortar.";

    // ──────────────────────────── State Machine ────────────────────────────

    private enum LessonState
    {
        PendingWeightSelection,
        PendingPowder,
        Pouring,
        Balanced,
    }

    // ──────────────────────────── Private State & Cache ────────────────────────────

    private LessonState currentState = LessonState.PendingWeightSelection;
    private bool achievedBalanceThisSession;

    // Cached values — texts only rebuild when values change.
    private float cachedLeft   = -1f;
    private float cachedRight  = -1f;
    private bool  cachedLocked = false;
    private LessonState cachedState = (LessonState)(-1);
    private bool cachedBalanced = false;

    private const float MassDirtyThreshold = 0.005f; // g — min change before text rebuilds

    // ──────────────────────────── Lifecycle ────────────────────────────

    private void Start()
    {
        if (balanceController != null)
        {
            balanceController.onBalanced.AddListener(OnScaleBalanced);
            balanceController.onUnbalanced.AddListener(OnScaleUnbalanced);
        }

        if (virtualWeightSelector != null)
            virtualWeightSelector.onTargetCleared.AddListener(ResetLesson);

        SetIndicators(false);
        ForceRefreshAll();
    }

    private void OnDestroy()
    {
        if (balanceController != null)
        {
            balanceController.onBalanced.RemoveListener(OnScaleBalanced);
            balanceController.onUnbalanced.RemoveListener(OnScaleUnbalanced);
        }

        if (virtualWeightSelector != null)
            virtualWeightSelector.onTargetCleared.RemoveListener(ResetLesson);
    }

    private void Update()
    {
        float left       = GetLeftMass();
        float right      = GetRightMass();
        bool targetLocked = HasRightTarget(right);
        bool isBalanced   = balanceController != null
            ? balanceController.IsBalanced
            : Mathf.Abs(right - left) < 0.5f;

        bool leftDirty  = Mathf.Abs(left  - cachedLeft)  > MassDirtyThreshold;
        bool rightDirty = Mathf.Abs(right - cachedRight) > MassDirtyThreshold;
        bool lockDirty  = targetLocked != cachedLocked;
        bool balDirty   = isBalanced   != cachedBalanced;

        if (!leftDirty && !rightDirty && !lockDirty && !balDirty) return;

        cachedLeft    = left;
        cachedRight   = right;
        cachedLocked  = targetLocked;
        cachedBalanced = isBalanced;

        RefreshAll(left, right, targetLocked, isBalanced);
    }

    // ──────────────────────────── Core Refresh ────────────────────────────

    private void RefreshAll(float left, float right, bool targetLocked, bool isBalanced)
    {
        UpdateState(left, right, targetLocked, isBalanced);
        UpdateTargetText(right, targetLocked);
        UpdateLeftMassText(left);
        UpdateDifferenceText(left, right);
        UpdateRemainingText(left, right, targetLocked);
        UpdateStatusText(left, right, isBalanced, targetLocked);
        UpdateInstructionText();
        SetIndicators(currentState == LessonState.Balanced);
    }

    /// <summary>Forces a full UI refresh regardless of cache. Called on Start and ResetLesson.</summary>
    private void ForceRefreshAll()
    {
        cachedLeft    = -1f;
        cachedRight   = -1f;
        cachedLocked  = false;
        cachedBalanced = false;

        float left       = GetLeftMass();
        float right      = GetRightMass();
        bool targetLocked = HasRightTarget(right);
        bool isBalanced   = balanceController != null
            ? balanceController.IsBalanced
            : Mathf.Abs(right - left) < 0.5f;

        cachedLeft    = left;
        cachedRight   = right;
        cachedLocked  = targetLocked;
        cachedBalanced = isBalanced;

        RefreshAll(left, right, targetLocked, isBalanced);
    }

    private float GetLeftMass()
    {
        if (powderDepositZone != null) return powderDepositZone.DepositedGrams;
        return leftZone != null ? leftZone.TotalGrams : 0f;
    }

    private float GetRightMass()
    {
        if (virtualWeightSelector != null && virtualWeightSelector.IsLocked)
            return virtualWeightSelector.LockedRightMassGrams;

        return rightZone != null ? rightZone.TotalGrams : 0f;
    }

    private bool HasRightTarget(float rightMass)
    {
        return (virtualWeightSelector != null && virtualWeightSelector.IsLocked) ||
               rightMass > 0.001f;
    }

    // ──────────────────────────── State Machine ────────────────────────────

    private void UpdateState(float leftMass, float rightMass, bool targetLocked, bool isBalanced)
    {
        LessonState next;

        if (!targetLocked)
        {
            next = LessonState.PendingWeightSelection;
        }
        else if (isBalanced && leftMass > 0.001f && rightMass > 0.001f)
        {
            next = LessonState.Balanced;
        }
        else if (leftMass > 0.001f)
        {
            next = LessonState.Pouring;
        }
        else
        {
            next = LessonState.PendingPowder;
        }

        currentState = next;
    }

    // ──────────────────────────── Text Updates ────────────────────────────

    private void UpdateTargetText(float rightMass, bool targetLocked)
    {
        if (targetMassText == null) return;
        targetMassText.text = targetLocked
            ? $"Target Kanan: {rightMass:F0} g"
            : "Target Kanan: --";
    }

    private void UpdateLeftMassText(float leftMass)
    {
        if (leftMassText != null)
            leftMassText.text = $"Kiri (Bubuk): {leftMass:F2} g";
    }

    private void UpdateDifferenceText(float leftMass, float rightMass)
    {
        if (differenceText == null) return;
        float diff = Mathf.Abs(rightMass - leftMass);
        differenceText.text = $"Selisih: {diff:F2} g";
        differenceText.color = diff < 0.5f && leftMass > 0.001f ? balancedColor : neutralColor;
    }

    private void UpdateRemainingText(float leftMass, float rightMass, bool targetLocked)
    {
        if (remainingText == null) return;

        if (!targetLocked)
        {
            remainingText.text  = "Sisa: --";
            remainingText.color = neutralColor;
            return;
        }

        float sisa = Mathf.Max(0f, rightMass - leftMass);
        remainingText.text  = $"Sisa: {sisa:F2} g";
        remainingText.color = sisa < 0.001f ? balancedColor : neutralColor;
    }

    private void UpdateStatusText(float leftMass, float rightMass, bool isBalanced, bool targetLocked)
    {
        if (statusText == null) return;

        if (!targetLocked)
        {
            statusText.text  = "Belum ada target kanan";
            statusText.color = neutralColor;
            return;
        }

        if (isBalanced && leftMass > 0.001f)
        {
            statusText.text  = "SEIMBANG \u2713";
            statusText.color = balancedColor;
            return;
        }

        float diff = rightMass - leftMass;
        if (diff > 0.001f)
        {
            statusText.text  = $"Kanan lebih berat ({diff:F2} g)";
            statusText.color = imbalancedColor;
        }
        else if (diff < -0.001f)
        {
            statusText.text  = $"Kiri terlalu berat ({Mathf.Abs(diff):F2} g)";
            statusText.color = warningColor;
        }
        else
        {
            statusText.text  = "Menimbang...";
            statusText.color = neutralColor;
        }
    }

    private void UpdateInstructionText()
    {
        if (instructionText == null) return;

        switch (currentState)
        {
            case LessonState.PendingWeightSelection:
                instructionText.text  = Instr_SelectWeight;
                instructionText.color = neutralColor;
                break;
            case LessonState.PendingPowder:
                instructionText.text  = Instr_ScoopPowder;
                instructionText.color = neutralColor;
                break;
            case LessonState.Pouring:
                instructionText.text  = Instr_PourPowder;
                instructionText.color = neutralColor;
                break;
            case LessonState.Balanced:
                instructionText.text  = Instr_Balanced;
                instructionText.color = balancedColor;
                break;
        }
    }

    // ──────────────────────────── Balance Events ────────────────────────────

    private void OnScaleBalanced()
    {
        SetIndicators(true);
        if (achievedBalanceThisSession) return;
        achievedBalanceThisSession = true;
        onBalanceAchieved?.Invoke();
    }

    private void OnScaleUnbalanced() => SetIndicators(false);

    private void SetIndicators(bool balanced)
    {
        if (balancedIndicator != null) balancedIndicator.SetActive(balanced);
        if (imbalancedIndicator != null)
            imbalancedIndicator.SetActive(!balanced && currentState != LessonState.PendingWeightSelection);
    }

    // ──────────────────────────── Public API ────────────────────────────

    /// <summary>Resets the lesson state (also triggered by VirtualWeightSelector.onTargetCleared).</summary>
    public void ResetLesson()
    {
        achievedBalanceThisSession = false;
        currentState = LessonState.PendingWeightSelection;
        SetIndicators(false);
        powderDepositZone?.ResetDeposit();
        ForceRefreshAll();
        onLessonReset?.Invoke();
    }

    public string GetCurrentStateName() => currentState.ToString();

    public bool IsBalancedWithMass
    {
        get
        {
            float left  = GetLeftMass();
            float right = GetRightMass();
            bool balanced = balanceController != null
                ? balanceController.IsBalanced
                : Mathf.Abs(right - left) < 0.5f;
            return balanced && left > 0.001f && right > 0.001f;
        }
    }
}
