using TMPro;
using UnityEngine;

public class Step3ChecklistManager : MonoBehaviour
{
    [SerializeField] private TMP_Text checklistText;

    private int currentDose = 0;
    private int totalDose = 10;

    private bool gridDone = false;
    private bool takePowderDone = false;
    private bool doseDone = false;
    private bool allDone = false;

    private void Start()
    {
        RefreshText();
    }

    public void CheckGrid()
    {
        gridDone = true;
        RefreshText();
    }

    public void CheckTakePowder()
    {
        takePowderDone = true;
        RefreshText();
    }

    public void UpdateDoseProgress(int current, int total)
    {
        currentDose = current;
        totalDose = total;

        if (currentDose > 0)
            doseDone = true;

        RefreshText();
    }

    public void CheckFinished()
    {
        currentDose = totalDose;
        doseDone = true;
        allDone = true;
        RefreshText();
    }

    private void RefreshText()
    {
        if (checklistText == null) return;

        checklistText.text =
            "Step 3 - Pembagian Serbuk\n\n" +
            (gridDone ? "[OK] " : "[ ] ") + "Bentangkan 10 kertas perkamen\n" +
            (takePowderDone ? "[OK] " : "[ ] ") + "Ambil bubuk homogen dari mortar\n" +
            (doseDone ? "[OK] " : "[ ] ") + "Isi perkamen: " + currentDose + "/" + totalDose + "\n" +
            (allDone ? "[OK] " : "[ ] ") + "Semua kertas perkamen terisi";
    }
}