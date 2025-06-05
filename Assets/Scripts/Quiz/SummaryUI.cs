using TMPro;
using UnityEngine;

public class SummaryUI : GameEventListener<SummaryData>
{
    [SerializeField] TextMeshProUGUI scoreText;
    [SerializeField] TextMeshProUGUI correctAnswersText;
    [SerializeField] TextMeshProUGUI wrongAnswersText;
    [SerializeField] TextMeshProUGUI nameText;
    [SerializeField] TextMeshProUGUI classText;
    [SerializeField] TMP_InputField nameInput;
    [SerializeField] TMP_InputField classInput;

    public void SetSummary(SummaryData data)
    {
        if (!string.IsNullOrEmpty(nameInput.text))
            nameText.text = string.Concat("Nama: ", nameInput.text);
        else
            nameText.text = string.Concat("Nama: -");

        if (!string.IsNullOrEmpty(classInput.text))
            classText.text = string.Concat("Kelas: ", classInput.text);
        else
            classText.text = string.Concat("Kelas: -");

        scoreText.text = string.Concat("Score\n", data.score);
        correctAnswersText.text = string.Concat("Correct Answers\n", data.correctAnswer);
        wrongAnswersText.text = string.Concat("Wrong Answers\n", data.wrongAnswer);
    }
}
