using UnityEngine;

public class HomogenPowderScoopTrigger : MonoBehaviour
{

    [SerializeField] private Step3ChecklistManager checklistManager;
private bool alreadyChecked = false;
    private void OnTriggerEnter(Collider other)
    {
        PowderScoopController scoop = other.GetComponentInParent<PowderScoopController>();

        if (scoop == null)
            return;

        scoop.TakePowder();

        if (!alreadyChecked && checklistManager != null)
{
    checklistManager.CheckTakePowder();
    alreadyChecked = true;
}

        Debug.Log("Sendok mengambil bubuk homogen dari mortar.");
    }
}