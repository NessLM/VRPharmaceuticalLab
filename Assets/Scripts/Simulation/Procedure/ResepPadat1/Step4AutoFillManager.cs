using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Step4AutoFillManager : MonoBehaviour
{
    [Header("Panel")]
    [SerializeField] private GameObject autoFillPanel;
    [SerializeField] private bool hidePanelOnStart = true;

    [Header("Start Button")]
    [SerializeField] private Button startButton;

    [Header("Animation")]
    [SerializeField] private float delayBetweenCapsules = 0.15f;

    [Header("Sequential Pour")]
    [SerializeField] private CapsuleFillingSequenceManager sequenceManager;

    [Header("Checklist")]
    [SerializeField] private Step4ChecklistManager checklistManager;

    [Header("Step Manager")]
    [SerializeField] private ResepPadat1StepManager stepManager;

    private readonly List<CapsuleAutoFill> _capsules = new List<CapsuleAutoFill>();

    private bool _sequenceStarted;

    private void Awake()
    {
        if (autoFillPanel != null)
            autoFillPanel.SetActive(false);
    }

    private void OnEnable()
    {
        if (startButton != null)
            startButton.onClick.AddListener(OnStartClicked);
    }

    private void OnDisable()
    {
        if (startButton != null)
            startButton.onClick.RemoveListener(OnStartClicked);
    }

    public void OnPillsPoured(IEnumerable<GameObject> pouredPills)
    {
        _capsules.Clear();

        if (pouredPills != null)
        {
            var snapshot = new List<GameObject>(pouredPills);

            foreach (var pill in snapshot)
            {
                if (pill == null)
                    continue;

                CapsuleAutoFill capsule = pill.GetComponent<CapsuleAutoFill>();

                if (capsule != null)
                    _capsules.Add(capsule);
            }
        }

        if (checklistManager != null)
            checklistManager.CheckCapsulesReady();

        if (autoFillPanel != null)
            autoFillPanel.SetActive(true);
    }

    public void OnStartClicked()
    {
        if (_sequenceStarted)
            return;

        _sequenceStarted = true;

        if (hidePanelOnStart && autoFillPanel != null)
            autoFillPanel.SetActive(false);

        if (checklistManager != null)
            checklistManager.CheckAutoFillStarted();

        if (sequenceManager != null)
        {
            sequenceManager.BeginSequence(_capsules);
            return;
        }

        StartCoroutine(PlayAllCapsules());
    }

    private IEnumerator PlayAllCapsules()
    {
        if (_capsules.Count == 0)
        {
            CapsuleAutoFill[] found =
                Object.FindObjectsByType<CapsuleAutoFill>(FindObjectsSortMode.None);

            _capsules.AddRange(found);
        }

        List<CapsuleAutoFill> capsules = new List<CapsuleAutoFill>(_capsules);

        foreach (CapsuleAutoFill capsule in capsules)
        {
            if (capsule == null)
                continue;

            capsule.Play();

            if (delayBetweenCapsules > 0)
                yield return new WaitForSeconds(delayBetweenCapsules);
        }

        bool anyPlaying = true;

        while (anyPlaying)
        {
            anyPlaying = false;

            foreach (CapsuleAutoFill capsule in capsules)
            {
                if (capsule != null && capsule.IsPlaying)
                {
                    anyPlaying = true;
                    break;
                }
            }

            yield return null;
        }

        if (checklistManager != null)
            checklistManager.CheckAllCapsulesFilled();

        yield return new WaitForSeconds(1f);

        if (stepManager != null)
        {
            Debug.Log("Masuk Step 5");
            stepManager.SetStep(5);
        }
    }
}