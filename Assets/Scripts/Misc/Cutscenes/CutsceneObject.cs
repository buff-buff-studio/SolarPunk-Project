using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CutsceneObject : MonoBehaviour
{
    public Action OnCutsceneEnd;

    public virtual void Play() { }

    public virtual void Stop()
    {
        OnCutsceneEnd?.Invoke();
    }
}