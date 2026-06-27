using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class PestleAutoCleanSequence : MonoBehaviour
{

    [SerializeField] private ResepPadat1StepManager stepManager;
    [Header("Penumbuk")]
    [SerializeField] private XRGrabInteractable pestleGrab;
    [SerializeField] private Transform pestleObject;

    [Header("Sudip")]
    [SerializeField] private XRGrabInteractable spatulaGrab;
    [SerializeField] private Transform spatulaObject;
    [SerializeField] private GameObject spatulaToHideAfterClean;

    [Header("Titik Animasi Penumbuk")]
    [SerializeField] private Transform restPoint;
    [SerializeField] private Transform cleanPoint;
    [SerializeField] private Transform pestleSidePoint;

    [Header("Titik Animasi Sudip")]
    [SerializeField] private Transform spatulaCleanStartPoint;
    [SerializeField] private Transform spatulaCleanEndPoint;

    [Header("Bubuk di ujung penumbuk")]
    [SerializeField] private GameObject powderOnPestle;

    [Header("Trigger Sudip")]
    [SerializeField] private GameObject cleanTrigger;

    [Header("Checklist")]
    [SerializeField] private Step2ChecklistManager checklistManager;

    [Header("Durasi Animasi")]
    [SerializeField] private float moveToRestDuration = 0.6f;
    [SerializeField] private float moveToCleanDuration = 0.6f;
    [SerializeField] private float spatulaMoveDuration = 0.35f;
    [SerializeField] private float pestleReturnDuration = 0.6f;
    [SerializeField] private int cleanSwipeCount = 3;

    private bool readyToClean = false;
    private bool cleaned = false;

    private void Start()
    {
        if (powderOnPestle != null)
            powderOnPestle.SetActive(false);

        if (cleanTrigger != null)
            cleanTrigger.SetActive(false);
    }

    public void StartRestMode()
    {
        if (pestleObject == null)
            return;

        StartCoroutine(RestRoutine());
    }

    private IEnumerator RestRoutine()
    {
        if (pestleGrab != null)
            pestleGrab.enabled = false;

        SetRigidBodyState(pestleObject, true);

        yield return MoveObjectToPoint(pestleObject, restPoint, moveToRestDuration);

        if (powderOnPestle != null)
            powderOnPestle.SetActive(true);

        if (cleanTrigger != null)
            cleanTrigger.SetActive(true);

        readyToClean = true;

        Debug.Log("Penumbuk senderan dan siap dibersihkan sudip.");
    }

    public void CleanWithSpatula()
    {
        if (!readyToClean || cleaned)
            return;

        cleaned = true;
        StartCoroutine(CleanRoutine());
    }

    private IEnumerator CleanRoutine()
    {
        if (cleanTrigger != null)
            cleanTrigger.SetActive(false);

        if (spatulaGrab != null)
            spatulaGrab.enabled = false;

        SetRigidBodyState(spatulaObject, true);

        yield return MoveObjectToPoint(pestleObject, cleanPoint, moveToCleanDuration);

        yield return MoveObjectToPoint(spatulaObject, spatulaCleanStartPoint, spatulaMoveDuration);

        for (int i = 0; i < cleanSwipeCount; i++)
        {
            yield return MoveObjectToPoint(spatulaObject, spatulaCleanEndPoint, spatulaMoveDuration);
            yield return MoveObjectToPoint(spatulaObject, spatulaCleanStartPoint, spatulaMoveDuration);
        }

        if (powderOnPestle != null)
            powderOnPestle.SetActive(false);

        if (spatulaToHideAfterClean != null)
            spatulaToHideAfterClean.SetActive(false);
        else if (spatulaObject != null)
            spatulaObject.gameObject.SetActive(false);

        yield return MoveObjectToPoint(pestleObject, pestleSidePoint, pestleReturnDuration);

        if (checklistManager != null)
    checklistManager.CheckSpatula();

if (stepManager != null)
{
    stepManager.SetStep(3);
    Debug.Log("Step 3 aktif setelah penumbuk dibersihkan.");
}
else
{
    Debug.LogWarning("Step Manager belum diisi di PestleAutoCleanSequence.");
}
        Debug.Log("Sudip membersihkan ujung penumbuk, lalu penumbuk kembali ke samping mortar.");
    }

    private void SetRigidBodyState(Transform target, bool locked)
    {
        if (target == null) return;

        Rigidbody rb = target.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.useGravity = !locked;
            rb.isKinematic = locked;
        }
    }

    private IEnumerator MoveObjectToPoint(Transform targetObject, Transform targetPoint, float duration)
    {
        if (targetObject == null || targetPoint == null)
            yield break;

        Vector3 startPos = targetObject.position;
        Quaternion startRot = targetObject.rotation;

        float timer = 0f;

        while (timer < duration)
        {
            timer += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, timer / duration);

            targetObject.position = Vector3.Lerp(startPos, targetPoint.position, t);
            targetObject.rotation = Quaternion.Slerp(startRot, targetPoint.rotation, t);

            yield return null;
        }

        targetObject.position = targetPoint.position;
        targetObject.rotation = targetPoint.rotation;
    }
}