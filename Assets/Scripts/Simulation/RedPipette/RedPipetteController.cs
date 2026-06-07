using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

/// <summary>
/// VR Pharmacy - Red Pipette Controller v15 Soft Snap Detach + Free Dispense
///
/// Fix utama:
/// 1. Snap final selalu memakai TipPoint sebagai ujung bawah pipette.
/// 2. Kalau SnapTipPoint kosong, TipPoint masuk ke tengah FillZone lalu turun sedalam Tip Depth Below Mouth.
/// 3. Ada bottom clamp agar TipPoint tidak menembus dasar gelas.
/// 4. Rotasi snap dihitung dari LiquidTopPoint -> TipPoint.
/// 5. LiquidMesh dibuat dalam LOCAL SPACE parent pipette, bukan world-space, supaya tidak jadi tiang panjang/tembus.
/// 6. Support multi target: GelasUkur untuk Suck, GelasPyrex untuk Dispense.
/// 7. Soft snap: pipette nempel saat dekat, lepas saat controller/grab ditarik melewati jarak tertentu.
/// 8. Anti re-snap loop supaya setelah lepas tidak langsung lengket lagi.
/// 9. Free dispense: kalau ditekan di luar GelasUkur, air keluar dari ujung pipette dengan animasi semburan.
/// </summary>
public class RedPipetteController : MonoBehaviour
{
    public enum TransferMode
    {
        None,
        Suck,
        Dispense,
        SuckAndDispense
    }

    [Serializable]
    public class SnapTarget
    {
        public string name = "Gelas Target";

        [Header("Container")]
        public LiquidContainer container;
        public Transform mouth;            // Biasanya FillZone.
        public Collider mouthCollider;     // Collider FillZone.

        [Header("Optional Exact Final Point")]
        [Tooltip("Optional. Kalau diisi, TipPoint akan tepat ke point ini. Kalau kosong, posisi dihitung dari FillZone + depth.")]
        public Transform snapTipPoint;

        [Header("Optional Bottom Limit")]
        [Tooltip("Optional. Titik dasar aman gelas. Kalau kosong, script coba cari LiquidSpace / collider container.")]
        public Transform bottomLimitPoint;

        [Tooltip("Optional. Collider ruang cairan. Kalau kosong, script coba cari child bernama LiquidSpace.")]
        public Collider liquidSpaceCollider;

        [Header("Snap Detection")]
        [Tooltip("Jarak horizontal TipPoint ke tengah FillZone agar snap aktif.")]
        public float snapTriggerRadius = 0.12f;

        [Tooltip("TipPoint boleh berada setinggi ini di atas mulut dan tetap snap.")]
        public float snapAboveMouthAllowance = 0.22f;

        [Tooltip("TipPoint boleh berada sedalam ini di bawah mulut dan tetap snap.")]
        public float snapBelowMouthAllowance = 0.28f;

        [Header("Final Snap Position")]
        [Tooltip("Dipakai kalau Snap Tip Point kosong. Lebih besar = TipPoint lebih dalam ke gelas.")]
        public float tipDepthBelowMouth = 0.34f;

        [Tooltip("Geser target masuk X/Z dari tengah FillZone. Pakai ini kalau mau pipette dekat dinding gelas, bukan tepat center.")]
        public Vector2 snapXZOffset = Vector2.zero;

        [Tooltip("ON = TipPoint tidak akan diturunkan melewati dasar aman gelas.")]
        public bool clampTipAboveBottom = true;

        [Tooltip("Jarak aman TipPoint dari dasar gelas. Naikkan kalau masih tembus bawah.")]
        public float minTipClearanceFromBottom = 0.035f;

        [Header("Liquid Transfer")]
        public TransferMode transferMode = TransferMode.Suck;
    }

    [Header("Snap Targets")]
    [Tooltip("Rekomendasi: Element 0 = GelasUkur/Suck, Element 1 = GelasPyrex/Dispense.")]
    [SerializeField] private SnapTarget[] snapTargets;

    [Header("Pipette References")]
    [SerializeField] private Transform tipPoint;
    [SerializeField] private Transform liquidBottomPoint;
    [SerializeField] private Transform liquidTopPoint;
    [SerializeField] private Transform liquidVisual;
    [SerializeField] private Renderer liquidRenderer;
    [SerializeField] private Material liquidMaterial;
    [SerializeField] private Collider bulbClickCollider;

    [Header("Legacy Single Source Fallback")]
    [Tooltip("Dipakai hanya kalau Snap Targets kosong.")]
    [SerializeField] private LiquidContainer sourceContainer;
    [SerializeField] private Transform sourceMouth;
    [SerializeField] private Collider sourceMouthCollider;

    [Header("Snap Behaviour")]
    [SerializeField] private bool enableAutoSnap = true;
    [SerializeField] private bool requirePipetteGrabbedToSnap = true;
    [SerializeField] private bool snapInstantly = true;
    [SerializeField] private bool holdSnapPose = true;
    [SerializeField] private bool alignUsingLiquidTopToTipAxis = true;
    [SerializeField] private float snapPositionSpeed = 40f;
    [SerializeField] private float snapRotationSpeed = 32f;

    [Header("Physics While Snapped")]
    [SerializeField] private bool makeKinematicWhileSnapped = true;

    [Tooltip("ON = collision pipette dengan gelas diabaikan saat snap agar bisa masuk ke dalam gelas. Matikan kalau collider gelas sudah punya lubang yang benar.")]
    [SerializeField] private bool ignoreTargetCollisionsWhileNearOrSnapped = true;

    [SerializeField] private float collisionIgnoreRadius = 0.18f;

    [Header("Unsnap")]
    [SerializeField] private bool allowPullToUnsnap = true;

    [Tooltip("Jarak tangan/controller dari posisi awal snap sebelum pipette dilepas. 0.12 = sekitar 12 cm. Turunkan kalau masih terlalu lengket.")]
    [SerializeField] private float pullUnsnapDistance = 0.13f;

    [Tooltip("Cooldown setelah unsnap supaya tidak langsung snap ulang pada frame berikutnya.")]
    [SerializeField] private float reSnapCooldown = 0.20f;

