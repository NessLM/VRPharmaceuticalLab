using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Ditempel pada tombol Level (1/2/3) di PanelQuiz_Padat.
/// Saat diklik: menetapkan level pada QuizManager lalu membuka panel berikutnya
/// (default: panel Intro Nama/Kelas), mengikuti pola PanelTrigger.
/// </summary>
[RequireComponent(typeof(Button))]
public class QuizLevelSelector : MonoBehaviour
{
    [SerializeField] private QuizManager quizManager;
    [SerializeField] private PanelEvent panelEvent;

    [Tooltip("Level yang dipilih: 1 = Mudah, 2 = Sedang, 3 = Sulit.")]
    [SerializeField] private int level = 1;

    [Tooltip("Index panel yang dibuka setelah level dipilih (panel Intro Nama/Kelas).")]
    [SerializeField] private int nextPanelId = 7;

    private void Start()
    {
        GetComponent<Button>().onClick.AddListener(OnClick);
    }

    public void OnClick()
    {
        if (quizManager != null)
            quizManager.SetLevel(level);

        if (panelEvent != null)
            panelEvent.Raise(nextPanelId);
    }
}
