using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class QuizUI : GameEventListener<Question>
{

    [Header("Ref")]
    [SerializeField] private TextMeshProUGUI questionText;
    [SerializeField] private Button[] answerButtons;

    [Header("Broadcaster")]
    [SerializeField] private AnswerEvent answerEvent;

    public void Setup(Question data)
    {
        questionText.text = data.question;
        Answer[] answers = data.answers;

        ShuffleUtility.Shuffle(answers);

        for (int i = 0; i < answerButtons.Length; i++)
        {
            TextMeshProUGUI buttonText = answerButtons[i].GetComponentInChildren<TextMeshProUGUI>();
            buttonText.text = answers[i].text;
            answerButtons[i].onClick.RemoveAllListeners();
            int id = i;
            answerButtons[i].onClick.AddListener(() => { OnClickAnswer(answers[id].isCorrect); });
        }
    }

    void OnClickAnswer(bool isCorrect)
    {
        if (isCorrect)
        {
            Debug.Log("Correct Answer");
        }
        else
        {
            Debug.Log("Wrong Answer");
        }
        answerEvent.Raise(isCorrect);
    }
}
