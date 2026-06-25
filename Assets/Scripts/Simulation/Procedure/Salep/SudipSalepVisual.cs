using UnityEngine;

/// <summary>
/// Visual salep yang menempel di ujung Sudip. Dibangun sendiri (gumpalan krim kecil) dan
/// dipasang ke SudipTip. Load() menampilkan saat salep diambil dari ujung stamper;
/// Unload() menyembunyikan saat salep sudah dipindah ke pot.
/// Memakai tekstur krim hasil generate (Resources/SalepTex/cream_surface) bila ada.
/// </summary>
[DisallowMultipleComponent]
public sealed class SudipSalepVisual : MonoBehaviour
{
    [SerializeField] private Transform sudipTip;
    // Radius dunia (meter) gumpalan salep di ujung sudip. Kecil = cungkilan krim mungil.
    [SerializeField] private float blobRadius = 0.008f;
    // Warna salep pucat (sama referensi salep 2-4). Material UNLIT → tidak "blown out" putih.
    [SerializeField] private Color salepColor = new Color(0.94f, 0.89f, 0.62f, 1f);
    // Offset 0 → gumpalan duduk PERSIS di ujung sudip (menempel), bukan melayang. Offset
    // lama ikut terskala besar oleh sudip (lossyScale ~6-9x) sehingga blob tampak mengambang.
    [SerializeField] private Vector3 localOffset = Vector3.zero;

    private Transform blob;
    private bool loaded;
    private Vector3 _fullBlobScale;

    public bool IsLoaded => loaded;

    public void Configure(Transform tip)
    {
        if (tip != null)
            sudipTip = tip;
        EnsureBlob();
    }

    private void EnsureBlob()
    {
        if (blob != null)
            return;

        Transform parent = sudipTip != null ? sudipTip : transform;

        // Gumpalan salep = kubah kecil pipih (scoop) yang halus, BUKAN bola primitif yang
        // melar. Dibangun dengan SpoonPowderMoundVisual (radius kecil, smooth) agar tampak
        // seperti cungkilan krim mungil yang duduk rapi di ujung sudip.
        GameObject go = new GameObject("SudipSalepBlob");
        go.AddComponent<MeshFilter>();
        MeshRenderer mr = go.AddComponent<MeshRenderer>();

        blob = go.transform;
        blob.SetParent(parent, false);
        blob.localPosition = localOffset;
        // Sudip diskalakan besar (lossyScale ~6.5-9x). Mesh blob dibangun pada ukuran DUNIA
        // (blobRadius dalam meter), jadi lawan skala parent supaya gumpalan tampil kecil &
        // wajar di ujung sudip, bukan "segede gaban".
        _fullBlobScale = InverseParentScale(parent);
        blob.localScale = _fullBlobScale;

        // UNLIT supaya warna krim pucat tetap terlihat & tidak "blown out" putih di
        // bawah pencahayaan scene yang terang.
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
        // Krim MATTE solid (tanpa tekstur foto realistis) → tampilan natural & konsisten
        // dengan salep di mortar/pot.
        Material mat = new Material(shader) { name = "Runtime_SudipSalep" };
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", salepColor);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", salepColor);
        mr.sharedMaterial = mat;

        // Kubah kecil & pipih: lebar > tinggi, halus (tanpa butiran) seperti krim.
        SpoonPowderMoundVisual scoop = go.AddComponent<SpoonPowderMoundVisual>();
        scoop.Configure(
            blobRadius * 1.4f,   // radiusX (sedikit lonjong mengikuti mata sudip)
            blobRadius * 1.0f,   // radiusZ
            blobRadius * 0.18f,  // baseHeight (tipis)
            blobRadius * 0.7f,   // moundHeight (pipih, tidak lancip)
            blobRadius * 0.06f,  // noise lembut
            mat);

        go.SetActive(false);
    }

    public void Load()
    {
        EnsureBlob();
        loaded = true;
        if (blob != null)
        {
            blob.gameObject.SetActive(true);
            blob.localScale = _fullBlobScale;
        }
    }

    /// <summary>
    /// Sisa salep di ujung sudip (1 = penuh, 0 = habis). Saat menuang ke pot, gumpalan
    /// menyusut "sedikit demi sedikit".
    /// </summary>
    public void SetFill(float t)
    {
        EnsureBlob();
        if (blob == null)
            return;
        float k = Mathf.Clamp01(t);
        blob.gameObject.SetActive(loaded && k > 0.02f);
        blob.localScale = _fullBlobScale * Mathf.Lerp(0.25f, 1f, k);
    }

    // Skala lokal yang membatalkan lossyScale parent (komponen-per-komponen) sehingga
    // ukuran dunia gumpalan = ukuran mesh aslinya (blobRadius dalam meter).
    private static Vector3 InverseParentScale(Transform parent)
    {
        if (parent == null)
            return Vector3.one;
        Vector3 ls = parent.lossyScale;
        return new Vector3(
            Mathf.Approximately(ls.x, 0f) ? 1f : 1f / ls.x,
            Mathf.Approximately(ls.y, 0f) ? 1f : 1f / ls.y,
            Mathf.Approximately(ls.z, 0f) ? 1f : 1f / ls.z);
    }

    public void Unload()
    {
        loaded = false;
        if (blob != null)
            blob.gameObject.SetActive(false);
    }
}
