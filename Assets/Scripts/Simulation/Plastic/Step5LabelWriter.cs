using System.Collections;
using UnityEngine;

public class Step5LabelWriter : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject writePanel;
    [SerializeField] private GameObject closePlasticPanel;

    [Header("Label")]
    [SerializeField] private GameObject blankLabel;
    [SerializeField] private GameObject writtenLabel;

    [Header("Checklist")]
    [SerializeField] private Step5ChecklistManager checklist;

    [Header("Finish")]
[SerializeField] private GameObject finishPanel;

private bool plasticClosed = false;

    [SerializeField] private float writingDuration = 2f;

    private bool finished = false;

    public void OnClickWrite()
    {
        if (finished)
            return;

        finished = true;

        StartCoroutine(WriteRoutine());
    }

    IEnumerator WriteRoutine()
    {
        writePanel.SetActive(false);

        Debug.Log("Sedang menulis etiket...");

        yield return new WaitForSeconds(writingDuration);

        if (blankLabel != null)
            blankLabel.SetActive(false);

        if (writtenLabel != null)
            writtenLabel.SetActive(true);

        if (checklist != null)
            checklist.CheckLabelWritten();

        if (closePlasticPanel != null)
            closePlasticPanel.SetActive(true);
    }

   public void OnClickClosePlastic()
{
    if (plasticClosed)
        return;

    plasticClosed = true;

    if (closePlasticPanel != null)
        closePlasticPanel.SetActive(false);

    if (checklist != null)
    {
        checklist.CheckBagClosed();
        checklist.CheckFinished();
    }

    if (finishPanel != null)
        finishPanel.SetActive(true);

    Debug.Log("Step 5 selesai.");
}
}