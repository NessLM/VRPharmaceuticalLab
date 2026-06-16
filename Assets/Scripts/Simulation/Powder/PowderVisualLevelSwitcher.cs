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

    private void Awake()
    {
        ApplyVisual(0f);
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
            levelObjects[index].SetActive(true);
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