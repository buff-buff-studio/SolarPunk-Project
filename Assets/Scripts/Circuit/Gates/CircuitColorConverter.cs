using System.Collections.Generic;
using NetBuff.Misc;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

namespace Solis.Circuit.Gates
{
    /// <summary>
    /// Basic gate component that can be used to create simple logic circuits.
    /// </summary>
    public class CircuitColorConverter : CircuitComponent
    {
        private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

        #region Inspector Fields
        [Header("REFERENCES")]
        public CircuitPlug input;
        public CircuitPlug output;
        public CircuitPlug rData, gData, bData;
        public MeshRenderer meshRenderer;
        public int materialIndex;

        [Header("SETTINGS")]
        public int powerToBreak = 2;
        [Range(1,5)]
        public float colorIntensity = 1.5f;
        public bool invisibleOnPlay = false;
        #endregion

        private FloatNetworkValue power = new(0);

        #region Unity Callbacks
        protected override void OnEnable()
        {
            base.OnEnable();
            WithValues(power);

            if(!power.AttachedTo)
                power.AttachedTo = this;

            UpdateMaterial();
            if(invisibleOnPlay)
            {
                transform.GetChild(0).gameObject.SetActive(false);
            }

            power.OnValueChanged += (_, __) => UpdateMaterial();
        }
        
        private void OnValidate()
        {
            materialIndex = Mathf.Clamp(materialIndex, 0, meshRenderer.sharedMaterials.Length - 1);
        }
        #endregion

        #region Abstract Methods Implementation
        public override CircuitData ReadOutput(CircuitPlug plug)
        {
            var count = input.Connections.Length;
            var pow = 0f;
            for(var i = 0; i < count; i++)
                pow += input.ReadOutput(i).power;

            if(!power.AttachedTo)
                power.AttachedTo = this;

            power.Value = pow;

            if (power.Value <= 0)
                return new CircuitData(false);
            else if (power.Value >= powerToBreak)
                return new CircuitData(power.Value,
                    new Vector3(-1, -1, -1));
            else
                return new CircuitData(power.Value,
                    new Vector3(rData.ReadOutput().power, gData.ReadOutput().power, bData.ReadOutput().power));
        }
        
        protected override void OnRefresh()
        {
            UpdateMaterial();
        }

        public override IEnumerable<CircuitPlug> GetPlugs()
        {
            yield return input;
            yield return output;

            yield return rData;
            yield return gData;
            yield return bData;
        }
        #endregion

        #region Private Methods
        public void UpdateMaterial()
        {
#if UNITY_EDITOR
            if(PrefabStageUtility.GetCurrentPrefabStage() != null)
                return;
#endif

            if(meshRenderer == null) return;

            var color = Color.black;
            if (power.Value > 0)
            {
                color.r = rData.ReadOutput().IsPowered ? 1 : 0;
                color.g = gData.ReadOutput().IsPowered ? 1 : 0;
                color.b = bData.ReadOutput().IsPowered ? 1 : 0;
                color *= Mathf.Pow(2, colorIntensity);
            }

            meshRenderer.materials[materialIndex].SetColor(EmissionColor, color*1.5f);
        }
        #endregion
    }
}