using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class PublicAnimationEvent : MonoBehaviour
{
    public List<UnityEvent> events = new List<UnityEvent>();

    public void InvokeEvent(int index)
    {
        if (index >= events.Count)
        {
            Debug.LogError($"Index {index} out of range");
            return;
        }

        events[index].Invoke();
    }
}
