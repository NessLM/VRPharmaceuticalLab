using UnityEngine;

/// <summary>
/// Visual krim (Vaselin Album / petroleum jelly) di piring neraca. Saat menimbang Vaselin,
/// mound bubuk disembunyikan (PowderVisualLevelSwitcher.SetSuppressed) dan sebagai gantinya
/// muncul gundukan krim putih mengilap yang skalanya mengikuti jumlah yang sudah ditimbang.
/// Untuk bahan bubuk (Asam/Sulfur) komponen ini nonaktif sehingga visual bubuk normal kembali.
/// Dibuat & di-wire runtime oleh SalepIngredientRuntimeSetup, lalu dikontrol SalepBench.
/// </summary>
[DisallowMultipleComponent]
public sealed class SalepPanCreamVisual : MonoBehaviour
{
    private PowderDepositZone zone;
    private PowderVisualLevelSwitcher powderSwitcher;
    private Transform creamRoot;
    private CreamMoundVisual cream;
    private Material creamMaterial;
    private bool active;

    [SerializeField] private float minScale = 0.45f;
    [SerializeField] private float maxScale = 1f;

    /// <summary>Bangun mound krim sebagai child piring, sembunyikan dulu.</summary>
    public void Setup(PowderDepositZone depositZone, PowderVisualLevelSwitcher switcher, Material material)
    {
        zone = depositZone;
        powderSwitcher = switcher;
        creamMaterial = material != null ? material : CreateGlossyCreamMaterial();

        if (creamRoot == null)
        {
            GameObject go = new GameObject("SalepPanCream_Mound");
            creamRoot = go.transform;

            Transform anchor = switcher != null
                ? switcher.transform
                : (zone != null ? zone.transform : transform);
            creamRoot.SetParent(anchor, false);
            creamRoot.localPosition = Vector3.zero;
            creamRoot.localRotation = Quaternion.identity;
            creamRoot.localScale = Vector3.one;

            go.AddComponent<MeshFilter>();
            go.AddComponent<MeshRenderer>();
            cream = go.AddComponent<CreamMoundVisual>();
            // Ukuran disamakan dengan footprint mound bubuk piring (~9 cm lebar, ~3 cm tinggi).
            cream.Configure(0.045f, 0.034f, 0.004f, 0.016f, 0.012f, 0.0018f, creamMaterial);
        }

        creamRoot.gameObject.SetActive(false);
    }

    /// <summary>Mulai mode krim: sembunyikan bubuk, tampilkan krim mengikuti isi pan.</summary>
    public void Begin()
    {
        active = true;
        if (powderSwitcher != null)
            powderSwitcher.SetSuppressed(true);
        UpdateFill();
    }

    /// <summary>Hentikan mode krim: kembalikan visual bubuk normal.</summary>
    public void Stop()
    {
        active = false;
        if (creamRoot != null)
            creamRoot.gameObject.SetActive(false);
        if (powderSwitcher != null)
            powderSwitcher.SetSuppressed(false);
    }

    private void Update()
    {
        if (active)
            UpdateFill();
    }

    private void UpdateFill()
    {
        if (zone == null || creamRoot == null)
            return;

        float max = Mathf.Max(1f, zone.MaxDepositMg);
        float ratio = Mathf.Clamp01(zone.DepositedMg / max);
        bool show = ratio > 0.01f;

        if (creamRoot.gameObject.activeSelf != show)
            creamRoot.gameObject.SetActive(show);

        if (show)
        {
            float s = Mathf.Lerp(minScale, maxScale, ratio);
            creamRoot.localScale = new Vector3(s, s, s);
        }
    }

    private static Material CreateGlossyCreamMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");
        if (shader == null)
            return null;

        Material m = new Material(shader) { name = "Runtime_SalepPanCream" };
        Color cream = new Color(0.97f, 0.96f, 0.92f, 1f); // putih gading mengilap (petroleum jelly)
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", cream);
        if (m.HasProperty("_Color")) m.SetColor("_Color", cream);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.85f);
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
        return m;
    }
}