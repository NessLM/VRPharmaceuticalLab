using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class CTMMortarPourTrigger : MonoBehaviour
{
    [SerializeField] private Transform pourPoint;
    [SerializeField] private GameObject[] powderStages;
    [SerializeField] private ParticleSystem pourParticle;
    [SerializeField] private float moveDuration = 0.4f;
    [SerializeField] private float pourDuration = 0.9f;
    [SerializeField] private GameObject paracetamolMortarTrigger;

    private bool hasPoured = false;

    private void Start()
    {
        foreach (GameObject powder in powderStages)
        {
            if (powder != null)
                powder.SetActive(false);
        }

        if (pourParticle != null)
            pourParticle.gameObject.SetActive(false);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (hasPoured) return;

        XRGrabInteractable grab = other.GetComponentInParent<XRGrabInteractable>();
        if (grab == null) return;

        if (!grab.CompareTag("Perkamen")) return;

        hasPoured = true;
        StartCoroutine(PourRoutine(grab));
    }

    private IEnumerator PourRoutine(XRGrabInteractable grab)
    {
        Transform paper = grab.transform;

        grab.enabled = false;

        Rigidbody rb = paper.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.useGravity = false;
            rb.isKinematic = true;
        }

        Vector3 startPos = paper.position;
        Quaternion startRot = paper.rotation;

        Quaternion targetRot = pourPoint.rotation * Quaternion.Euler(65f, 0f, 0f);

        float timer = 0f;

        while (timer < moveDuration)
        {
            timer += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, timer / moveDuration);

            paper.position = Vector3.Lerp(startPos, pourPoint.position, t);
            paper.rotation = Quaternion.Slerp(startRot, targetRot, t);

            yield return null;
        }

        paper.position = pourPoint.position;
        paper.rotation = targetRot;

        if (pourParticle != null)
        {
            pourParticle.gameObject.SetActive(true);
            pourParticle.Play();
        }

        float delay = pourDuration / powderStages.Length;

        for (int i = 0; i < powderStages.Length; i++)
        {
            if (powderStages[i] != null)
                powderStages[i].SetActive(true);

            yield return new WaitForSeconds(delay);
        }

        if (pourParticle != null)
            pourParticle.Stop();

        paper.gameObject.SetActive(false);

        Debug.Log("CTM berhasil dituangkan ke mortar.");

        if (paracetamolMortarTrigger != null)
    paracetamolMortarTrigger.SetActive(true);

gameObject.SetActive(false);
    }
}