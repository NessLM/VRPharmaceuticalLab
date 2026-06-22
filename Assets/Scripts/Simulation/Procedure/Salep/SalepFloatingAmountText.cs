using TMPro;
using UnityEngine;

/// <summary>
/// Floating world-space text yang muncul lalu naik &amp; memudar, mis. "+50 mg" / "+2 g".
/// Self-building: dipanggil lewat SalepFloatingAmountText.Spawn(...). Tidak butuh wiring scene.
/// </summary>
[DisallowMultipleComponent]
public sealed class SalepFloatingAmountText : MonoBehaviour
{
    private TextMeshPro label;
    private float lifetime = 1.1f;
    private float age;
    private float riseSpeed = 0.22f;
    private Color baseColor = Color.white;

    public static void Spawn(Vector3 worldPosition, string text, Color color, float fontSize = 0.06f)
    {
        GameObject go = new GameObject("SalepFloatingAmount");
        go.transform.position = worldPosition;

        SalepFloatingAmountText floating = go.AddComponent<SalepFloatingAmountText>();
        floating.Initialize(text, color, fontSize);
    }

    private void Initialize(string text, Color color, float fontSize)
    {
        label = gameObject.AddComponent<TextMeshPro>();
        label.text = text;
        label.fontSize = Mathf.Max(0.5f, fontSize * 60f);
        label.alignment = TextAlignmentOptions.Center;
        label.color = color;
        label.textWrappingMode = TextWrappingModes.NoWrap;

        RectTransform rect = label.GetComponent<RectTransform>();
        if (rect != null)
            rect.sizeDelta = new Vector2(2.5f, 0.6f);

        transform.localScale = Vector3.one * 0.02f;
        baseColor = color;
        age = 0f;

        FaceCamera();
    }

    private void Update()
    {
        age += Time.deltaTime;
        float t = lifetime > 0f ? age / lifetime : 1f;

        transform.position += Vector3.up * riseSpeed * Time.deltaTime;
        FaceCamera();

        if (label != null)
        {
            Color c = baseColor;
            c.a = Mathf.Lerp(1f, 0f, Mathf.SmoothStep(0.45f, 1f, t));
            label.color = c;
        }

        // Sedikit pop di awal.
        float pop = 1f + Mathf.Clamp01(1f - age * 6f) * 0.25f;
        transform.localScale = Vector3.one * 0.02f * pop;

        if (age >= lifetime)
            Destroy(gameObject);
    }

    private void FaceCamera()
    {
        if (Camera.main == null)
            return;

        Vector3 dir = transform.position - Camera.main.transform.position;
        if (dir.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
    }
}
