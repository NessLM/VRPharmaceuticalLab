using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class ClickableWiki : MonoBehaviour
{
    Button button;
    [SerializeField] ItemWikiEvent itemWikiEvent;
    [SerializeField] TextMeshProUGUI titleText;

    public void Setup(string title)
    {
        titleText.text = title;
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        button = GetComponent<Button>();
        button.onClick.AddListener(() => ShowInfo(button.transform.GetSiblingIndex()));
    }

    void ShowInfo(int id)
    {
        itemWikiEvent.Raise(id);
    }
}
