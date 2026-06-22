using UnityEngine;

public class ParacetamolScoopTrigger : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        PowderScoopController scoop = other.GetComponentInParent<PowderScoopController>();

        if (scoop != null)
        {
            scoop.TakePowder();
            Debug.Log("Bubuk Paracetamol muncul di sendok.");
        }
    }
}