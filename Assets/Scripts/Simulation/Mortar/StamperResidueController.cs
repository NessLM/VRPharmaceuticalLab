using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

[DisallowMultipleComponent]
public class StamperResidueController : MonoBehaviour
{
    [Header("Residue Visual")]
    [SerializeField] private GameObject residueVisual;

    [Header("Scrape Detection")]
    [SerializeField] private Transform residuePoint;
    [SerializeField] private Transform sudipTip;
    [SerializeField] private XRGrabInteractable sudipGrab;
    [SerializeField] private bool requireSudipHeld = true;
    [SerializeField] private float scrapeRadius = 0.08f;
    [SerializeField] private float requiredScrapeTime = 0.45f;

    [Header("Debug")]
    [SerializeField] private bool hasResidue;
    [SerializeField] private float scrapeTimer;

    public bool HasResidue => hasResidue;
    public bool IsCleaned => !hasResidue;

    private void Awake()
    {
        SetResidueVisible(false);
    }

    private void Update()
    {
        if (!hasResidue)
        {
            scrapeTimer = 0f;
            return;
        }

        if (residuePoint == null || sudipTip == null)
            return;

        if (requireSudipHeld && (sudipGrab == null || !sudipGrab.isSelected))
        {
            scrapeTimer = 0f;
            return;
        }

        float distance = Vector3.Distance(residuePoint.position, sudipTip.position);

        if (distance > scrapeRadius)
        {
            scrapeTimer = 0f;
            return;
        }

        scrapeTimer += Time.deltaTime;

        if (scrapeTimer >= requiredScrapeTime)
            ClearResidue();
    }

    public void ShowResidue()
    {
        hasResidue = true;
        scrapeTimer = 0f;
        SetResidueVisible(true);
    }

    public void ClearResidue()
    {
        hasResidue = false;
        scrapeTimer = 0f;
        SetResidueVisible(false);
    }

    private void SetResidueVisible(bool value)
    {
        if (residueVisual != null)
            residueVisual.SetActive(value);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        scrapeRadius = Mathf.Max(0.01f, scrapeRadius);
        requiredScrapeTime = Mathf.Max(0.05f, requiredScrapeTime);
    }
#endif
}