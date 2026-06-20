using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

[DisallowMultipleComponent]
public class StamperResidueController : MonoBehaviour
{
    [Header("Mixing State")]
    [SerializeField] private MortarController mortar;

    [Header("Residue Visual")]
    [SerializeField] private GameObject residueVisual;

    [Header("Scrape Detection")]
    [SerializeField] private Transform residuePoint;
    [SerializeField] private Transform sudipTip;
    [SerializeField] private XRGrabInteractable sudipGrab;
    [SerializeField] private XRGrabInteractable stamperGrab;
    [SerializeField] private bool requireSudipHeld = true;
    [SerializeField] private bool requireStamperReleased = true;
    [SerializeField] private float scrapeRadius = 0.08f;
    [SerializeField] private float requiredScrapeTime = 0.45f;

    [Header("Events")]
    [SerializeField] private UnityEvent onResidueCleared = new UnityEvent();

    [Header("Debug")]
    [SerializeField] private bool hasResidue;
    [SerializeField] private float scrapeTimer;

    private Vector3 residueOriginalScale = Vector3.one;

    public bool HasResidue => hasResidue;
    public bool IsCleaned => !hasResidue;

    private void Awake()
    {
        if (stamperGrab == null)
            stamperGrab = GetComponent<XRGrabInteractable>();

        if (residueVisual != null)
            residueOriginalScale = residueVisual.transform.localScale;

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
            ResetResiduePose();
            return;
        }

        if (requireStamperReleased && stamperGrab != null && stamperGrab.isSelected)
        {
            scrapeTimer = 0f;
            ResetResiduePose();
            return;
        }

        float distance = Vector3.Distance(residuePoint.position, sudipTip.position);

        if (distance > scrapeRadius)
        {
            scrapeTimer = 0f;
            ResetResiduePose();
            return;
        }

        scrapeTimer += Time.deltaTime;
        AnimateScrape();

        if (scrapeTimer >= requiredScrapeTime)
            ClearResidue();
    }

    public void ShowResidue()
    {
        hasResidue = true;
        scrapeTimer = 0f;
        ResetResiduePose();
        SetResidueVisible(true);
    }

    public void BindMortar(MortarController targetMortar)
    {
        mortar = targetMortar;
    }

    public void ClearResidue()
    {
        hasResidue = false;
        scrapeTimer = 0f;
        SetResidueVisible(false);

        mortar?.CompleteScrape();
        onResidueCleared?.Invoke();
    }

    private void AnimateScrape()
    {
        if (residueVisual == null)
            return;

        float t = requiredScrapeTime > 0f ? Mathf.Clamp01(scrapeTimer / requiredScrapeTime) : 1f;
        float shrink = Mathf.Lerp(1f, 0.2f, t);
        residueVisual.transform.localScale = residueOriginalScale * shrink;
        residueVisual.transform.localRotation *= Quaternion.Euler(0f, 0f, 220f * Time.deltaTime);
    }

    private void ResetResiduePose()
    {
        if (residueVisual != null)
            residueVisual.transform.localScale = residueOriginalScale;
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
