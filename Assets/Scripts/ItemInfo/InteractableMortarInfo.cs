using UnityEngine;
using UnityEngine.Video;

[CreateAssetMenu(fileName = "InteractableMortarInfo", menuName = "ScriptableObjects/InteractableMortarInfo", order = 1)]
public class InteractableMortarInfo : ScriptableObject
{
    public string itemName;
    [TextArea(3, 10)] public string itemDescription;

    // hanya video
    public VideoClip[] galleryVideos;
}