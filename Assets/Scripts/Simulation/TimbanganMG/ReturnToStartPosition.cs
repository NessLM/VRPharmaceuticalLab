using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class ReturnToStartPosition : MonoBehaviour
{
    [SerializeField] private float returnDuration = 0.4f;

    private Vector3 startPosition;
    private Quaternion startRotation;
    private Rigidbody rb;
    private XRGrabInteractable grab;
    private Coroutine returnRoutine;

    private void Awake()
    {
        startPosition = transform.position;
        startRotation = transform.rotation;

        rb = GetComponent<Rigidbody>();
        grab = GetComponent<XRGrabInteractable>();
    }

    public void ReturnToStart()
    {
        if (returnRoutine != null)
            StopCoroutine(returnRoutine);

        returnRoutine = StartCoroutine(ReturnRoutine());
    }

    private IEnumerator ReturnRoutine()
    {
        if (grab != null)
            grab.enabled = false;

        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.useGravity = false;
            rb.isKinematic = true;
        }

        Vector3 fromPos = transform.position;
        Quaternion fromRot = transform.rotation;

        float timer = 0f;

        while (timer < returnDuration)
        {
            timer += Time.deltaTime;

            float t = timer / returnDuration;
            t = Mathf.SmoothStep(0f, 1f, t);

            transform.position = Vector3.Lerp(fromPos, startPosition, t);
            transform.rotation = Quaternion.Slerp(fromRot, startRotation, t);

            yield return null;
        }

        transform.position = startPosition;
        transform.rotation = startRotation;

        if (grab != null)
            grab.enabled = true;

        Debug.Log(gameObject.name + " kembali ke weight box.");
    }
}