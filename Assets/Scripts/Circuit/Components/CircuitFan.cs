using System;
using System.Collections;
using System.Collections.Generic;
using NetBuff.Misc;
using Solis.Circuit.Interfaces;
using Solis.Data;
using Solis.Misc;
using Solis.Player;
using UnityEngine;
using UnityEngine.Serialization;

namespace Solis.Circuit.Components
{
    public class CircuitFan : CircuitComponent
    {
        #region Inspector Fields
        [Header("REFERENCES")]
        public CircuitPlug input;
        public WindmillRotator windmillRotator;
        public ParticleSystem windParticles;

        [Header("STATE")]
        public BoolNetworkValue isOn = new(false);

        [Header("SETTINGS")]
        public int tickRate = 10;
        public float windSpeed = 2f;
        public float windRadius = 1f;
        public float windHeight = 5f;
        #endregion

        #region Unity Callbacks
        protected override void OnEnable()
        {
            WithValues(isOn);
            base.OnEnable();

            windmillRotator.ChangeState(isOn.Value, true);
            if (isOn.Value) windParticles.Play();
            else windParticles.Stop();

            _OnValueChanged(isOn.Value, isOn.Value);
            isOn.OnValueChanged += _OnValueChanged;

            InvokeRepeating(nameof(WindArea), 0, 1f / tickRate);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            isOn.OnValueChanged -= _OnValueChanged;
            CancelInvoke(nameof(WindArea));
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

        #region Private Methods
        private void WindArea()
        {
            if(isOn.Value == false) return;

            Collider[] results = new Collider[16];
            var count = Physics.OverlapCapsuleNonAlloc(transform.position, transform.position + Vector3.up * windHeight, windRadius, results);
            var speed = (windSpeed * windmillRotator.Power) * 1f/(float)tickRate;

            for (var i = 0; i < count; i++)
            {
                if (results[i] == null)
                    continue;

                if(results[i].TryGetComponent(out IHeavyObject _))
                    continue;

                var rb = results[i].attachedRigidbody;
                if (rb == null)
                {
                    if (results[i].TryGetComponent(out PlayerControllerBase p))
                    {
                        if (p.IsJumping)
                        {
                            p.velocity.y = 0;
                            p.IsJumping = false;
                            continue;
                        }

                        p.velocity.y += speed;
                        continue;
                    }
                    else continue;
                }

                var force = Vector3.up * speed;
                rb.AddForce(force, ForceMode.Impulse);
                Debug.Log("Object is being pushed by wind");
            }
        }

        private void _OnValueChanged(bool old, bool now)
        {
            windmillRotator.ChangeState(now);

            if (now) windParticles.Play();
            else windParticles.Stop();

            if (now) onToggleComponent.Invoke();
        }
        #endregion

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            var center = transform.position + ((Vector3.up * windHeight) / 2);
            DrawWireCapsule(center, transform.rotation, windRadius, windHeight);
        }
#endif
    }
}