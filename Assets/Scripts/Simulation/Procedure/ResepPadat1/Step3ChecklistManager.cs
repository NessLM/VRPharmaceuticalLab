using TMPro;
using UnityEngine;

public class Step3ChecklistManager : MonoBehaviour
{
    [SerializeField] private TMP_Text checklistText;

    private int currentDose = 0;
    private int totalDose = 10;

    private void Start()
    {
        RefreshText();
    }

    public void CheckGrid()
    {
        SetLineOK("Bentangkan");
    }

    public void CheckTakePowder()
    {
        SetLineOK("Ambil bubuk");
    }

    public void UpdateDoseProgress(int current, int total)
    {
        currentDose = current;
        totalDose = total;
        RefreshText();
    }

    public void CheckFinished()
    {
        currentDose = totalDose;
        RefreshText();
        SetLineOK("Isi perkamen");
        SetLineOK("Semua kertas");
    }

    private void RefreshText()
    {
        if (checklistText == null) return;

        checklistText.text =
            "Step 3 - Pembagian Serbuk\n\n" +
            "[ ] Bentangkan 10 kertas perkamen\n" +
            "[ ] Ambil bubuk homogen dari mortar\n" +
            "[ ] Isi perkamen: " + currentDose + "/" + totalDose + "\n" +
            "[ ] Semua kertas perkamen terisi";
    }

    private void SetLineOK(string keyword)
    {
        if (checklistText == null) return;

        string[] lines = checklistText.text.Split('\n');

        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].Contains(keyword))
            {
                lines[i] = "[OK] " + lines[i]
                    .Replace("[ ]", "")
                    .Replace("[OK]", "")
                    .Trim();

                checklistText.text = string.Join("\n", lines);
                return;
            }
        }
    }
}