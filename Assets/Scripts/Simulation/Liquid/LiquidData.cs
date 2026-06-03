using UnityEngine;

[CreateAssetMenu(menuName = "VR Pharmacy/Liquid/Liquid Data")]
public class LiquidData : ScriptableObject
{
    public string liquidName = "Water";
    public Color liquidColor = new Color(0.25f, 0.65f, 1f, 0.45f);
}
