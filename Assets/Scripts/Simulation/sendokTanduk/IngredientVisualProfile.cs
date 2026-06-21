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

    public string IngredientId => string.IsNullOrWhiteSpace(ingredientId) ? name : ingredientId;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? IngredientId : displayName;
    public IngredientVisualType VisualType => visualType;
    public Material SpoonMaterial => spoonMaterial;
    public Color FallbackColor => fallbackColor;
    public Color ScoopFxColor => scoopFxColor;
    public bool IsCream => visualType == IngredientVisualType.CreamOintment;

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
}
