using UnityEngine;

public class PowderDropTrigger : MonoBehaviour
{
    [Header("Visual Bubuk di Perkamen")]
    [SerializeField] private GameObject[] powderStages;

    [Header("Target Jumlah Tuang")]
    [SerializeField] private int requiredDrops = 3;

    [Header("Visual Neraca")]
    [SerializeField] private BalanceScaleVisual scaleVisual;

    private int currentDrops = 0;
    private bool isFinished = false;

    private void Start()
    {
        HideAllPowderStages();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (isFinished)
            return;

        PowderScoopController scoop = other.GetComponentInParent<PowderScoopController>();

        if (scoop == null)
            return;

        if (!scoop.HasPowder)
            return;

        currentDrops++;

        scoop.RemovePowder();
        ShowPowderStage(currentDrops);

        Debug.Log("CTM dituang ke perkamen: " + currentDrops + " / " + requiredDrops);

        if (currentDrops >= requiredDrops)
        {
            isFinished = true;

            Debug.Log("CTM sudah cukup. Neraca seimbang.");

            if (scaleVisual != null)
                scaleVisual.SetBalanced();

            PerkamenResultMover mover = GetComponentInParent<PerkamenResultMover>(true);

            if (mover != null)
            {
                mover.EnableMove();
                Debug.Log("CTM selesai. Perkamen bisa diklik.");
            }
            else
            {
                Debug.LogWarning("PerkamenResultMover tidak ditemukan di parent CTM_DropTrigger.");
            }

            gameObject.SetActive(false);
        }
    }

    private void ShowPowderStage(int dropNumber)
    {
        int index = dropNumber - 1;

        if (powderStages == null)
            return;

        if (index < 0 || index >= powderStages.Length)
            return;

        if (powderStages[index] != null)
            powderStages[index].SetActive(true);
    }

    private void HideAllPowderStages()
    {
        if (powderStages == null)
            return;

        foreach (GameObject powder in powderStages)
        {
            if (powder != null)
                powder.SetActive(false);
        }
    }
}