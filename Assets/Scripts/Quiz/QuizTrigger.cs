using UnityEngine;
using UnityEngine.Events;

public class QuizTrigger : GameEventListener<int>
{
    [SerializeField] int idPanel = 3;
    public UnityEvent OnStartQuiz;

    public void TriggerQuiz(int id)
    {
        if (idPanel == id)
            OnStartQuiz.Invoke();
    }
}
