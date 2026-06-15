using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public class BalanceWeightResetter : MonoBehaviour
{
    [Header("Auto Ambil Dari Root Ini")]
    [Tooltip("Isi dengan object anakTimbangan kalau sebagian besar anak timbangan ada di dalam root ini.")]
    [SerializeField] private Transform weightsRoot;

    [Header("Tambahan Manual")]
    [Tooltip("Isi object anak timbangan yang berada di luar root anakTimbangan, misalnya anaktimbangansusulan2g dan anaktimbangansusulan3g.")]
    [SerializeField] private XRGrabInteractable[] extraWeights;

    private readonly List<WeightData> savedWeights = new();

    private class WeightData
    {
        public XRGrabInteractable grab;
        public Transform transform;
        public Transform originalParent;

        public Vector3 worldPosition;
        public Quaternion worldRotation;
        public Vector3 localScale;

        public Rigidbody rb;
        public bool originalKinematic;
        public bool originalUseGravity;
        public bool originalDetectCollisions;
    }

    private void Awake()
    {
        SaveInitialPositions();
    }

    private void SaveInitialPositions()
    {
        savedWeights.Clear();

        HashSet<XRGrabInteractable> uniqueWeights = new HashSet<XRGrabInteractable>();

        if (weightsRoot != null)
        {
            XRGrabInteractable[] rootWeights = weightsRoot.GetComponentsInChildren<XRGrabInteractable>(true);

            foreach (XRGrabInteractable weight in rootWeights)
            {
                if (weight != null)
                    uniqueWeights.Add(weight);
            }
        }

        if (extraWeights != null)
        {
            foreach (XRGrabInteractable weight in extraWeights)
            {
                if (weight != null)
                    uniqueWeights.Add(weight);
            }
        }

        foreach (XRGrabInteractable grab in uniqueWeights)
        {
            Transform t = grab.transform;
            Rigidbody rb = grab.GetComponent<Rigidbody>();

            WeightData data = new WeightData
            {
                grab = grab,
                transform = t,
                originalParent = t.parent,

                worldPosition = t.position,
                worldRotation = t.rotation,
                localScale = t.localScale,

                rb = rb,
                originalKinematic = rb != null && rb.isKinematic,
                originalUseGravity = rb != null && rb.useGravity,
                originalDetectCollisions = rb == null || rb.detectCollisions
            };

            savedWeights.Add(data);
        }
    }

    public void ResetAllWeights()
    {
        StopAllCoroutines();
        StartCoroutine(ResetRoutine());
    }

    private IEnumerator ResetRoutine()
    {
        foreach (WeightData data in savedWeights)
        {
            ForceRelease(data.grab);

            if (data.rb != null)
            {
                data.rb.linearVelocity = Vector3.zero;
                data.rb.angularVelocity = Vector3.zero;
                data.rb.isKinematic = true;
                data.rb.detectCollisions = false;
            }

            data.transform.SetParent(data.originalParent, true);
            data.transform.position = data.worldPosition;
            data.transform.rotation = data.worldRotation;
            data.transform.localScale = data.localScale;
        }

        yield return new WaitForFixedUpdate();

        foreach (WeightData data in savedWeights)
        {
            if (data.rb != null)
            {
                data.rb.linearVelocity = Vector3.zero;
                data.rb.angularVelocity = Vector3.zero;
                data.rb.detectCollisions = data.originalDetectCollisions;
                data.rb.isKinematic = data.originalKinematic;
                data.rb.useGravity = data.originalUseGravity;
            }
        }
    }

    private void ForceRelease(XRGrabInteractable grab)
    {
        if (grab == null || !grab.isSelected)
            return;

        XRInteractionManager manager = grab.interactionManager;

        if (manager == null)
            return;

        List<IXRSelectInteractor> selectingInteractors = new List<IXRSelectInteractor>(grab.interactorsSelecting);

        foreach (IXRSelectInteractor interactor in selectingInteractors)
        {
            manager.SelectExit(interactor, grab);
        }
    }
}