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
    [SerializeField] private float blobRadius = 0.012f;
    // Warna salep pucat (sama referensi salep 2-4). Material UNLIT → tidak "blown out" putih.
    [SerializeField] private Color salepColor = new Color(0.94f, 0.89f, 0.62f, 1f);
    [SerializeField] private Vector3 localOffset = new Vector3(0f, 0.004f, 0f);

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
        blob.localScale = Vector3.one;
        _fullBlobScale = Vector3.one;

        // UNLIT supaya warna krim pucat tetap terlihat & tidak "blown out" putih di
        // bawah pencahayaan scene yang terang.
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
        Material mat = new Material(shader) { name = "Runtime_SudipSalep" };
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", salepColor);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", salepColor);

        Texture2D creamTex = Resources.Load<Texture2D>("SalepTex/cream_surface");
        if (creamTex != null)
        {
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", creamTex);
            if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", creamTex);
        }
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

    public void Unload()
    {
        loaded = false;
        if (blob != null)
            blob.gameObject.SetActive(false);
    }
}
