using System;
using System.Collections.Generic;
using NetBuff.Misc;
using UnityEngine;

namespace Solis.Circuit.Components
{
    public class CircuitConveyorBelt : CircuitComponent
    {
        private Rigidbody _rb;
        private Collider _collider;
        public Renderer conveyorRenderer;
        public int[] materialIndices = new int[1]{1};
        public float speed = 3f;
        public float acceleration = 5f;
        public CircuitPlug input;
        public CircuitPlug data;

        public BoolNetworkValue isOnValue = new(false);
        public FloatNetworkValue speedValue = new(0);

        public List<CircuitConveyorBelt> connectedBelts;

        public bool ColliderEnabled
        {
            get => _collider.enabled;
            set => _collider.enabled = value;
        }

        private float _speed;

        private static readonly int Inverse = Shader.PropertyToID("_Inverse");
        private static readonly int IsOff = Shader.PropertyToID("_IsOff");
        private static readonly int Speed = Shader.PropertyToID("_ScrollSpeedY");

        protected override void OnEnable()
        {
            WithValues(isOnValue, speedValue);
            base.OnEnable();
            _rb = gameObject.AddComponent<Rigidbody>();
            TryGetComponent(out _collider);
            _rb.isKinematic = true;
            
            if (HasAuthority)
                speedValue.Value = isOnValue.Value ? speed : 0f;

            if(conveyorRenderer)
            {
                isOnValue.OnValueChanged += IsOnValueChanged;
                speedValue.OnValueChanged += SpeedValueOnValueChanged;
            }

        }

        private void SpeedValueOnValueChanged(float oldvalue, float newvalue)
        {
            foreach (var i in materialIndices)
            {
                conveyorRenderer.materials[i].SetInt(Inverse, newvalue < 0 ? 0 : 1);
            }
        }

        private void IsOnValueChanged(bool oldvalue, bool newvalue)
        {
            foreach (var i in materialIndices)
            {
                conveyorRenderer.materials[i].SetInt(IsOff, !newvalue ? 1 : 0);
            }
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

            if (!isOnValue.AttachedTo || !speedValue.AttachedTo)
            {
                isOnValue.AttachedTo = this;
                speedValue.AttachedTo = this;
            }

            isOnValue.Value = input.ReadOutput().power > 0.5f;
            _speed = data.ReadOutput().power < 0.5f ? speed : -speed;
            connectedBelts.ForEach(belt => belt.OnRefresh());
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