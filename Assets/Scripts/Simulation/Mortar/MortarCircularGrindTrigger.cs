using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MortarCircularGrindTrigger : MonoBehaviour
{
    [Header("Gerakan Gerus")]
    [SerializeField] private Transform grindCenter;
    [SerializeField] private int requiredRotations = 8;

    [Header("Visual")]
    [SerializeField] private GameObject homogenPowder;

    [Header("UI Progress")]
    [SerializeField] private GameObject progressPanel;
    [SerializeField] private TMP_Text progressText;
    [SerializeField] private Image progressFill;

[SerializeField] private GameObject[] powdersToHideWhenFinished;
    private Transform pestle;
    private bool isGrinding = false;
    private bool isFinished = false;

    private float lastAngle;
    private float accumulatedAngle = 0f;

    private void Start()
    {
        if (homogenPowder != null)
            homogenPowder.SetActive(false);

        if (progressPanel != null)
            progressPanel.SetActive(false);

        UpdateProgressUI(0f);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (isFinished)
            return;

        if (!other.CompareTag("Penumbuk"))
            return;

        pestle = other.transform;
        isGrinding = true;

        Vector3 dir = pestle.position - grindCenter.position;
        lastAngle = Mathf.Atan2(dir.z, dir.x) * Mathf.Rad2Deg;

        if (progressPanel != null)
            progressPanel.SetActive(true);

        Debug.Log("Mode gerus dimulai.");
    }

    private void OnTriggerExit(Collider other)
    {
        if (pestle == null)
            return;

        if (other.transform == pestle)
        {
            isGrinding = false;
            pestle = null;
            Debug.Log("Penumbuk keluar dari area gerus.");
        }
    }

    private void Update()
    {
        if (!isGrinding || pestle == null || isFinished)
            return;

        Vector3 dir = pestle.position - grindCenter.position;
        float currentAngle = Mathf.Atan2(dir.z, dir.x) * Mathf.Rad2Deg;

        float delta = Mathf.DeltaAngle(lastAngle, currentAngle);

        // Kalau arah kebalik, nanti ganti < 0 jadi > 0
        if (delta < 0f)
            accumulatedAngle += Mathf.Abs(delta);

        lastAngle = currentAngle;

        float targetAngle = requiredRotations * 360f;
        float progress = Mathf.Clamp01(accumulatedAngle / targetAngle);

        UpdateProgressUI(progress);

        if (progress >= 1f)
            FinishGrinding();
    }

    private void UpdateProgressUI(float progress)
    {
        if (progressText != null)
            progressText.text = "Menggerus: " + Mathf.RoundToInt(progress * 100f) + "%";

        if (progressFill != null)
            progressFill.fillAmount = progress;
    }

    private void FinishGrinding()
    {
        isFinished = true;
        isGrinding = false;

foreach (GameObject powder in powdersToHideWhenFinished)
{
    if (powder != null)
        powder.SetActive(false);
}
        if (homogenPowder != null)
            homogenPowder.SetActive(true);

        if (progressPanel != null)
            progressPanel.SetActive(false);

        Debug.Log("Penggerusan selesai. Bubuk homogen muncul.");
    }
}