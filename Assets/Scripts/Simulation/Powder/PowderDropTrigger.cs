using System.Collections;
using UnityEngine;

public class PowderDropTrigger : MonoBehaviour
{
    [Header("Visual Bubuk di Perkamen")]
    [SerializeField] private GameObject[] powderStages;

    [Header("Target Jumlah Tuang")]
    [SerializeField] private int requiredDrops = 3;

    [Header("Visual Neraca")]
    [SerializeField] private BalanceScaleVisual scaleVisual;

    [Header("Object yang hilang setelah CTM selesai")]
    [SerializeField] private GameObject objectToHideAfterFinish;
    [SerializeField] private float hideDuration = 0.5f;

    [SerializeField] private Step1ChecklistManager checklistManager;

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

            if (checklistManager != null)
                checklistManager.CheckCTMDone();

            StartCoroutine(FinishRoutine());
        }
    }

    private IEnumerator FinishRoutine()
    {
        if (objectToHideAfterFinish != null)
            yield return StartCoroutine(ShrinkAndHide(objectToHideAfterFinish));

        gameObject.SetActive(false);
    }

    private IEnumerator ShrinkAndHide(GameObject obj)
    {
        Vector3 startScale = obj.transform.localScale;
        float timer = 0f;

        while (timer < hideDuration)
        {
            timer += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, timer / hideDuration);
            obj.transform.localScale = Vector3.Lerp(startScale, Vector3.zero, t);
            yield return null;
        }

        obj.transform.localScale = Vector3.zero;
        obj.SetActive(false);
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