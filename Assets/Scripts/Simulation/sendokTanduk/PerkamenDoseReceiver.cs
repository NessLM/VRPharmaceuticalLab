using UnityEngine;

public class PerkamenDoseReceiver : MonoBehaviour
{
    [SerializeField] private GameObject dosePowderVisual;
    [SerializeField] private DoseDistributionManager doseManager;

    private bool hasDose = false;

    private void Start()
    {
        if (dosePowderVisual != null)
            dosePowderVisual.SetActive(false);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (hasDose)
            return;

        PowderScoopController scoop = other.GetComponentInParent<PowderScoopController>();

        if (scoop == null)
            return;

        if (!scoop.HasPowder)
            return;

        hasDose = true;

        scoop.RemovePowder();

        if (dosePowderVisual != null)
            dosePowderVisual.SetActive(true);

        if (doseManager != null)
            doseManager.AddDose();

        Debug.Log("Bubuk berhasil ditaruh ke perkamen dosis.");
    }
}