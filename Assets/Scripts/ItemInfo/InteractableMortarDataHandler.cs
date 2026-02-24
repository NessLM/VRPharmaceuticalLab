using TMPro;
using UnityEngine;
using UnityEngine.Video;

public class InteractableMortarDataHandler : MonoBehaviour
{
    [SerializeField] InteractableMortarInfo data;

    [Header("UI")]
    [SerializeField] TextMeshProUGUI titleText;
    [SerializeField] TextMeshProUGUI descText;
    [SerializeField] VideoPlayer videoPlayer;

    // hapus Start() → biar ga auto jalan

    public void Initialize()
    {
        if (data == null)
        {
            Debug.LogError("Data belum diassign ke InteractableMortarDataHandler!");
            return;
        }

        if (titleText != null)
            titleText.text = data.itemName;

        if (descText != null)
            descText.text = data.itemDescription;

        if (videoPlayer != null && data.galleryVideos != null && data.galleryVideos.Length > 0)
        {
            videoPlayer.clip = data.galleryVideos[0];
            videoPlayer.Play();
        }
        else if (videoPlayer != null)
        {
            videoPlayer.Stop();
        }
    }
}