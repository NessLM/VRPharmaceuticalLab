using UnityEngine;

// Setup runtime untuk bahan salep. Dipanggil manual dari SalepProcedureManager,
// BUKAN auto-run saat scene load (RuntimeInitializeOnLoadMethod dihapus).
public static class SalepIngredientRuntimeSetup
{
    public static void ConfigureScene()
    {
        Material asamMaterial = ConfigurePowderJar(
            "Jar_AsamSalisilat",
            "AsamSalisilat",
            "Asam Salisilat",
            IngredientVisualType.PowderWhiteCrystal,
            new Color(0.97f, 0.975f, 0.96f, 1f));

        ConfigurePowderJar(
            "Jar_SulfurPP",
            "SulfurPP",
            "Sulfur PP",
            IngredientVisualType.PowderYellow,
            new Color(1f, 0.9f, 0.46f, 1f));

        Material creamMaterial = ConfigureCreamJar();

        HornSpoon spoon = Object.FindFirstObjectByType<HornSpoon>(
            FindObjectsInactive.Include);
        if (spoon != null)
        {
            spoon.ConfigureIngredientScoopSupport(true, 0.085f);
            spoon.EnsureCreamVisual(creamMaterial != null ? creamMaterial : asamMaterial);
        }
    }

    private static Material ConfigurePowderJar(
        string jarName,
        string ingredientId,
        string displayName,
        IngredientVisualType visualType,
        Color color)
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
