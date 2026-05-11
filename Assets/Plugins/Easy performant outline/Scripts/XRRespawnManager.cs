using UnityEngine;

// Script ini tempel di XR Origin (XR Rig), bukan di RespawnPoint.
public class XRRespawnManager : MonoBehaviour
{
    [Header("XR References")]

    // Parent utama player VR kamu.
    // Isi dengan XR Origin (XR Rig).
    public Transform xrOrigin;

    // CharacterController dari XR Origin.
    // Dimatikan sebentar saat respawn supaya teleport tidak dilawan collision.
    public CharacterController characterController;

    // Kamera/HMD player.
    // Ini yang dicek jatuh, karena di VR yang benar-benar kamu lihat adalah kamera.
    public Transform mainCamera;


    [Header("Respawn Settings")]

    // Titik respawn di lantai/pad.
    // Isi dengan RespawnPoint.
    public Transform respawnPoint;

    // Kalau kamera turun melewati Y ini, player akan respawn.
    public float fallLimitY = -20f;

    // Tinggi kepala/kamera setelah respawn.
    // Karena RespawnPoint kamu ada di lantai, kamera harus dinaikkan.
    public float respawnHeadHeight = 1.6f;

    // Jeda anti-loop setelah respawn.
    public float respawnCooldown = 0.5f;

    private float lastRespawnTime = -999f;


    private void Start()
    {
        // Kalau XR Origin belum diisi, otomatis pakai object tempat script ini ditempel.
        if (xrOrigin == null)
        {
            xrOrigin = transform;
        }

        // Kalau CharacterController belum diisi, ambil dari object yang sama.
        if (characterController == null)
        {
            characterController = GetComponent<CharacterController>();
        }

        // Kalau Main Camera belum diisi, coba ambil Camera.main.
        // Tapi tetap lebih aman drag Main Camera manual di Inspector.
        if (mainCamera == null && Camera.main != null)
        {
            mainCamera = Camera.main.transform;
        }
    }


    private void Update()
    {
        // Kalau reference penting belum lengkap, script tidak jalan.
        if (xrOrigin == null || mainCamera == null || respawnPoint == null)
        {
            return;
        }

        // Anti-loop: setelah respawn, tunggu sebentar sebelum bisa respawn lagi.
        if (Time.time - lastRespawnTime < respawnCooldown)
        {
            return;
        }

        // Cek posisi jatuh dari Main Camera / HMD, bukan dari XR Origin.
        if (mainCamera.position.y < fallLimitY)
        {
            Respawn();
        }
    }


    public void Respawn()
    {
        lastRespawnTime = Time.time;

        // Matikan CharacterController sementara.
        // CharacterController memang mengatur movement dengan collision,
        // jadi saat teleport lebih aman dimatikan sebentar.
        if (characterController != null)
        {
            characterController.enabled = false;
        }

        // Rotasi player mengikuti arah RespawnPoint, tapi hanya sumbu Y.
        // Ini mencegah player miring/terbalik.
        xrOrigin.rotation = Quaternion.Euler(0f, respawnPoint.eulerAngles.y, 0f);

        // Paksa transform update dulu setelah rotasi.
        Physics.SyncTransforms();

        // Target posisi kamera setelah respawn.
        // RespawnPoint ada di lantai, lalu ditambah tinggi kepala.
        Vector3 targetCameraPosition = respawnPoint.position + Vector3.up * respawnHeadHeight;

        // Hitung selisih dari posisi kamera sekarang ke target kamera.
        Vector3 cameraMoveOffset = targetCameraPosition - mainCamera.position;

        // Geser XR Origin sebesar selisih itu.
        // Jadi yang dijamin sampai ke titik respawn adalah kamera/HMD.
        xrOrigin.position += cameraMoveOffset;

        // Sinkronkan physics setelah teleport.
        Physics.SyncTransforms();

        // Hidupkan lagi CharacterController.
        if (characterController != null)
        {
            characterController.enabled = true;
        }

        Debug.Log("Respawn berhasil: kamera dipindahkan ke RespawnPoint.");
    }
}