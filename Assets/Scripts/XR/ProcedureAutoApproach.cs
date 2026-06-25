using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Menganimasikan sebuah objek yang bisa di-grab (botol / pot salep) supaya MENDEKAT
/// sendiri ke sisi sebuah anchor (mis. Mortar) saat memasuki tahap akhir prosedur.
/// Tetap bisa di-grab: begitu pemain meraihnya, animasi berhenti dan XR mengambil alih.
///
/// Hidup berdampingan dengan ToolSurfaceSnap & XRGrabInteractable:
/// - Selama mendekat, Rigidbody dibuat kinematic dan posisi digerakkan di LateUpdate
///   (menang atas script lain). ToolSurfaceSnap tidak ikut campur kecuali masuk zona snap.
/// - Saat di-grab, animasi langsung berhenti tanpa menyentuh Rigidbody (biar XR yang atur).
/// </summary>
[DisallowMultipleComponent]
public sealed class ProcedureAutoApproach : MonoBehaviour
{
    [Tooltip("Offset world dari anchor (mortar) ke posisi tujuan. Default: 22 cm di depan mortar (ke arah pemain).")]
    [SerializeField] private Vector3 besideOffset = new Vector3(0f, 0f, -0.22f);
    [Tooltip("Kecepatan gerak mendekat (m/detik).")]
    [SerializeField] private float moveSpeed = 0.7f;
    [Tooltip("Kecepatan rotasi menegak (derajat/detik).")]
    [SerializeField] private float rotateSpeed = 140f;
    [Tooltip("Pertahankan ketinggian (Y) objek saat ini agar tetap menempel permukaan meja.")]
    [SerializeField] private bool preserveY = true;

    private Rigidbody rb;
    private XRGrabInteractable grab;
    private ToolSurfaceSnap snap;

    private bool approaching;
    private Vector3 targetPos;
    private Quaternion targetRot;

    private void EnsureRefs()
    {
        if (rb == null) rb = GetComponent<Rigidbody>();
        if (grab == null) grab = GetComponent<XRGrabInteractable>();
        if (snap == null) snap = GetComponent<ToolSurfaceSnap>();
    }

    /// <summary>Mulai animasi mendekat ke sisi anchor (mortar) memakai besideOffset.</summary>
    public void ApproachBeside(Transform anchor)
    {
        if (anchor == null)
            return;

        Vector3 pos = anchor.position + besideOffset;
        if (preserveY)
            pos.y = transform.position.y;

        // PERTAHANKAN rotasi objek saat ini (= pose istirahat tegaknya). JANGAN paksa
        // Euler(0,yaw,0): model botol pose tegaknya sering punya komponen X/Z non-nol
        // (mis. rotasi import), sehingga memaksanya (0,yaw,0) membuatnya MIRING 90°.
        // Dengan menyimpan rotasi sekarang, botol tetap berdiri tegak selama mendekat.
        ApproachTo(pos, transform.rotation);
    }

    /// <summary>Mulai animasi mendekat ke posisi/rotasi world tertentu.</summary>
    public void ApproachTo(Vector3 worldPos, Quaternion worldRot)
    {
        EnsureRefs();

        // Jika sedang digenggam, jangan paksa pindah.
        if (grab != null && grab.isSelected)
            return;

        targetPos = worldPos;
        targetRot = worldRot;
        approaching = true;

        // Nonaktifkan ToolSurfaceSnap selama mendekat: kalau objek berada di SurfaceSnapZone,
        // snap akan terus menarik X/Z balik ke titik istirahat → berkelahi dengan animasi
        // (gerak jadi setengah). Diaktifkan lagi saat selesai / saat di-grab.
        if (snap != null)
            snap.enabled = false;

        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.useGravity = false;
            rb.isKinematic = true;
        }
    }

    public void Cancel()
    {
        approaching = false;
        if (snap != null)
            snap.enabled = true;
    }

    private void LateUpdate()
    {
        if (!approaching)
            return;

        // Pemain meraih objek → hentikan animasi, biarkan XR yang mengatur Rigidbody.
        if (grab != null && grab.isSelected)
        {
            approaching = false;
            if (snap != null)
                snap.enabled = true;   // kembalikan fungsi snap normal setelah dilepas
            return;
        }

        transform.position = Vector3.MoveTowards(transform.position, targetPos, moveSpeed * Time.deltaTime);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, rotateSpeed * Time.deltaTime);

        if (Vector3.Distance(transform.position, targetPos) < 0.004f &&
            Quaternion.Angle(transform.rotation, targetRot) < 1f)
        {
            transform.position = targetPos;
            transform.rotation = targetRot;
            approaching = false;

            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.isKinematic = true;
                rb.useGravity = false;
            }

            // Aktifkan lagi ToolSurfaceSnap supaya bisa di-grab & snap normal setelah ini.
            if (snap != null)
                snap.enabled = true;
        }
    }
}
