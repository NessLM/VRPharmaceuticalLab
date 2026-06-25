using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// VRChat-style removable lid/cap for bottles and jars (botol CTM, toples difenhidramin, dll).
///
/// Setup:
///   1. Lid GameObject: add XRGrabInteractable + Rigidbody + BottleLid.
///   2. Create an empty child on the bottle mouth → assign as closedAnchor.
///   3. Set bottleRoot to the bottle's root Transform.
///   4. Wire onOpened / onClosed events as needed (e.g., to PillBottleController).
///
/// Behavior:
///   - Grab lid → lid detaches from bottle (IsOpen = true), bisa dibawa bebas.
///   - Release DEKAT mulut botol (≤ snapRadius) → menutup (snap balik).
///   - Release di antara → jatuh dengan gravity (bisa diambil lagi).
///   - Jika tutup TERLALU JAUH dari botolnya (≥ autoReturnDistance) → otomatis balik ke
///     botol (jarak yang memicu auto-return), supaya tidak pernah hilang.
/// </summary>
[RequireComponent(typeof(XRGrabInteractable), typeof(Rigidbody))]
public class BottleLid : MonoBehaviour
{
    [Header("Bottle Reference")]
    [Tooltip("Root Transform of the bottle. Lid re-parents here when closed.")]
    [SerializeField] private Transform bottleRoot;

    [Header("Closed Anchor")]
    [Tooltip("Empty Transform placed at the bottle mouth (world position where lid sits when closed).")]
    [SerializeField] private Transform closedAnchor;
    [Tooltip("Jarak (m) di mana melepas tutup akan langsung menutup ke botol.")]
    [SerializeField] private float snapRadius = 0.08f;

    [Header("Auto Return By Distance")]
    [Tooltip("Jika ON, saat tutup terlalu jauh dari botolnya ia otomatis kembali ke botol.")]
    [SerializeField] private bool autoReturnWhenFar = true;
    [Tooltip("Ambang jarak (m) dari posisi tertutup. Jika jarak tutup ≥ nilai ini, tutup " +
             "otomatis balik ke botol — baik saat dilepas maupun saat menggelinding jauh.")]
    [SerializeField] private float autoReturnDistance = 0.45f;

    [Header("Physics")]
    [Tooltip("Saat dijatuhkan (jarak menengah), tutup memakai gravity agar mendarat wajar di meja.")]
    [SerializeField] private bool useGravityWhenDropped = true;

    [Header("Debug")]
    [SerializeField] private bool debugLogs;

    [Header("Events")]
    public UnityEvent onOpened;
    public UnityEvent onClosed;

    /// <summary>True when the lid has been removed from the bottle.</summary>
    public bool IsOpen { get; private set; }

    private XRGrabInteractable _grab;
    private Rigidbody _rb;

    private Transform _homeParent;
    private Vector3 _homeLocalPosition;
    private Quaternion _homeLocalRotation;
    private Vector3 _homeLocalScale = Vector3.one;
    private bool _homeCaptured;

    private void Awake()
    {
        ResolveComponents();
        CaptureHome();
    }

    private void CaptureHome()
    {
        if (_homeCaptured)
            return;

        _homeParent = transform.parent;
        _homeLocalPosition = transform.localPosition;
        _homeLocalRotation = transform.localRotation;
        _homeLocalScale = transform.localScale; // simpan skala asli → cegah tutup "mengecil"
        _homeCaptured = true;

        if (bottleRoot == null && _homeParent != null)
            bottleRoot = _homeParent;
    }

    private void OnEnable()
    {
        ResolveComponents();

        if (_grab != null)
        {
            _grab.selectEntered.AddListener(OnGrabbed);
            _grab.selectExited.AddListener(OnReleased);
        }
    }

    private void OnDisable()
    {
        if (_grab != null)
        {
            _grab.selectEntered.RemoveListener(OnGrabbed);
            _grab.selectExited.RemoveListener(OnReleased);
        }
    }

    private void Start()
    {
        // Ensure lid is closed at start
        SnapToClosed(fireEvent: false);
    }

    private void OnGrabbed(SelectEnterEventArgs args)
    {
        ResolveComponents();
        // Detach from bottle so it can be freely moved
        transform.SetParent(null, true);
        IsOpen = true;
        if (_rb != null)
            _rb.useGravity = false; // XRI controls position while held
        onOpened?.Invoke();
        Log($"{gameObject.name} opened");
    }

