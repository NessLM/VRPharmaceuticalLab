using UnityEngine;

[CreateAssetMenu(
    fileName = "SyrupRecipe",
    menuName = "VR Pharmacy/Syrup Recipe"
)]
public class SyrupRecipeDefinition : ScriptableObject
{
    [Header("Recipe Identity")]
    public string recipeName = "Sirup Difenhidramin";

    [Header("Step 3 - Powder Weighing")]
    public string powderName = "Difenhidramin HCl";
    public float targetPowderMg = 250f;
    public float toleranceMg = 10f;
    public float scoopStepMg = 50f;

    [Header("Step 5 - Water")]
    public float targetWaterMl = 100f;
    public float waterPortionMl = 50f;
    public float waterToleranceMl = 2f;

    [Header("Visual")]
    public float powderVisualMaxMg = 250f;

    [Header("Etiket")]
    public string etiketProductLine = "DIFENHIDRAMIN 250 mg / 100 ml";

    [TextArea(2, 4)]
    public string completionDetail =
        "Sirup Difenhidramin 250 mg / 100 ml sudah dibuat, dimasukkan ke botol, diberi etiket, dan ditutup.";
}
