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

    [Header("Pemisahan dua serbuk (Asam kiri, Sulfur kanan)")]
    [Tooltip("Jarak pisah kedua mound serbuk (local visualRoot units) saat belum homogen.")]
    [SerializeField] private float splitOffset = 0.27f;

    [Header("Warna per fase")]
    [SerializeField] private Color asamWhite = new Color(0.96f, 0.965f, 0.95f, 1f);
    [SerializeField] private Color sulfurYellow = new Color(0.98f, 0.92f, 0.55f, 1f);
    [SerializeField] private Color powderMixColor = new Color(0.97f, 0.94f, 0.72f, 1f);
    [SerializeField] private Color salepIvory = new Color(0.97f, 0.95f, 0.82f, 1f);

    private Transform visualRoot;
    private SpoonPowderMoundVisual powderMound;   // mound A: Asam (putih)
    private SpoonPowderMoundVisual powderMoundB;   // mound B: Sulfur (kuning)
    private CreamMoundVisual creamMound;
    private Material runtimePowderMaterial;
    private Material runtimePowderMaterialB;
    private Material runtimeCreamMaterial;

    private SalepMortarPhase phase = SalepMortarPhase.Empty;
    private float fill01;

    [Header("Efek mengaduk (saat menggerus)")]
    [Tooltip("Lama efek aduk aktif setelah progress terakhir bertambah (detik).")]
    [SerializeField] private float mixEffectHoldSeconds = 0.18f;
    [SerializeField] private float mixPulseAmount = 0.06f;
    [SerializeField] private float mixPulseSpeed = 11f;

    private ParticleSystem mixPuffFx;
    private float lastMixTime = float.NegativeInfinity;
    private float lastObservedFill;
    private bool MixingActive => Application.isPlaying && Time.time - lastMixTime <= mixEffectHoldSeconds;

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
            powderMound = BuildPowderMound("MortarPowderMound", asamWhite, out runtimePowderMaterial, "Runtime_SalepMortarPowderA");
        }

        if (powderMoundB == null)
        {
            powderMoundB = BuildPowderMound("MortarPowderMoundB", sulfurYellow, out runtimePowderMaterialB, "Runtime_SalepMortarPowderB");
        }

        if (creamMound == null)
        {
            GameObject creamObject = new GameObject("MortarCreamMound");
            creamObject.transform.SetParent(visualRoot, false);
            creamObject.AddComponent<MeshFilter>();
            creamObject.AddComponent<MeshRenderer>();
            creamMound = creamObject.AddComponent<CreamMoundVisual>();
            runtimeCreamMaterial = CreateMaterial("Runtime_SalepMortarCream", salepIvory, 0.55f);
            creamMound.Configure(0.40f, 0.36f, 0.03f, 0.13f, 0.11f, 0.012f, runtimeCreamMaterial);
            creamObject.SetActive(false);
        }
    }

    private SpoonPowderMoundVisual BuildPowderMound(string objectName, Color color, out Material material, string materialName)
    {
        GameObject powderObject = new GameObject(objectName);
        powderObject.transform.SetParent(visualRoot, false);
        powderObject.AddComponent<MeshFilter>();
        powderObject.AddComponent<MeshRenderer>();
        SpoonPowderMoundVisual mound = powderObject.AddComponent<SpoonPowderMoundVisual>();
        material = CreateMaterial(materialName, color, 0.18f);
        // Radii besar (local visualRoot units) agar mound mengisi mangkuk mortar,
        // sebanding dengan visual bubuk bawaan (~0.13 m). visualRoot world scale ~0.13.
        mound.Configure(0.36f, 0.32f, 0.03f, 0.14f, 0.012f, material);
        powderObject.SetActive(false);
        return mound;
    }

    public void SetPhase(SalepMortarPhase newPhase, float newFill01)
    {
        EnsureChildren();

        float clamped = Mathf.Clamp01(newFill01);

        // Saat fase mengaduk dan progress bertambah → user sedang menggerus: picu efek.
        bool mixingPhase = newPhase == SalepMortarPhase.PowderMix || newPhase == SalepMortarPhase.CreamAdded;
        if (mixingPhase && newPhase == phase && clamped > lastObservedFill + 0.0005f)
        {
            lastMixTime = Time.time;
            EmitMixPuff();
        }
        lastObservedFill = clamped;

        phase = newPhase;
        fill01 = clamped;
        Refresh();
    }

    private void Update()
    {
        // Pulsa kecil pada mound aktif saat user sedang menggerus, biar terasa "teraduk".
        // Saat tidak mengaduk, pulse = 1 (skala kembali normal).
        float pulse = MixingActive
            ? 1f + Mathf.Sin(Time.time * mixPulseSpeed) * mixPulseAmount
            : 1f;
        ApplyPulse(powderMound, pulse);
        ApplyPulse(powderMoundB, pulse);
        ApplyPulse(creamMound, pulse);
    }

    private static void ApplyPulse(MonoBehaviour mound, float pulse)
    {
        if (mound == null || !mound.gameObject.activeSelf)
            return;
        Vector3 s = mound.transform.localScale;
        // Skala dasar mound = 1; terapkan pulse pada XZ saja (jaga tinggi).
        mound.transform.localScale = new Vector3(pulse, 1f, pulse);
    }

    private void EmitMixPuff()
    {
        EnsureMixPuff();
        if (mixPuffFx == null)
            return;

        // Warna puff = campuran warna fase saat ini (putih+kuning saat PowderMix).
        Color a = phase == SalepMortarPhase.CreamAdded ? salepIvory : asamWhite;
        Color b = phase == SalepMortarPhase.CreamAdded ? salepIvory : sulfurYellow;
        ParticleSystem.MainModule main = mixPuffFx.main;
        main.startColor = new ParticleSystem.MinMaxGradient(a, b);
        mixPuffFx.Emit(6);
    }

    private void EnsureMixPuff()
    {
        if (mixPuffFx != null)
            return;

        EnsureChildren();
        GameObject fxObject = new GameObject("MortarMixPuffFX");
        fxObject.transform.SetParent(transform, false);
        fxObject.transform.localPosition = localPosition;
        fxObject.transform.localRotation = Quaternion.identity;

        mixPuffFx = fxObject.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = mixPuffFx.main;
        main.loop = false;
        main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.45f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.14f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.004f, 0.010f);
        main.gravityModifier = -0.02f;
        main.maxParticles = 60;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startColor = new ParticleSystem.MinMaxGradient(asamWhite, sulfurYellow);

        ParticleSystem.EmissionModule emission = mixPuffFx.emission;
        emission.enabled = false;
        emission.rateOverTime = 0f;

        ParticleSystem.ShapeModule shape = mixPuffFx.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Hemisphere;
        shape.radius = 0.03f;

        ParticleSystemRenderer renderer = mixPuffFx.GetComponent<ParticleSystemRenderer>();
        if (renderer == null)
            renderer = fxObject.AddComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;

        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");
        if (shader != null)
            renderer.sharedMaterial = new Material(shader) { name = "Runtime_MortarMixPuff" };
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

        visualRoot.localPosition = localPosition;
        visualRoot.localRotation = Quaternion.Euler(localEuler);

        switch (phase)
        {
            case SalepMortarPhase.Empty:
                powderMound.gameObject.SetActive(false);
                powderMoundB.gameObject.SetActive(false);
                creamMound.gameObject.SetActive(false);
                break;

            case SalepMortarPhase.AsamPowder:
                // Hanya Asam (putih), di tengah.
                creamMound.gameObject.SetActive(false);
                powderMoundB.gameObject.SetActive(false);
                powderMound.gameObject.SetActive(true);
                powderMound.transform.localPosition = Vector3.zero;
                ApplyColor(runtimePowderMaterial, asamWhite);
                SetRootScale(Mathf.Lerp(0.6f, fullScaleMultiplier, fill01));
                break;

            case SalepMortarPhase.PowderMix:
            {
                // Dua serbuk terpisah: Asam kiri (putih), Sulfur kanan (kuning).
                // fill01 = tingkat homogen (0 = baru ditambah/terpisah, 1 = menyatu).
                creamMound.gameObject.SetActive(false);
                powderMound.gameObject.SetActive(true);
                powderMoundB.gameObject.SetActive(true);

                float sep = Mathf.Lerp(splitOffset, 0f, fill01);
                powderMound.transform.localPosition = new Vector3(-sep, 0f, 0f);
                powderMoundB.transform.localPosition = new Vector3(sep, 0f, 0f);

                ApplyColor(runtimePowderMaterial, Color.Lerp(asamWhite, powderMixColor, fill01));
                ApplyColor(runtimePowderMaterialB, Color.Lerp(sulfurYellow, powderMixColor, fill01));
                SetRootScale(fullScaleMultiplier * 0.85f);
                break;
            }

            case SalepMortarPhase.PowdersHomogeneous:
                // Sudah menyatu: satu mound campuran di tengah.
                creamMound.gameObject.SetActive(false);
                powderMoundB.gameObject.SetActive(false);
                powderMound.gameObject.SetActive(true);
                powderMound.transform.localPosition = Vector3.zero;
                ApplyColor(runtimePowderMaterial, powderMixColor);
                SetRootScale(fullScaleMultiplier * 0.9f);
                break;

            case SalepMortarPhase.CreamAdded:
                // Cream di atas, serbuk campuran masih terlihat di bawah.
                powderMoundB.gameObject.SetActive(false);
                powderMound.gameObject.SetActive(true);
                powderMound.transform.localPosition = Vector3.zero;
                ApplyColor(runtimePowderMaterial, powderMixColor);
                creamMound.gameObject.SetActive(true);
                ApplyColor(runtimeCreamMaterial, Color.Lerp(asamWhite, salepIvory, 0.5f));
                SetRootScale(Mathf.Lerp(0.9f, fullScaleMultiplier, fill01));
                break;

            case SalepMortarPhase.SalepHomogeneous:
                // Salep jadi: cream ivory homogen.
                powderMound.gameObject.SetActive(false);
                powderMoundB.gameObject.SetActive(false);
                creamMound.gameObject.SetActive(true);
                ApplyColor(runtimeCreamMaterial, salepIvory);
                SetRootScale(Mathf.Lerp(0.9f, fullScaleMultiplier, fill01));
                break;
        }
    }

    private void SetRootScale(float multiplier)
    {
        visualRoot.localScale = Vector3.one * (baseScale * multiplier);
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
