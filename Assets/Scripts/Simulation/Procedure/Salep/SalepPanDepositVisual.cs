using UnityEngine;

/// <summary>
/// Visual bahan di atas piring timbangan untuk workflow Salep.
/// Self-building: membuat mound powder (SpoonPowderMoundVisual) dan cream (CreamMoundVisual)
/// sendiri, lalu menumbuhkannya sesuai jumlah yang sudah ditimbang (mg) terhadap target.
///
/// Powder dan cream memakai visual berbeda:
///   - powder: mound granular rendah
///   - cream : mound smooth, lebih tinggi &amp; menggunung
/// </summary>
[DisallowMultipleComponent]
public sealed class SalepPanDepositVisual : MonoBehaviour
{
    [Header("Penempatan di atas pan (local)")]
    [SerializeField] private Vector3 localOffset = new Vector3(0f, 0.005f, 0f);

    [Header("Skala mound")]
    [Tooltip("Skala dasar mound powder saat target tercapai.")]
    [SerializeField] private float powderFullScale = 1.1f;
    [Tooltip("Skala dasar mound cream saat target tercapai (lebih besar dari powder).")]
    [SerializeField] private float creamFullScale = 1.7f;
    [SerializeField] private float minScale = 0.28f;

    private Transform moundRoot;
    private SpoonPowderMoundVisual powderMound;
    private CreamMoundVisual creamMound;

    private bool isCream;
    private float targetMg = 200f;
    private float fullScale = 1.1f;

    private void EnsureChildren()
    {
        if (moundRoot == null)
        {
            GameObject root = new GameObject("SalepPanMoundRoot");
            moundRoot = root.transform;
            moundRoot.SetParent(transform, false);
            moundRoot.localPosition = localOffset;
            moundRoot.localRotation = Quaternion.identity;
        }

        if (powderMound == null)
        {
            GameObject powderObject = new GameObject("PanPowderMound");
            powderObject.transform.SetParent(moundRoot, false);
            powderObject.AddComponent<MeshFilter>();
            powderObject.AddComponent<MeshRenderer>();
            powderMound = powderObject.AddComponent<SpoonPowderMoundVisual>();
            powderObject.SetActive(false);
        }

        if (creamMound == null)
        {
            GameObject creamObject = new GameObject("PanCreamMound");
            creamObject.transform.SetParent(moundRoot, false);
            creamObject.AddComponent<MeshFilter>();
            creamObject.AddComponent<MeshRenderer>();
            creamMound = creamObject.AddComponent<CreamMoundVisual>();
            creamObject.SetActive(false);
        }
    }

    /// <summary>Pilih tipe visual + material sesuai bahan aktif, lalu reset ke 0.</summary>
    public void ConfigureForIngredient(IngredientVisualProfile profile, Material visualMaterial)
    {
        if (profile == null)
            return;

        EnsureChildren();

        isCream = profile.IsCream;
        targetMg = profile.TargetTotalMg;
        fullScale = isCream ? creamFullScale : powderFullScale;

        if (isCream)
        {
            creamMound.Configure(0.05f, 0.05f, 0.006f, 0.03f, 0.03f, 0.0028f, visualMaterial);
            powderMound.gameObject.SetActive(false);
        }
        else
        {
            powderMound.Configure(0.055f, 0.055f, 0.005f, 0.013f, 0.001f, visualMaterial);
            creamMound.gameObject.SetActive(false);
        }

        SetAmountMg(0f);
    }

    /// <summary>Tumbuhkan mound sesuai jumlah (mg) yang sudah ditimbang.</summary>
    public void SetAmountMg(float depositedMg)
    {
        EnsureChildren();

        float t = Mathf.Clamp01(depositedMg / Mathf.Max(1f, targetMg));
        bool show = depositedMg > 0.001f;

        GameObject active = isCream ? creamMound.gameObject : powderMound.gameObject;
        GameObject inactive = isCream ? powderMound.gameObject : creamMound.gameObject;

        if (inactive.activeSelf)
            inactive.SetActive(false);

        active.SetActive(show);

        if (!show)
            return;

        // Lebar tumbuh linear, tinggi tumbuh sedikit lebih cepat agar terasa "menumpuk".
        float widthScale = Mathf.Lerp(minScale, fullScale, t);
        float heightScale = Mathf.Lerp(minScale * 0.7f, fullScale, Mathf.Sqrt(t));
        moundRoot.localScale = new Vector3(widthScale, heightScale, widthScale);
    }

    public void Clear()
    {
        SetAmountMg(0f);
    }
}
