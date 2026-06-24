using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

[DisallowMultipleComponent]
public class SpoonPowderDumpByRotation : MonoBehaviour
{
    private enum LocalAxis
    {
        Up,
        Down,
        Forward,
        Back,
        Right,
        Left
    }

    [Header("References")]
    [SerializeField] private HornSpoon hornSpoon;
    [SerializeField] private XRGrabInteractable grabInteractable;

    [Tooltip("Pakai PowderHoldPoint kalau ada. Kalau tidak, pakai sendokTanduk.")]
    [SerializeField] private Transform orientationReference;

    [Header("Dump Detection")]
    [SerializeField] private bool requireHeld = true;

    [Tooltip("Axis lokal mana yang dianggap menghadap bawah saat sendok dibalik.")]
    [SerializeField] private LocalAxis axisToPointDown = LocalAxis.Up;

    [Tooltip("0.75 = agak longgar, 0.9 = harus benar-benar terbalik.")]
    [Range(0.1f, 1f)]
    [SerializeField] private float upsideDownThreshold = 0.75f;

    [Tooltip("Berapa lama harus dalam posisi terbalik sebelum bubuk dibuang.")]
    [SerializeField] private float requiredUpsideDownTime = 0.45f;

    [Tooltip("Harus balik agak normal dulu sebelum bisa dump lagi.")]
    [Range(-1f, 1f)]
    [SerializeField] private float resetThreshold = 0.25f;

    [Header("Dump")]
    [SerializeField] private float dumpAmountMg = 99999f;
    [SerializeField] private float cooldownAfterDump = 0.5f;

    [Header("Optional FX")]
    [SerializeField] private ParticleSystem dumpFx;

    [Header("Debug")]
    [SerializeField] private bool debugLogs;
    [SerializeField] private float currentDownDot;
    [SerializeField] private float upsideDownTimer;
    [SerializeField] private bool canDumpAgain = true;

    private float nextAllowedDumpTime;

    private void Awake()
    {
        if (hornSpoon == null)
            hornSpoon = GetComponent<HornSpoon>();

        if (grabInteractable == null)
            grabInteractable = GetComponent<XRGrabInteractable>();

        if (orientationReference == null)
        {
            if (hornSpoon != null && hornSpoon.TipTransform != null)
                orientationReference = hornSpoon.TipTransform;
            else
                orientationReference = transform;
        }
    }

    private void Update()
    {
        if (hornSpoon == null || orientationReference == null)
            return;

        if (requireHeld && (grabInteractable == null || !grabInteractable.isSelected))
        {
            upsideDownTimer = 0f;
            return;
        }

        if (hornSpoon.IsEmpty)
        {
            upsideDownTimer = 0f;
            return;
        }

        Vector3 checkedAxis = GetWorldAxis(orientationReference, axisToPointDown);
        currentDownDot = Vector3.Dot(checkedAxis.normalized, Vector3.down);

        bool isUpsideDown = currentDownDot >= upsideDownThreshold;

        if (!isUpsideDown)
        {
            upsideDownTimer = 0f;

            if (currentDownDot <= resetThreshold)
                canDumpAgain = true;

            return;
        }

        if (!canDumpAgain)
            return;

        upsideDownTimer += Time.deltaTime;

        if (upsideDownTimer >= requiredUpsideDownTime && Time.time >= nextAllowedDumpTime)
        {
            DumpPowder();
        }
    }

    public void DumpPowder()
    {
        if (hornSpoon == null || hornSpoon.IsEmpty)
            return;

        float removedMg = hornSpoon.RemovePowder(dumpAmountMg);

        if (removedMg <= 0.001f)
            return;

        PlayDumpFx();

        upsideDownTimer = 0f;
        canDumpAgain = false;
        nextAllowedDumpTime = Time.time + cooldownAfterDump;

        if (debugLogs)
            Debug.Log($"[SpoonPowderDumpByRotation] Dumped {removedMg:0.###} mg from spoon.", this);
    }

    private void PlayDumpFx()
    {
        if (dumpFx == null)
            return;

        if (orientationReference != null)
        {
            dumpFx.transform.position = orientationReference.position;
            dumpFx.transform.rotation = orientationReference.rotation;
        }

        // Warna FX ikut bahan yang dipegang sendok (Sulfur kuning, Asam putih, dll).
        if (hornSpoon != null)
        {
            ParticleSystem.MainModule main = dumpFx.main;
            Color fxColor = hornSpoon.CurrentIngredientColor;
            fxColor.a = main.startColor.color.a;
            main.startColor = fxColor;
        }

        dumpFx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        dumpFx.Play(true);
    }

    private Vector3 GetWorldAxis(Transform reference, LocalAxis axis)
    {
        switch (axis)
        {
            case LocalAxis.Up:
                return reference.up;

            case LocalAxis.Down:
                return -reference.up;

            case LocalAxis.Forward:
                return reference.forward;

            case LocalAxis.Back:
                return -reference.forward;

            case LocalAxis.Right:
                return reference.right;

            case LocalAxis.Left:
                return -reference.right;

            default:
                return reference.up;
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        requiredUpsideDownTime = Mathf.Max(0.05f, requiredUpsideDownTime);
        cooldownAfterDump = Mathf.Max(0f, cooldownAfterDump);
        dumpAmountMg = Mathf.Max(1f, dumpAmountMg);
    }
#endif
}