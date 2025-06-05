using TMPro;
using UnityEngine;

public class ItemsWiki : GameEventListener<int>
{
    [SerializeField] InteractableItemInfo[] itemInfos;
    [SerializeField] TextMeshProUGUI titleText;
    [SerializeField] TextMeshProUGUI descText;
    [SerializeField] GameObject buttonTriggerPrefab;
    [SerializeField] Transform buttonListContainer;

    bool isInit;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if (isInit)
            return;

        PopulateButton();
        ShowInfo(0);

        isInit = true;
    }

    void PopulateButton()
    {
        int length = itemInfos.Length;
        for (int i = 0; i < length; i++)
        {
            GameObject button = Instantiate(buttonTriggerPrefab, buttonListContainer);
            button.GetComponent<ClickableWiki>().Setup(itemInfos[i].itemName);
        }
    }

    public void ShowInfo(int id)
    {
        titleText.text = itemInfos[id].itemName;
        descText.text = itemInfos[id].itemDescription;
    }
}
