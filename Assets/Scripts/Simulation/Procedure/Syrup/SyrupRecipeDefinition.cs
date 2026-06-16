using UnityEngine;

[CreateAssetMenu(fileName = "SyrupRecipe", menuName = "VR Pharmacy/Syrup Recipe")]
public class SyrupRecipeDefinition : ScriptableObject
{
    [Header("Recipe Identity")]
    public string recipeName = "Sirup Difenhidramin";

    [Header("Step 3 - Powder Weighing")]
    public string powderName = "Difenhidramin";
    public float targetPowderMg = 250f;
    public float toleranceMg = 10f;
    public float scoopStepMg = 50f;

    [Header("Visual")]
    public float powderVisualMaxMg = 250f;
}