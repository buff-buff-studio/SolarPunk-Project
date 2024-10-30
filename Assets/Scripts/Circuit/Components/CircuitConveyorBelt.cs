using System;
using System.Collections.Generic;
using NetBuff.Misc;
using UnityEngine;

namespace Solis.Circuit.Components
{
    public class CircuitConveyorBelt : CircuitComponent
    {
        private Rigidbody _rb;
        public float speed = 3f;
        public float acceleration = 5f;
        public CircuitPlug input;
        public CircuitPlug data;

        public BoolNetworkValue isOnValue = new BoolNetworkValue();
        public FloatNetworkValue speedValue = new FloatNetworkValue();

        private float _speed;
        
        protected override void OnEnable()
        {
            WithValues(isOnValue, speedValue);
            base.OnEnable();
            _rb = gameObject.AddComponent<Rigidbody>();
            _rb.isKinematic = true;
            
            if (HasAuthority)
                speedValue.Value = isOnValue.Value ? speed : 0f;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            Destroy(_rb);
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.yellow;
            
            var t = transform;
            var pos = t.position;
            var forward = t.forward;
            var tip = pos + forward * speed;
            var right = t.right;

            var sign = -Mathf.Sign(speed);
            
            Gizmos.DrawLine(pos, tip);
            Gizmos.DrawLine(tip, tip + (sign * forward + right).normalized * 0.1f);
            Gizmos.DrawLine(tip, tip + (sign * forward - right).normalized * 0.1f);
        }

        public override CircuitData ReadOutput(CircuitPlug plug)
        {
            return new CircuitData();
        }

        public override IEnumerable<CircuitPlug> GetPlugs()
        {
            yield return input;
            yield return data;
        }

        protected override void OnRefresh()
        {
            if (!HasAuthority)
                return;
            isOnValue.Value = input.ReadOutput().power > 0.5f;
            _speed = data.ReadOutput().power < 0.5f ? speed : -speed;
        }

        private void FixedUpdate()
        {
            if (!HasAuthority)
                return;

            speedValue.Value = Mathf.Lerp(speedValue.Value,  isOnValue.Value ? _speed : 0f, Time.fixedDeltaTime * acceleration);

            var delta = transform.forward * (speedValue.Value * Time.fixedDeltaTime);
            _rb.position -= delta;
            // ReSharper disable once Unity.InefficientPropertyAccess
            _rb.MovePosition(_rb.position + delta);
        }
    }
}