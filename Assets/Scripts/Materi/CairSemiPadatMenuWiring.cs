using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Bootstrap runtime untuk menu CairSemiPadat di VRLabSimulation.
/// Menyambungkan tombol "Quiz" &amp; "Material" saat PLAY MODE tanpa mengedit file scene
/// (pola yang sama dengan SalepBench yang sudah terbukti jalan). AMAN: hanya Play Mode,
/// tidak menyimpan scene.
///
/// Yang dilakukan:
///  - MATERI: buat MateriBoardController otomatis (jika belum ada) + wire Button_Material
///    OnClick -> OpenMateri (muncul melayang di depan pemain).
///  - QUIZ: perbaiki slot panel Summary (index 5 yang null di PanelManager) + wire
///    Button_Quiz OnClick -> tampilkan panel nama/kelas (atau panel soal). Wire tombol
///    "Mulai" pada panel nama/kelas -> mulai quiz.
///
/// Tidak menyentuh/menimpa script milik VRLab; semua via penambahan listener runtime.
/// </summary>
public static class CairSemiPadatMenuWiring
{
    private const string SceneName = "VRLabSimulation";
    private const string CairTag = "CairSemiPadat";

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
            Debug.LogError($"[CairSemiPadatMenuWiring] Gagal wiring: {ex}");
        }
    }

    // ------------------------------------------------------------------ MATERI
    private static void WireMateri()
    {
        Button materiButton = FindMenuButton("Button_Material");
        if (materiButton == null)
        {
            Debug.LogWarning("[CairSemiPadatMenuWiring] Button_Material (CairSemiPadat) tidak ditemukan.");
            return;
        }

        MateriBoardController controller =
            Object.FindFirstObjectByType<MateriBoardController>(FindObjectsInactive.Include);
        if (controller == null)
        {
            GameObject host = new GameObject("MateriBoard (auto)");
            controller = host.AddComponent<MateriBoardController>();
        }

        materiButton.onClick.RemoveListener(controller.OpenMateri);
        materiButton.onClick.AddListener(controller.OpenMateri);

        Debug.Log("[CairSemiPadatMenuWiring] Button_Material -> MateriBoardController.OpenMateri tersambung.");
    }

    // -------------------------------------------------------------------- QUIZ
    private static void WireQuiz()
    {
        PanelManager panelManager =
            Object.FindFirstObjectByType<PanelManager>(FindObjectsInactive.Include);
        if (panelManager == null || panelManager.gameEvent == null)
        {
            Debug.LogWarning("[CairSemiPadatMenuWiring] PanelManager / PanelEvent tidak ditemukan.");
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

        // Perbaiki slot Summary (QuizManager.FinishQuiz me-Raise panel index 5).
        EnsureSummaryPanel(panels, 5);

        // Index yang memicu StartQuiz (QuizTrigger.idPanel, default 3 = panel soal).
        int quizStartId = GetQuizTriggerPanelId();

        // Button_Quiz menampilkan panel nama/kelas dulu bila ada; jika tidak, langsung soal.
        int menuTargetIndex = introIndex >= 0 ? introIndex : quizStartId;

        Button quizButton = FindMenuButton("Button_Quiz");
        if (quizButton != null)
        {
            int idx = menuTargetIndex;
            quizButton.onClick.AddListener(() => panelEvent.Raise(idx));
            Debug.Log($"[CairSemiPadatMenuWiring] Button_Quiz -> PanelEvent.Raise({idx}) tersambung.");
        }
        else
        {
            Debug.LogWarning("[CairSemiPadatMenuWiring] Button_Quiz (CairSemiPadat) tidak ditemukan.");
        }

        // Tombol "Mulai" pada panel nama/kelas -> panel soal (memicu StartQuiz via QuizTrigger).
        if (introIndex >= 0 && panels != null && introIndex < panels.Count)
        {
            Button mulai = FindButtonInPanel(panels[introIndex], "mulai");
            if (mulai != null)
            {
                int qIdx = quizStartId;
                mulai.onClick.AddListener(() => panelEvent.Raise(qIdx));
                Debug.Log($"[CairSemiPadatMenuWiring] Tombol 'Mulai' -> PanelEvent.Raise({qIdx}) tersambung.");
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
            Debug.Log($"[CairSemiPadatMenuWiring] Slot panel Summary (index {summaryIndex}) diisi otomatis.");
        }

        // Buang slot null sisanya supaya PanelManager.ShowPanel tidak NRE saat
        // memanggil SetActive pada entri kosong.
        int removed = panels.RemoveAll(p => p == null);
        if (removed > 0)
            Debug.Log($"[CairSemiPadatMenuWiring] {removed} slot panel kosong dibersihkan.");
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

    private static List<GameObject> GetPanels(PanelManager panelManager)
    {
        FieldInfo field = typeof(PanelManager).GetField(
            "panels", BindingFlags.NonPublic | BindingFlags.Instance);
        return field != null ? field.GetValue(panelManager) as List<GameObject> : null;
    }

    // --------------------------------------------------------------- HELPERS
    private static Button FindMenuButton(string buttonName)
    {
        Button[] buttons = Object.FindObjectsByType<Button>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (Button button in buttons)
        {
            if (button.name == buttonName && HasAncestorContaining(button.transform, CairTag))
                return button;
        }

        // Fallback: nama persis tanpa cek ancestor.
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
