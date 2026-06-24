using UnityEngine;

public class PowderVisualLevelSwitcher : MonoBehaviour
{
    [Header("Visual Levels")]
    [Tooltip("Isi dengan visual bubuk dari kecil ke besar. Kosong = hidden.")]
    [SerializeField] private GameObject[] levelObjects;

    [Header("Amount Mapping")]
    [SerializeField] private float maxVisualMg = 250f;
    [SerializeField] private bool hideWhenZero = true;

    [Header("Debug")]
    [SerializeField] private float debugCurrentMg;

    public float CurrentMg { get; private set; }

    private bool hasTint;
    private Color tint = Color.white;
    private MaterialPropertyBlock mpb;
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    private void Awake()
    {
        ApplyVisual(0f);
    }

    /// <summary>
    /// Warnai semua mesh level (Asam putih, Sulfur kuning, Vaselin ivory) tanpa mengubah
    /// material aslinya — pakai MaterialPropertyBlock supaya aman untuk workflow lain (Sirup).
    /// </summary>
    public void SetTint(Color color)
    {
        hasTint = true;
        tint = color;
        ApplyTintToAll();
    }

    /// <summary>Hapus pewarnaan, kembalikan ke warna material asli (mis. saat reset / Sirup).</summary>
    public void ClearTint()
    {
        hasTint = false;
        tint = Color.white;
        ApplyTintToAll();
    }

    private void ApplyTintToAll()
    {
        if (levelObjects == null)
            return;

        for (int i = 0; i < levelObjects.Length; i++)
            ApplyTint(levelObjects[i]);
    }

    private void ApplyTint(GameObject go)
    {
        if (go == null)
            return;

        Renderer[] renderers = go.GetComponentsInChildren<Renderer>(true);
        if (renderers == null || renderers.Length == 0)
            return;

        mpb ??= new MaterialPropertyBlock();
        foreach (Renderer r in renderers)
        {
            if (r == null)
                continue;
            r.GetPropertyBlock(mpb);
            mpb.SetColor(BaseColorId, hasTint ? tint : Color.white);
            mpb.SetColor(ColorId, hasTint ? tint : Color.white);
            r.SetPropertyBlock(mpb);
        }
    }

    public void SetAmountMg(float amountMg)
    {
        CurrentMg = Mathf.Max(0f, amountMg);
        debugCurrentMg = CurrentMg;
        ApplyVisual(CurrentMg);
    }

    public void Clear()
    {
        SetAmountMg(0f);
    }

    private void ApplyVisual(float amountMg)
    {
        if (levelObjects == null || levelObjects.Length == 0)
            return;

        for (int i = 0; i < levelObjects.Length; i++)
        {
            if (levelObjects[i] != null)
                levelObjects[i].SetActive(false);
        }

        if (amountMg <= 0.001f && hideWhenZero)
            return;

        float ratio = Mathf.Clamp01(amountMg / Mathf.Max(1f, maxVisualMg));

        int index = Mathf.CeilToInt(ratio * levelObjects.Length) - 1;
        index = Mathf.Clamp(index, 0, levelObjects.Length - 1);

        if (levelObjects[index] != null)
        {
            levelObjects[index].SetActive(true);
            if (hasTint)
                ApplyTint(levelObjects[index]);
        }
    }

    public void SetMaxVisualMg(float value)
    {
        maxVisualMg = Mathf.Max(1f, value);
        ApplyVisual(CurrentMg);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        maxVisualMg = Mathf.Max(1f, maxVisualMg);

        if (Application.isPlaying)
            ApplyVisual(debugCurrentMg);
    }
#endif
}