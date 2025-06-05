using System.IO;
using UnityEngine;


public class QuizManager : GameEventListener<bool>
{
    [SerializeField] TextAsset questionData;

    [Header("Broadcaster")]
    [SerializeField] QuestionEvent questionEvent;
    [SerializeField] SummaryEvent summaryEvent;
    [SerializeField] PanelEvent panelEvent;

    private int currentQuestionIndex = 0;
    [SerializeField] Question[] randomizedQuestions;
    int score = 0;
    int correctAnswer = 0;
    int wrongAnswer = 0;

    public void StartQuiz()
    {
        string rawData = questionData.text;
        randomizedQuestions = new Question[0];
        randomizedQuestions = GetShuffleQuestion(rawData);
        InitQuestion();
    }

    void InitQuestion()
    {
        questionEvent.Raise(randomizedQuestions[currentQuestionIndex]);
    }

    void FinishQuiz()
    {
        SummaryData data = new SummaryData();
        data.score = score = correctAnswer * 4;
        data.correctAnswer = correctAnswer;
        data.wrongAnswer = wrongAnswer;
        summaryEvent.Raise(data);

        panelEvent.Raise(5);
    }

    void NextQuestion()
    {
        currentQuestionIndex++;

        if (currentQuestionIndex >= randomizedQuestions.Length)
        {
            FinishQuiz();
            correctAnswer = 0;
            wrongAnswer = 0;
            currentQuestionIndex = 0;
            return;
        }

        InitQuestion();
    }

    public void CheckAnswer(bool isCorrect)
    {
        if (isCorrect)
        {
            score += 4;
            correctAnswer++;
        }
        else
        {
            wrongAnswer++;
        }
        NextQuestion();
    }

    Question[] GetShuffleQuestion(string rawData)
    {
        QuestionWrapper wrapper = JsonUtility.FromJson<QuestionWrapper>(rawData);

        if (wrapper == null || wrapper.questions == null)
        {
            Debug.LogError("Failed to parse questions from JSON.");
            return new Question[0];
        }

        Question[] questions = wrapper.questions;
        ShuffleUtility.Shuffle(questions);
        Debug.Log("Questions: " + questions.Length);
        Debug.Log("Questions: " + questions[0].question);
        return questions;
    }

}

[System.Serializable]
public class QuestionWrapper
{
    public Question[] questions;
}

[System.Serializable]
public class Answer
{
    public string text;
    public bool isCorrect;
}

[System.Serializable]
public class Question
{
    public string question;
    public Answer[] answers;
}
