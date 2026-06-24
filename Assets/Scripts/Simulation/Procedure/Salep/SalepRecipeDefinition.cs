using UnityEngine;

/// <summary>
/// Sumber data tunggal yang bisa diedit untuk resep Salep (obat semi padat).
/// Dipakai oleh SalepProcedureManager (target step + etiket) dan
/// SalepIngredientRuntimeSetup (konfigurasi visual + takaran bahan).
///
/// Edit nilai-nilai di bawah dari Inspector pada asset:
/// Assets/Scripts/Simulation/Procedure/Salep/SalepRecipe_Default.asset
/// </summary>
[CreateAssetMenu(
    fileName = "SalepRecipe_Default",
    menuName = "VRLab/Salep Recipe Definition",
    order = 0)]
public sealed class SalepRecipeDefinition : ScriptableObject
{
    [System.Serializable]
    public sealed class Ingredient
    {
        [Tooltip("ID internal bahan (harus konsisten: AsamSalisilat / SulfurPP / VaselinAlbum).")]
        public string ingredientId = "AsamSalisilat";

        [Tooltip("Nama tampilan di UI, mis. \"Asam Salisilat\".")]
        public string displayName = "Asam Salisilat";

        [Tooltip("Jumlah per satu scoop (mg). Asam=50, Sulfur=100, Vaselin=2000 (2 g).")]
        public float amountPerScoopMg = 50f;

        [Tooltip("Total target yang harus ditimbang (mg). Asam=200, Sulfur=400, Vaselin=9400 (9.4 g).")]
        public float targetTotalMg = 200f;

        [Tooltip("Tampilkan progres dalam gram (Vaselin) atau mg (powder).")]
        public bool displayInGrams = false;

        public IngredientVisualType visualType = IngredientVisualType.PowderWhiteCrystal;

        [Tooltip("Warna bahan untuk visual mound/scoop/floating text.")]
        public Color color = new Color(0.97f, 0.975f, 0.96f, 1f);

        public float AmountPerScoopMg => Mathf.Max(1f, amountPerScoopMg);
        public float TargetTotalMg => Mathf.Max(AmountPerScoopMg, targetTotalMg);
    }

    [Header("Bahan Aktif")]
    public Ingredient asamSalisilat = new Ingredient
    {
        ingredientId = "AsamSalisilat",
        displayName = "Asam Salisilat",
        amountPerScoopMg = 50f,
        targetTotalMg = 200f,
        displayInGrams = false,
        visualType = IngredientVisualType.PowderWhiteCrystal,
        color = new Color(0.97f, 0.975f, 0.96f, 1f)
    };

    public Ingredient sulfurPP = new Ingredient
    {
        ingredientId = "SulfurPP",
        displayName = "Sulfur PP",
        amountPerScoopMg = 100f,
        targetTotalMg = 400f,
        displayInGrams = false,
        visualType = IngredientVisualType.PowderYellow,
        color = new Color(1f, 0.9f, 0.46f, 1f)
    };

    [Header("Basis")]
    public Ingredient vaselinAlbum = new Ingredient
    {
        ingredientId = "VaselinAlbum",
        displayName = "Vaselin Album",
        amountPerScoopMg = 1000f,
        targetTotalMg = 10000f,
        displayInGrams = true,
        visualType = IngredientVisualType.CreamOintment,
        color = new Color(0.97f, 0.935f, 0.84f, 1f)
    };

    [Header("Toleransi & Validasi")]
    [Tooltip("Toleransi target timbang (mg).")]
    public float toleranceMg = 1f;

    [Tooltip("Progres mixing (0..1) yang diperlukan agar step gerus/aduk dianggap selesai.")]
    public float mixProgressRequired = 1f;

    [Header("Etiket")]
    public string etiketProductLine = "SALEP 2-4 (As. Salisilat 2% + Sulfur 4%)";
    public string etiketCompletionTitle = "SIMULASI SALEP SELESAI";
    [TextArea(2, 4)]
    public string etiketCompletionDetail =
        "Salep sudah selesai dibuat, dipindahkan ke pot, dan diberi etiket.";

    /// <summary>Total zat aktif (mg) = Asam + Sulfur.</summary>
    public float TotalActiveMg => asamSalisilat.TargetTotalMg + sulfurPP.TargetTotalMg;

    public Ingredient GetById(string id)
    {
        if (string.IsNullOrEmpty(id))
            return null;
        if (asamSalisilat != null && id == asamSalisilat.ingredientId) return asamSalisilat;
        if (sulfurPP != null && id == sulfurPP.ingredientId) return sulfurPP;
        if (vaselinAlbum != null && id == vaselinAlbum.ingredientId) return vaselinAlbum;
        return null;
    }
}
