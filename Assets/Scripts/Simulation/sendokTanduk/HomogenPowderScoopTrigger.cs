using UnityEngine;

public class HomogenPowderScoopTrigger : MonoBehaviour
{
    [Header("Visual bubuk homogen di mortar dari penuh ke habis")]
    [SerializeField] private GameObject[] powderLevels;

    [Header("Target jumlah pengambilan")]
    [SerializeField] private int requiredScoops = 10;

    [Header("Checklist Step 3")]
    [SerializeField] private Step3ChecklistManager checklistManager;

    private int currentScoops = 0;
    private bool alreadyCheckedTakePowder = false;

    private void Start()
    {
        ShowPowderLevel(0);
    }

    private void OnTriggerEnter(Collider other)
    {
        PowderScoopController scoop = other.GetComponentInParent<PowderScoopController>();

        if (scoop == null)
            return;

        if (scoop.HasPowder)
            return;

        if (currentScoops >= requiredScoops)
            return;

        currentScoops++;

        scoop.TakePowder();

        if (!alreadyCheckedTakePowder && checklistManager != null)
        {
            checklistManager.CheckTakePowder();
            alreadyCheckedTakePowder = true;
        }

        UpdateMortarPowderVisual();

        Debug.Log("Sendok mengambil bubuk homogen: " + currentScoops + " / " + requiredScoops);
    }

    private void UpdateMortarPowderVisual()
    {
        if (powderLevels == null || powderLevels.Length == 0)
            return;

        float progress = (float)currentScoops / requiredScoops;
        int levelIndex = Mathf.Clamp(
            Mathf.FloorToInt(progress * powderLevels.Length),
            0,
            powderLevels.Length - 1
        );

        ShowPowderLevel(levelIndex);
    }

    private void ShowPowderLevel(int activeIndex)
    {
        for (int i = 0; i < powderLevels.Length; i++)
        {
            if (powderLevels[i] != null)
                powderLevels[i].SetActive(i == activeIndex);
        }
    }
}