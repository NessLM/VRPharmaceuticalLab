using UnityEngine;

[CreateAssetMenu(fileName = "InteractableItemInfo", menuName = "ScriptableObjects/InteractableItemInfo", order = 1)]
public class InteractableItemInfo : ScriptableObject
{
    public string itemName;
    [TextArea(3, 10)] public string itemDescription;
    public Sprite[] galleryImages;
}
