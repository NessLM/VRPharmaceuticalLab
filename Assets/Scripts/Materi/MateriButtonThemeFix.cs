using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class MateriButtonThemeFix : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform buttonRoot;

    [Header("Button Colors")]
    [SerializeField] private Color normalColor = new Color(0.72f, 0.43f, 0.0f, 1f);
    [SerializeField] private Color hoverColor = new Color(0.95f, 0.60f, 0.08f, 1f);
    [SerializeField] private Color pressedColor = new Color(0.50f, 0.28f, 0.0f, 1f);
    [SerializeField] private Color selectedColor = new Color(0.86f, 0.50f, 0.0f, 1f);
    [SerializeField] private Color textColor = Color.white;

    [Header("Options")]
    [SerializeField] private bool forceEveryLateUpdate = true;

    private Button[] cachedButtons;

    private void Awake()
    {
        Setup();
    }

    private void OnEnable()
    {
        Setup();
        StartCoroutine(ApplyDelayed());
    }

    private void LateUpdate()
    {
        if (!forceEveryLateUpdate)
            return;

        if (cachedButtons == null || cachedButtons.Length == 0)
            CacheButtons();

        foreach (Button button in cachedButtons)
        {
            if (button == null)
                continue;

            MateriButtonThemeItem item = button.GetComponent<MateriButtonThemeItem>();
            if (item != null)
                item.ApplyCurrentState();
        }
    }

    private void Setup()
    {
        CacheButtons();

        foreach (Button button in cachedButtons)
        {
            if (button == null)
                continue;

            button.transition = Selectable.Transition.None;

            MateriButtonThemeItem item = button.GetComponent<MateriButtonThemeItem>();
            if (item == null)
                item = button.gameObject.AddComponent<MateriButtonThemeItem>();

            item.Configure(normalColor, hoverColor, pressedColor, selectedColor, textColor);
            item.ApplyNormal();
        }
    }

    private void CacheButtons()
    {
        Transform root = buttonRoot != null ? buttonRoot : transform;
        cachedButtons = root.GetComponentsInChildren<Button>(true);
    }

    private IEnumerator ApplyDelayed()
    {
        yield return null;
        Setup();

        yield return new WaitForEndOfFrame();
        Setup();
    }
}

public sealed class MateriButtonThemeItem : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler,
    IPointerDownHandler,
    IPointerUpHandler,
    ISelectHandler,
    IDeselectHandler
{
    private Image image;
    private TMP_Text text;

    private Color normalColor;
    private Color hoverColor;
    private Color pressedColor;
    private Color selectedColor;
    private Color textColor;

    private bool isHover;
    private bool isPressed;
    private bool isSelected;

    public void Configure(
        Color normal,
        Color hover,
        Color pressed,
        Color selected,
        Color textCol)
    {
        normalColor = normal;
        hoverColor = hover;
        pressedColor = pressed;
        selectedColor = selected;
        textColor = textCol;

        image = GetComponent<Image>();

        if (image == null)
        {
            Button button = GetComponent<Button>();
            if (button != null)
                image = button.targetGraphic as Image;
        }

        text = GetComponentInChildren<TMP_Text>(true);
    }

    public void ApplyNormal()
    {
        isHover = false;
        isPressed = false;
        ApplyCurrentState();
    }

    public void ApplyCurrentState()
    {
        if (image == null)
            image = GetComponent<Image>();

        if (text == null)
            text = GetComponentInChildren<TMP_Text>(true);

        if (image != null)
        {
            if (isPressed)
                image.color = pressedColor;
            else if (isHover)
                image.color = hoverColor;
            else if (isSelected)
                image.color = selectedColor;
            else
                image.color = normalColor;
        }

        if (text != null)
            text.color = textColor;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        isHover = true;
        ApplyCurrentState();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isHover = false;
        isPressed = false;
        ApplyCurrentState();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        isPressed = true;
        ApplyCurrentState();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        isPressed = false;
        isSelected = true;
        ApplyCurrentState();
    }

    public void OnSelect(BaseEventData eventData)
    {
        isSelected = true;
        ApplyCurrentState();
    }

    public void OnDeselect(BaseEventData eventData)
    {
        isSelected = false;
        ApplyCurrentState();
    }
}