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

        CurrentText = string.Empty;
        _KeyboardGameobject.SetActive(true);
        if (string.IsNullOrEmpty(inputField.text))
            CurrentText = inputField.text;

        _CurrentInputField = inputField;
        _CurrentInputField.text = CurrentText;
    }

    public void OpenNumpad(TMP_InputField inputField)
    {

        CurrentText = string.Empty;
        _NumpadGameObject.SetActive(true);
        if (string.IsNullOrEmpty(inputField.text))
            CurrentText = inputField.text;

        _CurrentInputField = inputField;
        _CurrentInputField.text = CurrentText;
    }

    public void Done()
    {
        CurrentText = string.Empty;
        _KeyboardGameobject.SetActive(false);
    }

    public void AddingChar(string character)
    {
        CurrentText = CurrentText + character;
        _CurrentInputField.text = CurrentText;
    }

    public void DelChar()
    {
        if (CurrentText.Length == 0) return;
        CurrentText = CurrentText.Remove(CurrentText.Length - 1);
        _CurrentInputField.text = CurrentText;
    }

}

