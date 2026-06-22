#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Manual-only repair tool. Auto-run dihapus agar tidak overwrite layout yang sudah diset manual.
// Jalankan hanya lewat menu: Tools > VR Lab > Repair Salep Hierarchy And Intro
internal static class SalepSceneVisualRepair
{
    private const string ScenePath = "Assets/Scenes/VRLabSimulation.unity";

    private const string RecipeText =
        "R/ Asam Salisilat 200 mg\n" +
        "Sulfur PP 400 mg\n" +
        "Vaselin Album ad 10 g\n\n" +
        "M.f. Unguentum\n" +
        "S. u.e.";

    private const string MeaningText =
        "Arti Resep:\n\n" +
        "Sediaan dibuat dalam bentuk salep dengan bobot akhir 10 gram.\n" +
        "Asam Salisilat dan Sulfur PP digunakan sebagai zat aktif.\n" +
        "Vaselin Album digunakan sebagai basis salep hingga mencapai 10 gram.";

    private const string CompositionText =
        "Komposisi:\n\n" +
        "Dalam 10 gram salep mengandung:\n" +
        "Asam Salisilat 200 mg\n" +
        "Sulfur PP 400 mg\n" +
        "Vaselin Album ad 10 g";

    [MenuItem("Tools/VR Lab/Repair Salep Hierarchy And Intro")]
    private static void RepairFromMenu()
    {
        RunRepair();
    }

