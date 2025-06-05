using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class InteractableItemDataHandler : MonoBehaviour
{
    [SerializeField] InteractableItemInfo data;

    [Header("UI")]
    [SerializeField] TextMeshProUGUI titleText;
    [SerializeField] TextMeshProUGUI descText;
    // [SerializeField] Image[] galleryImages;
    [SerializeField] Image galleryImage;
    [SerializeField] Transform galleryParent;


    void Start()
    {
        Initiliaze();
    }

    void Initiliaze()
    {
        titleText.text = data.itemName;
        descText.text = data.itemDescription;

        // for one
        galleryImage.sprite = data.galleryImages[0];

        // for Gallery
        // for (int i = 0; i < data.galleryImages.Length; i++)
        // {
        //     Image image = Instantiate(galleryImage, galleryParent).GetComponentInChildren<Image>();
        //     image.sprite = data.galleryImages[i];
        //     if (i == 0)
        //         image.gameObject.SetActive(true);
        // }
    }

    // next gallery
    public void NextImage()
    {

    }
}
