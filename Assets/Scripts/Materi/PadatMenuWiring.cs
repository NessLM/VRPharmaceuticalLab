using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Bootstrap runtime untuk menu Padat di VRLabSimulation_Padat.
/// Menyambungkan tombol "Quiz" & "Material" saat PLAY MODE tanpa mengedit file scene.
/// </summary>
public static class PadatMenuWiring
{
    private const string SceneName = "VRLabSimulation_Padat";
    private const string PadatTag = "Padat";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (SceneManager.GetActiveScene().name != SceneName)
            return;

        try
        {
            WireMateri();
            WireQuiz();
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[PadatMenuWiring] Gagal wiring: {ex}");
        }
    }

    private static void WireMateri()
    {
        Button materiButton = FindMenuButton("Button_Material");
        if (materiButton == null)
        {
            Debug.LogWarning("[PadatMenuWiring] Button_Material (Padat) tidak ditemukan.");
            return;
        }

        MateriBoardController controller =
            Object.FindFirstObjectByType<MateriBoardController>(FindObjectsInactive.Include);
        if (controller == null)
        {
            Debug.LogWarning("[PadatMenuWiring] MateriBoardController tidak ditemukan di scene.");
            return;
        }

        materiButton.onClick.RemoveListener(controller.OpenMateri);
        materiButton.onClick.AddListener(controller.OpenMateri);

        Debug.Log("[PadatMenuWiring] Button_Material -> MateriBoardController.OpenMateri tersambung.");
    }

    private static void WireQuiz()
    {
        PanelManager panelManager =
            Object.FindFirstObjectByType<PanelManager>(FindObjectsInactive.Include);
        if (panelManager == null || panelManager.gameEvent == null)
        {
            Debug.LogWarning("[PadatMenuWiring] PanelManager / PanelEvent tidak ditemukan.");
            return;
        }

        GameEvent<int> panelEvent = panelManager.gameEvent;
        List<GameObject> panels = GetPanels(panelManager);

        int introIndex = -1;
        if (panels != null)
        {
            for (int i = 0; i < panels.Count; i++)
            {
                if (panels[i] == null)
                    continue;
                if (introIndex < 0 && panels[i].GetComponentInChildren<TMP_InputField>(true) != null)
                {
                    introIndex = i;
                    break;
                }
            }
        }

        // Perbaiki slot Summary
        EnsureSummaryPanel(panels, 5);

        int quizStartId = GetQuizTriggerPanelId();

        // Alur baru: Button_Quiz membuka panel pilih Level (PanelQuiz_Padat) lebih dulu,
        // baru dari sana ke panel Intro (Nama/Kelas). Jika panel level tidak ada,
        // fallback ke panel intro seperti sebelumnya.
        int levelMenuIndex = GetPanelIndexByName(panels, "PanelQuiz_Padat");
        int menuTargetIndex = levelMenuIndex >= 0
            ? levelMenuIndex
            : (introIndex >= 0 ? introIndex : quizStartId);

        Button quizButton = FindMenuButton("Button_Quiz");
        if (quizButton != null)
        {
            int idx = menuTargetIndex;
            quizButton.onClick.AddListener(() => panelEvent.Raise(idx));
            Debug.Log($"[PadatMenuWiring] Button_Quiz -> PanelEvent.Raise({idx}) tersambung.");
        }
        else
        {
            Debug.LogWarning("[PadatMenuWiring] Button_Quiz (Padat) tidak ditemukan.");
        }

        if (introIndex >= 0 && panels != null && introIndex < panels.Count)
        {
            Button mulai = FindButtonInPanel(panels[introIndex], "mulai");
            if (mulai != null)
            {
                int qIdx = quizStartId;
                mulai.onClick.AddListener(() => panelEvent.Raise(qIdx));
                Debug.Log($"[PadatMenuWiring] Tombol 'Mulai' -> PanelEvent.Raise({qIdx}) tersambung.");
            }
        }
    }

    private static void EnsureSummaryPanel(List<GameObject> panels, int summaryIndex)
    {
        if (panels == null)
            return;

        while (panels.Count <= summaryIndex)
            panels.Add(null);

        if (panels[summaryIndex] != null)
            return;

        SummaryUI summary = Object.FindFirstObjectByType<SummaryUI>(FindObjectsInactive.Include);
        if (summary != null)
        {
            panels[summaryIndex] = summary.gameObject;
            Debug.Log($"[PadatMenuWiring] Slot panel Summary (index {summaryIndex}) diisi otomatis.");
        }

        int removed = panels.RemoveAll(p => p == null);
        if (removed > 0)
            Debug.Log($"[PadatMenuWiring] {removed} slot panel kosong dibersihkan.");
    }

    private static int GetQuizTriggerPanelId()
    {
        QuizTrigger trigger = Object.FindFirstObjectByType<QuizTrigger>(FindObjectsInactive.Include);
        if (trigger == null)
            return 3;

        FieldInfo field = typeof(QuizTrigger).GetField(
            "idPanel", BindingFlags.NonPublic | BindingFlags.Instance);
        if (field != null && field.GetValue(trigger) is int id)
            return id;

        return 3;
    }

    private static int GetPanelIndexByName(List<GameObject> panels, string panelName)
    {
        if (panels == null)
            return -1;

        for (int i = 0; i < panels.Count; i++)
        {
            if (panels[i] != null && panels[i].name == panelName)
                return i;
        }
        return -1;
    }

    private static List<GameObject> GetPanels(PanelManager panelManager)
    {
        FieldInfo field = typeof(PanelManager).GetField(
            "panels", BindingFlags.NonPublic | BindingFlags.Instance);
        return field != null ? field.GetValue(panelManager) as List<GameObject> : null;
    }

    private static Button FindMenuButton(string buttonName)
    {
        Button[] buttons = Object.FindObjectsByType<Button>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (Button button in buttons)
        {
            if (button.name == buttonName && HasAncestorContaining(button.transform, PadatTag))
                return button;
        }

        foreach (Button button in buttons)
        {
            if (button.name == buttonName)
                return button;
        }

        return null;
    }

    private static Button FindButtonInPanel(GameObject panel, string labelContainsLower)
    {
        if (panel == null)
            return null;

        Button[] buttons = panel.GetComponentsInChildren<Button>(true);
        foreach (Button button in buttons)
        {
            if (button.name.ToLowerInvariant().Contains(labelContainsLower))
                return button;

            TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
            if (label != null && label.text.ToLowerInvariant().Contains(labelContainsLower))
                return button;
        }

        return null;
    }

    private static bool HasAncestorContaining(Transform start, string token)
    {
        Transform current = start;
        while (current != null)
        {
            if (current.name.Contains(token))
                return true;
            current = current.parent;
        }
        return false;
    }
}
