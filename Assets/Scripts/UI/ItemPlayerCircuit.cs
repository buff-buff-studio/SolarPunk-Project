using System.Collections.Generic;
using _Scripts.UI;
using NetBuff.Components;
using NetBuff.Misc;
using Solis.Circuit;
using UnityEngine;

namespace UI
{
    public class ItemPlayerCircuit : CircuitComponent
    {
        public ItemPlayerText currentDialog;
        public CircuitPlug input;
            
        [Header("STATE")]
        public BoolNetworkValue isOn = new(false);
            
        #region Unity Callbacks
        protected override void OnEnable()
        {
            WithValues(isOn);
            base.OnEnable();
                
            _OnValueChanged(isOn.Value, isOn.Value);
            isOn.OnValueChanged += _OnValueChanged;
        }
    
        protected override void OnDisable()
        {
            base.OnDisable();
            isOn.OnValueChanged -= _OnValueChanged;
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
            if(isOn.AttachedTo != null && HasAuthority)
                isOn.Value = input.ReadOutput().power > 0;
        }
        #endregion
            
        private void _OnValueChanged(bool old, bool now)
        {
            if (now)
            {
                onToggleComponent.Invoke();
                InteractablePanel.Instance.PlayDialog(currentDialog);
            }
            
        }
    }
}
