using UnityEngine;
using UnityEngine.Events;

public class GameEventListener<T> : MonoBehaviour
{
    [Header("Event")]
    public GameEvent<T> gameEvent;

    [Header("Response")]
    public UnityEvent<T> response;

    private void OnEnable() => gameEvent?.Register(this);
    private void OnDisable() => gameEvent?.Unregister(this);
    public void OnEventRaised(T value) => response.Invoke(value);
}
