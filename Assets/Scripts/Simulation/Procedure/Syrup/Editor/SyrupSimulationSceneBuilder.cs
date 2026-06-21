using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Read-only scene check. Etiket panel layout and Lazy Follow settings are authored
/// directly in VRLabSimulation.unity and are never rebuilt or overwritten by code.
/// </summary>
public static class SyrupSimulationSceneBuilder
{
    [MenuItem("Tools/VR Lab/Validate Syrup Scene")]
    private static void ValidateLoadedScene()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.path != "Assets/Scenes/VRLabSimulation.unity")
        {
            Debug.LogWarning("[SyrupScene] Open Assets/Scenes/VRLabSimulation.unity before validating.");
            return;
        }

        EtiketWorkflow workflow = Object.FindFirstObjectByType<EtiketWorkflow>(FindObjectsInactive.Include);
        GameObject panel = FindSceneObject("[UI] Etiket World Panel");
        GameObject bottle = FindSceneObject("bottle");

        if (workflow == null || panel == null || bottle == null)
        {
            Debug.LogError("[SyrupScene] Missing EtiketWorkflow, Etiket panel, or bottle reference.");
            return;
        }

        Debug.Log("[SyrupScene] Scene references are present. Panel configuration remains scene-authored.");
    }

    private static GameObject FindSceneObject(string objectName)
    {
        GameObject[] objects = Object.FindObjectsByType<GameObject>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        foreach (GameObject candidate in objects)
        {
            if (candidate.scene.IsValid() && candidate.name == objectName)
                return candidate;
        }

        return null;
    }
}
