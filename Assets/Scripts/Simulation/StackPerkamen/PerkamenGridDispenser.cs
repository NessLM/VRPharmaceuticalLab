using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

[RequireComponent(typeof(XRSimpleInteractable))]
public class PerkamenGridDispenser : MonoBehaviour
{
    [SerializeField] private GameObject perkamenPrefab;
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private Transform[] gridPoints;
    [SerializeField] private float spreadDuration = 0.6f;
    [SerializeField] private float delayBetweenPapers = 0.05f;

    private XRSimpleInteractable interactable;
    private bool hasSpawned = false;

    private void Awake()
    {
        interactable = GetComponent<XRSimpleInteractable>();
    }

    private void OnEnable()
    {
        if (interactable == null)
            interactable = GetComponent<XRSimpleInteractable>();

        interactable.selectEntered.AddListener(OnSelected);
    }

    private void OnDisable()
    {
        if (interactable != null)
            interactable.selectEntered.RemoveListener(OnSelected);
    }

    private void OnSelected(SelectEnterEventArgs args)
    {
        SpawnGrid();
    }

    public void SpawnGrid()
    {
        if (hasSpawned)
            return;

        hasSpawned = true;
        StartCoroutine(SpawnRoutine());
    }

    private IEnumerator SpawnRoutine()
    {
        for (int i = 0; i < gridPoints.Length; i++)
        {
            if (gridPoints[i] == null || perkamenPrefab == null)
                continue;

            Transform startPoint = spawnPoint != null ? spawnPoint : transform;

            GameObject paper = Instantiate(
                perkamenPrefab,
                startPoint.position,
                startPoint.rotation
            );

            paper.SetActive(true);

            StartCoroutine(MovePaper(paper.transform, gridPoints[i]));

            yield return new WaitForSeconds(delayBetweenPapers);
        }
    }

    private IEnumerator MovePaper(Transform paper, Transform target)
    {
        Vector3 startPos = paper.position;
        Quaternion startRot = paper.rotation;

        float timer = 0f;

        while (timer < spreadDuration)
        {
            timer += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, timer / spreadDuration);

            paper.position = Vector3.Lerp(startPos, target.position, t);
            paper.rotation = Quaternion.Slerp(startRot, target.rotation, t);

            yield return null;
        }

        paper.position = target.position;
        paper.rotation = target.rotation;
        paper.SetParent(target, true);
    }
}