    [Tooltip("ON = setelah dilepas, TipPoint harus keluar dulu dari snap window sebelum bisa snap lagi. Ini yang mencegah pipette terasa terlalu lengket.")]
    [SerializeField] private bool requireExitBeforeReSnap = true;

    [SerializeField] private bool unsnapWhenReleased = false;

    [Header("Suck / Dispense")]
    [SerializeField] private float maxPipetteMl = 50f;
    [SerializeField] private float transferRateMlPerSecond = 25f;
    [SerializeField] private bool useLeftMouseInEditor = true;
    [SerializeField] private bool allowGlobalLeftMouseWhenSnapped = true;

    [Header("Free Dispense / Air Spray")]
    [Tooltip("ON = saat tombol ditekan di luar target Suck/GelasUkur, isi pipette keluar dari ujung pipette, bukan ngisap.")]
    [SerializeField] private bool allowFreeDispenseWhenOutsideSuckTarget = true;

    [Tooltip("Opsional. Titik keluarnya air. Kalau kosong, TipPoint dipakai.")]
    [SerializeField] private Transform freeDispenseOrigin;

    [Tooltip("Opsional. ParticleSystem semburan/tetes air. Kalau kosong, script tetap membuat animasi LineRenderer otomatis.")]
    [SerializeField] private ParticleSystem freeDispenseParticle;

    [Tooltip("Kecepatan air keluar saat ditekan di luar GelasUkur.")]
    [SerializeField] private float freeDispenseRateMlPerSecond = 25f;

    [Tooltip("Panjang visual garis semburan air dari ujung pipette.")]
    [SerializeField] private float freeDispenseVisualLength = 0.18f;

    [Tooltip("Ketebalan visual garis semburan air.")]
    [SerializeField] private float freeDispenseVisualWidth = 0.004f;

    [SerializeField] private bool useFreeDispenseLineVisual = true;
    [SerializeField] private Material freeDispenseLineMaterial;

    [Header("LiquidMesh Visual")]
    [SerializeField] private bool hideVisualWhenEmpty = true;

    [Tooltip("Panjang maksimal liquid visual dalam local space. Kalau LiquidMesh masih kepanjangan, turunkan ini.")]
    [SerializeField] private float maxVisualLocalLength = 0.36f;

    [SerializeField] private float visualBottomRadius = 0.0012f;
    [SerializeField] private float visualTopRadius = 0.0035f;
    [SerializeField, Range(8, 48)] private int visualSegments = 24;
    [SerializeField] private Color fallbackVisualColor = new Color(0.25f, 0.65f, 1f, 0.55f);

    [Header("Debug")]
    [SerializeField] private bool drawGizmos = true;
    [SerializeField] private bool debugLogs = false;

    private Rigidbody rb;
    private Collider[] pipetteColliders;
    private Collider[] ignoredTargetColliders;
    private Component grabInteractable;

    private SnapTarget activeTarget;
    private bool isSnapped;
    private bool isTransferring;
    private bool collisionsIgnored;
    private bool savedKinematic;
    private float nextAllowedSnapTime;
    private bool waitingToExitSnapWindow;

    private Transform snapInteractor;
    private Vector3 snapInteractorStartPosition;
    private bool hasSnapInteractorStartPosition;

    private float pipetteMl;
    private LiquidData pipetteLiquid;

    private MeshFilter liquidMeshFilter;
    private Mesh generatedLiquidMesh;
    private bool warnedAddLiquidMissing;

    private LineRenderer freeDispenseLine;
    private Material runtimeFreeDispenseLineMaterial;
    private float freeDispenseAnimTime;

    public bool IsSnapped => isSnapped;
    public bool IsTransferring => isTransferring;
    public float PipetteMl => pipetteMl;

    private void Reset()
    {
        AutoFindReferences();
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        pipetteColliders = GetComponentsInChildren<Collider>(true);
        grabInteractable = FindGrabInteractableComponent();

        AutoFindReferences();
        RepairLegacyFallback();
        AutoRepairSnapTargets();
        EnsureFreeDispenseLine();
        RefreshVisual();
    }

    private void OnValidate()
    {
        snapPositionSpeed = Mathf.Max(1f, snapPositionSpeed);
        snapRotationSpeed = Mathf.Max(1f, snapRotationSpeed);
        collisionIgnoreRadius = Mathf.Max(0.01f, collisionIgnoreRadius);
        pullUnsnapDistance = Mathf.Max(0.03f, pullUnsnapDistance);
        reSnapCooldown = Mathf.Max(0f, reSnapCooldown);
        maxPipetteMl = Mathf.Max(0.01f, maxPipetteMl);
        transferRateMlPerSecond = Mathf.Max(0f, transferRateMlPerSecond);
        freeDispenseRateMlPerSecond = Mathf.Max(0f, freeDispenseRateMlPerSecond);
        freeDispenseVisualLength = Mathf.Max(0.01f, freeDispenseVisualLength);
        freeDispenseVisualWidth = Mathf.Max(0.0005f, freeDispenseVisualWidth);
        maxVisualLocalLength = Mathf.Max(0.03f, maxVisualLocalLength);
        visualBottomRadius = Mathf.Max(0.0001f, visualBottomRadius);
        visualTopRadius = Mathf.Max(visualBottomRadius, visualTopRadius);

        if (snapTargets != null)
        {
            foreach (SnapTarget target in snapTargets)
            {
                if (target == null)
                    continue;

                target.snapTriggerRadius = Mathf.Max(0.005f, target.snapTriggerRadius);
                target.snapAboveMouthAllowance = Mathf.Max(0f, target.snapAboveMouthAllowance);
                target.snapBelowMouthAllowance = Mathf.Max(0f, target.snapBelowMouthAllowance);
                target.tipDepthBelowMouth = Mathf.Max(0f, target.tipDepthBelowMouth);
                target.minTipClearanceFromBottom = Mathf.Max(0f, target.minTipClearanceFromBottom);
            }
        }
    }

