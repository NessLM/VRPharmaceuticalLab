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
    // Maks skala dikecilkan dari 1.9 → 1.2 supaya gundukan terbesar JELAS muat di
    // dalam bowl mortar (tidak overflow/kelewat tepi). Orchestrator boleh fine-tune.
    [SerializeField] private float fullScaleMultiplier = 1.2f;

    [Header("Pemisahan dua serbuk (Asam kiri, Sulfur kanan)")]
    [Tooltip("Jarak pisah kedua mound serbuk (local visualRoot units) saat belum homogen.")]
    [SerializeField] private float splitOffset = 0.27f;

    [Header("Warna per fase")]
    [SerializeField] private Color asamWhite = new Color(0.96f, 0.965f, 0.95f, 1f);
    [SerializeField] private Color sulfurYellow = new Color(0.98f, 0.92f, 0.55f, 1f);
    [SerializeField] private Color powderMixColor = new Color(0.97f, 0.94f, 0.72f, 1f);
    // Salep 2-4 asli = krim KUNING PUCAT lembut (lihat referensi). Material krim UNLIT
    // jadi warna tampil persis seperti ini tanpa "blown out" putih → pucat pun tetap
    // terbaca sebagai krim kuning, bukan kosong.
    [SerializeField] private Color salepIvory = new Color(0.94f, 0.89f, 0.62f, 1f);

    [Header("Mesh bubuk (butiran)")]
    [Tooltip("Skala mound saat memakai mesh granul asli proyek (Pile_03_M_Granules).")]
    [SerializeField] private float granuleMoundScale = 0.34f;

    private Transform visualRoot;
    private Transform powderMound;    // mound A: Asam (putih)
    private Transform powderMoundB;   // mound B: Sulfur (kuning)
    private SpoonPowderMoundVisual powderShapeA;
    private SpoonPowderMoundVisual powderShapeB;
    private float powderMoundBaseScale = 1f;
    private float creamMoundBaseScale = 1f;   // ukuran kubah krim (tumbuh saat Vaselin dituang)
    private Transform creamMound;
    private Material runtimePowderMaterial;
    private Material runtimePowderMaterialB;
    private Material runtimeCreamMaterial;
    private Mesh granuleMesh;

    // --- Reuse mesh tumpukan bubuk ASLI mortar (Bubuk_Level_01..03 di bawah
    // MortarPowderVisualRoot) supaya serbuk Salep berbentuk GUNDUKAN nyata, MENEMPEL dasar
    // mortar, dan tumbuh bertahap 3 level dari bawah — bukan disk melayang prosedural. ---
    private Transform nativePowderRoot;     // MortarPowderVisualRoot (child mortar)
    private Transform salepPowder;          // objek heap serbuk Salep (pakai mesh asli)
    private Material salepPowderMat;
    private Mesh nativeHeapMesh;
    private Vector3[] heapLevelPos;         // 3 posisi lokal level (dari Bubuk_Level_01..03)
    private Vector3[] heapLevelScale;       // 3 skala lokal level
    private Vector3 salepPowderBaseScale = Vector3.one;

    // Bentuk geometri serbuk saat ini: penuh (satu kubah) atau dua setengah split warna.
    private enum PowderGeo { None, FullSingle, SplitHalves }
    private PowderGeo powderGeo = PowderGeo.None;

    // Parameter kubah serbuk (dipakai untuk kubah penuh & dua setengah split).
    private const float PowderRadiusX = 0.42f;
    private const float PowderRadiusZ = 0.38f;
    private const float PowderBaseH = 0.05f;
    private const float PowderMoundH = 0.34f;
    private const float PowderNoise = 0.012f;
    private const float PowderGrain = 0.28f;

    private SalepMortarPhase phase = SalepMortarPhase.Empty;
    private float fill01;
    private float sizeAmount01 = 1f;

    // Skala kubah krim/salep — disamakan dengan serbuk (fullScaleMultiplier) agar salep
    // benar-benar MENGGUNUNG & terlihat di dalam mortar (skala kecil membuatnya tenggelam
    // di dasar bowl). Aman dari "blown out" putih karena material krim kini UNLIT.
    private const float CreamMaxScale = 1.2f;

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
            powderShapeA = powderMound.GetComponent<SpoonPowderMoundVisual>();
        }

        if (powderMoundB == null)
        {
            powderMoundB = BuildPowderMound("MortarPowderMoundB", sulfurYellow, out runtimePowderMaterialB, "Runtime_SalepMortarPowderB");
            powderShapeB = powderMound != null ? powderMoundB.GetComponent<SpoonPowderMoundVisual>() : null;
        }

        if (creamMound == null)
        {
            GameObject creamObject = new GameObject("MortarCreamMound");
            creamObject.transform.SetParent(visualRoot, false);
            creamObject.AddComponent<MeshFilter>();
            var creamMeshRenderer = creamObject.AddComponent<MeshRenderer>();
            creamMound = creamObject.transform;
            // Krim/salep memakai material UNLIT: warnanya terkunci & TIDAK "blown out" putih
            // oleh lampu scene yang sangat terang (penyebab salep tampak kosong/putih).
            runtimeCreamMaterial = CreateUnlitCreamMaterial("Runtime_SalepMortarCream", salepIvory);
            creamMeshRenderer.sharedMaterial = runtimeCreamMaterial;
            // Pakai BUILDER GUNDUKAN yang SAMA dengan serbuk (SpoonPowderMoundVisual) — bentuk
            // tinggi & menggunung yang TERBUKTI terlihat di mortar (CreamMoundVisual lama terlalu
            // datar → tenggelam di dasar bowl → tampak kosong). Material krim unlit + tekstur.
            var creamShape = creamObject.AddComponent<SpoonPowderMoundVisual>();
            creamShape.Configure(0.42f, 0.38f, 0.05f, 0.34f, 0.012f, runtimeCreamMaterial);
            creamObject.SetActive(false);
        }

        EnsureNativeHeap();
    }

    // Bangun objek heap serbuk Salep yang MEREUSE mesh tumpukan bubuk asli mortar
    // (MortarPowderMound_Generated) + menangkap 3 preset level (posisi & skala) dari
    // Bubuk_Level_01..03. Hasilnya: gundukan bubuk nyata yang menempel dasar mortar dan
    // tumbuh bertahap dari bawah, sama persis seperti visual bubuk Sirup yang sudah benar.
    private void EnsureNativeHeap()
    {
        if (salepPowder != null)
            return;

        if (nativePowderRoot == null)
        {
            foreach (Transform t in transform.GetComponentsInChildren<Transform>(true))
                if (t.name == "MortarPowderVisualRoot") { nativePowderRoot = t; break; }
        }
        if (nativePowderRoot == null)
            return;

        heapLevelPos = new Vector3[3];
        heapLevelScale = new Vector3[3];
        string[] names = { "Bubuk_Level_01", "Bubuk_Level_02", "Bubuk_Level_03" };
        for (int i = 0; i < 3; i++)
        {
            Transform lv = null;
            foreach (Transform c in nativePowderRoot)
                if (c.name == names[i]) { lv = c; break; }

            if (lv != null)
            {
                heapLevelPos[i] = lv.localPosition;
                heapLevelScale[i] = lv.localScale;
                if (nativeHeapMesh == null)
                {
                    var mf = lv.GetComponent<MeshFilter>();
                    if (mf != null) nativeHeapMesh = mf.sharedMesh;
                }
            }
            else
            {
                // Fallback sesuai data terukur bila objek level tidak ketemu.
                heapLevelPos[i] = new Vector3(0f, i == 2 ? -0.93f : -1.12f, 0f);
                heapLevelScale[i] = i == 0 ? Vector3.one * 0.5f
                    : i == 1 ? Vector3.one * 0.8f
                    : new Vector3(1f, 1.456f, 1f);
            }
        }

        GameObject go = new GameObject("SalepPowderHeap");
        go.transform.SetParent(nativePowderRoot, false);
        var mfp = go.AddComponent<MeshFilter>();
        mfp.sharedMesh = nativeHeapMesh;
        var mrp = go.AddComponent<MeshRenderer>();
        // Material runtime sendiri (JANGAN sentuh material 'Difenhidramin' milik Sirup).
        salepPowderMat = CreateMaterial("Runtime_SalepPowderHeap", asamWhite, 0.06f, SurfaceTex.Powder);
        ApplyEmission(salepPowderMat, asamWhite * 0.18f);
        mrp.sharedMaterial = salepPowderMat;

        salepPowder = go.transform;
        salepPowder.localPosition = heapLevelPos[0];
        salepPowder.localScale = heapLevelScale[0];
        salepPowderBaseScale = heapLevelScale[0];
        go.SetActive(false);
    }

    private int HeapLevelIndex(float amount01)
    {
        float a = Mathf.Clamp01(amount01);
        if (a < 0.2f) return 0;
        if (a < 0.55f) return 1;
        return 2;
    }

    // Pilih preset level (3 tahap) berbasis jumlah → gundukan naik bertahap dari dasar.
    private void ApplyHeapLevel(float amount01)
    {
        if (salepPowder == null || heapLevelPos == null)
            return;
        int idx = HeapLevelIndex(amount01);
        salepPowder.localPosition = heapLevelPos[idx];
        salepPowderBaseScale = heapLevelScale[idx];
        salepPowder.localScale = heapLevelScale[idx];
    }

    // Tampilkan HANYA heap serbuk Salep (matikan mound prosedural lama).
    private void ShowHeapOnly()
    {
        if (powderMound != null) powderMound.gameObject.SetActive(false);
        if (powderMoundB != null) powderMoundB.gameObject.SetActive(false);
        if (salepPowder != null) salepPowder.gameObject.SetActive(true);
    }

    private Transform BuildPowderMound(string objectName, Color color, out Material material, string materialName)
    {
        GameObject powderObject = new GameObject(objectName);
        powderObject.transform.SetParent(visualRoot, false);
        MeshFilter meshFilter = powderObject.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = powderObject.AddComponent<MeshRenderer>();
        // Matte + tekstur serbuk halus → permukaan terbaca sebagai BUBUK (bukan pil mulus,
        // bukan butiran/partikel/kubus). Bentuk gundukan besar & membulat sesuai selera.
        material = CreateMaterial(materialName, color, 0.06f, SurfaceTex.Powder);
        // Emisi sangat lembut mengikuti warna serbuk → tetap terbaca (putih/kuning) tanpa
        // mudah "blown out" putih oleh pencahayaan terang. Lebih halus daripada krim.
        ApplyEmission(material, color * 0.22f);
        meshRenderer.sharedMaterial = material;

        // Gundukan halus (dome) — tekstur yang membuatnya tampak seperti serbuk, bukan pil.
        SpoonPowderMoundVisual mound = powderObject.AddComponent<SpoonPowderMoundVisual>();
        mound.Configure(0.42f, 0.38f, 0.05f, 0.34f, 0.012f, material);
        powderMoundBaseScale = 1f;

        powderObject.transform.localScale = Vector3.one * powderMoundBaseScale;
        powderObject.SetActive(false);
        return powderObject.transform;
    }

    /// <summary>
    /// Beri mesh granul asli proyek (mis. Pile_03_M_Granules dari plate timbangan) supaya
    /// mound bubuk mortar tampak seperti bubuk butiran, bukan bola halus. Dipanggil
    /// SalepBench saat bind. Aman dipanggil sebelum/sesudah mound dibuat.
    /// </summary>
    public void ConfigureGranuleSource(Mesh mesh)
    {
        // No-op disengaja: mound bubuk mortar sekarang dibangun granular secara prosedural
        // (lihat BuildPowderMound). Mesh smooth dari plate timbangan TIDAK lagi dipakai
        // supaya isi mortar tampak seperti bubuk butiran, bukan kubah/bola halus.
        granuleMesh = mesh;
    }

    public void SetPhase(SalepMortarPhase newPhase, float newFill01)
    {
        SetPhase(newPhase, newFill01, 1f);
    }

    /// <summary>
    /// fill01 = makna per-fase (homogenitas untuk PowderMix, jumlah krim untuk CreamAdded).
    /// amount01 = UKURAN gundukan keseluruhan (0 kecil → 1 penuh). Dipakai agar isi mortar
    /// tumbuh dari sedikit ke banyak saat dituang berkali-kali.
    /// </summary>
    public void SetPhase(SalepMortarPhase newPhase, float newFill01, float newAmount01)
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
        sizeAmount01 = Mathf.Clamp01(newAmount01);
        Refresh();
    }

    private void Update()
    {
        // Pulsa kecil pada mound aktif saat user sedang menggerus, biar terasa "teraduk".
        // Saat tidak mengaduk, pulse = 1 (skala kembali normal).
        float pulse = MixingActive
            ? 1f + Mathf.Sin(Time.time * mixPulseSpeed) * mixPulseAmount
            : 1f;
        // Heap serbuk asli: pulse XZ saja (jaga tinggi & skala non-uniform level).
        ApplyPulseVec(salepPowder, salepPowderBaseScale, pulse);
        // Krim memakai skala basis yang tumbuh (creamMoundBaseScale) → kubah krim membesar
        // bertahap tanpa mengubah ukuran serbuk di bawahnya.
        ApplyPulse(creamMound, creamMoundBaseScale, pulse);
    }

    private static void ApplyPulse(Transform mound, float baseScale, float pulse)
    {
        if (mound == null || !mound.gameObject.activeSelf)
            return;
        // Pulse hanya pada XZ (jaga tinggi), dikali skala dasar mound.
        mound.localScale = new Vector3(baseScale * pulse, baseScale, baseScale * pulse);
    }

    // Versi untuk skala dasar non-uniform (heap level: X/Y/Z berbeda). Pulse XZ, jaga Y.
    private static void ApplyPulseVec(Transform mound, Vector3 baseScale, float pulse)
    {
        if (mound == null || !mound.gameObject.activeSelf)
            return;
        mound.localScale = new Vector3(baseScale.x * pulse, baseScale.y, baseScale.z * pulse);
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
                if (salepPowder != null) salepPowder.gameObject.SetActive(false);
                if (powderMound != null) powderMound.gameObject.SetActive(false);
                if (powderMoundB != null) powderMoundB.gameObject.SetActive(false);
                creamMound.gameObject.SetActive(false);
                break;

            case SalepMortarPhase.AsamPowder:
                // Asam (putih). Gundukan NYATA menempel dasar mortar (mesh asli), tumbuh
                // bertahap 3 level dari bawah sesuai sizeAmount01 (jumlah dituang).
                creamMound.gameObject.SetActive(false);
                ShowHeapOnly();
                ApplyColor(salepPowderMat, asamWhite);
                ApplyHeapLevel(sizeAmount01);
                break;

            case SalepMortarPhase.PowderMix:
                // Asam putih + Sulfur kuning dalam satu gundukan. Sulfur lebih banyak
                // (400 vs 200 mg) → condong kuning; saat menggerus (fill01→1) membaur ke
                // warna campuran. Gundukan tetap menempel dasar & bertingkat.
                creamMound.gameObject.SetActive(false);
                ShowHeapOnly();
                ApplyColor(salepPowderMat, Color.Lerp(Color.Lerp(asamWhite, sulfurYellow, 0.6f), powderMixColor, fill01));
                ApplyHeapLevel(sizeAmount01);
                break;

            case SalepMortarPhase.PowdersHomogeneous:
                // Campuran homogen: gundukan penuh berwarna campuran.
                creamMound.gameObject.SetActive(false);
                ShowHeapOnly();
                ApplyColor(salepPowderMat, powderMixColor);
                ApplyHeapLevel(1f);
                break;

            case SalepMortarPhase.CreamAdded:
                // Krim Vaselin DI ATAS gundukan serbuk yang sudah penuh. Serbuk tetap penuh
                // (menempel dasar), HANYA kubah krim yang tumbuh bertahap mengikuti Vaselin.
                ShowHeapOnly();
                ApplyColor(salepPowderMat, powderMixColor);
                ApplyHeapLevel(1f);
                creamMound.gameObject.SetActive(true);
                ApplyColor(runtimeCreamMaterial, salepIvory);
                SetRootScale(fullScaleMultiplier);
                creamMoundBaseScale = Mathf.Lerp(0.3f, 1f, sizeAmount01);
                break;

            case SalepMortarPhase.SalepHomogeneous:
                // Salep jadi: krim kuning homogen penuh; serbuk disembunyikan.
                if (salepPowder != null) salepPowder.gameObject.SetActive(false);
                if (powderMound != null) powderMound.gameObject.SetActive(false);
                if (powderMoundB != null) powderMoundB.gameObject.SetActive(false);
                creamMound.gameObject.SetActive(true);
                ApplyColor(runtimeCreamMaterial, salepIvory);
                SetRootScale(CreamMaxScale);
                creamMoundBaseScale = 1f;
                break;
        }
    }

    private void SetRootScale(float multiplier)
    {
        visualRoot.localScale = Vector3.one * (baseScale * multiplier);
    }

    // Rekonfigurasi bentuk serbuk hanya saat mode berubah (hindari rebuild mesh tiap frame).
    // FullSingle = satu kubah penuh (mound A). SplitHalves = dua setengah kubah: A [0..180]
    // (kiri, Asam putih) + B [180..360] (kanan, Sulfur kuning), keduanya di pusat → satu
    // gundukan dome yang dibagi setengah-setengah berdasarkan warna.
    private void SetPowderGeometry(PowderGeo mode)
    {
        if (powderGeo == mode)
            return;
        powderGeo = mode;
        if (powderShapeA == null || powderShapeB == null)
            return;

        if (mode == PowderGeo.SplitHalves)
        {
            powderShapeA.Configure(PowderRadiusX, PowderRadiusZ, PowderBaseH, PowderMoundH, PowderNoise,
                runtimePowderMaterial, true, PowderGrain, 1337, 0f, 180f);
            powderShapeB.Configure(PowderRadiusX, PowderRadiusZ, PowderBaseH, PowderMoundH, PowderNoise,
                runtimePowderMaterialB, true, PowderGrain, 911, 180f, 360f);
        }
        else // FullSingle: mound A jadi kubah penuh
        {
            powderShapeA.Configure(PowderRadiusX, PowderRadiusZ, PowderBaseH, PowderMoundH, PowderNoise,
                runtimePowderMaterial, true, PowderGrain, 1337, 0f, 360f);
        }
    }

    // Kuantisasi jumlah (0..1) ke MIN 3 level ukuran diskret agar tumpukan tumbuh
    // bertahap (sedikit → banyak) saat bahan dituang berulang, bukan mulus terus.
    // Level 1 ≈ 0.5, Level 2 ≈ 0.75, Level 3 ≈ 1.0 dari skala maksimum.
    private static float QuantizeSizeScale(float amount01, float maxScale)
    {
        float a = Mathf.Clamp01(amount01);
        float level;
        if (a < 0.33f) level = 0.5f;
        else if (a < 0.66f) level = 0.75f;
        else level = 1f;
        return maxScale * level;
    }

    // Tekstur serbuk & krim hasil generate (Resources/SalepTex). Dipakai sebagai base map
    // agar permukaan tampak seperti bubuk/krim asli (bukan pil mulus / kubus / partikel).
    private static Texture2D _powderTex;
    private static Texture2D _creamTex;
    private static bool _texLoaded;

    private static void EnsureTextures()
    {
        if (_texLoaded)
            return;
        _powderTex = Resources.Load<Texture2D>("SalepTex/powder_fine");
        _creamTex = Resources.Load<Texture2D>("SalepTex/cream_surface");
        _texLoaded = true;
    }

    private enum SurfaceTex { None, Powder, Cream }

    private Material CreateMaterial(string materialName, Color color, float smoothness)
    {
        return CreateMaterial(materialName, color, smoothness, SurfaceTex.None);
    }

    private Material CreateMaterial(string materialName, Color color, float smoothness, SurfaceTex tex)
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

        EnsureTextures();
        Texture2D map = tex == SurfaceTex.Powder ? _powderTex : (tex == SurfaceTex.Cream ? _creamTex : null);
        if (map != null)
        {
            // Tile rendah (≈1) supaya tekstur TIDAK berulang jadi pola kotak/waffle.
            // Satu peta menutup mound → tampak halus seperti bubuk/krim asli.
            float tile = tex == SurfaceTex.Powder ? 1.15f : 1f;
            if (material.HasProperty("_BaseMap"))
            {
                material.SetTexture("_BaseMap", map);
                material.SetTextureScale("_BaseMap", new Vector2(tile, tile));
            }
            if (material.HasProperty("_MainTex"))
            {
                material.SetTexture("_MainTex", map);
                material.SetTextureScale("_MainTex", new Vector2(tile, tile));
            }
        }
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

    // Material UNLIT untuk krim/salep: warna terkunci, tidak terpengaruh pencahayaan terang
    // (anti "blown out" putih). Tekstur krim tetap dipakai untuk detail permukaan.
    private Material CreateUnlitCreamMaterial(string materialName, Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) return null;

        Material material = new Material(shader) { name = materialName };
        ApplyColor(material, color);

        EnsureTextures();
        if (_creamTex != null)
        {
            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", _creamTex);
            if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", _creamTex);
        }
        return material;
    }

    private static void ApplyEmission(Material material, Color emission)
    {
        if (material == null)
            return;
        material.EnableKeyword("_EMISSION");
        material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        if (material.HasProperty("_EmissionColor"))
            material.SetColor("_EmissionColor", emission);
    }
}
