using UnityEngine;

public class DoseDistributionManager : MonoBehaviour
{
    [SerializeField] private int requiredDoseCount = 10;
    [SerializeField] private Step3ChecklistManager checklistManager;

    private int currentDoseCount = 0;

    private void Start()
    {
        if (checklistManager != null)
            checklistManager.UpdateDoseProgress(0, requiredDoseCount);
    }

    public void AddDose()
    {
        currentDoseCount++;

        Debug.Log("Perkamen terisi: " + currentDoseCount + " / " + requiredDoseCount);

        if (checklistManager != null)
            checklistManager.UpdateDoseProgress(currentDoseCount, requiredDoseCount);

        if (currentDoseCount >= requiredDoseCount)
        {
            if (checklistManager != null)
                checklistManager.CheckFinished();

            Debug.Log("Semua 10 perkamen sudah terisi bubuk.");
        }
    }
}