    private void Update()
    {
        if (tipPoint == null)
            return;

        bool grabbed = IsPipetteGrabbed();
        SnapTarget candidate = FindBestSnapCandidate();

        HandleCollisionIgnore(candidate);

        if (waitingToExitSnapWindow && candidate == null)
            waitingToExitSnapWindow = false;

        if (enableAutoSnap && !isSnapped)
        {
            bool canSnap = candidate != null &&
                           Time.time >= nextAllowedSnapTime &&
                           !waitingToExitSnapWindow &&
                           (!requirePipetteGrabbedToSnap || grabbed);

            if (canSnap)
            {
                activeTarget = candidate;
                SetSnapped(true);
                ApplySnapPose(GetFinalTipPosition(activeTarget), true);
            }
        }

        if (isSnapped)
        {
            if (activeTarget == null)
            {
                SetSnapped(false);
            }
            else if (unsnapWhenReleased && requirePipetteGrabbedToSnap && !grabbed)
            {
                SetSnapped(false);
            }
            else if (allowPullToUnsnap && ShouldUnsnapByPull())
            {
                SetSnapped(false);
            }
            else if (holdSnapPose)
            {
                ApplySnapPose(GetFinalTipPosition(activeTarget), snapInstantly);
            }
        }

        HandleTransfer();
        RefreshVisual();
    }

    private SnapTarget FindBestSnapCandidate()
    {
        SnapTarget[] targets = GetTargets();
        if (targets == null || targets.Length == 0)
            return null;

        SnapTarget best = null;
        float bestScore = float.MaxValue;

        foreach (SnapTarget target in targets)
        {
            if (!IsTargetValid(target))
                continue;

            if (!IsTipInsideSnapWindow(target, out float score))
                continue;

            if (score < bestScore)
            {
                best = target;
                bestScore = score;
            }
        }

        return best;
    }

    private SnapTarget[] GetTargets()
    {
        if (snapTargets != null && snapTargets.Length > 0)
            return snapTargets;

        if (sourceContainer == null && sourceMouth == null && sourceMouthCollider == null)
            return null;

        SnapTarget fallback = new SnapTarget
        {
            name = "Legacy GelasUkur Source",
            container = sourceContainer,
            mouth = sourceMouth,
            mouthCollider = sourceMouthCollider,
            snapTriggerRadius = 0.12f,
            snapAboveMouthAllowance = 0.22f,
            snapBelowMouthAllowance = 0.28f,
            tipDepthBelowMouth = 0.34f,
            minTipClearanceFromBottom = 0.035f,
            clampTipAboveBottom = true,
            transferMode = TransferMode.Suck
        };

        return new[] { fallback };
    }

    private bool IsTargetValid(SnapTarget target)
    {
        if (target == null)
            return false;

        if (target.snapTipPoint != null)
            return true;

        return target.mouthCollider != null || target.mouth != null || target.container != null;
    }

    private bool IsTipInsideSnapWindow(SnapTarget target, out float score)
    {
        score = float.MaxValue;

        Vector3 mouth = GetMouthTopCenter(target);
        Vector3 tip = tipPoint.position;

        float dx = tip.x - mouth.x;
        float dz = tip.z - mouth.z;
        float horizontalDistance = Mathf.Sqrt(dx * dx + dz * dz);

        bool horizontalOk = horizontalDistance <= target.snapTriggerRadius;
        bool verticalOk = tip.y <= mouth.y + target.snapAboveMouthAllowance &&
                          tip.y >= mouth.y - target.snapBelowMouthAllowance;

        score = horizontalDistance;
        return horizontalOk && verticalOk;
    }

    private Bounds GetMouthBounds(SnapTarget target)
    {
        if (target == null)
            return new Bounds(transform.position, Vector3.one * 0.05f);

        if (target.mouthCollider != null)
            return target.mouthCollider.bounds;

        if (target.mouth != null)
        {
            Collider c = target.mouth.GetComponent<Collider>();
            if (c != null)
                return c.bounds;

            return new Bounds(target.mouth.position, Vector3.one * 0.08f);
        }

        if (target.container != null)
            return new Bounds(target.container.transform.position, Vector3.one * 0.08f);

        if (target.snapTipPoint != null)
            return new Bounds(target.snapTipPoint.position + Vector3.up * target.tipDepthBelowMouth, Vector3.one * 0.08f);

        return new Bounds(transform.position, Vector3.one * 0.05f);
    }

    private Vector3 GetMouthTopCenter(SnapTarget target)
    {
        if (target != null && target.snapTipPoint != null)
            return target.snapTipPoint.position + Vector3.up * target.tipDepthBelowMouth;

        Bounds b = GetMouthBounds(target);
        Vector2 offset = target != null ? target.snapXZOffset : Vector2.zero;

        return new Vector3(b.center.x + offset.x, b.max.y, b.center.z + offset.y);
    }

    private Vector3 GetFinalTipPosition(SnapTarget target)
    {
        if (target != null && target.snapTipPoint != null)
            return target.snapTipPoint.position;

        Vector3 mouthTop = GetMouthTopCenter(target);
        float depth = target != null ? target.tipDepthBelowMouth : 0.34f;

        Vector3 final = new Vector3(mouthTop.x, mouthTop.y - depth, mouthTop.z);

        if (target != null && target.clampTipAboveBottom)
        {
            if (TryGetBottomY(target, out float bottomY))
            {
                float minY = bottomY + target.minTipClearanceFromBottom;
                if (final.y < minY)
                    final.y = minY;
            }
        }

        return final;
    }

    private bool TryGetBottomY(SnapTarget target, out float bottomY)
    {
        bottomY = 0f;

        if (target == null)
            return false;

        if (target.bottomLimitPoint != null)
        {
            bottomY = target.bottomLimitPoint.position.y;
            return true;
        }

        if (target.liquidSpaceCollider == null && target.container != null)
        {
            Transform liquidSpace = FindDeepChild(target.container.transform, "LiquidSpace");
            if (liquidSpace != null)
                target.liquidSpaceCollider = liquidSpace.GetComponent<Collider>();
        }

        if (target.liquidSpaceCollider != null)
        {
            bottomY = target.liquidSpaceCollider.bounds.min.y;
            return true;
        }

        if (target.container != null)
        {
            Collider[] cols = target.container.GetComponentsInChildren<Collider>(true);
            bool found = false;
            float minY = float.PositiveInfinity;

            foreach (Collider c in cols)
            {
                if (c == null)
                    continue;

                if (c == target.mouthCollider)
                    continue;

                // Trigger FillZone / PourZone biasanya bukan dasar fisik gelas.
                if (c.isTrigger && c.name.IndexOf("Fill", StringComparison.OrdinalIgnoreCase) >= 0)
                    continue;

                minY = Mathf.Min(minY, c.bounds.min.y);
                found = true;
            }

            if (found)
            {
                bottomY = minY;
                return true;
            }
        }

        return false;
    }

