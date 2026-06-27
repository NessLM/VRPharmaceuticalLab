using UnityEngine;

public class HomogenPowderScoopTrigger : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        PowderScoopController scoop = other.GetComponentInParent<PowderScoopController>();

        if (scoop == null)
            return;

        scoop.TakePowder();

        Debug.Log("Sendok mengambil bubuk homogen dari mortar.");
    }
}