using UnityEngine;

public enum SalepMortarPhase
{
    Empty,
    AsamPowder,          // Step 3: powder putih
    PowderMix,           // Step 6: powder putih + kuning (belum homogen)
    PowdersHomogeneous,  // Step 7: campuran bubuk homogen
    CreamAdded,          // Step 9 awal: powder + cream
    SalepHomogeneous     // Step 9 akhir: salep homogen ivory
}

/// <summary>
/// Visual isi mortar untuk workflow Salep. Self-building: membuat satu mound powder dan
/// satu mound cream sebagai child, lalu mengganti tampilannya per fase + fill amount.
///
/// Mortar root biasanya rotation X -90, jadi simpan offset/euler/scale via serialized field
/// supaya bisa di-tune di editor tanpa mengubah script.
/// </summary>
[DisallowMultipleComponent]
public sealed class SalepMortarVisual : MonoBehaviour
{
    [Header("Penempatan visual di dasar mortar (local)")]
    [SerializeField] private Vector3 localPosition = new Vector3(0f, 0f, 0.0001f);
    [SerializeField] private Vector3 localEuler = new Vector3(-90f, 0f, 0f);
    [SerializeField] private float baseScale = 0.0018f;
    [SerializeField] private float fullScaleMultiplier = 1.9f;

    [Header("Warna per fase")]
    [SerializeField] private Color asamWhite = new Color(0.96f, 0.965f, 0.95f, 1f);
    [SerializeField] private Color sulfurYellow = new Color(0.98f, 0.92f, 0.55f, 1f);
    [SerializeField] private Color powderMixColor = new Color(0.97f, 0.94f, 0.72f, 1f);
    [SerializeField] private Color salepIvory = new Color(0.97f, 0.95f, 0.82f, 1f);

    private Transform visualRoot;
    private SpoonPowderMoundVisual powderMound;
    private MeshRenderer powderRenderer;
    private CreamMoundVisual creamMound;
    private MeshRenderer creamRenderer;
    private Material runtimePowderMaterial;
    private Material runtimeCreamMaterial;

    private SalepMortarPhase phase = SalepMortarPhase.Empty;
    private float fill01;

    private void EnsureChildren()
    {
        if (visualRoot == null)
        {
            GameObject root = new GameObject("SalepMortarVisualRoot");
            visualRoot = root.transform;
            visualRoot.SetParent(transform, false);
            visualRoot.localPosition = localPosition;
            visualRoot.localRotation = Quaternion.Euler(localEuler);
            visualRoot.localScale = Vector3.one * baseScale;
        }

        if (powderMound == null)
        {
            GameObject powderObject = new GameObject("MortarPowderMound");
            powderObject.transform.SetParent(visualRoot, false);
            powderObject.AddComponent<MeshFilter>();
            powderRenderer = powderObject.AddComponent<MeshRenderer>();
            powderMound = powderObject.AddComponent<SpoonPowderMoundVisual>();
            runtimePowderMaterial = CreateMaterial("Runtime_SalepMortarPowder", asamWhite, 0.18f);
            powderMound.Configure(0.06f, 0.06f, 0.006f, 0.014f, 0.0014f, runtimePowderMaterial);
            powderObject.SetActive(false);
        }

        if (creamMound == null)
        {
            GameObject creamObject = new GameObject("MortarCreamMound");
            creamObject.transform.SetParent(visualRoot, false);
            creamObject.AddComponent<MeshFilter>();
            creamRenderer = creamObject.AddComponent<MeshRenderer>();
            creamMound = creamObject.AddComponent<CreamMoundVisual>();
            runtimeCreamMaterial = CreateMaterial("Runtime_SalepMortarCream", salepIvory, 0.55f);
            creamMound.Configure(0.062f, 0.062f, 0.006f, 0.022f, 0.018f, 0.0026f, runtimeCreamMaterial);
            creamObject.SetActive(false);
        }
    }

    public void SetPhase(SalepMortarPhase newPhase, float newFill01)
    {
        EnsureChildren();
        phase = newPhase;
        fill01 = Mathf.Clamp01(newFill01);
        Refresh();
    }

    public void SetFill(float newFill01)
    {
        SetPhase(phase, newFill01);
    }

    public void Clear()
    {
        SetPhase(SalepMortarPhase.Empty, 0f);
    }

    private void Refresh()
    {
        EnsureChildren();

        bool showCream = phase == SalepMortarPhase.CreamAdded || phase == SalepMortarPhase.SalepHomogeneous;
        bool showPowder = !showCream && phase != SalepMortarPhase.Empty;

        powderMound.gameObject.SetActive(showPowder);
        creamMound.gameObject.SetActive(showCream || phase == SalepMortarPhase.CreamAdded);

        float scale = baseScale * Mathf.Lerp(0.6f, fullScaleMultiplier, fill01);
        visualRoot.localScale = Vector3.one * scale;
        visualRoot.localPosition = localPosition;
        visualRoot.localRotation = Quaternion.Euler(localEuler);

        Color powderColor = phase switch
        {
            SalepMortarPhase.AsamPowder => asamWhite,
            SalepMortarPhase.PowderMix => Color.Lerp(asamWhite, sulfurYellow, 0.5f),
            SalepMortarPhase.PowdersHomogeneous => powderMixColor,
            _ => powderMixColor
        };
        ApplyColor(runtimePowderMaterial, powderColor);

        // Cream berubah dari putih krem ke ivory homogen.
        Color creamColor = phase == SalepMortarPhase.SalepHomogeneous
            ? salepIvory
            : Color.Lerp(asamWhite, salepIvory, 0.5f);
        ApplyColor(runtimeCreamMaterial, creamColor);

        // Saat CreamAdded, powder masih terlihat sedikit di bawah cream.
        if (phase == SalepMortarPhase.CreamAdded)
        {
            powderMound.gameObject.SetActive(true);
            ApplyColor(runtimePowderMaterial, powderMixColor);
        }
    }

    private Material CreateMaterial(string materialName, Color color, float smoothness)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");
        if (shader == null)
            return null;

        Material material = new Material(shader) { name = materialName };
        ApplyColor(material, color);
        if (material.HasProperty("_Smoothness"))
            material.SetFloat("_Smoothness", smoothness);
        return material;
    }

    private static void ApplyColor(Material material, Color color)
    {
        if (material == null)
            return;
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color"))
            material.SetColor("_Color", color);
    }
}
