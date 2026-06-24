using TMPro;
using UnityEngine;

/// <summary>
/// Label peringatan world-space yang membangun dirinya sendiri: panel amber membulat +
/// ikon "!" + teks, selalu menghadap kamera (billboard), muncul lalu memudar otomatis.
/// Dipakai PowderDepositZone untuk peringatan ramah "taruh anak timbangan dulu" tanpa
/// perlu wiring manual di scene. Mengekspos TMP_Text lewat <see cref="Text"/> agar bisa
/// dipakai sebagai target warningText yang sudah ada.
/// </summary>
[DisallowMultipleComponent]
public sealed class WorldWarningLabel : MonoBehaviour
{
    private TMP_Text label;
    private Transform panelRoot;
    private CanvasGroup canvasGroup;
    private Canvas canvas;
    private float visibleTimer;
    private float fadeSpeed = 6f;
    private Camera cam;

    public TMP_Text Text => label;

    /// <summary>Bangun label di atas posisi tertentu (world), parent ke owner.</summary>
    public static WorldWarningLabel Create(Transform parent, Vector3 worldPosition)
    {
        GameObject go = new GameObject("DepositWarningLabel");
        go.transform.SetParent(parent, false);
        go.transform.position = worldPosition;
        WorldWarningLabel w = go.AddComponent<WorldWarningLabel>();
        w.Build();
        return w;
    }

    private void Build()
    {
        // World-space canvas supaya bisa pakai panel + TMP UGUI yang rapi.
        canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        var scaler = gameObject.AddComponent<UnityEngine.UI.CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 10f;

        RectTransform canvasRect = canvas.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(360f, 96f);
        canvasRect.localScale = Vector3.one * 0.0016f; // ~0.58m lebar di world

        canvasGroup = gameObject.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;

        // Panel amber membulat.
        GameObject panel = new GameObject("Panel");
        panel.transform.SetParent(transform, false);
        panelRoot = panel.transform;
        var panelImg = panel.AddComponent<UnityEngine.UI.Image>();
        panelImg.color = new Color(0.95f, 0.62f, 0.07f, 0.96f);
        panelImg.sprite = BuildRoundedSprite();
        panelImg.type = UnityEngine.UI.Image.Type.Sliced;
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        // Ikon "!" bulat putih di kiri.
        GameObject icon = new GameObject("Icon");
        icon.transform.SetParent(panel.transform, false);
        var iconText = icon.AddComponent<TextMeshProUGUI>();
        iconText.text = "!";
        iconText.fontStyle = FontStyles.Bold;
        iconText.alignment = TextAlignmentOptions.Center;
        iconText.fontSize = 64f;
        iconText.color = Color.white;
        RectTransform iconRect = icon.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0f, 0f);
        iconRect.anchorMax = new Vector2(0.22f, 1f);
        iconRect.offsetMin = Vector2.zero;
        iconRect.offsetMax = Vector2.zero;

        // Teks pesan.
        GameObject textGo = new GameObject("Message");
        textGo.transform.SetParent(panel.transform, false);
        label = textGo.AddComponent<TextMeshProUGUI>();
        label.text = "";
        label.fontStyle = FontStyles.Bold;
        label.alignment = TextAlignmentOptions.Left;
        label.enableWordWrapping = true;
        label.fontSize = 26f;
        label.color = Color.white;
        RectTransform textRect = textGo.GetComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0.24f, 0f);
        textRect.anchorMax = new Vector2(0.98f, 1f);
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        gameObject.SetActive(true);
    }

    private static Sprite _roundedSprite;
    private static Sprite BuildRoundedSprite()
    {
        if (_roundedSprite != null)
            return _roundedSprite;

        const int size = 64;
        const int radius = 18;
        var tex = new Texture2D(size, size, TextureFormat.ARGB32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                bool inside = true;
                int cx = Mathf.Clamp(x, radius, size - radius);
                int cy = Mathf.Clamp(y, radius, size - radius);
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy));
                if (d > radius) inside = false;
                tex.SetPixel(x, y, inside ? Color.white : new Color(1, 1, 1, 0));
            }
        }
        tex.Apply();
        _roundedSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
            SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
        return _roundedSprite;
    }

    public void Show(string message, float duration)
    {
        if (label != null)
            label.text = message;
        visibleTimer = Mathf.Max(0.1f, duration);
        gameObject.SetActive(true);
    }

    private void LateUpdate()
    {
        if (cam == null)
            cam = Camera.main;

        // Billboard menghadap kamera.
        if (cam != null)
        {
            Vector3 dir = transform.position - cam.transform.position;
            if (dir.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
        }

        float target = visibleTimer > 0f ? 1f : 0f;
        if (canvasGroup != null)
            canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, target, fadeSpeed * Time.deltaTime);

        if (visibleTimer > 0f)
            visibleTimer -= Time.deltaTime;
    }
}