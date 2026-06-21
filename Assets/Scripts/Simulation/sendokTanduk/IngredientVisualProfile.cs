using UnityEngine;

public enum IngredientVisualType
{
    PowderWhiteCrystal,
    PowderYellow,
    CreamOintment
}

[DisallowMultipleComponent]
public sealed class IngredientVisualProfile : MonoBehaviour
{
    [SerializeField] private string ingredientId = "Ingredient";
    [SerializeField] private string displayName = "Ingredient";
    [SerializeField] private IngredientVisualType visualType = IngredientVisualType.PowderWhiteCrystal;
    [SerializeField] private Material spoonMaterial;
    [SerializeField] private Color fallbackColor = Color.white;
    [SerializeField] private Color scoopFxColor = new Color(0.94f, 0.92f, 0.82f, 0.42f);

    [Header("Scoop / Takaran (mg)")]
    [Tooltip("Jumlah yang berpindah ke sendok setiap satu scoop, dalam mg. " +
             "Asam Salisilat = 50, Sulfur PP = 100, Vaselin Album = 2000 (2 g).")]
    [SerializeField] private float amountPerScoopMg = 50f;

    [Tooltip("Total target bahan yang harus ditimbang, dalam mg. " +
             "Asam Salisilat = 200, Sulfur PP = 400, Vaselin Album = 9400 (9.4 g).")]
    [SerializeField] private float targetTotalMg = 200f;

    [Tooltip("Tampilkan progress dalam gram (untuk Vaselin) atau mg (untuk powder).")]
    [SerializeField] private bool displayInGrams = false;

    public string IngredientId => string.IsNullOrWhiteSpace(ingredientId) ? name : ingredientId;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? IngredientId : displayName;
    public IngredientVisualType VisualType => visualType;
    public Material SpoonMaterial => spoonMaterial;
    public Color FallbackColor => fallbackColor;
    public Color ScoopFxColor => scoopFxColor;
    public bool IsCream => visualType == IngredientVisualType.CreamOintment;

    public float AmountPerScoopMg => Mathf.Max(1f, amountPerScoopMg);
    public float TargetTotalMg => Mathf.Max(AmountPerScoopMg, targetTotalMg);
    public bool DisplayInGrams => displayInGrams;

    /// <summary>Format jumlah (mg) sesuai unit display bahan, mis. "+50 mg" atau "+2 g".</summary>
    public string FormatAmount(float amountMg, bool withSign)
    {
        string sign = withSign ? "+" : string.Empty;
        if (displayInGrams)
            return $"{sign}{amountMg / 1000f:0.##} g";
        return $"{sign}{amountMg:0.#} mg";
    }

    public void Configure(
        string newIngredientId,
        string newDisplayName,
        IngredientVisualType newVisualType,
        Material newSpoonMaterial,
        Color newFallbackColor,
        Color newScoopFxColor)
    {
        ingredientId = string.IsNullOrWhiteSpace(newIngredientId)
            ? name
            : newIngredientId.Trim();
        displayName = string.IsNullOrWhiteSpace(newDisplayName)
            ? ingredientId
            : newDisplayName.Trim();
        visualType = newVisualType;
        spoonMaterial = newSpoonMaterial;
        fallbackColor = newFallbackColor;
        scoopFxColor = newScoopFxColor;
    }

    /// <summary>Set data takaran per scoop &amp; target total bahan ini.</summary>
    public void ConfigureScoop(float newAmountPerScoopMg, float newTargetTotalMg, bool newDisplayInGrams)
    {
        amountPerScoopMg = Mathf.Max(1f, newAmountPerScoopMg);
        targetTotalMg = Mathf.Max(amountPerScoopMg, newTargetTotalMg);
        displayInGrams = newDisplayInGrams;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        amountPerScoopMg = Mathf.Max(1f, amountPerScoopMg);
        targetTotalMg = Mathf.Max(amountPerScoopMg, targetTotalMg);
    }
#endif
}
