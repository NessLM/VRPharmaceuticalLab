using System.Collections;
using TMPro;
using UnityEngine;

public class TimerQuestion : GameEventListener<Question>
{
    [SerializeField] int startTimeInSeconds = 10;
    [SerializeField] TextMeshProUGUI timerText;
    private Coroutine countdownCoroutine;

    [SerializeField] AnswerEvent answerEvent;

    public void StartCountdownTimer()
    {
        if (countdownCoroutine != null)
            StopCoroutine(countdownCoroutine);

        countdownCoroutine = StartCoroutine(StartCountdown(startTimeInSeconds));
    }

    private IEnumerator StartCountdown(int seconds)
    {
        int remainingTime = seconds;

        while (remainingTime > 0)
        {
            UpdateTimerUI(remainingTime);
            yield return new WaitForSeconds(1f);
            remainingTime--;
        }

        UpdateTimerUI(0);
        OnTimerEnd();
    }

    private void UpdateTimerUI(int seconds)
    {
        if (timerText != null)
        {
            timerText.text = seconds.ToString();
        }

        Debug.Log("Time left: " + seconds);
    }

    private void OnTimerEnd()
    {
        Debug.Log("Countdown Finished!");
        answerEvent.Raise(false);
    }

    public void StopCountdownTimer()
    {
        if (countdownCoroutine != null)
            StopCoroutine(countdownCoroutine);
    }
}
