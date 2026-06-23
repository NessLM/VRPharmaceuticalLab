using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class PerkamenResultMover : MonoBehaviour
{
    [SerializeField] private Transform resultPoint;
    [SerializeField] private ReturnToStartPosition ctmWeightReturn;
    [SerializeField] private float moveDuration = 0.5f;
    [SerializeField] private Step1IngredientPhaseManager phaseManager;

    private GameObject clickArea;
    private XRGrabInteractable grab;
    private bool canMove = false;
    private bool moved = false;

    private void Awake()
    {
        grab = GetComponent<XRGrabInteractable>();

        Transform foundClickArea = transform.Find("ClickArea_Perkamen");
        if (foundClickArea != null)
        {
            clickArea = foundClickArea.gameObject;
            clickArea.SetActive(false);
        }
        else
        {
            Debug.LogWarning("ClickArea_Perkamen tidak ditemukan di child singleperkamen.");
        }
    }

    public void EnableMove()
    {
        if (moved) return;

        canMove = true;

        if (clickArea != null)
        {
            clickArea.SetActive(true);
            Debug.Log("ClickArea_Perkamen aktif.");
        }
    }

    public void MoveToResultPoint()
    {
        Debug.Log("CLICK PERKAMEN MASUK");

        if (!canMove || moved) return;

        moved = true;
        canMove = false;

        if (clickArea != null)
            clickArea.SetActive(false);

        if (grab != null)
            grab.enabled = false;

        StartCoroutine(MoveRoutine());
    }

    private IEnumerator MoveRoutine()
    {
        transform.SetParent(null, true);

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.useGravity = false;
            rb.isKinematic = true;
        }

        Vector3 startPos = transform.position;
        Quaternion startRot = transform.rotation;

        float timer = 0f;

        while (timer < moveDuration)
        {
            timer += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, timer / moveDuration);

            transform.position = Vector3.Lerp(startPos, resultPoint.position, t);
            transform.rotation = Quaternion.Slerp(startRot, resultPoint.rotation, t);

            yield return null;
        }

        transform.position = resultPoint.position;
        transform.rotation = resultPoint.rotation;
        transform.SetParent(resultPoint, true);

        if (grab != null)
    grab.enabled = true; 

        if (ctmWeightReturn != null)
        {
            ctmWeightReturn.ReturnToStart();
            Debug.Log("Anak timbangan CTM balik ke weight box.");
        }
        else
        {
            Debug.LogWarning("CTM Weight Return belum diisi di PerkamenResultMover.");
        }

       Debug.Log("Perkamen CTM pindah ke CTM_ResultPoint.");

if (phaseManager != null)
{
    phaseManager.EnableParacetamolPhase();
    Debug.Log("Fase Paracetamol aktif.");
}
else
{
    Debug.LogWarning("Phase Manager belum diisi di PerkamenResultMover.");
}
    }
}