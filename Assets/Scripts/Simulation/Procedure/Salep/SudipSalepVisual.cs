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
    [SerializeField] private Color salepColor = new Color(0.93f, 0.82f, 0.48f, 1f);
    [SerializeField] private Vector3 localOffset = new Vector3(0f, 0.004f, 0f);

    private Transform blob;
    private bool loaded;

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

        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "SudipSalepBlob";
        // buang collider primitive supaya tidak mengganggu fisika/grab.
        Collider col = go.GetComponent<Collider>();
        if (col != null) Destroy(col);

        blob = go.transform;
        blob.SetParent(parent, false);
        blob.localPosition = localOffset;
        // sedikit pipih seperti gumpalan salep di sendok sudip.
        blob.localScale = new Vector3(blobRadius * 2.4f, blobRadius * 1.3f, blobRadius * 2.4f);

        MeshRenderer mr = go.GetComponent<MeshRenderer>();
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        Material mat = new Material(shader) { name = "Runtime_SudipSalep" };
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", salepColor);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", salepColor);
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.78f);

        Texture2D creamTex = Resources.Load<Texture2D>("SalepTex/cream_surface");
        if (creamTex != null)
        {
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", creamTex);
            if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", creamTex);
        }
        mr.sharedMaterial = mat;

        go.SetActive(false);
    }

    public void Load()
    {
        EnsureBlob();
        loaded = true;
        if (blob != null)
            blob.gameObject.SetActive(true);
    }

    public void Unload()
    {
        loaded = false;
        if (blob != null)
            blob.gameObject.SetActive(false);
    }
}
