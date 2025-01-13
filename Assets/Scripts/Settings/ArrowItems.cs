using System;
using System.Collections.Generic;
using Interface;
using Solis.Interface.Input;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Solis.Settings
{
    public class ArrowItems : Selectable
    {
        [SerializeField]
        private bool interactable;

        [Space] 
        public bool invert = false;
        public List<string> items;
        public int currentIndex;
        
        [Space]
        public Button previousButton;
        public Button nextButton;
        public TextMeshProUGUI display;
        private Label label;
        
        [Space]
        public UnityEvent<int> onChangeItem;

        public bool Interactable
        {
            get => interactable;
            set
            {
                interactable = value;
                previousButton.interactable = value;
                nextButton.interactable = value;
            }
        }
        
        private void Awake()
        {
            previousButton.onClick.AddListener(!invert ? PreviousItem : NextItem);
            nextButton.onClick.AddListener(invert ? PreviousItem : NextItem);
            display.TryGetComponent(out label);
        }

        private void Update()
        {
            if (IsHighlighted())
            {
            }
        }

        public override void OnMove(AxisEventData eventData)
        {
            if (!IsActive() || !IsInteractable())
            {
                base.OnMove(eventData);
                return;
            }

            switch (eventData.moveDir)
            {
                case MoveDirection.Left:
                    if (FindSelectableOnLeft() == null)
                        PreviousItem();
                    else base.OnMove(eventData);
                    break;
                case MoveDirection.Right:
                    if (FindSelectableOnRight() == null)
                        NextItem();
                    else base.OnMove(eventData);
                    break;
                default:
                    base.OnMove(eventData);
                    break;
            }
        }

        private void Start()
        {
            UpdateDisplay();
        }
        
        public void SetItems(List<string> newItems)
        {
            items = newItems;
            UpdateDisplay();
        }

        private void PreviousItem()
        {
            currentIndex--;
            if (currentIndex < 0)
                currentIndex = items.Count - 1;
            UpdateDisplay();
            onChangeItem?.Invoke(currentIndex);
        }
        
        private void NextItem()
        {
            currentIndex++;
            if (currentIndex >= items.Count)
                currentIndex = 0;
            UpdateDisplay();
            onChangeItem?.Invoke(currentIndex);
        }
        
        private void UpdateDisplay()
        {
            if(currentIndex < 0 || currentIndex >= items.Count) 
                return;
            if(!label)
                display.text = items[currentIndex];
            else
                label.Localize(items[currentIndex]);
        }

        private void OnValidate()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                Interactable = interactable;
            }
            
            if(items.Count == 0) return;
            
            currentIndex = Mathf.Clamp(currentIndex, 0, items.Count - 1);
            UpdateDisplay();
#endif
        }

        public void Rebuild(CanvasUpdate executing)
        {
        }

        public void LayoutComplete()
        {
        }

        public void GraphicUpdateComplete()
        {
        }
    }
}