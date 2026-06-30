using System.Collections;
using UnityEngine;

public class Step5Manager : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject panelInsertCapsule;
    [SerializeField] private GameObject panelWriteLabel;
    [SerializeField] private GameObject panelFinish;

    [Header("Checklist")]
    [SerializeField] private Step5ChecklistManager checklistManager;

    [Header("Target Dalam Plastik")]
    [SerializeField] private Transform capsulesInsideTarget;

    [Header("Visual Isi Plastik")]
    [SerializeField] private GameObject filledCapsulesVisual;

    [Header("Animasi")]
    [SerializeField] private float moveDuration = 0.5f;
    [SerializeField] private float delayPerCapsule = 0.08f;

    private bool alreadyInserted = false;

    public void StartStep5()
    {
        Debug.Log("Panel Insert Capsule Muncul");

        if (panelInsertCapsule != null)
            panelInsertCapsule.SetActive(true);
    }

    public void OnInsertCapsulesButton()
    {
        if (alreadyInserted)
            return;

        alreadyInserted = true;

        StartCoroutine(InsertCapsulesRoutine());
    }

    IEnumerator InsertCapsulesRoutine()
    {
        panelInsertCapsule.SetActive(false);

        GameObject[] capsules =
            GameObject.FindGameObjectsWithTag("Capsule");

        foreach (GameObject capsule in capsules)
        {
            StartCoroutine(MoveCapsule(capsule.transform));

            yield return new WaitForSeconds(delayPerCapsule);
        }

        // Tunggu semua animasi selesai
        yield return new WaitForSeconds(1f);

        // Hide kapsul asli
        foreach (GameObject capsule in capsules)
        {
            capsule.SetActive(false);
        }

        // Tampilkan visual isi plastik
        if (filledCapsulesVisual != null)
        {
            filledCapsulesVisual.SetActive(true);
        }

        // Checklist
        if (checklistManager != null)
            checklistManager.CheckCapsulesInserted();

        // Panel berikutnya
        if (panelWriteLabel != null)
            panelWriteLabel.SetActive(true);
    }

    IEnumerator MoveCapsule(Transform capsule)
    {
        Vector3 startPos = capsule.position;
        Quaternion startRot = capsule.rotation;

        Vector3 targetPos =
            capsulesInsideTarget.position +
            Random.insideUnitSphere * 0.02f;

        Quaternion targetRot = Random.rotation;

        float t = 0f;

        while (t < moveDuration)
        {
            t += Time.deltaTime;

            float p = t / moveDuration;

            capsule.position =
                Vector3.Lerp(startPos, targetPos, p);

            capsule.rotation =
                Quaternion.Slerp(startRot, targetRot, p);

            yield return null;
        }

        capsule.SetParent(capsulesInsideTarget);
    }
}