using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class SelectableBackground : MonoBehaviour
{
    public static Selectable Selected;
    public static Action<RectTransform> OnSelected;

    public Image target;
    public Selectable selectable;
    private bool _selected;
    private void OnEnable()
    {
        InvokeRepeating(nameof(CheckUpdate), 0, .2f);
    }

    private void OnDisable()
    {
        CancelInvoke(nameof(CheckUpdate));
    }

    private void CheckUpdate()
    {
        if (!_selected)
        {
            if (EventSystem.current.currentSelectedGameObject == selectable.gameObject)
            {
                Selected = selectable;
                _selected = true;
                target.enabled = true;
                if(TryGetComponent(out RectTransform rect))
                    OnSelected?.Invoke(rect);
            }
        }else if (EventSystem.current.currentSelectedGameObject != selectable.gameObject)
        {
            Selected = null;
            _selected = false;
            target.enabled = false;
        }
    }
}
