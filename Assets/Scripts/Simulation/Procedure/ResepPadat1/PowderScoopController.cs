using UnityEngine;

public class PowderScoopController : MonoBehaviour
{
    [SerializeField] private GameObject powderOnSpoon;

    public bool HasPowder { get; private set; }

    public void TakePowder()
    {
        if (HasPowder)
            return;

        HasPowder = true;

        if (powderOnSpoon != null)
            powderOnSpoon.SetActive(true);

        Debug.Log("Bubuk CTM muncul di sendok.");
    }

    public void RemovePowder()
    {
        HasPowder = false;

        if (powderOnSpoon != null)
            powderOnSpoon.SetActive(false);
    }
}