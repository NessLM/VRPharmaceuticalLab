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
    // Kuning Sulfur lebih PEKAT supaya jelas terbaca kuning (bukan putih) walau kena
    // pencahayaan terang & efek faset miring.
    [SerializeField] private Color sulfurYellow = new Color(0.93f, 0.80f, 0.32f, 1f);
    [SerializeField] private Color powderMixColor = new Color(0.95f, 0.89f, 0.62f, 1f);
    // Salep 2-4 asli = krim KUNING PUCAT lembut (lihat referensi). Material krim UNLIT
    // jadi warna tampil persis seperti ini tanpa "blown out" putih → pucat pun tetap
    // terbaca sebagai krim kuning, bukan kosong.
    [SerializeField] private Color salepIvory = new Color(0.94f, 0.89f, 0.62f, 1f);
    // Vaselin Album saat baru dituang: krim KUNING HANGAT lebih pekat agar JELAS berbeda
    // dari serbuk putih di bawahnya. Saat diaduk, warnanya membaur ke salepIvory yang pucat.
    [SerializeField] private Color vaselinCream = new Color(0.95f, 0.82f, 0.50f, 1f);

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

    // --- Sisa salep menempel di mangkuk saat dikeruk pakai sudip ---
    // Model: cincin "smear" di pinggir (lengket di permukaan bowl) + 1 bagian tengah,
    // semuanya bertekstur. Saat dikeruk, potongan pinggir HILANG berurutan dari satu sisi
    // (bukan mengecil seragam), lalu bagian tengah terakhir menyusut habis.
    private Transform residueRoot;
    private Transform[] residueSegments;
    private Transform residueCenter;
    private Material residueMaterial;
    private const int ResidueSegmentCount = 8;

    // --- Reuse mesh tumpukan bubuk ASLI mortar (Bubuk_Level_01..03 di bawah
    // MortarPowderVisualRoot) supaya serbuk Salep berbentuk GUNDUKAN nyata, MENEMPEL dasar
    // mortar, dan tumbuh bertahap 3 level dari bawah — bukan disk melayang prosedural. ---
    private Transform nativePowderRoot;     // MortarPowderVisualRoot (child mortar)
    private Transform salepPowder;          // heap A: Asam (putih)
    private Material salepPowderMat;
    private Transform salepPowderB;         // heap B: Sulfur (kuning) — untuk tampilan 2 warna
    private Material salepPowderBMat;
    private Vector3 salepPowderBBaseScale = Vector3.one;
    // Heap C: KRIM Vaselin/salep. MEREUSE mesh & level native yang SAMA dengan serbuk supaya
    // ukuran & posisinya BENAR di dalam bowl (bukan kubah prosedural mungil yang melayang).
    // Permukaan HALUS (bukan granular) + warna krim → terbaca sebagai salep.
    private Transform salepCream;
    private Material salepCreamMat;
    private Vector3 salepCreamBaseScale = Vector3.one;
    private float heapMeshHalfX = 0.5f;     // setengah lebar mesh (untuk separasi dua heap)
    private Mesh nativeHeapMesh;
    private Mesh granularHeapMesh;          // salinan flat-shaded bergerigi (dipakai 2 heap)
    private Vector3[] heapLevelPos;         // 3 posisi lokal level (dari Bubuk_Level_01..03)
    private Vector3[] heapLevelScale;       // 3 skala lokal level
    private Vector3 salepPowderBaseScale = Vector3.one;
    // Faktor tinggi gundukan: sedikit ditinggikan agar terbaca menggunung (mesh asli ceper),
    // tapi tetap wajar.
    private const float HeapHeightBoost = 1.5f;
    // Faktor lebar = 1.0 → footprint SAMA PERSIS dengan visual bubuk Sirup (mesh & skala
    // level yang sama). Sebelumnya 0.62 membuatnya kekecilan dibanding Sirup.
    private const float HeapWidthFactor = 1.0f;
    // Pengecil tiap tumpukan saat mode dua-warna (Asam + Sulfur berdampingan) supaya
    // KEDUANYA muat di dalam mangkuk (1 tumpukan penuh = selebar bowl). 0.72 + separasi
    // kecil → dua tumpukan saling tumpang sedikit jadi SATU gundukan dua-warna yang besar.
    private const float TwoColorFit = 0.72f;

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

    // --- Model DUA TUMPUKAN TERPISAH (Asam putih KIRI + Sulfur kuning KANAN) ---
    // Tiap bahan tumbuh sendiri (level), warna sendiri, TIDAK menyusut saat bahan lain
    // ditambah, dan TIDAK membaur warna sampai DIGERUS (homogenitas naik).
    private float asamAmt01;     // 0..1 jumlah Asam yang sudah dituang
    private float sulfurAmt01;   // 0..1 jumlah Sulfur yang sudah dituang
    private float homog01;       // 0 = dua warna terpisah; 1 = homogen menyatu
    private float lastObservedHomog;

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

        heapMeshHalfX = nativeHeapMesh != null ? nativeHeapMesh.bounds.extents.x : 0.5f;
        // Mesh tumpukan asli HALUS → tampak cair. Pakai salinan flat-shaded bergerigi
        // agar TERBACA sebagai serbuk kering bergranula. Dipakai bersama oleh kedua heap.
        granularHeapMesh = BuildGranularHeapMesh(nativeHeapMesh);

        // Heap A: Asam (putih).
        GameObject go = new GameObject("SalepPowderHeap");
        go.transform.SetParent(nativePowderRoot, false);
        var mfp = go.AddComponent<MeshFilter>();
        mfp.sharedMesh = granularHeapMesh;
        var mrp = go.AddComponent<MeshRenderer>();
        // Material runtime sendiri (JANGAN sentuh material 'Difenhidramin' milik Sirup).
        // FULL MATTE tanpa emisi/gloss/tekstur → serbuk kering, bukan gumpalan cair.
        salepPowderMat = CreateMatteMaterial("Runtime_SalepPowderHeap", asamWhite);
        mrp.sharedMaterial = salepPowderMat;

        salepPowder = go.transform;
        salepPowder.localPosition = heapLevelPos[0];
        salepPowder.localScale = heapLevelScale[0];
        salepPowderBaseScale = heapLevelScale[0];
        go.SetActive(false);

        // Heap B: Sulfur (kuning) — untuk Step 6 dua warna (Asam putih + Sulfur kuning).
        GameObject goB = new GameObject("SalepPowderHeapB");
        goB.transform.SetParent(nativePowderRoot, false);
        var mfpB = goB.AddComponent<MeshFilter>();
        mfpB.sharedMesh = granularHeapMesh;
        var mrpB = goB.AddComponent<MeshRenderer>();
        salepPowderBMat = CreateMatteMaterial("Runtime_SalepPowderHeapB", sulfurYellow);
        mrpB.sharedMaterial = salepPowderBMat;

        salepPowderB = goB.transform;
        salepPowderB.localPosition = heapLevelPos[0];
        salepPowderB.localScale = heapLevelScale[0];
        salepPowderBBaseScale = heapLevelScale[0];
        goB.SetActive(false);
    }

    // Salinan mesh tumpukan dengan permukaan butiran: tiap vertex digeser sepanjang
    // normal sebesar noise stabil (hash) → siluet & permukaan bergerigi seperti serbuk,
    // bukan kubah cair yang mulus. Tidak mengubah mesh asli (clone).
    private Mesh BuildGranularHeapMesh(Mesh src)
    {
        if (src == null)
            return null;

        // 1) Displace tiap vertex ke luar pakai hash per-index (butiran tak beraturan).
        var srcVerts = src.vertices;
        var srcNormals = src.normals;
        if (srcNormals == null || srcNormals.Length != srcVerts.Length)
        {
            var tmp = Object.Instantiate(src);
            tmp.RecalculateNormals();
            srcNormals = tmp.normals;
            if (Application.isPlaying) Destroy(tmp); else DestroyImmediate(tmp);
        }

        var b = src.bounds;
        // Amplitudo butiran: cukup KASAR agar jelas terbaca sebagai BUBUK bergranula (bukan
        // kubah halus), tapi tetap DIBATASI (clamp di bawah) agar tidak menembus mangkuk.
        float amp = Mathf.Max(b.size.x, b.size.z) * 0.09f;

        var displaced = new Vector3[srcVerts.Length];
        for (int i = 0; i < srcVerts.Length; i++)
        {
            int h = (i * 374761393) ^ (i << 13) ^ (i >> 7);
            float r = ((h & 0x7fffffff) % 1000) / 1000f;
            int h2 = (i * 668265263) ^ (i << 9);
            float r2 = ((h2 & 0x7fffffff) % 1000) / 1000f;
            float disp = (r * 0.7f + r2 * 0.3f) * amp;
            Vector3 nrm = srcNormals != null && i < srcNormals.Length ? srcNormals[i] : Vector3.up;
            Vector3 d = srcVerts[i] + nrm * disp;

            // CLAMP CONTAINMENT — kunci agar butiran tidak menembus keluar mangkuk:
            // 1) Jangan turun di bawah dasar mesh (tidak menembus lantai bowl).
            d.y = Mathf.Max(d.y, srcVerts[i].y);
            // 2) Jangan melebar melebihi radius footprint asli (tidak menembus dinding).
            Vector2 origXZ = new Vector2(srcVerts[i].x, srcVerts[i].z);
            Vector2 newXZ = new Vector2(d.x, d.z);
            float origR = origXZ.magnitude;
            if (newXZ.magnitude > origR)
                newXZ = newXZ.normalized * origR;
            d.x = newXZ.x; d.z = newXZ.y;

            displaced[i] = d;
        }

        // 2) UNWELD jadi FLAT-SHADED: tiap segitiga punya 3 vertex sendiri + 1 normal bidang.
        //    Kunci agar tiap faset menangkap cahaya berbeda → butiran serbuk JELAS terlihat
        //    (shading halus meratakan bump sehingga tampak cair; flat-shading tidak).
        var tris = src.triangles;
        var nv = new Vector3[tris.Length];
        var nn = new Vector3[tris.Length];
        var nu = new Vector2[tris.Length];
        var nt = new int[tris.Length];
        for (int t = 0; t < tris.Length; t += 3)
        {
            Vector3 a = displaced[tris[t]];
            Vector3 bb = displaced[tris[t + 1]];
            Vector3 c = displaced[tris[t + 2]];
            Vector3 fn = Vector3.Cross(bb - a, c - a).normalized;
            nv[t] = a; nv[t + 1] = bb; nv[t + 2] = c;
            nn[t] = fn; nn[t + 1] = fn; nn[t + 2] = fn;
            nu[t] = new Vector2(0f, 0f); nu[t + 1] = new Vector2(1f, 0f); nu[t + 2] = new Vector2(0.5f, 1f);
            nt[t] = t; nt[t + 1] = t + 1; nt[t + 2] = t + 2;
        }

        Mesh m = new Mesh { name = "SalepGranularHeap" };
        if (nv.Length > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        m.vertices = nv;
        m.normals = nn;
        m.uv = nu;
        m.triangles = nt;
        m.RecalculateBounds();
        return m;
    }

    private int HeapLevelIndex(float amount01)
    {
        float a = Mathf.Clamp01(amount01);
        if (a < 0.34f) return 0;   // sedikit → pile kecil di dasar
        if (a < 0.7f) return 1;    // sedang
        return 2;                  // banyak → penuh
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

    // Tampilkan HANYA heap serbuk A (matikan mound prosedural lama + heap B).
    private void ShowHeapOnly()
    {
        if (powderMound != null) powderMound.gameObject.SetActive(false);
        if (powderMoundB != null) powderMoundB.gameObject.SetActive(false);
        if (salepPowderB != null) salepPowderB.gameObject.SetActive(false);
        if (salepPowder != null) salepPowder.gameObject.SetActive(true);
    }

    // ====== MODEL DUA TUMPUKAN TERPISAH ======
    // Asam (putih) di KIRI, Sulfur (kuning) di KANAN. Tiap tumpukan tumbuh sendiri per
    // LEVEL (4 tahap) sesuai jumlahnya. Warna TETAP (tidak membaur) sampai digerus; saat
    // homog naik 0→1 keduanya mendekat ke tengah, warna membaur, lalu MENYATU jadi satu
    // gundukan homogen di homog >= 0.9.
    private void RenderPiles(float asam01, float sulfur01, float homogeneity01)
    {
        creamMound.gameObject.SetActive(false);
        if (powderMound != null) powderMound.gameObject.SetActive(false);
        if (powderMoundB != null) powderMoundB.gameObject.SetActive(false);
        if (salepPowder == null || salepPowderB == null)
            return;

        float h = Mathf.Clamp01(homogeneity01);

        // Hampir/sudah homogen → satu gundukan penuh campuran (Sulfur disembunyikan).
        if (h >= 0.9f)
        {
            salepPowderB.gameObject.SetActive(false);
            salepPowder.gameObject.SetActive(true);
            ApplyColor(salepPowderMat, powderMixColor);
            GetLevelPose(1f, TwoColorFit, out Vector3 cpos, out Vector3 cscale);
            salepPowder.localPosition = cpos;
            salepPowder.localScale = cscale;
            salepPowderBaseScale = cscale;
            return;
        }

        // Separasi: penuh saat terpisah (homog 0) → 0 saat menyatu (homog 1).
        float sep = heapMeshHalfX * 0.34f * Mathf.Lerp(1f, 0f, h);

        // --- Tumpukan ASAM (putih → campuran saat digerus), KIRI ---
        bool hasAsam = asam01 > 0.001f;
        salepPowder.gameObject.SetActive(hasAsam);
        if (hasAsam)
        {
            ApplyColor(salepPowderMat, Color.Lerp(asamWhite, powderMixColor, h));
            GetLevelPose(asam01, TwoColorFit, out Vector3 pos, out Vector3 scale);
            salepPowder.localPosition = pos + new Vector3(-sep, 0f, 0f);
            salepPowder.localScale = scale;
            salepPowderBaseScale = scale;
        }

        // --- Tumpukan SULFUR (kuning → campuran saat digerus), KANAN ---
        bool hasSulfur = sulfur01 > 0.001f;
        salepPowderB.gameObject.SetActive(hasSulfur);
        if (hasSulfur)
        {
            ApplyColor(salepPowderBMat, Color.Lerp(sulfurYellow, powderMixColor, h));
            GetLevelPose(sulfur01, TwoColorFit, out Vector3 pos, out Vector3 scale);
            salepPowderB.localPosition = pos + new Vector3(sep, 0f, 0f);
            salepPowderB.localScale = scale;
            salepPowderBBaseScale = scale;
        }
    }

    // 4 LEVEL diskret (sedikit → banyak). Memakai 3 preset native + satu level terkecil
    // (preset0 diperkecil) → total 4 tahap pertumbuhan yang jelas terlihat.
    private static int PowderLevel4(float a)
    {
        a = Mathf.Clamp01(a);
        if (a < 0.25f) return 0;   // baru sedikit
        if (a < 0.5f) return 1;
        if (a < 0.75f) return 2;
        return 3;                  // penuh
    }

    // Pose (posisi + skala) untuk sebuah tumpukan pada jumlah tertentu, dikecilkan 'fit'
    // agar dua tumpukan muat berdampingan di mangkuk. Skala menempel dasar (mesh base y=0).
    private void GetLevelPose(float amount01, float fit, out Vector3 pos, out Vector3 scale)
    {
        switch (PowderLevel4(amount01))
        {
            case 0: pos = heapLevelPos[0]; scale = heapLevelScale[0] * (0.55f * fit); break;
            case 1: pos = heapLevelPos[0]; scale = heapLevelScale[0] * fit; break;
            case 2: pos = heapLevelPos[1]; scale = heapLevelScale[1] * fit; break;
            default: pos = heapLevelPos[2]; scale = heapLevelScale[2] * fit; break;
        }
    }

    private void EnsureResidue()
    {
        EnsureChildren();
        if (residueRoot != null)
            return;

        GameObject rootGo = new GameObject("SalepBowlResidue");
        residueRoot = rootGo.transform;
        residueRoot.SetParent(visualRoot, false);
        residueRoot.localPosition = Vector3.zero;
        residueRoot.localScale = Vector3.one;

        // Material krim MATTE solid (warna salep pucat, TANPA tekstur foto) supaya warnanya
        // SAMA dengan kubah salep → tidak ada perubahan warna mendadak saat mulai dikeruk.
        residueMaterial = CreateUnlitCreamMaterial("Runtime_SalepBowlResidue", salepIvory);

        residueSegments = new Transform[ResidueSegmentCount];
        const float ring = 0.30f;
        for (int i = 0; i < ResidueSegmentCount; i++)
        {
            float ang = (i / (float)ResidueSegmentCount) * Mathf.PI * 2f;
            GameObject seg = new GameObject("ResidueSeg_" + i);
            seg.AddComponent<MeshFilter>();
            seg.AddComponent<MeshRenderer>();
            Transform st = seg.transform;
            st.SetParent(residueRoot, false);
            st.localPosition = new Vector3(Mathf.Cos(ang) * ring, 0f, Mathf.Sin(ang) * ring);
            var sm = seg.AddComponent<SpoonPowderMoundVisual>();
            // Smear krim MENEMPEL di pinggir mangkuk: gundukan pipih halus (non-granular) warna
            // krim matte — natural, bukan tekstur kasar/foto.
            sm.Configure(0.14f, 0.11f, 0.015f, 0.06f, 0.01f, residueMaterial);
            residueSegments[i] = st;
            seg.SetActive(false);
        }

        GameObject center = new GameObject("ResidueCenter");
        center.AddComponent<MeshFilter>();
        center.AddComponent<MeshRenderer>();
        residueCenter = center.transform;
        residueCenter.SetParent(residueRoot, false);
        residueCenter.localPosition = Vector3.zero;
        var cm = center.AddComponent<SpoonPowderMoundVisual>();
        cm.Configure(0.22f, 0.20f, 0.03f, 0.12f, 0.02f, residueMaterial, true, 0.35f, 99);
        center.SetActive(false);

        residueRoot.gameObject.SetActive(false);
    }

    /// <summary>
    /// Tampilkan salep yang menempel di mangkuk lalu KIKIS sedikit demi sedikit saat
    /// dikeruk pakai sudip. scrapeProgress01: 0 = penuh (cincin pinggir + bagian tengah),
    /// 1 = habis. Potongan pinggir HILANG berurutan dari satu sisi (bukan mengecil seragam),
    /// bagian tengah bertahan paling akhir.
    /// </summary>
    public void SetScrapeResidue(float scrapeProgress01)
    {
        EnsureResidue();

        // Yang tampil HANYA residu mangkuk — sembunyikan dome/serbuk.
        if (creamMound != null) creamMound.gameObject.SetActive(false);
        if (salepPowder != null) salepPowder.gameObject.SetActive(false);
        if (salepPowderB != null) salepPowderB.gameObject.SetActive(false);
        if (powderMound != null) powderMound.gameObject.SetActive(false);
        if (powderMoundB != null) powderMoundB.gameObject.SetActive(false);

        phase = SalepMortarPhase.SalepHomogeneous;
        SetRootScale(CreamMaxScale);

        float p = Mathf.Clamp01(scrapeProgress01);
        float remain = 1f - p;
        bool anyLeft = remain > 0.04f;

        residueRoot.gameObject.SetActive(anyLeft);

        // Inti salep = LANJUTAN kubah krim yang sama (warna & material sama) yang MENYUSUT
        // halus seiring dikeruk → tidak ada lonjakan warna. ApplyPulse di Update memakai
        // creamMoundBaseScale sebagai skala.
        creamMound.gameObject.SetActive(anyLeft);
        if (anyLeft)
        {
            ApplyColor(runtimeCreamMaterial, salepIvory);
            creamMoundBaseScale = Mathf.Lerp(0.25f, 1f, remain);
            creamMound.localPosition = Vector3.zero;
        }

        // Sisa salep yang menempel di pinggir mangkuk: potongan HILANG berurutan dari satu
        // sisi (kesan dikeruk per bagian). Warna sama dengan inti (krim matte), bukan tekstur.
        int gone = Mathf.FloorToInt(p / 0.85f * ResidueSegmentCount);
        for (int i = 0; i < ResidueSegmentCount; i++)
            residueSegments[i].gameObject.SetActive(anyLeft && i >= gone);

        if (residueCenter != null)
            residueCenter.gameObject.SetActive(false);
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

    /// <summary>
    /// Set isi serbuk mortar sebagai DUA tumpukan terpisah yang tumbuh sendiri-sendiri.
    /// asam01/sulfur01 = jumlah masing-masing (0..1) → tiap tumpukan naik per LEVEL.
    /// homogeneity01 = tingkat gerusan: 0 = dua warna terpisah jelas; 1 = menyatu homogen.
    /// Dipakai Step 3 (Asam), Step 4-5 (Sulfur), Step 6 (gerus).
    /// </summary>
    public void SetPowderPiles(float asam01, float sulfur01, float homogeneity01)
    {
        EnsureChildren();

        float h = Mathf.Clamp01(homogeneity01);
        // Saat homogenitas bertambah → user sedang menggerus: picu efek aduk + pulse.
        if (h > lastObservedHomog + 0.0005f)
        {
            lastMixTime = Time.time;
            EmitMixPuff();
        }
        lastObservedHomog = h;

        asamAmt01 = Mathf.Clamp01(asam01);
        sulfurAmt01 = Mathf.Clamp01(sulfur01);
        homog01 = h;
        phase = SalepMortarPhase.PowderMix; // ditangani RenderPiles via case PowderMix
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
        ApplyPulseVec(salepPowderB, salepPowderBBaseScale, pulse);
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
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.75f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.04f, 0.12f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.006f, 0.014f);
        main.gravityModifier = -0.015f;
        main.maxParticles = 140;
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

        // Residu mangkuk hanya tampil lewat SetScrapeResidue (bypass Refresh). Fase normal
        // apa pun menyembunyikannya supaya tidak tertinggal.
        if (residueRoot != null)
            residueRoot.gameObject.SetActive(false);

        switch (phase)
        {
            case SalepMortarPhase.Empty:
                if (salepPowder != null) salepPowder.gameObject.SetActive(false);
                if (powderMound != null) powderMound.gameObject.SetActive(false);
                if (powderMoundB != null) powderMoundB.gameObject.SetActive(false);
                creamMound.gameObject.SetActive(false);
                break;

            case SalepMortarPhase.AsamPowder:
            case SalepMortarPhase.PowderMix:
                // DUA tumpukan terpisah & tumbuh sendiri: Asam (putih, KIRI) + Sulfur
                // (kuning, KANAN). Tiap bahan naik per LEVEL sesuai jumlahnya, warna tetap
                // (tidak membaur) sampai DIGERUS (homog01 naik) → baru menyatu jadi homogen.
                creamMound.gameObject.SetActive(false);
                RenderPiles(asamAmt01, sulfurAmt01, homog01);
                break;

            case SalepMortarPhase.PowdersHomogeneous:
                // Campuran homogen: satu gundukan penuh berwarna campuran.
                creamMound.gameObject.SetActive(false);
                ShowHeapOnly();
                ApplyColor(salepPowderMat, powderMixColor);
                ApplyHeapLevel(1f);
                break;

            case SalepMortarPhase.CreamAdded:
            {
                // Step 7-8: Vaselin masuk lalu DIADUK → 4 Transisi MULUS menuju salep smooth.
                // mix (fill01)   = tingkat pengadukan: 0 bergumpal → 1 smooth.
                // creamAmt (sizeAmount01) = jumlah krim Vaselin yang dituang (0..1).
                float mix = fill01;
                float creamAmt = sizeAmount01;

                if (powderMound != null) powderMound.gameObject.SetActive(false);
                if (powderMoundB != null) powderMoundB.gameObject.SetActive(false);
                if (salepPowderB != null) salepPowderB.gameObject.SetActive(false);

                // Kubah krim utama: kuning hangat (Vaselin baru) → pucat (salep homogen)
                // seiring diaduk, supaya BEDA warnanya dari serbuk putih di bawah.
                creamMound.gameObject.SetActive(true);
                ApplyColor(runtimeCreamMaterial, Color.Lerp(vaselinCream, salepIvory, mix));
                SetRootScale(fullScaleMultiplier);

                // --- 4 TRANSISI BERTAHAP (VISUAL SMOOTH BLEND): ---
                if (mix < 0.25f)
                {
                    // TAHAP 1: Baru Ditambah (mix < 25%).
                    // Krim sudah JELAS TERLIHAT menyelimuti serbuk (floor dinaikkan agar batch 1
                    // pun terbaca, tidak cuma titik kecil), bubuk kuning-putih di bawah masih utuh.
                    creamMoundBaseScale = Mathf.Lerp(0.7f, 0.85f, mix / 0.25f);
                    
                    if (salepPowder != null)
                    {
                        salepPowder.gameObject.SetActive(true);
                        ApplyColor(salepPowderMat, powderMixColor);
                        int idx = HeapLevelIndex(1f);
                        salepPowder.localPosition = heapLevelPos[idx];
                        salepPowderBaseScale = heapLevelScale[idx];
                        salepPowder.localScale = salepPowderBaseScale;
                    }
                }
                else if (mix < 0.50f)
                {
                    // TAHAP 2: Mulai Terlarut (25% - 50%).
                    // Krim membesar melingkari bubuk, bubuk mulai menyusut & berubah warna memucat.
                    float t = (mix - 0.25f) / 0.25f; // 0..1
                    creamMoundBaseScale = Mathf.Lerp(0.6f, 0.8f, t);

                    if (salepPowder != null)
                    {
                        salepPowder.gameObject.SetActive(true);
                        // Bubuk mulai melarut: warna memucat ke arah ivory salep
                        ApplyColor(salepPowderMat, Color.Lerp(powderMixColor, salepIvory, 0.35f * t));
                        int idx = HeapLevelIndex(1f);
                        salepPowder.localPosition = heapLevelPos[idx];
                        // Menyusut 15%
                        float shrink = Mathf.Lerp(1.0f, 0.85f, t);
                        salepPowderBaseScale = heapLevelScale[idx] * shrink;
                        salepPowder.localScale = salepPowderBaseScale;
                    }
                }
                else if (mix < 0.75f)
                {
                    // TAHAP 3: Setengah Larut (50% - 75%).
                    // Krim menyelimuti hampir seluruh bagian, bubuk menyusut drastis & memucat penuh (jejak).
                    float t = (mix - 0.50f) / 0.25f; // 0..1
                    creamMoundBaseScale = Mathf.Lerp(0.8f, 0.95f, t);

                    if (salepPowder != null)
                    {
                        salepPowder.gameObject.SetActive(true);
                        // Bubuk hampir seluruhnya menyatu: warna didominasi ivory salep
                        ApplyColor(salepPowderMat, Color.Lerp(Color.Lerp(powderMixColor, salepIvory, 0.35f), salepIvory, 0.8f * t));
                        int idx = HeapLevelIndex(1f);
                        salepPowder.localPosition = heapLevelPos[idx];
                        // Menyusut drastis ke 55%
                        float shrink = Mathf.Lerp(0.85f, 0.55f, t);
                        salepPowderBaseScale = heapLevelScale[idx] * shrink;
                        salepPowder.localScale = salepPowderBaseScale;
                    }
                }
                else
                {
                    // TAHAP 4: Homogen (75% - 100%).
                    // Bubuk sepenuhnya larut dan hilang, menyisakan salep smooth penuh di permukaan.
                    float t = (mix - 0.75f) / 0.25f; // 0..1
                    creamMoundBaseScale = Mathf.Lerp(0.95f, 1.0f, t);

                    if (salepPowder != null)
                        salepPowder.gameObject.SetActive(false);
                }

                // PENTING (bug "baru muncul pas Batch 2"): selama menuang Vaselin (mix masih
                // rendah), ukuran kubah krim mengikuti JUMLAH yang sudah dituang (creamAmt).
                // Min 0.6 supaya batch 1 SUDAH JELAS TERLIHAT (bukan titik kecil), tumbuh ke
                // 1.0 di batch 2. Saat sudah diaduk creamAmt=1 → tahap aduk tak terpengaruh.
                creamMoundBaseScale *= Mathf.Lerp(0.78f, 1f, creamAmt);

                // Krim duduk di pusat mangkuk (menyelimuti serbuk). CATATAN: jangan pakai
                // koordinat heap serbuk di sini — heap berada di root berbeda (nativePowderRoot)
                // sehingga mencampur ruang koordinat malah membuat krim tenggelam/menghilang.
                creamMound.localPosition = Vector3.zero;
                break;
            }

            case SalepMortarPhase.SalepHomogeneous:
            {
                // Salep JADI: kubah krim SMOOTH pucat. fill01 dipakai sebagai JUMLAH salep di
                // mortar (0 = kosong). Ini membuat mortar bisa benar-benar KOSONG (mis. setelah
                // salep dipindah ke pot / saat step lanjut) — bukan selalu dome penuh.
                if (salepPowder != null) salepPowder.gameObject.SetActive(false);
                if (salepPowderB != null) salepPowderB.gameObject.SetActive(false);
                if (powderMound != null) powderMound.gameObject.SetActive(false);
                if (powderMoundB != null) powderMoundB.gameObject.SetActive(false);

                float amt = Mathf.Clamp01(fill01);
                // Ambang 0.08: nilai sentinel "kosong/selesai" (0.05) terbaca sebagai KOSONG,
                // sehingga mortar benar-benar bersih saat step selesai / idle.
                bool show = amt > 0.08f;
                creamMound.gameObject.SetActive(show);
                if (show)
                {
                    ApplyColor(runtimeCreamMaterial, salepIvory);
                    SetRootScale(CreamMaxScale);
                    creamMoundBaseScale = amt;
                    creamMound.localPosition = Vector3.zero;
                }
                break;
            }
        }
    }

    private void SetRootScale(float multiplier)
    {
        visualRoot.localScale = Vector3.one * (baseScale * multiplier);
    }

    // Rekonfigurasi bentuk serbuk hanya saat mode berubah(hindari rebuild mesh tiap frame).
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

    // Material bubuk kering: matte total (smoothness 0, metallic 0), TANPA emisi, TANPA
    // specular highlight → terlihat sebagai serbuk, bukan gumpalan cair mengilap.
    private Material CreateMatteMaterial(string materialName, Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        if (shader == null) return null;

        Material material = new Material(shader) { name = materialName };
        ApplyColor(material, color);
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0f);
        if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", 0f);
        if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0f);
        if (material.HasProperty("_SpecularHighlights")) material.SetFloat("_SpecularHighlights", 0f);
        if (material.HasProperty("_EnvironmentReflections")) material.SetFloat("_EnvironmentReflections", 0f);
        material.DisableKeyword("_EMISSION");
        material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
        return material;
    }

    // Material krim/salep SEDERHANA (sengaja TIDAK realistis sesuai permintaan): matte
    // total, tanpa gloss, tanpa emisi, tanpa tekstur foto krim. Warna solid lembut →
    // tampak seperti salep "kartun/simpel", bukan krim foto-realistis.
    private Material CreateUnlitCreamMaterial(string materialName, Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        if (shader == null) return null;

        Material material = new Material(shader) { name = materialName };
        ApplyColor(material, color);

        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0f);
        if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", 0f);
        if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0f);
        if (material.HasProperty("_SpecularHighlights")) material.SetFloat("_SpecularHighlights", 0f);
        if (material.HasProperty("_EnvironmentReflections")) material.SetFloat("_EnvironmentReflections", 0f);
        material.DisableKeyword("_EMISSION");
        material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
        // Tidak memakai tekstur krim → warna solid sederhana (kurang realistis, sesuai mau).
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
