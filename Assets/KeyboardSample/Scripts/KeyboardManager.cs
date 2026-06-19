using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class KeyboardManager : MonoBehaviour
{
    #region singleton
    public static KeyboardManager Instance;
    private void Awake()
    {
        Instance = this;
    }

    #endregion
    [SerializeField] private GameObject _KeyboardGameobject;
    [SerializeField] private GameObject _NumpadGameObject;

    public string CurrentText;
    private TMP_InputField _CurrentInputField;

    public void OpenKeybord(TMP_InputField inputField)
    {
        if (inputField == null)
            return;

        CurrentText = inputField.text ?? string.Empty;
        if (_KeyboardGameobject != null)
            _KeyboardGameobject.SetActive(true);

        _CurrentInputField = inputField;
        _CurrentInputField.text = CurrentText;
    }

    public void OpenNumpad(TMP_InputField inputField)
    {
        if (inputField == null)
            return;

        CurrentText = inputField.text ?? string.Empty;
        if (_NumpadGameObject != null)
            _NumpadGameObject.SetActive(true);

        _CurrentInputField = inputField;
        _CurrentInputField.text = CurrentText;
    }

    public void Done()
    {
        CurrentText = string.Empty;
        if (_KeyboardGameobject != null)
            _KeyboardGameobject.SetActive(false);

        if (_NumpadGameObject != null)
            _NumpadGameObject.SetActive(false);

        _CurrentInputField = null;
    }

    public void AddingChar(string character)
    {
        if (_CurrentInputField == null)
            return;

        CurrentText = CurrentText + character;
        _CurrentInputField.text = CurrentText;
    }

    public void DelChar()
    {
        if (_CurrentInputField == null || CurrentText.Length == 0) return;
        CurrentText = CurrentText.Remove(CurrentText.Length - 1);
        _CurrentInputField.text = CurrentText;
    }

}
