using UnityEngine;

public class PestleCleanTrigger : MonoBehaviour
{
    [SerializeField] private PestleAutoCleanSequence cleanSequence;

    private bool triggered = false;

    private void OnTriggerEnter(Collider other)
    {
        if (triggered)
            return;

        if (!other.CompareTag("Sudip"))
            return;

        triggered = true;

        if (cleanSequence != null)
            cleanSequence.CleanWithSpatula();
        else
            Debug.LogWarning("Clean Sequence belum diisi di PestleCleanTrigger.");
    }
}