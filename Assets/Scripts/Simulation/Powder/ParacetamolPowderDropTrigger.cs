using System.Collections;
using UnityEngine;

public class ParacetamolPowderDropTrigger : MonoBehaviour
{
    [SerializeField] private GameObject[] powderStages;
    [SerializeField] private int requiredDrops = 5;
    [SerializeField] private BalanceScaleVisual scaleVisual;

    [Header("Object yang hilang setelah Paracetamol selesai")]
    [SerializeField] private GameObject objectToHideAfterFinish;
    [SerializeField] private float hideDuration = 0.5f;

[Header("Checklist")]
[SerializeField] private Step1ChecklistManager checklistManager;
    private int currentDrops = 0;
    private bool isFinished = false;

    private void Start()
    {
        foreach (GameObject powder in powderStages)
        {
            if (powder != null)
                powder.SetActive(false);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (isFinished) return;

        PowderScoopController scoop = other.GetComponentInParent<PowderScoopController>();
        if (scoop == null) return;
        if (!scoop.HasPowder) return;

        currentDrops++;
        scoop.RemovePowder();

        int index = currentDrops - 1;
        if (index >= 0 && index < powderStages.Length && powderStages[index] != null)
            powderStages[index].SetActive(true);

        Debug.Log("Paracetamol dituang: " + currentDrops + " / " + requiredDrops);

        if (currentDrops >= requiredDrops)
        {
            isFinished = true;

            if (scaleVisual != null)
                scaleVisual.SetBalanced();

            ParacetamolResultMover mover = GetComponentInParent<ParacetamolResultMover>(true);
            if (mover != null)
            {
                mover.EnableMove();
                Debug.Log("Paracetamol selesai. Perkamen bisa diklik.");
            }
            else
            {
                Debug.LogWarning("ParacetamolResultMover tidak ditemukan di parent singleperkamen.");
            }

            if (checklistManager != null)
    checklistManager.CheckParaDone();


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
}