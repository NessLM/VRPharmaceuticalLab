using UnityEngine;
using System.Collections.Generic;

public abstract class GameEvent<T> : ScriptableObject
{
    private List<GameEventListener<T>> listeners = new();

    public void Raise(T value)
    {
        for (int i = listeners.Count - 1; i >= 0; i--)
            listeners[i].OnEventRaised(value);
    }

    public void Register(GameEventListener<T> listener)
    {
        if (!listeners.Contains(listener))
            listeners.Add(listener);
    }

    public void Unregister(GameEventListener<T> listener)
    {
        if (listeners.Contains(listener))
            listeners.Remove(listener);
    }
}
