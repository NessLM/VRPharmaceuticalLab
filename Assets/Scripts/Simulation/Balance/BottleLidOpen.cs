using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class BottleLidOpen : MonoBehaviour
{
    public enum BottleType
    {
        CTM,
        Paracetamol
    }

    [Header("Jenis Botol")]
    [SerializeField] private BottleType bottleType;

    [Header("Tutup Botol")]
    [SerializeField] private Transform lidObject;

    [Header("Badan Botol")]
    [SerializeField] private XRGrabInteractable bottleGrab;

    [Header("Trigger Ambil Bubuk")]
    [SerializeField] private GameObject scoopTrigger;

    [Header("Gerakan Tutup")]
    [SerializeField] private Vector3 openLocalOffset = new Vector3(0f, 0.08f, 0f);
    [SerializeField] private float openDuration = 0.35f;

    [Header("Checklist")]
    [SerializeField] private Step1ChecklistManager checklistManager;

    private Vector3 closedLocalPosition;
    private bool isOpen = false;
    private bool isMoving = false;

    private XRSimpleInteractable lidInteractable;

    private void Awake()
    {
        if (lidObject == null)
            lidObject = transform;

        closedLocalPosition = lidObject.localPosition;
        lidInteractable = GetComponent<XRSimpleInteractable>();

        if (scoopTrigger != null)
            scoopTrigger.SetActive(false);

        if (bottleGrab != null)
            bottleGrab.enabled = false;
    }

    public void OpenLid()
    {
        if (isOpen || isMoving)
            return;

        StartCoroutine(OpenRoutine());
    }

    private IEnumerator OpenRoutine()
    {
        isMoving = true;

        Vector3 startPos = lidObject.localPosition;
        Vector3 targetPos = closedLocalPosition + openLocalOffset;

        float timer = 0f;

        while (timer < openDuration)
        {
            timer += Time.deltaTime;
            float t = timer / openDuration;
            t = Mathf.SmoothStep(0f, 1f, t);

            lidObject.localPosition = Vector3.Lerp(startPos, targetPos, t);

            yield return null;
        }

        lidObject.localPosition = targetPos;

        if (scoopTrigger != null)
            scoopTrigger.SetActive(true);

        if (bottleGrab != null)
            bottleGrab.enabled = true;

        if (lidInteractable != null)
            lidInteractable.enabled = false;

        lidObject.gameObject.SetActive(false);

        isOpen = true;
        isMoving = false;

        if (checklistManager != null)
        {
            if (bottleType == BottleType.CTM)
                checklistManager.CheckCTMBottle();
            else if (bottleType == BottleType.Paracetamol)
                checklistManager.CheckParaBottle();
        }

        Debug.Log("Botol terbuka: tutup hilang, botol bisa digrab, trigger bubuk aktif.");
    }
}