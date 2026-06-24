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
        IgnoreInterWeightCollisions();
    }

    [Header("Anti-Bounce (collider)")]
    [Tooltip("Collider baki tempat anak timbangan (weightbox). Tabrakan anak timbangan vs " +
             "baki diabaikan agar tidak 'mental' saat digrab. Kosongkan untuk auto-cari " +
             "object bernama mengandung 'weightbox'.")]
    [SerializeField] private Collider[] trayColliders;

    // BUG FIX (mental saat grab): anak timbangan tersusun rapat & melayang sedikit di atas
    // baki, jadi collider-nya bisa tumpang tindih satu sama lain / dengan baki. Saat satu
    // di-grab → jadi non-kinematic → PhysX mendorongnya keluar (depenetration) dengan impuls
    // besar → terlempar. Solusi (TANPA mengubah gravity/feel): abaikan tabrakan ANTAR anak
    // timbangan DAN anak timbangan vs baki. Anak timbangan tetap dinamis + gravity normal,
    // tetap menabrak piring neraca & meja. Trigger (GrabCollider) otomatis dilewati.
    private void IgnoreInterWeightCollisions()
    {
        var solidColliders = new List<Collider>();
        foreach (WeightData data in savedWeights)
        {
            if (data.grab == null)
                continue;
            foreach (Collider col in data.grab.GetComponentsInChildren<Collider>(true))
            {
                if (col != null && !col.isTrigger)
                    solidColliders.Add(col);
            }
        }

        // Abaikan antar anak timbangan.
        for (int i = 0; i < solidColliders.Count; i++)
        {
            for (int j = i + 1; j < solidColliders.Count; j++)
            {
                if (solidColliders[i] != null && solidColliders[j] != null)
                    Physics.IgnoreCollision(solidColliders[i], solidColliders[j], true);
            }
        }

        // Abaikan anak timbangan vs baki (weightbox), termasuk weightboxsusulan.
        List<Collider> trays = ResolveTrayColliders();
        foreach (Collider tray in trays)
        {
            if (tray == null || tray.isTrigger)
                continue;
            foreach (Collider weight in solidColliders)
            {
                if (weight != null)
                    Physics.IgnoreCollision(weight, tray, true);
            }
        }
    }

    private List<Collider> ResolveTrayColliders()
    {
        var trays = new List<Collider>();

        if (trayColliders != null)
        {
            foreach (Collider c in trayColliders)
                if (c != null) trays.Add(c);
        }

        if (trays.Count == 0)
        {
            // Auto-cari semua object bernama mengandung "weightbox" di scene.
            foreach (Transform t in Object.FindObjectsByType<Transform>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (t == null)
                    continue;
                if (t.name.IndexOf("weightbox", System.StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                foreach (Collider c in t.GetComponents<Collider>())
                    if (c != null) trays.Add(c);
            }
        }

        return trays;
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

            // Kembalikan state interaksi WeightItem (hasBeenPickedUp=false, terkunci di baki)
            // supaya tidak perlu reset dua kali dan tetap tenang sampai di-grab lagi.
            if (data.grab != null)
            {
                WeightItem item = data.grab.GetComponent<WeightItem>();
                if (item != null)
                    item.ResetInteractionState();
            }
        }

        // Re-assert: IgnoreCollision bisa hilang bila collider sempat di-disable/enable.
        IgnoreInterWeightCollisions();
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