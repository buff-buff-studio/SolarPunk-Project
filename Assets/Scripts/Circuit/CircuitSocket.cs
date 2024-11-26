using System.Collections.Generic;
using NetBuff.Components;
using UnityEngine;

namespace Solis.Circuit
{
    /// <summary>
    /// A socket on a circuit board that can be connected to other components.
    /// </summary>
    [RequireComponent( typeof(NetworkIdentity))]
    public class CircuitSocket : CircuitComponent
    {
        #region Inspector Fields
        [Header("REFERENCES")]
        public CircuitPlug @internal;
        public CircuitPlug outlet;
        
        [Header("SETTINGS")]
        public bool selfPowered;
        #endregion

        #region Abstract Methods Implementation
        public override CircuitData ReadOutput(CircuitPlug plug)
        {
            if (selfPowered)
                return new CircuitData { power = 1 };
            
            if(@internal.type == CircuitPlugType.Output)
                return outlet.ReadOutput();
            
            return @internal.ReadOutput();
        }

        protected override void OnRefresh()
        {
           
        }

        public override IEnumerable<CircuitPlug> GetPlugs()
        {
            yield return @internal;
            yield return outlet;
        }
        #endregion
        
        private Vector3 _lastPosition;

        protected override void OnEnable()
        {
            base.OnEnable();
            
            if(_lastPosition == Vector3.zero)
                _lastPosition = transform.position;
        }
        
        private void Update()
        {
            if (_lastPosition != transform.position)
                _lastPosition = transform.position;
        }
    }
}