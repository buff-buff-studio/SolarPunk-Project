using System.Collections.Generic;
using NetBuff.Misc;
using Solis.Data;
using Solis.Packets;
using Solis.Player;
using UnityEngine;

namespace Solis.Circuit.Components
{
    /// <summary>
    /// A button that can be pressed by a player and will stay on for a certain amount of time.
    /// </summary>
    public class CircuitTemporizedButton : CircuitInteractive
    {
        #region Inspector Fields

        public float timeOn = 4;
        public Vector3 knobOn = new(0, 0.15f, 0f);
        public Vector3 knobOff = new(0, 0.25f, 0f);

        [Space]
        [Header("STATE")]
        public BoolNetworkValue isOn = new(false);

        [Space]
        [Header("REFERENCES")]
        public CircuitPlug output;
        public Transform knob;

        #endregion

        #region Private Fields
        private float _timeOnCounter;
        #endregion

        #region Unity Callbacks
        protected override void OnEnable()
        {
            WithValues(isOn);
            
            base.OnEnable();
            
            isOn.OnValueChanged += _OnValueChanged;
        }
        
        private void FixedUpdate()
        {
            knob.localPosition = Vector3.Lerp(knob.localPosition, isOn.Value ? knobOn : knobOff, Time.fixedDeltaTime * 10);

            if (!HasAuthority || !isOn.Value)
                return;
            
            _timeOnCounter = _timeOnCounter > 0 ? _timeOnCounter - Time.fixedDeltaTime : 0;
            if (_timeOnCounter <= 0) isOn.Value = false;
        }
        #endregion

        #region Abstract Methods Implementation
        public override CircuitData ReadOutput(CircuitPlug plug)
        {
            return new CircuitData(isOn.Value);
        }

        protected override void OnRefresh() { }

        public override IEnumerable<CircuitPlug> GetPlugs()
        {
            yield return output;
        }
        #endregion
        
        #region Private Methods
        protected override bool OnPlayerInteract(PlayerInteractPacket arg1, int arg2)
        {
            if (!PlayerChecker(arg1, out var player))
                return false;

            isOn.Value = true;
            _timeOnCounter = timeOn;
            onToggleComponent?.Invoke();
            return true;
        }
        
        private void _OnValueChanged(bool old, bool @new)
        {
            Refresh();
        }
        #endregion
    }
}