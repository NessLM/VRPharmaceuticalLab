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
    private const float VaselinScoopMg = 1000f;    // 1 g per scoop
    private const float VaselinTargetMg = 10000f;  // 10 g sesuai resep

    // Gelas pelarut Etanol untuk salep (object scene, bukan prefab).
    private const string EthanolGlassName = "Gelas pyrex 500ml";
    private static LiquidData ethanolLiquidCache;

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
        // Resep editable (jika ada) jadi sumber takaran. Fallback ke konstanta.
        SalepRecipeDefinition recipe = ResolveRecipe();
        SalepRecipeDefinition.Ingredient asam = recipe != null ? recipe.asamSalisilat : null;
        SalepRecipeDefinition.Ingredient sulfur = recipe != null ? recipe.sulfurPP : null;

        Material asamMaterial = ConfigurePowderJar(
            "Jar_AsamSalisilat",
            "AsamSalisilat",
            "Asam Salisilat",
            IngredientVisualType.PowderWhiteCrystal,
            asam != null ? asam.color : new Color(0.97f, 0.975f, 0.96f, 1f),
            asam != null ? asam.AmountPerScoopMg : AsamScoopMg,
            asam != null ? asam.TargetTotalMg : AsamTargetMg,
            false);

        ConfigurePowderJar(
            "Jar_SulfurPP",
            "SulfurPP",
            "Sulfur PP",
            IngredientVisualType.PowderYellow,
            sulfur != null ? sulfur.color : new Color(1f, 0.9f, 0.46f, 1f),
            sulfur != null ? sulfur.AmountPerScoopMg : SulfurScoopMg,
            sulfur != null ? sulfur.TargetTotalMg : SulfurTargetMg,
            false);

        Material creamMaterial = ConfigureCreamJar(recipe);

        HornSpoon spoon = Object.FindFirstObjectByType<HornSpoon>(
            FindObjectsInactive.Include);
        if (spoon != null)
        {
            // PENTING: matikan legacy direct-scoop (Physics.OverlapSphere per-frame yang
            // mengisi sendok 80 mg/s saat ujung sendok dekat toples). Itu penyebab
            // "didekatkan + nunggu → sendok penuh" tanpa trigger. Difenhidramin tidak
            // pernah mengaktifkannya, jadi dia trigger-only. Scoop Salep sekarang HANYA
            // lewat trigger ScoopBottleTarget (animasi sendok masuk-keluar), sama persis.
            spoon.ConfigureIngredientScoopSupport(false, 0.085f);
            spoon.EnsureCreamVisual(creamMaterial != null ? creamMaterial : asamMaterial);
        }

        BindBench(spoon, creamMaterial);

        // CATATAN: Etanol di Gelas Pyrex 500ml TIDAK diisi di sini. ConfigureScene jalan
        // tiap Play (auto) untuk SEMUA prosedur, jadi mengisi di sini membuat air muncul
        // dari awal. Pengisian dilakukan SalepProcedureManager saat masuk Step 3
        // (Asam Salisilat -> Mortar) lewat FillEthanolGlass(). Lihat juga EmptyEthanolGlass().
    }

    private static void BindBench(HornSpoon spoon, Material creamMaterial)
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

        // Visual krim Vaselin di piring (menggantikan mound bubuk saat menimbang Vaselin).
        if (depositZone != null)
        {
            PowderVisualLevelSwitcher switcher = depositZone.DepositVisual;
            SalepPanCreamVisual panCream = depositZone.GetComponent<SalepPanCreamVisual>();
            if (panCream == null)
                panCream = depositZone.gameObject.AddComponent<SalepPanCreamVisual>();
            panCream.Setup(depositZone, switcher, creamMaterial);
            bench.SetPanCream(panCream);
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

    // Isi Gelas Pyrex 500ml penuh dengan Etanol & beri label "Etanol".
    // Dipanggil saat masuk Step 3 (Asam Salisilat -> Mortar), BUKAN saat ConfigureScene,
    // supaya air baru muncul di tahap yang benar (bukan dari awal Play). Idempotent.
    public static void FillEthanolGlass()
    {
        LiquidContainer container = ResolveEthanolContainer();
        if (container == null)
            return;

        // Sudah terisi (mis. step di-replay) → jangan reset ke penuh, biarkan apa adanya.
        if (!container.IsEmpty)
            return;

        LiquidData ethanol = GetEthanolLiquid();
        container.SetLiquid(container.CapacityMl, ethanol);

        Debug.Log(
            $"[SalepIngredientRuntimeSetup] Etanol disiapkan penuh di '{EthanolGlassName}' " +
            $"({container.CapacityMl} ml).");
    }

    // Kosongkan Gelas Pyrex 500ml (dipakai saat mulai/keluar prosedur Salep agar air
    // tidak ada sampai tiba di Step 3).
    public static void EmptyEthanolGlass()
    {
        LiquidContainer container = ResolveEthanolContainer();
        if (container != null)
            container.EmptyLiquid();
    }

    private static LiquidContainer ResolveEthanolContainer()
    {
        GameObject glass = FindObjectByName(EthanolGlassName);
        if (glass == null)
        {
            Debug.LogWarning(
                $"[SalepIngredientRuntimeSetup] '{EthanolGlassName}' tidak ditemukan; Etanol tidak disiapkan.");
            return null;
        }

        LiquidContainer container = glass.GetComponent<LiquidContainer>();
        if (container == null)
            Debug.LogWarning(
                $"[SalepIngredientRuntimeSetup] '{EthanolGlassName}' tidak punya LiquidContainer.");

        return container;
    }

    private static LiquidData GetEthanolLiquid()
    {
        if (ethanolLiquidCache != null)
            return ethanolLiquidCache;

        // Pakai asset Resources/Ethanol kalau ada (bisa diedit di Inspector),
        // selain itu buat instance runtime ringan.
        LiquidData asset = Resources.Load<LiquidData>("Ethanol");
        if (asset != null)
        {
            ethanolLiquidCache = asset;
            return asset;
        }

        LiquidData runtime = ScriptableObject.CreateInstance<LiquidData>();
        runtime.name = "Etanol";
        runtime.liquidName = "Etanol";
        // Etanol bening: tint dingin sangat tipis dengan alpha rendah.
        runtime.liquidColor = new Color(0.85f, 0.92f, 1f, 0.28f);
        ethanolLiquidCache = runtime;
        return runtime;
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
        {
            scoopTarget.ApplyProfileScoopAmount();
            // Gaya Difenhidramin: scoop hanya lewat trigger + animasi sendok masuk-keluar,
            // bukan "didekatkan langsung dapat".
            scoopTarget.SetAutoScoop(false);
        }

        return material;
    }

    private static SalepRecipeDefinition ResolveRecipe()
    {
        SalepBench bench = Object.FindFirstObjectByType<SalepBench>(FindObjectsInactive.Include);
        if (bench != null && bench.Recipe != null)
            return bench.Recipe;

        return Resources.Load<SalepRecipeDefinition>("SalepRecipe_Default");
    }

    private static Material ConfigureCreamJar(SalepRecipeDefinition recipe)
    {
        GameObject jar = FindObjectByName("Jar_VaselinAlbum");
        if (jar == null)
            return null;

        SalepRecipeDefinition.Ingredient vaselin = recipe != null ? recipe.vaselinAlbum : null;
        float vaselinScoopMg = vaselin != null ? vaselin.AmountPerScoopMg : VaselinScoopMg;
        float vaselinTargetMg = vaselin != null ? vaselin.TargetTotalMg : VaselinTargetMg;

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

        Color color = vaselin != null ? vaselin.color : new Color(0.97f, 0.935f, 0.84f, 1f);
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
        profile.ConfigureScoop(vaselinScoopMg, vaselinTargetMg, true);

        PowderContainer container = jar.GetComponent<PowderContainer>();
        if (container != null)
            container.EnsureStock(vaselinTargetMg);

        ScoopBottleTarget scoopTarget = jar.GetComponentInChildren<ScoopBottleTarget>(true);
        if (scoopTarget != null)
        {
            scoopTarget.ApplyProfileScoopAmount();
            // Gaya Difenhidramin: scoop hanya lewat trigger + animasi sendok masuk-keluar.
            scoopTarget.SetAutoScoop(false);
        }

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
