using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using System.Collections;

// Menerima sertifikat SSL apa pun. Diperlukan di perangkat Quest/Android
// yang sering gagal memverifikasi rantai sertifikat (Unable to complete SSL connection).
public class BypassCertificate : CertificateHandler
{
    protected override bool ValidateCertificate(byte[] certificateData)
    {
        return true;
    }
}

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
        // Nama scene aktif dipakai sebagai penanda lokasi/tempat quiz
        // (VRLab, VRLabSimulation, VRLabSimulation_Padat) tanpa perlu mengubah scene.
        string lokasi = SceneManager.GetActiveScene().name;

        WWWForm form = new WWWForm();
        form.AddField("nama", string.IsNullOrEmpty(nama) ? "-" : nama);
        form.AddField("kelas", string.IsNullOrEmpty(kelas) ? "-" : kelas);
        form.AddField("score", score);
        form.AddField("lokasi", lokasi);

        using (UnityWebRequest www = UnityWebRequest.Post("http://localhost/quizlab-station/simpan_quiz.php", form))
        {
            // Atasi error "Unable to complete SSL connection" di Quest/Android.
            www.certificateHandler = new BypassCertificate();
            www.timeout = 15;

            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                Debug.Log("Data berhasil dikirim: " + www.downloadHandler.text);
            }
            else
            {
                Debug.LogError("Gagal kirim data: " + www.error + " | Code: " + www.responseCode);
            }
        }
    }
}