    private static void RunRepair()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.path != ScenePath)
        {
            Debug.LogWarning("[SalepSceneRepair] Buka scene VRLabSimulation.unity terlebih dahulu.");
            return;
        }

        Transform models = FindInScene(scene, "Models");
        Transform interactable = models != null ? FindDeepChild(models, "Interactable") : null;
        Transform procedureSystems = FindInScene(scene, "[SYS] ProcedureSystems");
        Transform syrupSystem = procedureSystems != null
            ? FindDeepChild(procedureSystems, "[SYS] SyrupProcedureSystem")
            : null;
        Transform salepSystem = procedureSystems != null
            ? FindDeepChild(procedureSystems, "[SYS] SalepProcedureSystem")
            : null;

        if (interactable == null || syrupSystem == null || salepSystem == null)
        {
            Debug.LogWarning("[SalepSceneRepair] Hierarchy utama tidak lengkap. Repair tidak dijalankan.");
            return;
        }

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Repair Salep hierarchy and intro");

        bool changed = false;

        Transform ingredientGroup = FindDeepChild(interactable, "[OBJ] SalepIngredients");
        if (ingredientGroup == null)
            ingredientGroup = FindDeepChild(salepSystem, "SalepMaterials");

        Transform duplicateTools = FindDeepChild(salepSystem, "SalepObjects");
        if (ingredientGroup != null)
        {
            Undo.RecordObject(ingredientGroup.gameObject, "Move Salep ingredients");
            Undo.SetTransformParent(ingredientGroup, interactable, "Move Salep ingredients");
            ingredientGroup.name = "[OBJ] SalepIngredients";
            ingredientGroup.gameObject.SetActive(true);
            changed = true;
        }

        if (duplicateTools != null && duplicateTools != ingredientGroup)
        {
            Undo.RecordObject(duplicateTools.gameObject, "Archive duplicate Salep tools");
            Undo.SetTransformParent(duplicateTools, models, "Archive duplicate Salep tools");
            duplicateTools.name = "[ARCHIVE] Duplicate Salep Tools";
            duplicateTools.gameObject.SetActive(false);
            changed = true;
        }

        Undo.RecordObject(salepSystem, "Align Salep system");
        salepSystem.localPosition = syrupSystem.localPosition;
        salepSystem.localRotation = syrupSystem.localRotation;
        salepSystem.localScale = syrupSystem.localScale;
        changed = true;

        Transform syrupIntro = FindDeepChild(syrupSystem, "SyrupIntroPanel");
        Transform salepIntro = FindDeepChild(salepSystem, "SalepIntroPanel");
        if (syrupIntro != null && salepIntro != null)
        {
            CopyRectTransform(syrupIntro as RectTransform, salepIntro as RectTransform);

            RectTransform syrupWindow = FindDeepChild(syrupIntro, "RecipeWindow") as RectTransform;
            RectTransform salepWindow = FindDeepChild(salepIntro, "RecipeWindow") as RectTransform;
            CopyRectTransform(syrupWindow, salepWindow);

            Transform introBack = FindDeepChild(salepIntro, "BTN_BackSalep_Intro");
            if (introBack != null)
            {
                Undo.RecordObject(introBack.gameObject, "Hide overlapping Salep back button");
                introBack.gameObject.SetActive(false);
            }

            ConfigureIntroText(salepIntro);
            changed = true;
        }

        Transform stepUi = FindDeepChild(salepSystem, "SalepStepUI");
        if (stepUi != null)
        {
            Undo.RecordObject(stepUi.gameObject, "Hide Salep step UI before start");
            stepUi.gameObject.SetActive(false);
            changed = true;
        }

        if (changed)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            Undo.CollapseUndoOperations(undoGroup);
            Debug.Log("[SalepSceneRepair] Repair selesai (manual). Simpan scene untuk menyimpan perubahan.");
        }
    }

    private static void ConfigureIntroText(Transform salepIntro)
    {
        SetText(FindDeepChild(salepIntro, "TXT_Title"), "RESEP OBAT SEMI PADAT", 10f);
        SetText(FindDeepChild(salepIntro, "TXT_RecipeContent "),
            "Perhatikan Resep Sebelum Simulasi!", 5f);

        Transform first = FindDeepChild(salepIntro, "[1]_Child_RecipeContent");
        Transform second = FindDeepChild(salepIntro, "[2]_Child_RecipeContent");
        Transform third = FindDeepChild(salepIntro, "[3]_Child_RecipeContent");

        ConfigureTextBlock(first as RectTransform, RecipeText, 4f, new Vector2(-1.6f, 27f),
            new Vector2(140f, 27f));
        ConfigureTextBlock(second as RectTransform, MeaningText, 3.5f, new Vector2(-1.6f, 0f),
            new Vector2(140f, 28f));
        ConfigureTextBlock(third as RectTransform, CompositionText, 3.5f, new Vector2(-0.9f, -30f),
            new Vector2(150f, 25f));

        Transform start = FindDeepChild(salepIntro, "BTN_StartSalep");
        if (start != null)
            SetText(FindDeepChild(start, "Text"), "MULAI SIMULASI!", 5f);
    }

    private static void ConfigureTextBlock(
        RectTransform rect,
        string value,
        float fontSize,
        Vector2 anchoredPosition,
        Vector2 sizeDelta)
    {
        if (rect == null)
            return;

        Undo.RecordObject(rect, "Configure Salep recipe text");
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;
        SetText(rect, value, fontSize);
    }

    private static void SetText(Transform transform, string value, float fontSize)
    {
        if (transform == null)
            return;

        TMP_Text text = transform.GetComponent<TMP_Text>();
        if (text == null)
            return;

        Undo.RecordObject(text, "Configure Salep intro text");
        text.text = value;
        text.fontSize = fontSize;
        text.enableAutoSizing = false;
    }

    private static void CopyRectTransform(RectTransform source, RectTransform destination)
    {
        if (source == null || destination == null)
            return;

        Undo.RecordObject(destination, "Match Syrup intro transform");
        destination.localPosition = source.localPosition;
        destination.localRotation = source.localRotation;
        destination.localScale = source.localScale;
        destination.anchorMin = source.anchorMin;
        destination.anchorMax = source.anchorMax;
        destination.anchoredPosition = source.anchoredPosition;
        destination.sizeDelta = source.sizeDelta;
        destination.pivot = source.pivot;
    }

    private static Transform FindInScene(Scene scene, string targetName)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == targetName)
                return root.transform;

            Transform found = FindDeepChild(root.transform, targetName);
            if (found != null)
                return found;
        }

        return null;
    }

    private static Transform FindDeepChild(Transform root, string targetName)
    {
        if (root == null)
            return null;

        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        foreach (Transform child in children)
        {
            if (child != null && child.name == targetName)
                return child;
        }

        return null;
    }
}
#endif
