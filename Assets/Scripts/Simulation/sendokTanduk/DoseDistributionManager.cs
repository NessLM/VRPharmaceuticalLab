using System.Collections;
using UnityEngine;

public class DoseDistributionManager : MonoBehaviour
{
    [SerializeField] private int requiredDoseCount = 10;
    [SerializeField] private Step3ChecklistManager checklistManager;

    [Header("Step Manager")]
    [SerializeField] private ResepPadat1StepManager stepManager;

    [Header("Object hilang setelah Step 3 selesai")]
    [SerializeField] private GameObject[] objectsToHideAfterDone;
    [SerializeField] private float hideDuration = 0.6f;

    private int currentDoseCount = 0;
    private bool finished = false;

    private void Start()
    {
        if (checklistManager != null)
            checklistManager.UpdateDoseProgress(0, requiredDoseCount);
    }

    public void AddDose()
    {
        if (finished)
            return;

        currentDoseCount++;

        Debug.Log("Perkamen terisi: " + currentDoseCount + " / " + requiredDoseCount);

        if (checklistManager != null)
            checklistManager.UpdateDoseProgress(currentDoseCount, requiredDoseCount);

        if (currentDoseCount >= requiredDoseCount)
        {
            finished = true;

            if (checklistManager != null)
                checklistManager.CheckFinished();

            StartCoroutine(HideObjectsRoutine());

            Debug.Log("Semua 10 perkamen sudah terisi bubuk. Alat Step 3 disembunyikan.");
        }
    }

    private IEnumerator HideObjectsRoutine()
    {
        foreach (GameObject obj in objectsToHideAfterDone)
        {
            if (obj != null)
                StartCoroutine(ShrinkAndHide(obj));
        }

        yield return new WaitForSeconds(hideDuration + 0.1f);

        if (stepManager != null)
        {
            stepManager.SetStep(4);
            Debug.Log("Masuk ke Step 4.");
        }
        else
        {
            Debug.LogWarning("Step Manager belum diisi di DoseDistributionManager.");
        }
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