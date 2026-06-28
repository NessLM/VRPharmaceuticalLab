using TMPro;
using UnityEngine;

public class Step4ChecklistManager : MonoBehaviour
{
    [SerializeField] private TMP_Text checklistText;

    private bool bottleOpened = false;
    private bool capsulesReady = false;
    private bool autoFillStarted = false;
    private bool allCapsulesFilled = false;

    private void Start()
    {
        RefreshText();
    }

    public void CheckBottleOpened()
    {
        bottleOpened = true;
        RefreshText();
    }

    public void CheckCapsulesReady()
    {
        capsulesReady = true;
        RefreshText();
    }

    public void CheckAutoFillStarted()
    {
        autoFillStarted = true;
        RefreshText();
    }

    public void CheckAllCapsulesFilled()
    {
        allCapsulesFilled = true;
        RefreshText();
    }

    private void RefreshText()
    {
        if (checklistText == null) return;

        checklistText.text =
            "Step 4 - Pengisian Kapsul\n\n" +
            (bottleOpened ? "[OK] " : "[ ] ") + "Buka botol kapsul\n" +
            (capsulesReady ? "[OK] " : "[ ] ") + "Siapkan kapsul kosong\n" +
            (autoFillStarted ? "[OK] " : "[ ] ") + "Jalankan pengisian kapsul otomatis\n" +
            (allCapsulesFilled ? "[OK] " : "[ ] ") + "Semua kapsul terisi";
    }
}