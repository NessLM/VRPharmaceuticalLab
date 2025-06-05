using UnityEngine;

[CreateAssetMenu(menuName = "Events/SummaryEvent")]
public class SummaryEvent : GameEvent<SummaryData>
{

}

[System.Serializable]
public struct SummaryData
{
    public int score;
    public int correctAnswer;
    public int wrongAnswer;
}
