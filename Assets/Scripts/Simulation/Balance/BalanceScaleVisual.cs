using System.Collections;
using UnityEngine;

public class BalanceScaleVisual : MonoBehaviour
{
    [Header("Bagian yang digerakkan")]
    [SerializeField] private Transform scaleBeam;
    [SerializeField] private Transform leftWeight;
    [SerializeField] private Transform rightWeight;

    [Header("Rotasi Beam")]
    [SerializeField] private Vector3 balancedBeamRotation;
    [SerializeField] private Vector3 rightDownBeamRotation = new Vector3(0f, 0f, -8f);

    [Header("Naik Turun Piring")]
    [SerializeField] private float plateMoveAmount = 0.05f;

    [Header("Durasi Animasi")]
    [SerializeField] private float duration = 0.4f;

    private Vector3 leftStartPos;
    private Vector3 rightStartPos;

    private Coroutine routine;

    private void Awake()
    {
        if (scaleBeam != null)
            balancedBeamRotation = scaleBeam.localEulerAngles;

        if (leftWeight != null)
            leftStartPos = leftWeight.localPosition;

        if (rightWeight != null)
            rightStartPos = rightWeight.localPosition;
    }

    public void SetRightDown()
    {
        Vector3 leftTarget = leftStartPos + new Vector3(0f, plateMoveAmount, 0f);
        Vector3 rightTarget = rightStartPos + new Vector3(0f, -plateMoveAmount, 0f);

        AnimateTo(rightDownBeamRotation, leftTarget, rightTarget);
    }

    public void SetBalanced()
    {
        AnimateTo(balancedBeamRotation, leftStartPos, rightStartPos);
    }

    private void AnimateTo(Vector3 beamTargetEuler, Vector3 leftTarget, Vector3 rightTarget)
    {
        if (routine != null)
            StopCoroutine(routine);

        routine = StartCoroutine(AnimateRoutine(beamTargetEuler, leftTarget, rightTarget));
    }

    private IEnumerator AnimateRoutine(Vector3 beamTargetEuler, Vector3 leftTarget, Vector3 rightTarget)
    {
        Quaternion beamStartRot = scaleBeam.localRotation;
        Quaternion beamTargetRot = Quaternion.Euler(beamTargetEuler);

        Vector3 leftStart = leftWeight.localPosition;
        Vector3 rightStart = rightWeight.localPosition;

        float timer = 0f;

        while (timer < duration)
        {
            timer += Time.deltaTime;

            float t = timer / duration;
            t = Mathf.SmoothStep(0f, 1f, t);

            if (scaleBeam != null)
                scaleBeam.localRotation = Quaternion.Slerp(beamStartRot, beamTargetRot, t);

            if (leftWeight != null)
                leftWeight.localPosition = Vector3.Lerp(leftStart, leftTarget, t);

            if (rightWeight != null)
                rightWeight.localPosition = Vector3.Lerp(rightStart, rightTarget, t);

            yield return null;
        }

        if (scaleBeam != null)
            scaleBeam.localRotation = beamTargetRot;

        if (leftWeight != null)
            leftWeight.localPosition = leftTarget;

        if (rightWeight != null)
            rightWeight.localPosition = rightTarget;
    }
}