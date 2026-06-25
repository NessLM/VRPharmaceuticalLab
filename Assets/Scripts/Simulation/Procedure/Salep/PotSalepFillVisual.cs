using UnityEngine;

/// <summary>
/// Visual isi salep di dalam Pot Salep. Dibangun prosedural sebagai kubah krim STYLIZED
/// (bergelombang lembut, warna solid, UNLIT) — BUKAN PNG/tekstur foto realistis. Tumbuh
/// DARI BAWAH KE ATAS sesuai jumlah salep yang sudah dimasukkan (0..1, biasanya 4 langkah).
///
/// Dasar mesh ada di y=0 → menskala tinggi (Y) membuat isi tumbuh ke atas tanpa mengangkat
/// dasarnya, sehingga selalu menempel di dasar dalam pot dan menyatu dengannya.
/// </summary>
[DisallowMultipleComponent]
public sealed class PotSalepFillVisual : MonoBehaviour
{
    [SerializeField] private float radius = 0.035f;       // jari-jari isi (meter dunia)
    [SerializeField] private float fullHeight = 0.05f;    // tinggi saat penuh (meter dunia)
    [SerializeField] private Color creamColor = new Color(0.96f, 0.88f, 0.60f, 1f);
    [SerializeField] private Vector3 bottomLocalPos = new Vector3(0f, 0.03f, 0f);

    private Transform mound;
    private Material material;
    private float fill01;

    public float Fill01 => fill01;

    public void Configure(Vector3 innerBottomLocalPos, float worldRadius, float worldFullHeight)
    {
        bottomLocalPos = innerBottomLocalPos;
        radius = Mathf.Max(0.005f, worldRadius);
        fullHeight = Mathf.Max(0.005f, worldFullHeight);
        EnsureMound();
    }

    private void EnsureMound()
    {
        if (mound != null)
            return;

        GameObject go = new GameObject("PotSalepCreamFill");
        go.AddComponent<MeshFilter>();
        go.AddComponent<MeshRenderer>();
        mound = go.transform;
        mound.SetParent(transform, false);
        // Lawan skala parent supaya dimensi mesh = ukuran dunia yang diminta.
        mound.localScale = InverseParentScale(transform);

        // Material krim STYLIZED: solid + UNLIT, tanpa tekstur foto realistis (sesuai mau user).
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
        material = new Material(shader) { name = "Runtime_PotSalepCream" };
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", creamColor);
        if (material.HasProperty("_Color")) material.SetColor("_Color", creamColor);

        // Kubah krim bergelombang lembut (wavy), lebar penuh menutup mulut pot, sedikit
        // menggunung di tengah seperti permukaan salep pada referensi.
        var shape = go.AddComponent<SpoonPowderMoundVisual>();
        shape.Configure(radius, radius, fullHeight * 0.22f, fullHeight * 0.78f, radius * 0.07f, material);

        go.SetActive(false);
    }

    /// <summary>Isi 0..1. Tumbuh DARI BAWAH (lebar penuh sejak awal, tinggi naik dari dasar).</summary>
    public void SetFill01(float t)
    {
        EnsureMound();
        fill01 = Mathf.Clamp01(t);

        bool visible = fill01 > 0.001f;
        mound.gameObject.SetActive(visible);
        if (!visible)
            return;

        Vector3 inv = InverseParentScale(transform);
        mound.localPosition = bottomLocalPos;
        // Y tumbuh dari dasar (mesh base di y=0); X/Z penuh menutup dasar pot sejak awal.
        float h = Mathf.Max(0.1f, fill01);
        mound.localScale = new Vector3(inv.x, inv.y * h, inv.z);
    }

    public void Clear()
    {
        SetFill01(0f);
    }

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
}
