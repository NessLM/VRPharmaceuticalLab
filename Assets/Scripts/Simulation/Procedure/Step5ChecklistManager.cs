using TMPro;
using UnityEngine;

public class Step5ChecklistManager : MonoBehaviour
{
    [SerializeField] private TMP_Text checklistText;

    private bool capsulesInserted = false;
    private bool labelWritten = false;
    private bool bagClosed = false;
    private bool finished = false;

    private void Start()
    {
        RefreshText();
    }

    public void CheckCapsulesInserted()
    {
        capsulesInserted = true;
        RefreshText();
    }

    public void CheckLabelWritten()
    {
        labelWritten = true;
        RefreshText();
    }

    public void CheckBagClosed()
    {
        bagClosed = true;
        RefreshText();
    }

    public void CheckFinished()
    {
        finished = true;
        RefreshText();
    }

    private void RefreshText()
    {
        if (checklistText == null)
            return;

        checklistText.text =
            "Step 5 - Pengemasan\n\n" +
            (capsulesInserted ? "[OK] " : "[ ] ") + "Masukkan 10 kapsul ke plastik\n" +
            (labelWritten ? "[OK] " : "[ ] ") + "Tulis etiket\n" +
            (bagClosed ? "[OK] " : "[ ] ") + "Tutup plastik klip\n" +
            (finished ? "[OK] " : "[ ] ") + "Penyerahan obat selesai";
    }

    
}