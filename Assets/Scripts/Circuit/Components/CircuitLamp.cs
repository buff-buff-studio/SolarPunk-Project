using System;
using System.Collections.Generic;
using NetBuff.Misc;
using UnityEngine;

namespace Solis.Circuit.Components
{
    /// <summary>
    /// Used to display the power state of a circuit
    /// </summary>
    public class CircuitLamp : CircuitComponent
    {
        #region Inspector Fields
        [Header("REFERENCES")]
        #pragma warning disable 0109
        public new Renderer renderer;
        public new Light light;
        #pragma warning restore 0109
        public CircuitPlug input;
        [Space]
        [ColorUsage(false, true)]
        public Color colorOn = Color.red;

        public bool useColorOff = false;
        [ColorUsage(false, true)]
        public Color colorOff = Color.black;
        private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

        #endregion

        private FloatNetworkValue powered = new(0);

        private Color offColor => useColorOff ? colorOff : Color.black;

        private void Awake()
        {
            WithValues(powered);
            powered.OnValueChanged += RefreshLamp;
        }

        private void Start()
        {
            OnRefresh();
            RefreshLamp(0, powered.Value);
        }

        private void RefreshLamp(float oldvalue, float newvalue)
        {
            renderer.material.SetColor(EmissionColor, newvalue > 0.5f ? colorOn : offColor);
            light.color = newvalue > 0.5f ? colorOn : offColor;
        }

        #region Abstract Methods Implementation
        public override CircuitData ReadOutput(CircuitPlug plug)
        {
            return default;
        }

        protected override void OnRefresh()
        {
            if(HasAuthority)
            {
                if (!powered.AttachedTo)
                    powered.AttachedTo = this;
                powered.Value = input.ReadOutput().power;
            }
        }

        public override IEnumerable<CircuitPlug> GetPlugs()
        {
            yield return input;
        }
        #endregion
    }
}