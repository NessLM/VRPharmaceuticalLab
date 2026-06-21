using UnityEngine;
using UnityEngine.SceneManagement;

// Setup runtime untuk bahan salep.
// AMAN: auto-run hanya di PLAY MODE (RuntimeInitializeOnLoadMethod), TIDAK menyimpan
// scene, jadi tidak menyebabkan revert layout (penyebab revert dulu adalah script
// EDITOR [InitializeOnLoad] + SaveScene yang sudah dimatikan). Juga dipanggil ulang
// (idempotent) dari SalepProcedureManager.BeginSalepProcedure saat MULAI SIMULASI.
public static class SalepIngredientRuntimeSetup
{
    // Takaran per scoop & target total (mg) sesuai resep Salep.
    private const float AsamScoopMg = 50f;
    private const float AsamTargetMg = 200f;
    private const float SulfurScoopMg = 100f;
    private const float SulfurTargetMg = 400f;
    private const float VaselinScoopMg = 2000f;   // 2 g
    private const float VaselinTargetMg = 9400f;  // 9.4 g

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoConfigureOnPlay()
    {
        // Hanya untuk VRLabSimulation. Jangan sentuh scene Padat / lain.
        if (SceneManager.GetActiveScene().name != "VRLabSimulation")
            return;

        try
        {
            ConfigureScene();
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[SalepIngredientRuntimeSetup] Auto-config gagal: {ex}");
        }
    }

    public static void ConfigureScene()
    {
        Material asamMaterial = ConfigurePowderJar(
            "Jar_AsamSalisilat",
            "AsamSalisilat",
            "Asam Salisilat",
            IngredientVisualType.PowderWhiteCrystal,
            new Color(0.97f, 0.975f, 0.96f, 1f),
            AsamScoopMg,
            AsamTargetMg,
            false);

        ConfigurePowderJar(
            "Jar_SulfurPP",
            "SulfurPP",
            "Sulfur PP",
            IngredientVisualType.PowderYellow,
            new Color(1f, 0.9f, 0.46f, 1f),
            SulfurScoopMg,
            SulfurTargetMg,
            false);

        Material creamMaterial = ConfigureCreamJar();

        HornSpoon spoon = Object.FindFirstObjectByType<HornSpoon>(
            FindObjectsInactive.Include);
        if (spoon != null)
        {
            spoon.ConfigureIngredientScoopSupport(true, 0.085f);
            spoon.EnsureCreamVisual(creamMaterial != null ? creamMaterial : asamMaterial);
        }

        BindBench(spoon);
    }

    private static void BindBench(HornSpoon spoon)
    {
        PowderDepositZone depositZone = Object.FindFirstObjectByType<PowderDepositZone>(
            FindObjectsInactive.Include);
        MortarController mortar = Object.FindFirstObjectByType<MortarController>(
            FindObjectsInactive.Include);

        IngredientVisualProfile asam = GetProfile("Jar_AsamSalisilat");
        IngredientVisualProfile sulfur = GetProfile("Jar_SulfurPP");
        IngredientVisualProfile vaselin = GetProfile("Jar_VaselinAlbum");

        SalepBench bench = SalepBench.Instance;
        if (bench == null)
            bench = Object.FindFirstObjectByType<SalepBench>(FindObjectsInactive.Include);

        if (bench == null)
        {
            GameObject host = depositZone != null
                ? depositZone.gameObject
                : (mortar != null ? mortar.gameObject : new GameObject("[SYS] SalepBench"));
            bench = host.AddComponent<SalepBench>();
        }

        bench.Bind(depositZone, spoon, mortar, asam, sulfur, vaselin);

        Debug.Log(
            "[SalepBench] Konfigurasi Salep aktif. " +
            $"Asam={(asam != null ? asam.AmountPerScoopMg + "/" + asam.TargetTotalMg : "null")}, " +
            $"Sulfur={(sulfur != null ? sulfur.AmountPerScoopMg + "/" + sulfur.TargetTotalMg : "null")}, " +
            $"Vaselin={(vaselin != null ? vaselin.AmountPerScoopMg + "/" + vaselin.TargetTotalMg : "null")}, " +
            $"depositZone={(depositZone != null ? "OK" : "MISSING")}, " +
            $"spoon={(spoon != null ? "OK" : "MISSING")}, " +
            $"mortar={(mortar != null ? "OK" : "MISSING")}.");
    }

    private static IngredientVisualProfile GetProfile(string jarName)
    {
        GameObject jar = FindObjectByName(jarName);
        return jar != null ? jar.GetComponent<IngredientVisualProfile>() : null;
    }

