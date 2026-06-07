using UnityEngine;

public class CTMScoopTrigger : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        PowderScoopController scoop = other.GetComponentInParent<PowderScoopController>();

        if (scoop != null)
        {
            scoop.TakePowder();
        }
    }
}