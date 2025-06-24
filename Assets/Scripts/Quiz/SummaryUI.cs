using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using System.Collections;

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

        // Kirim ke database via PHP
        StartCoroutine(KirimKeDatabase(nameInput.text, classInput.text, data.score));
    }

    IEnumerator KirimKeDatabase(string nama, string kelas, int score)
    {
        WWWForm form = new WWWForm();
        form.AddField("nama", nama);
        form.AddField("kelas", kelas);
        form.AddField("score", score);

        using (UnityWebRequest www = UnityWebRequest.Post("https://vrlabfarmasismkn5pkp.fun/simpan_quiz.php", form))
        {
            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                Debug.Log("Data berhasil dikirim: " + www.downloadHandler.text);
            }
            else
            {
                Debug.LogError("Gagal kirim data: " + www.error);
            }
        }
    }
}