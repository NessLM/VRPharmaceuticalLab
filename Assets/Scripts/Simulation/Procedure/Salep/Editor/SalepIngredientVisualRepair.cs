#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Manual-only repair tool. Auto-run dihapus agar tidak overwrite material/visual yang sudah diset manual.
// Jalankan hanya lewat menu: Tools > VR Lab > Repair Salep Ingredient Visuals
internal static class SalepIngredientVisualRepair
{
    private const string ScenePath = "Assets/Scenes/VRLabSimulation.unity";
    private const string MaterialFolder = "Assets/Models/Materials/Salep/";

    [MenuItem("Tools/VR Lab/Repair Salep Ingredient Visuals")]
    private static void RepairFromMenu()
    {
        RunRepair();
    }

    private static void RunRepair()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.path != ScenePath)
        {
            Debug.LogWarning("[SalepIngredientVisual] Buka scene VRLabSimulation.unity terlebih dahulu.");
            return;
        }

        Transform models = FindInScene(scene, "Models");
        Transform interactable = models != null ? FindDeepChild(models, "Interactable") : null;
        Transform ingredients = interactable != null
            ? FindDeepChild(interactable, "[OBJ] SalepIngredients")
            : null;

        if (ingredients == null)
        {
            Debug.LogWarning("[SalepIngredientVisual] [OBJ] SalepIngredients tidak ditemukan.");
            return;
        }

        Material asamPowder = LoadMaterial("MAT_AsamSalisilatPowder.mat");
        Material sulfurPowder = LoadMaterial("MAT_SulfurPPPowder.mat");
        Material vaselinCream = LoadMaterial("MAT_VaselinAlbumCream.mat");
        Material vaselinSpoonCream = LoadMaterial("MAT_VaselinAlbumCream_Spoon.mat");
        Material asamAccent = LoadMaterial("MAT_AsamSalisilat_Accent.mat");
        Material sulfurAccent = LoadMaterial("MAT_SulfurPP_Accent.mat");
        Material vaselinAccent = LoadMaterial("MAT_VaselinAlbum_Accent.mat");

        ingredients.gameObject.SetActive(true);

        ConfigurePowderJar(
            ingredients,
            "Jar_AsamSalisilat",
            "Asam Salisilat",
            42f,
            asamPowder,
            asamAccent,
            new Color(0.97f, 0.975f, 0.96f, 1f),
            "AsamSalisilat",
            false);

        ConfigurePowderJar(
            ingredients,
            "Jar_SulfurPP",
            "Sulfur PP",
            48f,
            sulfurPowder,
            sulfurAccent,
            new Color(1f, 0.9f, 0.46f, 1f),
            "SulfurPP",
            true);

        ConfigureCreamJar(
            ingredients,
            "Jar_VaselinAlbum",
            "Vaselin Album",
            42f,
            vaselinCream,
            vaselinAccent,
            vaselinSpoonCream != null ? vaselinSpoonCream : vaselinCream,
            new Color(0.97f, 0.935f, 0.84f, 1f));

        ConfigureHornSpoon(scene, vaselinSpoonCream != null ? vaselinSpoonCream : vaselinCream);

        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("[SalepIngredientVisual] Repair selesai (manual). Simpan scene untuk menyimpan perubahan.");
    }

    private static void ConfigurePowderJar(
        Transform group,
        string jarName,
        string label,
        float fontSize,
        Material powderMaterial,
        Material accentMaterial,
        Color powderColor,
        string ingredientId,
        bool sulfurLayout)
    {
        Transform jar = FindDeepChild(group, jarName);
        if (jar == null)
            return;

        jar.gameObject.SetActive(true);
        DestroyUnusedLabelObjects(jar);
        ConfigureJarLabelMaterial(jar, accentMaterial);
        ConfigureLabel(jar, label, fontSize);
        ConfigureIngredientProfile(
            jar,
            ingredientId,
            label,
            sulfurLayout ? IngredientVisualType.PowderYellow : IngredientVisualType.PowderWhiteCrystal,
            powderMaterial,
            powderColor,
            new Color(powderColor.r, powderColor.g, powderColor.b, 0.42f));

        Transform visualRoot = FindDeepChild(jar, "PowderVisualRoot");
        if (visualRoot == null)
            visualRoot = CreateChild(jar, "PowderVisualRoot");

        visualRoot.gameObject.SetActive(true);
        DisableLegacyVisualChildren(visualRoot);

        Renderer core = EnsurePrimitive(
            visualRoot,
            "PowderVolumeCore",
            PrimitiveType.Cylinder,
            new Vector3(0f, 0.065f, 0f),
            new Vector3(0.24f, 0.065f, 0.24f),
            powderMaterial);

        Renderer topRenderer = EnsurePrimitive(
            visualRoot,
            "PowderTopSurface",
            PrimitiveType.Cylinder,
            new Vector3(0f, 0.135f, 0f),
            Vector3.one,
            powderMaterial);

        SpoonPowderMoundVisual naturalSurface =
            topRenderer.GetComponent<SpoonPowderMoundVisual>();
        if (naturalSurface == null)
            naturalSurface = topRenderer.gameObject.AddComponent<SpoonPowderMoundVisual>();
        naturalSurface.Configure(0.122f, 0.122f, 0.004f, 0.011f, 0.0008f, powderMaterial);

        Vector3[] positions = sulfurLayout
            ? new[]
            {
                new Vector3(-0.048f, 0.147f, -0.018f),
                new Vector3(0.036f, 0.148f, -0.028f),
                new Vector3(-0.012f, 0.151f, 0.028f),
                new Vector3(0.058f, 0.146f, 0.025f),
                new Vector3(-0.058f, 0.145f, 0.035f)
            }
            : new[]
            {
                new Vector3(-0.045f, 0.148f, -0.022f),
                new Vector3(0.038f, 0.147f, -0.025f),
                new Vector3(-0.008f, 0.153f, 0.025f),
                new Vector3(0.055f, 0.146f, 0.032f),
                new Vector3(-0.057f, 0.146f, 0.032f)
            };

        for (int i = 0; i < positions.Length; i++)
        {
            Transform oldMound = FindDirectChild(visualRoot, $"PowderMound_{i + 1:00}");
            if (oldMound != null)
                oldMound.gameObject.SetActive(false);
        }

        ConfigureContainer(jar, visualRoot, core, powderColor);
    }

    private static void ConfigureCreamJar(
        Transform group,
        string jarName,
        string label,
        float fontSize,
        Material creamMaterial,
        Material accentMaterial,
        Material spoonCreamMaterial,
        Color creamColor)
    {
        Transform jar = FindDeepChild(group, jarName);
        if (jar == null)
            return;

        jar.gameObject.SetActive(true);
        DestroyUnusedLabelObjects(jar);
        ConfigureJarLabelMaterial(jar, accentMaterial);
        ConfigureLabel(jar, label, fontSize);
        ConfigureIngredientProfile(
            jar,
            "VaselinAlbum",
            label,
            IngredientVisualType.CreamOintment,
            spoonCreamMaterial,
            creamColor,
            new Color(creamColor.r, creamColor.g, creamColor.b, 0.18f));

        Transform visualRoot = FindDeepChild(jar, "CreamVisualRoot");
        if (visualRoot == null)
            visualRoot = FindDeepChild(jar, "PowderVisualRoot");
        if (visualRoot == null)
            visualRoot = CreateChild(jar, "CreamVisualRoot");

        visualRoot.name = "CreamVisualRoot";
        visualRoot.gameObject.SetActive(true);
        DisableLegacyVisualChildren(visualRoot);

        Renderer core = EnsurePrimitive(
            visualRoot,
            "CreamVolumeCore",
            PrimitiveType.Cylinder,
            new Vector3(0f, 0.065f, 0f),
            new Vector3(0.24f, 0.065f, 0.24f),
            creamMaterial);

        Renderer creamTopRenderer = EnsurePrimitive(
            visualRoot,
            "CreamTopSurface",
            PrimitiveType.Sphere,
            new Vector3(0f, 0.137f, 0f),
            Vector3.one,
            creamMaterial);

        CreamMoundVisual naturalCream = creamTopRenderer.GetComponent<CreamMoundVisual>();
        if (naturalCream == null)
            naturalCream = creamTopRenderer.gameObject.AddComponent<CreamMoundVisual>();
        naturalCream.Configure(0.12f, 0.12f, 0.005f, 0.026f, 0.025f, 0.0024f, creamMaterial);

        Vector3[] foldPositions =
        {
            new Vector3(-0.042f, 0.151f, -0.018f),
            new Vector3(0.035f, 0.154f, -0.018f),
            new Vector3(-0.015f, 0.157f, 0.025f),
            new Vector3(0.055f, 0.149f, 0.032f)
        };

        Vector3[] foldScales =
        {
            new Vector3(0.065f, 0.014f, 0.035f),
            new Vector3(0.072f, 0.015f, 0.032f),
            new Vector3(0.08f, 0.016f, 0.038f),
            new Vector3(0.055f, 0.013f, 0.03f)
        };

        for (int i = 0; i < foldPositions.Length; i++)
        {
            Transform oldFold = FindDirectChild(visualRoot, $"CreamFold_{i + 1:00}");
            if (oldFold != null)
                oldFold.gameObject.SetActive(false);
        }

        ConfigureContainer(jar, visualRoot, core, creamColor);
    }

    private static void ConfigureContainer(
        Transform jar,
        Transform visualRoot,
        Renderer mainRenderer,
        Color color)
    {
        PowderContainer container = jar.GetComponent<PowderContainer>();
        if (container == null)
            return;

        SerializedObject serialized = new SerializedObject(container);
        SetObjectReference(serialized, "powderMesh", visualRoot);
        SetObjectReference(serialized, "powderRenderer", mainRenderer);
        SetVector(serialized, "emptyLocalScale", new Vector3(1f, 0.12f, 1f));
        SetVector(serialized, "fullLocalScale", Vector3.one);
        SetVector(serialized, "emptyLocalPosition", new Vector3(0f, 0.12f, 0f));
        SetVector(serialized, "fullLocalPosition", new Vector3(0f, 0.12f, 0f));

        SerializedProperty powderColor = serialized.FindProperty("powderColor");
        if (powderColor != null)
            powderColor.colorValue = color;

        serialized.ApplyModifiedPropertiesWithoutUndo();
        visualRoot.localPosition = new Vector3(0f, 0.12f, 0f);
        visualRoot.localRotation = Quaternion.identity;
        visualRoot.localScale = Vector3.one;
    }

    private static void ConfigureLabel(
        Transform jar,
        string textValue,
        float fontSize)
    {
        Transform textTransform = FindDeepChild(jar, "Text (TMP)");
        if (textTransform == null)
            return;

        TMP_Text text = textTransform.GetComponent<TMP_Text>();
        if (text == null)
            return;

        text.text = textValue;
        text.fontSize = fontSize;
        text.alignment = TextAlignmentOptions.Center;
        text.characterSpacing = -1.5f;
        text.margin = new Vector4(8f, 3f, 8f, 3f);
        text.overflowMode = TextOverflowModes.Overflow;

        RectTransform rect = textTransform as RectTransform;
        if (rect != null)
            rect.sizeDelta = new Vector2(42f, 9f);

        textTransform.localScale = Vector3.one * 0.0058f;
    }

    private static void ConfigureIngredientProfile(
        Transform jar,
        string ingredientId,
        string displayName,
        IngredientVisualType visualType,
        Material spoonMaterial,
        Color fallbackColor,
        Color scoopFxColor)
    {
        IngredientVisualProfile profile = jar.GetComponent<IngredientVisualProfile>();
        if (profile == null)
            profile = jar.gameObject.AddComponent<IngredientVisualProfile>();

        profile.Configure(
            ingredientId,
            displayName,
            visualType,
            spoonMaterial,
            fallbackColor,
            scoopFxColor);
        EditorUtility.SetDirty(profile);
    }

    private static void ConfigureHornSpoon(Scene scene, Material creamMaterial)
    {
        Transform spoonTransform = FindInScene(scene, "sendokTanduk");
        if (spoonTransform == null)
            return;

        HornSpoon spoon = spoonTransform.GetComponent<HornSpoon>();
        if (spoon == null)
            return;

        Transform holdPoint = FindDeepChild(spoonTransform, "PowderHoldPoint");
        if (holdPoint == null)
            holdPoint = spoonTransform;

        Transform creamTransform = FindDirectChild(holdPoint, "CreamOnSpoonVisual");
        if (creamTransform == null)
            creamTransform = CreateChild(holdPoint, "CreamOnSpoonVisual");

        MeshFilter filter = creamTransform.GetComponent<MeshFilter>();
        if (filter == null)
            filter = creamTransform.gameObject.AddComponent<MeshFilter>();

        MeshRenderer renderer = creamTransform.GetComponent<MeshRenderer>();
        if (renderer == null)
            renderer = creamTransform.gameObject.AddComponent<MeshRenderer>();

        CreamMoundVisual creamVisual = creamTransform.GetComponent<CreamMoundVisual>();
        if (creamVisual == null)
            creamVisual = creamTransform.gameObject.AddComponent<CreamMoundVisual>();

        creamTransform.localPosition = Vector3.zero;
        creamTransform.localRotation = Quaternion.identity;
        creamTransform.localScale = Vector3.one * 0.6f;
        creamVisual.Configure(0.04f, 0.026f, 0.004f, 0.018f, 0.024f, 0.0024f, creamMaterial);
        creamTransform.gameObject.SetActive(false);

        SerializedObject serialized = new SerializedObject(spoon);
        SetBool(serialized, "allowDirectPowderContainerScoop", true);
        SetBool(serialized, "requireIngredientProfileForDirectScoop", true);
        SetBool(serialized, "createCreamVisualIfMissing", true);
        SetFloat(serialized, "detectionRadius", 0.085f);
        SetObjectReference(serialized, "creamMesh", creamTransform);
        SetObjectReference(serialized, "creamRenderer", renderer);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(spoon);
    }

    private static void ConfigureJarLabelMaterial(Transform jar, Material labelMaterial)
    {
        Transform cylinder = FindDeepChild(jar, "Cylinder");
        Renderer renderer = cylinder != null ? cylinder.GetComponent<Renderer>() : null;
        if (renderer == null || labelMaterial == null)
            return;

        Material[] materials = renderer.sharedMaterials;
        if (materials.Length < 2)
        {
            Debug.LogWarning(
                $"[SalepIngredientVisual] {jar.name}/Cylinder tidak memiliki material slot Element 1.");
            return;
        }

        // Element 0 tetap body netral. Hanya slot label (Element 1) yang diganti.
        materials[1] = labelMaterial;
        renderer.sharedMaterials = materials;
    }

    private static void DestroyUnusedLabelObjects(Transform jar)
    {
        string[] unusedNames =
        {
            "LabelAccentBand",
            "[REMOVED] Unused Label Accent",
            "LabelSticker",
            "LabelVisual",
            "LabelBackground"
        };

        for (int i = jar.childCount - 1; i >= 0; i--)
        {
            Transform child = jar.GetChild(i);
            for (int j = 0; j < unusedNames.Length; j++)
            {
                if (child.name != unusedNames[j])
                    continue;

                Object.DestroyImmediate(child.gameObject);
                break;
            }
        }
    }

    private static void DisableLegacyVisualChildren(Transform root)
    {
        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            string name = child.name;
            bool keep =
                name == "PowderVolumeCore" ||
                name == "PowderTopSurface" ||
                name.StartsWith("PowderMound_") ||
                name == "CreamVolumeCore" ||
                name == "CreamTopSurface" ||
                name.StartsWith("CreamFold_");

            if (!keep)
                child.gameObject.SetActive(false);
        }
    }

    private static Renderer EnsurePrimitive(
        Transform parent,
        string name,
        PrimitiveType primitiveType,
        Vector3 localPosition,
        Vector3 localScale,
        Material material)
    {
        Transform child = FindDirectChild(parent, name);
        GameObject gameObject;

        if (child == null)
        {
            gameObject = GameObject.CreatePrimitive(primitiveType);
            gameObject.name = name;
            gameObject.transform.SetParent(parent, false);
        }
        else
        {
            gameObject = child.gameObject;
        }

        gameObject.layer = parent.gameObject.layer;
        gameObject.SetActive(true);
        gameObject.transform.localPosition = localPosition;
        gameObject.transform.localRotation = Quaternion.identity;
        gameObject.transform.localScale = localScale;

        Collider collider = gameObject.GetComponent<Collider>();
        if (collider != null)
            Object.DestroyImmediate(collider);

        Renderer renderer = gameObject.GetComponent<Renderer>();
        if (renderer != null && material != null)
            renderer.sharedMaterial = material;

        return renderer;
    }

    private static Transform CreateChild(Transform parent, string name)
    {
        GameObject child = new GameObject(name);
        child.transform.SetParent(parent, false);
        return child.transform;
    }

    private static void SetObjectReference(
        SerializedObject serialized,
        string propertyName,
        Object value)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property != null)
            property.objectReferenceValue = value;
    }

    private static void SetVector(
        SerializedObject serialized,
        string propertyName,
        Vector3 value)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property != null)
            property.vector3Value = value;
    }

    private static void SetBool(
        SerializedObject serialized,
        string propertyName,
        bool value)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property != null)
            property.boolValue = value;
    }

    private static void SetFloat(
        SerializedObject serialized,
        string propertyName,
        float value)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property != null)
            property.floatValue = value;
    }

    private static Material LoadMaterial(string fileName)
    {
        return AssetDatabase.LoadAssetAtPath<Material>(MaterialFolder + fileName);
    }

    private static Transform FindInScene(Scene scene, string objectName)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            Transform found = FindDeepChild(roots[i].transform, objectName);
            if (found != null)
                return found;
        }

        return null;
    }

    private static Transform FindDeepChild(Transform parent, string objectName)
    {
        if (parent == null)
            return null;
        if (parent.name == objectName)
            return parent;

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform found = FindDeepChild(parent.GetChild(i), objectName);
            if (found != null)
                return found;
        }

        return null;
    }

    private static Transform FindDirectChild(Transform parent, string objectName)
    {
        if (parent == null)
            return null;

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.name == objectName)
                return child;
        }

        return null;
    }
}
#endif
