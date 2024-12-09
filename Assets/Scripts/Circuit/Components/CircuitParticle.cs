using System;
using System.Collections;
using System.Collections.Generic;
using NetBuff.Misc;
using UnityEngine;

namespace Solis.Circuit.Components
{
    public class CircuitParticle : CircuitComponent
    {
        #region Inspector Fields
        [Header("REFERENCES")]
        public CircuitPlug input;
        public ParticleSystem particles;

        [Header("STATE")]
        public bool invert;
        #endregion

        private FloatNetworkValue powered = new(0);

        private void Awake()
        {
            if(!powered.AttachedTo)
                powered.AttachedTo = this;
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            WithValues(powered);
            powered.OnValueChanged += RefreshParticles;
        }

        private void Start()
        {
            OnRefresh();
            RefreshParticles(0, powered.Value);
        }

        private void RefreshParticles(float oldvalue, float newvalue)
        {
            if (newvalue > 0.5f ^ invert)
            {
                particles.Play();
            }
            else
            {
                particles.Stop();
            }
        }

        #region Abstract Methods Implementation
        public override CircuitData ReadOutput(CircuitPlug plug)
        {
            return default;
        }

        protected override void OnRefresh()
        {
            if(powered.CheckPermission())
                powered.Value = input.ReadOutput().power;
        }

        public override IEnumerable<CircuitPlug> GetPlugs()
        {
            yield return input;
        }
        #endregion
    }
}