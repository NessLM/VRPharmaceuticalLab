using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;


public class KeyboardModule : MonoBehaviour
{
    private string _Character;
    [SerializeField] private bool _IsDel = false;
    [SerializeField] private bool _IsDone = false;
    [SerializeField] private bool _IsSpace = false;

    private void Start()
    {
        if (_IsSpace == false)
            _Character = gameObject.name;
        else
            _Character = " ";

        Button myButt = GetComponent<Button>();
        myButt.onClick.AddListener(() => Typing());
    }

    public void Typing()
    {
        if (_IsDel == true)
        {
            KeyboardManager.Instance.DelChar();
            return;
        }

        if (_IsDone == true)
        {
            KeyboardManager.Instance.Done();
            return;
        }
        KeyboardManager.Instance.AddingChar(_Character);
    }
}

