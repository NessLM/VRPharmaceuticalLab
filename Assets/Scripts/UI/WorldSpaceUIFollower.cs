using UnityEngine;

/// <summary>
/// Membuat panel UI world-space (mis. panel instruksi langkah) selalu berada nyaman di
/// depan kamera VR (HMD) dan menghadap pemain.
///
/// PENTING (VR): Canvas Screen Space - Overlay TIDAK terlihat di dalam headset VR — hanya
/// muncul di Game view editor / XR Device Simulator. Untuk VR, Canvas harus World Space dan
/// ditempatkan di depan pemain. Komponen ini menangani penempatan + billboard tersebut.
///
/// Gerakan dihaluskan (lazy follow) agar tidak menimbulkan rasa pusing.
/// </summary>
[DisallowMultipleComponent]
public sealed class WorldSpaceUIFollower : MonoBehaviour
{
    [Tooltip("Jarak panel di depan kamera (meter).")]
    [SerializeField] private float distance = 2.0f;

    [Tooltip("Offset tinggi panel relatif terhadap mata pemain (meter). Positif = di atas mata.")]
    [SerializeField] private float heightOffset = 0.55f;

    [Tooltip("Waktu pelembutan gerak posisi. Lebih besar = lebih lambat/halus.")]
    [SerializeField] private float followSmoothTime = 0.25f;

    [Tooltip("Kecepatan pelembutan rotasi menghadap pemain.")]
    [SerializeField] private float rotationSmoothSpeed = 6f;

    private Camera targetCamera;
    private Vector3 velocity;

    /// <summary>Set kamera HMD secara eksplisit (opsional). Default: Camera.main.</summary>
    public void SetCamera(Camera cam)
    {
        targetCamera = cam;
    }

    private void OnEnable()
    {
        // Snap langsung saat aktif supaya panel tidak "terbang" dari posisi lamanya.
        if (ResolveCamera())
            ApplyImmediate();
    }

    private void LateUpdate()
    {
        if (!ResolveCamera())
            return;

        Vector3 targetPos = ComputeTargetPosition();
        transform.position = Vector3.SmoothDamp(transform.position, targetPos, ref velocity, followSmoothTime);

        Quaternion targetRot = ComputeTargetRotation();
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotationSmoothSpeed * Time.deltaTime);
    }

    private bool ResolveCamera()
    {
        if (targetCamera == null)
            targetCamera = Camera.main;
        return targetCamera != null;
    }

    private Vector3 ComputeTargetPosition()
    {
        Transform camT = targetCamera.transform;

        Vector3 flatForward = Vector3.ProjectOnPlane(camT.forward, Vector3.up);
        if (flatForward.sqrMagnitude < 0.0001f)
            flatForward = Vector3.forward;
        flatForward.Normalize();

        Vector3 pos = camT.position + flatForward * distance;
        pos.y = camT.position.y + heightOffset;
        return pos;
    }

    private Quaternion ComputeTargetRotation()
    {
        Vector3 toPanel = transform.position - targetCamera.transform.position;
        toPanel.y = 0f;
        if (toPanel.sqrMagnitude < 0.0001f)
            return transform.rotation;

        return Quaternion.LookRotation(toPanel, Vector3.up);
    }

    private void ApplyImmediate()
    {
        transform.position = ComputeTargetPosition();
        transform.rotation = ComputeTargetRotation();
        velocity = Vector3.zero;
    }
}
