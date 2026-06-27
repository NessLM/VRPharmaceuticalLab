using UnityEngine;

public class DoseDistributionManager : MonoBehaviour
{
    [SerializeField] private int requiredDoseCount = 10;

    private int currentDoseCount = 0;

    public void AddDose()
    {
        currentDoseCount++;

        Debug.Log("Perkamen terisi: " + currentDoseCount + " / " + requiredDoseCount);

        if (currentDoseCount >= requiredDoseCount)
        {
            Debug.Log("Semua 10 perkamen sudah terisi bubuk.");
        }
    }
}