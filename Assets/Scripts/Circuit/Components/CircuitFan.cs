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
        public float timer = 0f;

        [Header("SETTINGS")]
        public int tickRate = 10;
        public float windSpeed = 2f;
        public float windRadius = 1f;
        public float windHeight = 5f;
        public float activeAfter = 1.25f;
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

        private void Update()
        {
            if (isOn.Value)
                timer = activeAfter;
            else
                timer = Mathf.Max(0, timer - Time.deltaTime);    
        }

        #endregion

        #region Private Methods
        private void WindArea()
        {
            if(timer <= 0) return;

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
        public static void DrawWireCapsule(Vector3 _pos, Quaternion _rot, float _radius, float _height, Color _color = default(Color))
        {
            if (_color != default(Color))
                UnityEditor.Handles.color = _color;
            Matrix4x4 angleMatrix = Matrix4x4.TRS(_pos, _rot, UnityEditor.Handles.matrix.lossyScale);
            using (new UnityEditor.Handles.DrawingScope(angleMatrix))
            {
                var pointOffset = (_height - (_radius * 2)) / 2;

                //draw sideways
                UnityEditor.Handles.DrawWireArc(Vector3.up * pointOffset, Vector3.left, Vector3.back, -180, _radius);
                UnityEditor.Handles.DrawLine(new Vector3(0, pointOffset, -_radius), new Vector3(0, -_pos.y, -_radius));
                UnityEditor.Handles.DrawLine(new Vector3(0, pointOffset, _radius), new Vector3(0, -_pos.y, _radius));
                //UnityEditor.Handles.DrawWireArc(Vector3.down * pointOffset, Vector3.left, Vector3.back, 180, _radius);
                //draw frontways
                UnityEditor.Handles.DrawWireArc(Vector3.up * pointOffset, Vector3.back, Vector3.left, 180, _radius);
                UnityEditor.Handles.DrawLine(new Vector3(-_radius, pointOffset, 0), new Vector3(-_radius, -_pos.y, 0));
                UnityEditor.Handles.DrawLine(new Vector3(_radius, pointOffset, 0), new Vector3(_radius, -_pos.y, 0));
                //UnityEditor.Handles.DrawWireArc(Vector3.down * pointOffset, Vector3.back, Vector3.left, -180, _radius);
                //draw center
                UnityEditor.Handles.DrawWireDisc(Vector3.up * pointOffset, Vector3.up, _radius);
                UnityEditor.Handles.DrawWireDisc(Vector3.down * pointOffset, Vector3.up, _radius);

            }
        }
#endif
    }
}