    private void ApplySnapPose(Vector3 targetTipPosition, bool instant)
    {
        Quaternion targetRotation = GetTargetSnapRotation();

        if (instant)
        {
            transform.rotation = targetRotation;
            transform.position += targetTipPosition - tipPoint.position;
            StopRigidbodyMotion();
            return;
        }

        float rotT = 1f - Mathf.Exp(-snapRotationSpeed * Time.deltaTime);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotT);

        Vector3 delta = targetTipPosition - tipPoint.position;
        float posT = 1f - Mathf.Exp(-snapPositionSpeed * Time.deltaTime);
        transform.position += delta * posT;

        StopRigidbodyMotion();
    }

    private Quaternion GetTargetSnapRotation()
    {
        if (!alignUsingLiquidTopToTipAxis)
            return transform.rotation;

        if (liquidTopPoint == null || tipPoint == null)
            return transform.rotation;

        Vector3 currentDownAxis = tipPoint.position - liquidTopPoint.position;
        if (currentDownAxis.sqrMagnitude < 0.000001f)
            return transform.rotation;

        Quaternion delta = Quaternion.FromToRotation(currentDownAxis.normalized, Vector3.down);
        return delta * transform.rotation;
    }

    private void SetSnapped(bool value)
    {
        if (isSnapped == value)
            return;

        isSnapped = value;

        if (value)
        {
            snapInteractor = GetSelectingInteractorTransform();
            if (snapInteractor != null)
            {
                snapInteractorStartPosition = snapInteractor.position;
                hasSnapInteractorStartPosition = true;
            }
            else
            {
                hasSnapInteractorStartPosition = false;
            }
        }
        else
        {
            activeTarget = null;
            snapInteractor = null;
            hasSnapInteractorStartPosition = false;
            isTransferring = false;

            nextAllowedSnapTime = Time.time + reSnapCooldown;
            if (requireExitBeforeReSnap)
                waitingToExitSnapWindow = true;
        }

        if (rb != null && makeKinematicWhileSnapped)
        {
            if (value)
            {
                savedKinematic = rb.isKinematic;
                StopRigidbodyMotion();
                rb.isKinematic = true;
            }
            else
            {
                rb.isKinematic = savedKinematic;
                StopRigidbodyMotion();
            }
        }

        if (debugLogs)
            Debug.Log($"[RedPipette] Snapped = {isSnapped}, Target = {(activeTarget != null ? activeTarget.name : "None")}", this);
    }

    private bool ShouldUnsnapByPull()
    {
        if (!allowPullToUnsnap)
            return false;

        if (!IsPipetteGrabbed())
            return false;

        if (snapInteractor == null)
            snapInteractor = GetSelectingInteractorTransform();

        if (snapInteractor == null)
            return false;

        if (!hasSnapInteractorStartPosition)
        {
            snapInteractorStartPosition = snapInteractor.position;
            hasSnapInteractorStartPosition = true;
            return false;
        }

        return Vector3.Distance(snapInteractor.position, snapInteractorStartPosition) >= pullUnsnapDistance;
    }

    private void HandleTransfer()
    {
        if (!IsTransferInputHeld())
        {
            isTransferring = false;
            StopFreeDispenseVisual();
            return;
        }

        if (isSnapped && activeTarget != null && activeTarget.container != null)
        {
            bool mayDispense = activeTarget.transferMode == TransferMode.Dispense ||
                               activeTarget.transferMode == TransferMode.SuckAndDispense;

            bool maySuck = activeTarget.transferMode == TransferMode.Suck ||
                           activeTarget.transferMode == TransferMode.SuckAndDispense;

            // Kalau target ini memang target buang/isi, air masuk ke container target.
            if (mayDispense && pipetteMl > 0.001f)
            {
                isTransferring = true;
                StopFreeDispenseVisual();
                DispenseInto(activeTarget.container);
                return;
            }

            // Kalau sedang di GelasUkur / target Suck, tombol berarti ngisap, bukan buang.
            if (maySuck)
            {
                isTransferring = true;
                StopFreeDispenseVisual();
                SuckFrom(activeTarget.container);
                return;
            }
        }

        // Kalau tidak berada di GelasUkur / target Suck, tekan bulb = air keluar dari ujung pipette.
        if (allowFreeDispenseWhenOutsideSuckTarget && pipetteMl > 0.001f)
        {
            isTransferring = true;
            FreeDispenseToAir();
            return;
        }

        isTransferring = false;
        StopFreeDispenseVisual();
    }

    private void SuckFrom(LiquidContainer container)
    {
        if (container == null || container.IsEmpty)
            return;

        if (pipetteMl >= maxPipetteMl - 0.001f)
            return;

        float amount = Mathf.Min(transferRateMlPerSecond * Time.deltaTime, maxPipetteMl - pipetteMl);
        amount = Mathf.Min(amount, Mathf.Max(0f, container.CurrentMl));

        if (amount <= 0f)
            return;

        if (pipetteLiquid == null)
            pipetteLiquid = container.CurrentLiquid != null ? container.CurrentLiquid : container.LiquidType;

        container.RemoveLiquid(amount);
        pipetteMl += amount;
    }

    private void DispenseInto(LiquidContainer container)
    {
        if (container == null || pipetteMl <= 0.001f)
            return;

        float amount = Mathf.Min(transferRateMlPerSecond * Time.deltaTime, pipetteMl);

        if (TryAddLiquidByReflection(container, amount, pipetteLiquid, out float accepted))
        {
            accepted = Mathf.Clamp(accepted, 0f, amount);
            pipetteMl -= accepted;

            if (pipetteMl <= 0.001f)
            {
                pipetteMl = 0f;
                pipetteLiquid = null;
            }
        }
        else if (!warnedAddLiquidMissing)
        {
            warnedAddLiquidMissing = true;
            Debug.LogWarning("[RedPipette] LiquidContainer tidak punya AddLiquid/ReceiveLiquid/Fill/AddVolume yang cocok. Dispense ke target belum bisa.", this);
        }
    }

    private void FreeDispenseToAir()
    {
        if (pipetteMl <= 0.001f)
        {
            StopFreeDispenseVisual();
            return;
        }

        float amount = Mathf.Min(freeDispenseRateMlPerSecond * Time.deltaTime, pipetteMl);
        pipetteMl -= amount;

        UpdateFreeDispenseVisual(true);

        if (pipetteMl <= 0.001f)
        {
            pipetteMl = 0f;
            pipetteLiquid = null;
            StopFreeDispenseVisual();
        }
    }

    private void EnsureFreeDispenseLine()
    {
        if (!useFreeDispenseLineVisual)
            return;

        if (freeDispenseLine != null)
            return;

        Transform existing = FindDeepChild(transform, "Runtime_FreeDispenseLine");
        GameObject lineObject;

        if (existing != null)
            lineObject = existing.gameObject;
        else
        {
            lineObject = new GameObject("Runtime_FreeDispenseLine");
            lineObject.transform.SetParent(transform, false);
        }

        freeDispenseLine = lineObject.GetComponent<LineRenderer>();
        if (freeDispenseLine == null)
            freeDispenseLine = lineObject.AddComponent<LineRenderer>();

        freeDispenseLine.useWorldSpace = true;
        freeDispenseLine.positionCount = 2;
        freeDispenseLine.numCapVertices = 4;
        freeDispenseLine.numCornerVertices = 2;
        freeDispenseLine.widthMultiplier = freeDispenseVisualWidth;
        freeDispenseLine.enabled = false;

        if (freeDispenseLineMaterial != null)
        {
            freeDispenseLine.sharedMaterial = freeDispenseLineMaterial;
        }
        else
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
                shader = Shader.Find("Unlit/Color");

            if (shader != null)
            {
                runtimeFreeDispenseLineMaterial = new Material(shader);
                freeDispenseLine.sharedMaterial = runtimeFreeDispenseLineMaterial;
            }
        }
    }

    private void UpdateFreeDispenseVisual(bool show)
    {
        if (!show)
        {
            StopFreeDispenseVisual();
            return;
        }

        if (freeDispenseParticle != null && !freeDispenseParticle.isPlaying)
            freeDispenseParticle.Play(true);

        if (!useFreeDispenseLineVisual)
            return;

        EnsureFreeDispenseLine();
        if (freeDispenseLine == null)
            return;

        Transform origin = freeDispenseOrigin != null ? freeDispenseOrigin : tipPoint;
        if (origin == null)
            return;

        freeDispenseAnimTime += Time.deltaTime * 18f;

        Vector3 start = origin.position;
        Vector3 direction = GetTipOutDirection();
        float pulse = 0.78f + Mathf.Sin(freeDispenseAnimTime) * 0.12f;
        Vector3 end = start + direction * (freeDispenseVisualLength * pulse);

        Color color = pipetteLiquid != null ? pipetteLiquid.liquidColor : fallbackVisualColor;
        color.a = Mathf.Max(color.a, 0.65f);

        Color endColor = color;
        endColor.a = 0.05f;

        freeDispenseLine.widthMultiplier = freeDispenseVisualWidth;
        freeDispenseLine.startColor = color;
        freeDispenseLine.endColor = endColor;
        freeDispenseLine.SetPosition(0, start);
        freeDispenseLine.SetPosition(1, end);
        freeDispenseLine.enabled = true;
    }

    private void StopFreeDispenseVisual()
    {
        if (freeDispenseParticle != null && freeDispenseParticle.isPlaying)
            freeDispenseParticle.Stop(true, ParticleSystemStopBehavior.StopEmitting);

        if (freeDispenseLine != null)
            freeDispenseLine.enabled = false;
    }

    private Vector3 GetTipOutDirection()
    {
        if (tipPoint != null && liquidTopPoint != null)
        {
            Vector3 axis = tipPoint.position - liquidTopPoint.position;
            if (axis.sqrMagnitude > 0.000001f)
                return axis.normalized;
        }

        if (tipPoint != null)
            return -tipPoint.up;

        return -transform.up;
    }

    private bool TryAddLiquidByReflection(LiquidContainer container, float amount, LiquidData liquid, out float acceptedAmount)
    {
        acceptedAmount = 0f;
        if (container == null)
            return false;

        Type type = container.GetType();
        MethodInfo[] methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance);

        string[] methodNames = { "AddLiquid", "ReceiveLiquid", "Fill", "Add", "AddVolume", "SetLiquid" };

        foreach (string wantedName in methodNames)
        {
            foreach (MethodInfo method in methods)
            {
                if (method.Name != wantedName)
                    continue;

                ParameterInfo[] p = method.GetParameters();
                object[] args = BuildAddLiquidArgs(p, amount, liquid);
                if (args == null)
                    continue;

                float before = container.CurrentMl;
                object result = method.Invoke(container, args);
                float after = container.CurrentMl;
                float delta = Mathf.Max(0f, after - before);

                if (result is bool boolResult && boolResult == false)
                {
                    acceptedAmount = 0f;
                    return true;
                }

                if (result is float f)
                    acceptedAmount = Mathf.Max(0f, f);
                else if (result is double d)
                    acceptedAmount = Mathf.Max(0f, (float)d);
                else if (delta > 0f)
                    acceptedAmount = delta;
                else
                    acceptedAmount = amount;

                return true;
            }
        }

        return false;
    }

    private object[] BuildAddLiquidArgs(ParameterInfo[] p, float amount, LiquidData liquid)
    {
        if (p == null)
            return null;

        if (p.Length == 1 && IsNumberType(p[0].ParameterType))
            return new object[] { ConvertNumber(amount, p[0].ParameterType) };

        if (p.Length == 2)
        {
            if (IsNumberType(p[0].ParameterType) && IsLiquidDataCompatible(p[1].ParameterType))
                return new object[] { ConvertNumber(amount, p[0].ParameterType), liquid };

            if (IsLiquidDataCompatible(p[0].ParameterType) && IsNumberType(p[1].ParameterType))
                return new object[] { liquid, ConvertNumber(amount, p[1].ParameterType) };
        }

        return null;
    }

    private bool IsNumberType(Type type)
    {
        return type == typeof(float) || type == typeof(double) || type == typeof(int);
    }

    private object ConvertNumber(float value, Type type)
    {
        if (type == typeof(float)) return value;
        if (type == typeof(double)) return (double)value;
        if (type == typeof(int)) return Mathf.RoundToInt(value);
        return value;
    }

    private bool IsLiquidDataCompatible(Type type)
    {
        return type == typeof(LiquidData) || type.IsAssignableFrom(typeof(LiquidData));
    }

    private bool IsTransferInputHeld()
    {
        bool mouseHeld = useLeftMouseInEditor && Input.GetMouseButton(0);
        if (!mouseHeld)
            return false;

        if (allowGlobalLeftMouseWhenSnapped)
            return true;

        if (bulbClickCollider == null)
            return false;

        Camera cam = Camera.main;
        if (cam == null)
            return false;

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, 100f, ~0, QueryTriggerInteraction.Collide))
            return hit.collider == bulbClickCollider || hit.collider.transform.IsChildOf(bulbClickCollider.transform);

        return false;
    }

    private void RefreshVisual()
    {
        if (liquidVisual == null)
            return;

        bool show = !hideVisualWhenEmpty || pipetteMl > 0.001f;
        liquidVisual.gameObject.SetActive(show);

        if (!show)
            return;

        Transform bottomTransform = liquidBottomPoint != null ? liquidBottomPoint : tipPoint;
        Transform topTransform = liquidTopPoint;

        if (bottomTransform == null || topTransform == null)
        {
            liquidVisual.gameObject.SetActive(false);
            return;
        }

        Transform visualParent = liquidVisual.parent != null ? liquidVisual.parent : transform;

        Vector3 bottomLocal = visualParent.InverseTransformPoint(bottomTransform.position);
        Vector3 topLocal = visualParent.InverseTransformPoint(topTransform.position);
        Vector3 axisLocal = topLocal - bottomLocal;

        if (axisLocal.sqrMagnitude < 0.000001f)
        {
            liquidVisual.gameObject.SetActive(false);
            return;
        }

        float fullLength = axisLocal.magnitude;
        Vector3 directionLocal = axisLocal / fullLength;

        if (fullLength > maxVisualLocalLength)
            fullLength = maxVisualLocalLength;

        float fill01 = Mathf.Clamp01(pipetteMl / maxPipetteMl);
        float currentLength = Mathf.Max(0.001f, fullLength * fill01);

        Vector3 midLocal = bottomLocal + directionLocal * (currentLength * 0.5f);

        liquidVisual.localPosition = midLocal;
        liquidVisual.localRotation = Quaternion.FromToRotation(Vector3.up, directionLocal);
        liquidVisual.localScale = Vector3.one;

        BuildTaperedLiquidMesh(currentLength, fill01);
        ApplyLiquidMaterial();
    }

    private void BuildTaperedLiquidMesh(float length, float fill01)
    {
        if (liquidVisual == null)
            return;

        if (liquidMeshFilter == null)
            liquidMeshFilter = liquidVisual.GetComponent<MeshFilter>();

        if (liquidMeshFilter == null)
            liquidMeshFilter = liquidVisual.gameObject.AddComponent<MeshFilter>();

        if (generatedLiquidMesh == null)
        {
            generatedLiquidMesh = new Mesh();
            generatedLiquidMesh.name = "Runtime_RedPipette_LocalLiquid";
        }

        int seg = Mathf.Clamp(visualSegments, 8, 48);
        Vector3[] vertices = new Vector3[seg * 2 + 2];
        int[] triangles = new int[seg * 12];

        float half = length * 0.5f;
        float bottomRadius = visualBottomRadius;
        float topRadius = Mathf.Lerp(visualBottomRadius, visualTopRadius, Mathf.Clamp01(fill01));

        vertices[0] = new Vector3(0f, -half, 0f);
        vertices[1] = new Vector3(0f, half, 0f);

        for (int i = 0; i < seg; i++)
        {
            float angle = (i / (float)seg) * Mathf.PI * 2f;
            float cs = Mathf.Cos(angle);
            float sn = Mathf.Sin(angle);

            vertices[2 + i] = new Vector3(cs * bottomRadius, -half, sn * bottomRadius);
            vertices[2 + seg + i] = new Vector3(cs * topRadius, half, sn * topRadius);
        }

        int ti = 0;
        for (int i = 0; i < seg; i++)
        {
            int next = (i + 1) % seg;

            int b0 = 2 + i;
            int b1 = 2 + next;
            int t0 = 2 + seg + i;
            int t1 = 2 + seg + next;

            triangles[ti++] = b0;
            triangles[ti++] = t0;
            triangles[ti++] = t1;

            triangles[ti++] = b0;
            triangles[ti++] = t1;
            triangles[ti++] = b1;

            triangles[ti++] = 0;
            triangles[ti++] = b1;
            triangles[ti++] = b0;

            triangles[ti++] = 1;
            triangles[ti++] = t0;
            triangles[ti++] = t1;
        }

        generatedLiquidMesh.Clear();
        generatedLiquidMesh.vertices = vertices;
        generatedLiquidMesh.triangles = triangles;
        generatedLiquidMesh.RecalculateNormals();
        generatedLiquidMesh.RecalculateBounds();

        liquidMeshFilter.sharedMesh = generatedLiquidMesh;
    }

    private void ApplyLiquidMaterial()
    {
        if (liquidRenderer == null && liquidVisual != null)
            liquidRenderer = liquidVisual.GetComponent<Renderer>();

        if (liquidRenderer == null)
            return;

        if (liquidMaterial != null)
            liquidRenderer.sharedMaterial = liquidMaterial;

        Material mat = Application.isPlaying ? liquidRenderer.material : liquidRenderer.sharedMaterial;
        if (mat == null)
            return;

        Color color = pipetteLiquid != null ? pipetteLiquid.liquidColor : fallbackVisualColor;

        if (mat.HasProperty("_BaseColor"))
            mat.SetColor("_BaseColor", color);

        if (mat.HasProperty("_Color"))
            mat.SetColor("_Color", color);
    }

    private void HandleCollisionIgnore(SnapTarget candidate)
    {
        bool shouldIgnore = ignoreTargetCollisionsWhileNearOrSnapped && (isSnapped || IsCandidateNearForCollision(candidate));

        if (shouldIgnore == collisionsIgnored)
            return;

        if (pipetteColliders == null)
            pipetteColliders = GetComponentsInChildren<Collider>(true);

        Collider[] collidersToModify = ignoredTargetColliders;

        if (shouldIgnore)
        {
            SnapTarget target = activeTarget != null ? activeTarget : candidate;
            ignoredTargetColliders = GetTargetColliders(target);
            collidersToModify = ignoredTargetColliders;
        }

        if (pipetteColliders != null && collidersToModify != null)
        {
            foreach (Collider a in pipetteColliders)
            {
                if (a == null)
                    continue;

                foreach (Collider b in collidersToModify)
                {
                    if (b == null || a == b)
                        continue;

                    Physics.IgnoreCollision(a, b, shouldIgnore);
                }
            }
        }

        if (!shouldIgnore)
            ignoredTargetColliders = null;

        collisionsIgnored = shouldIgnore;
    }

    private bool IsCandidateNearForCollision(SnapTarget candidate)
    {
        if (candidate == null || tipPoint == null)
            return false;

        Vector3 mouth = GetMouthTopCenter(candidate);
        Vector3 tip = tipPoint.position;

        float dx = tip.x - mouth.x;
        float dz = tip.z - mouth.z;
        float horizontal = Mathf.Sqrt(dx * dx + dz * dz);

        bool horizontalNear = horizontal <= collisionIgnoreRadius;
        bool verticalNear = tip.y <= mouth.y + 0.30f && tip.y >= mouth.y - 0.42f;

        return horizontalNear && verticalNear;
    }

    private Collider[] GetTargetColliders(SnapTarget target)
    {
        if (target == null)
            return null;

        List<Collider> result = new List<Collider>();

        if (target.mouthCollider != null)
            result.Add(target.mouthCollider);

        if (target.container != null)
        {
            Collider[] cols = target.container.GetComponentsInChildren<Collider>(true);
            foreach (Collider c in cols)
            {
                if (c == null)
                    continue;

                // Ignore collision dengan semua collider container agar pipette bisa masuk ke gelas tertutup mesh collider.
                // Bottom clamp tetap mencegah TipPoint ditarik tembus dasar.
                if (!result.Contains(c))
                    result.Add(c);
            }
        }
        else if (target.mouth != null)
        {
            Collider[] cols = target.mouth.GetComponentsInParent<Collider>(true);
            foreach (Collider c in cols)
            {
                if (c != null && !result.Contains(c))
                    result.Add(c);
            }
        }

        return result.Count > 0 ? result.ToArray() : null;
    }

    private void StopRigidbodyMotion()
    {
        if (rb == null)
            return;

#if UNITY_6000_0_OR_NEWER
        rb.linearVelocity = Vector3.zero;
#else
        rb.velocity = Vector3.zero;
#endif
        rb.angularVelocity = Vector3.zero;
    }

    private bool IsPipetteGrabbed()
    {
        if (grabInteractable == null)
            grabInteractable = FindGrabInteractableComponent();

        if (grabInteractable == null)
            return false;

        PropertyInfo prop = grabInteractable.GetType().GetProperty("isSelected", BindingFlags.Public | BindingFlags.Instance);
        if (prop != null && prop.PropertyType == typeof(bool))
            return (bool)prop.GetValue(grabInteractable);

        return false;
    }

    private Transform GetSelectingInteractorTransform()
    {
        if (grabInteractable == null)
            grabInteractable = FindGrabInteractableComponent();

        if (grabInteractable == null)
            return null;

        PropertyInfo firstProp = grabInteractable.GetType().GetProperty("firstInteractorSelecting", BindingFlags.Public | BindingFlags.Instance);
        if (firstProp != null)
        {
            Transform first = ExtractTransformFromInteractorObject(firstProp.GetValue(grabInteractable));
            if (first != null)
                return first;
        }

        PropertyInfo listProp = grabInteractable.GetType().GetProperty("interactorsSelecting", BindingFlags.Public | BindingFlags.Instance);
        if (listProp != null)
        {
            object listValue = listProp.GetValue(grabInteractable);
            if (listValue is IEnumerable enumerable)
            {
                foreach (object interactor in enumerable)
                {
                    Transform t = ExtractTransformFromInteractorObject(interactor);
                    if (t != null)
                        return t;
                }
            }
        }

        return null;
    }

    private Transform ExtractTransformFromInteractorObject(object value)
    {
        if (value == null)
            return null;

        if (value is Component component)
            return component.transform;

        PropertyInfo transformProp = value.GetType().GetProperty("transform", BindingFlags.Public | BindingFlags.Instance);
        if (transformProp != null && typeof(Transform).IsAssignableFrom(transformProp.PropertyType))
            return transformProp.GetValue(value) as Transform;

        return null;
    }

    private Component FindGrabInteractableComponent()
    {
        Component[] comps = GetComponents<Component>();
        foreach (Component c in comps)
        {
            if (c == null)
                continue;

            if (c.GetType().Name.Contains("XRGrabInteractable"))
                return c;
        }

        return null;
    }

    [ContextMenu("Auto Find References")]
    public void AutoFindReferences()
    {
        if (tipPoint == null)
            tipPoint = FindDeepChild(transform, "TipPoint");

        if (liquidTopPoint == null)
            liquidTopPoint = FindDeepChild(transform, "LiquidTopPoint");

        if (liquidBottomPoint == null)
            liquidBottomPoint = FindDeepChild(transform, "LiquidBottomPoint");

        if (liquidBottomPoint == null)
            liquidBottomPoint = tipPoint;

        if (liquidVisual == null)
        {
            liquidVisual = FindDeepChild(transform, "LiquidMesh");
            if (liquidVisual == null)
                liquidVisual = FindDeepChild(transform, "LiquidVisual");
        }

        if (liquidRenderer == null && liquidVisual != null)
            liquidRenderer = liquidVisual.GetComponent<Renderer>();

        if (liquidMeshFilter == null && liquidVisual != null)
            liquidMeshFilter = liquidVisual.GetComponent<MeshFilter>();

        if (freeDispenseOrigin == null)
            freeDispenseOrigin = tipPoint;
    }

    [ContextMenu("Repair Legacy Fallback")]
    public void RepairLegacyFallback()
    {
        if (sourceContainer != null)
        {
            Transform fillZone = FindDeepChild(sourceContainer.transform, "FillZone");
            if (fillZone != null)
                sourceMouth = fillZone;
        }

        if (sourceMouth != null && sourceMouthCollider == null)
            sourceMouthCollider = sourceMouth.GetComponent<Collider>();
    }

    [ContextMenu("Auto Repair Snap Targets")]
    public void AutoRepairSnapTargets()
    {
        SnapTarget[] targets = GetTargets();
        if (targets == null)
            return;

        foreach (SnapTarget target in targets)
        {
            if (target == null || target.container == null)
                continue;

            if (target.mouth == null)
            {
                Transform fill = FindDeepChild(target.container.transform, "FillZone");
                if (fill != null)
                    target.mouth = fill;
            }

            if (target.mouthCollider == null && target.mouth != null)
                target.mouthCollider = target.mouth.GetComponent<Collider>();

            if (target.liquidSpaceCollider == null)
            {
                Transform space = FindDeepChild(target.container.transform, "LiquidSpace");
                if (space != null)
                    target.liquidSpaceCollider = space.GetComponent<Collider>();
            }
        }
    }

    [ContextMenu("Apply Stable Pipette Preset")]
    public void ApplyStablePipettePreset()
    {
        enableAutoSnap = true;
        requirePipetteGrabbedToSnap = true;
        snapInstantly = true;
        holdSnapPose = true;
        alignUsingLiquidTopToTipAxis = true;
        snapPositionSpeed = 40f;
        snapRotationSpeed = 32f;

        makeKinematicWhileSnapped = true;
        ignoreTargetCollisionsWhileNearOrSnapped = true;
        collisionIgnoreRadius = 0.14f;

        allowPullToUnsnap = true;
        pullUnsnapDistance = 0.13f;
        reSnapCooldown = 0.20f;
        requireExitBeforeReSnap = true;
        unsnapWhenReleased = false;

        maxPipetteMl = 50f;
        transferRateMlPerSecond = 25f;
        allowFreeDispenseWhenOutsideSuckTarget = true;
        freeDispenseRateMlPerSecond = 25f;
        freeDispenseVisualLength = 0.18f;
        freeDispenseVisualWidth = 0.004f;
        useFreeDispenseLineVisual = true;
        hideVisualWhenEmpty = true;
        maxVisualLocalLength = 0.36f;
        visualBottomRadius = 0.0012f;
        visualTopRadius = 0.0035f;

        if (snapTargets != null)
        {
            foreach (SnapTarget target in snapTargets)
            {
                if (target == null)
                    continue;

                target.snapTriggerRadius = 0.085f;
                target.snapAboveMouthAllowance = 0.18f;
                target.snapBelowMouthAllowance = 0.28f;
                target.tipDepthBelowMouth = 0.34f;
                target.snapXZOffset = Vector2.zero;
                target.clampTipAboveBottom = true;
                target.minTipClearanceFromBottom = 0.035f;
            }
        }

        RefreshVisual();
    }

    [ContextMenu("Make GelasUkur Snap Deeper")]
    public void MakeGelasUkurSnapDeeper()
    {
        if (snapTargets == null || snapTargets.Length == 0)
            return;

        snapTargets[0].tipDepthBelowMouth += 0.05f;
    }

    [ContextMenu("Make GelasUkur Snap Shallower")]
    public void MakeGelasUkurSnapShallower()
    {
        if (snapTargets == null || snapTargets.Length == 0)
            return;

        snapTargets[0].tipDepthBelowMouth = Mathf.Max(0.05f, snapTargets[0].tipDepthBelowMouth - 0.05f);
    }

    [ContextMenu("Reset LiquidMesh Local Transform")]
    public void ResetLiquidMeshLocalTransform()
    {
        if (liquidVisual == null)
            AutoFindReferences();

        if (liquidVisual == null)
            return;

        liquidVisual.localScale = Vector3.one;
        RefreshVisual();
    }

    [ContextMenu("Debug Fill Pipette Full")]
    public void DebugFillPipetteFull()
    {
        pipetteMl = maxPipetteMl;

        if (activeTarget != null && activeTarget.container != null)
            pipetteLiquid = activeTarget.container.CurrentLiquid != null ? activeTarget.container.CurrentLiquid : activeTarget.container.LiquidType;

        RefreshVisual();
    }

    [ContextMenu("Debug Empty Pipette")]
    public void DebugEmptyPipette()
    {
        pipetteMl = 0f;
        pipetteLiquid = null;
        RefreshVisual();
    }

    private Transform FindDeepChild(Transform parent, string childName)
    {
        if (parent == null)
            return null;

        foreach (Transform child in parent)
        {
            if (child.name == childName)
                return child;

            Transform result = FindDeepChild(child, childName);
            if (result != null)
                return result;
        }

        return null;
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawGizmos || tipPoint == null)
            return;

        SnapTarget[] targets = GetTargets();
        if (targets == null)
            return;

        foreach (SnapTarget target in targets)
        {
            if (!IsTargetValid(target))
                continue;

            Vector3 mouth = GetMouthTopCenter(target);
            Vector3 finalTip = GetFinalTipPosition(target);

            Gizmos.color = Color.white;
            Gizmos.DrawWireSphere(mouth, 0.015f);

            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(finalTip, 0.018f);
            Gizmos.DrawLine(mouth, finalTip);

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(mouth, target.snapTriggerRadius);

            if (TryGetBottomY(target, out float bottomY))
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(new Vector3(finalTip.x, bottomY + target.minTipClearanceFromBottom, finalTip.z), 0.012f);
            }
        }
    }
}
