using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class KeyboardLowerUpper : MonoBehaviour
{
    [SerializeField] GameObject lowerCase;
    [SerializeField] GameObject upperCase;

    bool isUpper = false;

    // Start is called before the first frame update
    void Start()
    {
        Button myButt = GetComponent<Button>();
        myButt.onClick.AddListener(() => ToggleCase());
    }

    void ToggleCase()
    {
        isUpper = !isUpper;
        if (isUpper)
        {
            lowerCase.SetActive(false);
            upperCase.SetActive(true);
        }
        else
        {
            lowerCase.SetActive(true);
            upperCase.SetActive(false);
        }
    }
}
