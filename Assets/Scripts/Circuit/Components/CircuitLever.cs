using System.Collections.Generic;
using NetBuff.Misc;
using Solis.Data;
using Solis.Packets;
using Solis.Player;
using UnityEngine;

namespace Solis.Circuit.Components
{
    /// <summary>
    /// A lever that can be toggled on and off by players.
    /// </summary>
    public class CircuitLever : CircuitInteractive
    {
        #region Inspector Fields
        [Header("REFERENCES")]
        public BoolNetworkValue isOn = new(false);
        public CircuitPlug output;
        public Transform handle;
        
        [Header("SETTINGS")]
        public float handleAngle = 60;
        #endregion

        #region Unity Callbacks
        protected override void OnEnable()
        {
            base.OnEnable();
            WithValues(isOn);

            isOn.OnValueChanged += _OnValueChanged;

            handle.localEulerAngles = new Vector3(isOn.Value ? handleAngle : 0, 0, 0);
        }

        protected void Update()
        {
            handle.localRotation = Quaternion.Lerp(handle.localRotation, Quaternion.Euler(isOn.Value ? handleAngle : -handleAngle, 0, 0), Time.deltaTime * 10);
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
            return new CircuitData(isOn.Value);
        }

        protected override void OnRefresh() { }

        public override IEnumerable<CircuitPlug> GetPlugs()
        {
            yield return output;
        }
        #endregion

        public void ChangeState(bool state)
        {
            if(!IsServer) return;

            isOn.Value = state;
        }

        public void ChangeState()
        {
            if(!IsServer) return;

            isOn.Value = !isOn.Value;
        }

        #region Private Methods

        protected override bool OnPlayerInteract(PlayerInteractPacket arg1, int arg2)
        {
            if (!PlayerChecker(arg1, out var player))
                return false;
            isOn.Value = !isOn.Value;
            return true;
        }

        private void _OnValueChanged(bool old, bool @new)
        {
            Refresh();
            onToggleComponent?.Invoke();
        }
        #endregion
    }
}