    private static Material ConfigurePowderJar(
        string jarName,
        string ingredientId,
        string displayName,
        IngredientVisualType visualType,
        Color color,
        float scoopMg,
        float targetMg,
        bool displayInGrams)
    {
        GameObject jar = FindObjectByName(jarName);
        if (jar == null)
            return null;

        Transform root = FindChild(jar.transform, "PowderVisualRoot");
        Transform top = root != null ? FindChild(root, "PowderTopSurface") : null;
        Transform core = root != null ? FindChild(root, "PowderVolumeCore") : null;
        Renderer renderer = top != null ? top.GetComponent<Renderer>() : null;
        Material material = renderer != null ? renderer.sharedMaterial : null;

        ConfigureVolumeCore(core);

        if (top != null)
        {
            SpoonPowderMoundVisual surface = top.GetComponent<SpoonPowderMoundVisual>();
            if (surface == null)
                surface = top.gameObject.AddComponent<SpoonPowderMoundVisual>();
            surface.Configure(0.122f, 0.122f, 0.004f, 0.011f, 0.0008f, material);
        }

        DisableChildrenByPrefix(root, "PowderMound_");

        IngredientVisualProfile profile = jar.GetComponent<IngredientVisualProfile>();
        if (profile == null)
            profile = jar.AddComponent<IngredientVisualProfile>();
        profile.Configure(
            ingredientId,
            displayName,
            visualType,
            material,
            color,
            new Color(color.r, color.g, color.b, 0.42f));
        profile.ConfigureScoop(scoopMg, targetMg, displayInGrams);

        // Pastikan jar punya stok cukup untuk total target bahan.
        PowderContainer container = jar.GetComponent<PowderContainer>();
        if (container != null)
            container.EnsureStock(targetMg);

        // Sinkronkan scoop amount pada ScoopBottleTarget jika ada.
        ScoopBottleTarget scoopTarget = jar.GetComponentInChildren<ScoopBottleTarget>(true);
        if (scoopTarget != null)
            scoopTarget.ApplyProfileScoopAmount();

        return material;
    }

    private static Material ConfigureCreamJar()
    {
        GameObject jar = FindObjectByName("Jar_VaselinAlbum");
        if (jar == null)
            return null;

        Transform root = FindChild(jar.transform, "CreamVisualRoot");
        Transform top = root != null ? FindChild(root, "CreamTopSurface") : null;
        Transform core = root != null ? FindChild(root, "CreamVolumeCore") : null;
        Renderer renderer = top != null ? top.GetComponent<Renderer>() : null;
        Material material = renderer != null ? renderer.sharedMaterial : null;

        ConfigureVolumeCore(core);

        if (top != null)
        {
            CreamMoundVisual surface = top.GetComponent<CreamMoundVisual>();
            if (surface == null)
                surface = top.gameObject.AddComponent<CreamMoundVisual>();
            surface.Configure(0.12f, 0.12f, 0.005f, 0.026f, 0.025f, 0.0024f, material);
        }

        DisableChildrenByPrefix(root, "CreamFold_");

        Color color = new Color(0.97f, 0.935f, 0.84f, 1f);
        IngredientVisualProfile profile = jar.GetComponent<IngredientVisualProfile>();
        if (profile == null)
            profile = jar.AddComponent<IngredientVisualProfile>();
        profile.Configure(
            "VaselinAlbum",
            "Vaselin Album",
            IngredientVisualType.CreamOintment,
            material,
            color,
            new Color(color.r, color.g, color.b, 0.18f));
        profile.ConfigureScoop(VaselinScoopMg, VaselinTargetMg, true);

        PowderContainer container = jar.GetComponent<PowderContainer>();
        if (container != null)
            container.EnsureStock(VaselinTargetMg);

        ScoopBottleTarget scoopTarget = jar.GetComponentInChildren<ScoopBottleTarget>(true);
        if (scoopTarget != null)
            scoopTarget.ApplyProfileScoopAmount();

        return material;
    }

    private static void ConfigureVolumeCore(Transform core)
    {
        if (core == null)
            return;

        core.localPosition = new Vector3(0f, 0.065f, 0f);
        core.localRotation = Quaternion.identity;
        core.localScale = new Vector3(0.24f, 0.065f, 0.24f);
    }

    private static GameObject FindObjectByName(string objectName)
    {
        Transform[] transforms = Object.FindObjectsByType<Transform>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        foreach (Transform candidate in transforms)
        {
            if (candidate != null && candidate.name == objectName)
                return candidate.gameObject;
        }

        return null;
    }

    private static Transform FindChild(Transform root, string childName)
    {
        if (root == null)
            return null;

        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        foreach (Transform child in children)
        {
            if (child != null && child.name == childName)
                return child;
        }

        return null;
    }

    private static void DisableChildrenByPrefix(Transform root, string prefix)
    {
        if (root == null)
            return;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child.name.StartsWith(prefix))
                child.gameObject.SetActive(false);
        }
    }
}