    private void OnReleased(SelectExitEventArgs args)
    {
        float dist = Vector3.Distance(transform.position, GetClosedWorldPosition());

        // Dekat mulut botol → tutup.
        if (dist <= snapRadius)
        {
            SnapToClosed(fireEvent: true);
            return;
        }

        // Terlalu jauh → otomatis balik ke botol (jarak yang memicu auto-return).
        if (autoReturnWhenFar && dist >= autoReturnDistance)
        {
            SnapToClosed(fireEvent: true);
            return;
        }

        // Jarak menengah → jatuh wajar dengan gravity, masih bisa diambil lagi.
        DropLid();
    }

    private void Update()
    {
        // Jaring pengaman: jika tutup (tidak sedang dipegang) menggelinding TERLALU JAUH
        // dari botolnya, kembalikan otomatis supaya tidak pernah hilang.
        if (!autoReturnWhenFar || !IsOpen)
            return;

        if (_grab != null && _grab.isSelected)
            return;

        float dist = Vector3.Distance(transform.position, GetClosedWorldPosition());
        if (dist >= autoReturnDistance)
            SnapToClosed(fireEvent: true);
    }

    private Vector3 GetClosedWorldPosition()
    {
        if (closedAnchor != null)
            return closedAnchor.position;
        if (_homeCaptured && _homeParent != null)
            return _homeParent.TransformPoint(_homeLocalPosition);
        if (_homeCaptured)
            return _homeLocalPosition;
        return transform.position;
    }

    private void SnapToClosed(bool fireEvent)
    {
        ResolveComponents();
        IsOpen = false;

        if (_rb != null)
        {
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
            _rb.isKinematic = true;
            _rb.useGravity = false;
        }

        // Selalu kembalikan ke pose awal lewat parent + local pos/rot/SCALE tersimpan.
        // Memakai localScale tersimpan mencegah tutup tampak "mengecil/membesar" akibat
        // perhitungan ulang skala saat re-parent (parent ber-skala non-1).
        if (_homeCaptured)
        {
            transform.SetParent(_homeParent, false);
            transform.localPosition = _homeLocalPosition;
            transform.localRotation = _homeLocalRotation;
            transform.localScale = _homeLocalScale;
        }
        else if (closedAnchor != null)
        {
            if (bottleRoot != null)
                transform.SetParent(bottleRoot, true);
            transform.SetPositionAndRotation(closedAnchor.position, closedAnchor.rotation);
        }
        else if (bottleRoot != null)
        {
            transform.SetParent(bottleRoot, true);
        }

        if (fireEvent)
        {
            onClosed?.Invoke();
            Log($"{gameObject.name} closed (snapped)");
        }
    }

    private void DropLid()
    {
        ResolveComponents();
        if (_rb == null)
            return;

        // MELAYANG DI TEMPAT (bukan jatuh, bukan drifting): saat dilepas di jarak menengah,
        // tutup dibekukan PERSIS di posisi terakhir → gravity mati, kecepatan nol, kinematic.
        // Jadi ia "terbang" diam di udara di tempat dilepas, masih bisa diambil lagi, dan
        // jaring-pengaman auto-return tetap berlaku bila terlalu jauh.
        _rb.linearVelocity = Vector3.zero;
        _rb.angularVelocity = Vector3.zero;
        _rb.useGravity = false;
        _rb.isKinematic = true;

        Log($"{gameObject.name} dropped (frozen floating)");
    }
    /// <summary>Programmatically force-closes the lid (re-snaps to bottle).</summary>
    public void ForceClose()
    {
        SnapToClosed(fireEvent: true);
    }

    public void ResetToClosed()
    {
        SnapToClosed(fireEvent: false);
    }

    /// <summary>Programmatically force-opens the lid (detaches without grab).</summary>
    public void ForceOpen()
    {
        ResolveComponents();
        transform.SetParent(null, true);
        IsOpen = true;
        if (_rb != null)
        {
            _rb.isKinematic = false;
            _rb.useGravity = useGravityWhenDropped;
        }
        onOpened?.Invoke();
    }

    private void ResolveComponents()
    {
        if (_grab == null)
            _grab = GetComponent<XRGrabInteractable>();

        if (_rb == null)
            _rb = GetComponent<Rigidbody>();
    }

    private void Log(string message)
    {
        if (debugLogs)
            Debug.Log($"[BottleLid] {message}", this);
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (closedAnchor == null) return;

        Gizmos.color = new Color(0f, 1f, 0.5f, 0.3f);
        Gizmos.DrawSphere(closedAnchor.position, snapRadius);
        Gizmos.color = new Color(0f, 1f, 0.5f, 0.85f);
        Gizmos.DrawWireSphere(closedAnchor.position, snapRadius);
        Gizmos.DrawLine(transform.position, closedAnchor.position);
    }
#endif
}
