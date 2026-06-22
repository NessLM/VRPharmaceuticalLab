using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class ParacetamolResultMover : MonoBehaviour
{
    [SerializeField] private Transform resultPoint;
    [SerializeField] private ReturnToStartPosition weight3gReturn;
    [SerializeField] private ReturnToStartPosition weight500mgReturn;
    [SerializeField] private float moveDuration = 0.5f;

    [Header("Object yang hilang setelah Paracetamol selesai")]
    [SerializeField] private GameObject[] objectsToHideAfterMove;
    [SerializeField] private float hideDuration = 0.6f;

    private GameObject clickArea;
    private XRGrabInteractable grab;
    private bool canMove = false;
    private bool moved = false;

    private void Awake()
    {
        grab = GetComponent<XRGrabInteractable>();

        Transform foundClickArea = transform.Find("ClickArea_Paracetamol");
        if (foundClickArea != null)
        {
            clickArea = foundClickArea.gameObject;
            clickArea.SetActive(false);
        }
        else
        {
            Debug.LogWarning("ClickArea_Paracetamol tidak ditemukan.");
        }
    }

    public void EnableMove()
    {
        if (moved) return;

        canMove = true;

        if (clickArea != null)
        {
            clickArea.SetActive(true);
            Debug.Log("ClickArea_Paracetamol aktif.");
        }
    }

    public void MoveToResultPoint()
    {
        Debug.Log("CLICK PARACETAMOL MASUK");

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

        if (weight3gReturn != null)
            weight3gReturn.ReturnToStart();

        if (weight500mgReturn != null)
            weight500mgReturn.ReturnToStart();

        foreach (GameObject obj in objectsToHideAfterMove)
        {
            if (obj != null)
                StartCoroutine(ShrinkAndHide(obj));
        }

        Debug.Log("Perkamen Paracetamol pindah dan anak timbangan kembali.");
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