using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Controls the visual state of the analytical balance (timbangan neraca).
/// Rotates the balance beam based on weight difference between left and right pans.
/// Attach to: Timbangan Neraca root GameObject.
/// </summary>
public class BalanceController : MonoBehaviour
{
    [Header("Beam Visual")]
    [Tooltip("The central beam Transform that tilts when weights differ.")]
    [SerializeField] private Transform beamTransform;
    [SerializeField] private float maxBeamAngleDegrees = 15f;
    [SerializeField] private float maxWeightDifferenceMg = 600f;
    [SerializeField] private float beamSmoothSpeed = 3f;

    [Header("Left Pan — Material Side")]
    [Tooltip("Optional visual mesh representing material on the left pan.")]
    [SerializeField] private Transform materialMesh;
    [SerializeField] private float maxMaterialDisplayMg = 600f;
    [SerializeField] private Vector3 emptyMaterialScale = new Vector3(0.5f, 0.001f, 0.5f);
    [SerializeField] private Vector3 fullMaterialScale = new Vector3(0.5f, 0.18f, 0.5f);

    [Header("Current Weights")]
    [SerializeField] private float currentLeftWeightMg = 0f;
    [SerializeField] private float currentRightWeightMg = 0f;

    [Header("Events")]
    public UnityEvent onBalanced;
    public UnityEvent onUnbalanced;
    public UnityEvent<float> onLeftWeightChanged;
    public UnityEvent<float> onRightWeightChanged;

    private const float BalancedTolerance = 1f; // mg

    private bool wasBalanced = false;
    private float currentBeamAngle = 0f;

    public float LeftWeightMg => currentLeftWeightMg;
    public float RightWeightMg => currentRightWeightMg;
    public bool IsBalanced => Mathf.Abs(currentLeftWeightMg - currentRightWeightMg) <= BalancedTolerance;

    private void Start()
    {
        UpdateMaterialVisual();
    }

    private void Update()
    {
        UpdateBeamRotation();
        CheckBalanceState();
    }

    /// <summary>Sets the weight of material on the left pan in mg.</summary>
    public void SetLeftWeight(float weightMg)
    {
        currentLeftWeightMg = Mathf.Max(0f, weightMg);
        UpdateMaterialVisual();
        onLeftWeightChanged?.Invoke(currentLeftWeightMg);
    }

    /// <summary>Sets the total measuring weight on the right pan in mg.</summary>
    public void SetRightWeight(float weightMg)
    {
        currentRightWeightMg = Mathf.Max(0f, weightMg);
        onRightWeightChanged?.Invoke(currentRightWeightMg);
    }

    private void UpdateBeamRotation()
    {
        if (beamTransform == null) return;

        float diff = currentLeftWeightMg - currentRightWeightMg;
        float normalizedDiff = Mathf.Clamp(diff / maxWeightDifferenceMg, -1f, 1f);
        float targetAngle = normalizedDiff * maxBeamAngleDegrees;

        currentBeamAngle = Mathf.Lerp(currentBeamAngle, targetAngle, Time.deltaTime * beamSmoothSpeed);
        beamTransform.localEulerAngles = new Vector3(0f, 0f, currentBeamAngle);
    }

    private void CheckBalanceState()
    {
        bool balanced = IsBalanced;
        if (balanced == wasBalanced) return;

        wasBalanced = balanced;
        if (balanced) onBalanced?.Invoke();
        else onUnbalanced?.Invoke();
    }

    private void UpdateMaterialVisual()
    {
        if (materialMesh == null) return;

        bool hasMaterial = currentLeftWeightMg > 0f;
        materialMesh.gameObject.SetActive(hasMaterial);

        if (hasMaterial)
        {
            float t = Mathf.Clamp01(currentLeftWeightMg / maxMaterialDisplayMg);
            materialMesh.localScale = Vector3.Lerp(emptyMaterialScale, fullMaterialScale, t);
        }
    }
}
