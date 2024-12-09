using System;
using System.Collections.Generic;
using NetBuff.Misc;
using UnityEngine;

namespace Solis.Circuit.Components
{
    public class CircuitDoor : CircuitComponent
    {
        #region Inspector Fields
        [Header("REFERENCES")]
        public CircuitPlug input;
        public GameObject open;
        public GameObject closed;
        
        [Header("STATE")]
        public BoolNetworkValue isOpen = new(false);
        #endregion

        #region Unity Callbacks

        private void Awake()
        {
            if(!isOpen.AttachedTo)
                isOpen.AttachedTo = this;
        }

        protected override void OnEnable()
        {
            WithValues(isOpen);
            base.OnEnable();
            
            _OnValueChanged(isOpen.Value, isOpen.Value);
            isOpen.OnValueChanged += _OnValueChanged;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            isOpen.OnValueChanged -= _OnValueChanged;
        }
        #endregion

        #region Abstract Methods Implementation
        public override CircuitData ReadOutput(CircuitPlug plug)
        {
            return new CircuitData();
        }

        public override IEnumerable<CircuitPlug> GetPlugs()
        {
            yield return input;
        }

        protected override void OnRefresh()
        {
            if(isOpen.AttachedTo != null && HasAuthority)
                isOpen.Value = input.ReadOutput().power > 0;
        }
        #endregion

        #region Private Methods
        private void _OnValueChanged(bool old, bool now)
        {
            open.SetActive(now);
            closed.SetActive(!now);
            if (now) onToggleComponent.Invoke();
        }
        #endregion
    }
}