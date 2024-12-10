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

        public FloatNetworkValue valuePower = new(0);
        public ColorNetworkValue valueColor = new(Color.black);

        #region Unity Callbacks
        protected override void OnEnable()
        {
            WithValues(valuePower, valueColor);
            base.OnEnable();
            
            if(invisibleOnPlay)
            {
                transform.GetChild(0).gameObject.SetActive(false);
            }
            valuePower.OnValueChanged += (_, __) => _UpdateMaterial();
            valueColor.OnValueChanged += (_, __) => _UpdateMaterial();
            _UpdateMaterial();
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

            if(!valuePower.AttachedTo)
                valuePower.AttachedTo = this;

            valuePower.Value = pow;

            if (valuePower.Value <= 0)
                return new CircuitData(false);
            else if (valuePower.Value >= powerToBreak)
                return new CircuitData(valuePower.Value,
                    new Vector3(-1, -1, -1));
            else
                return new CircuitData(valuePower.Value,
                    new Vector3(rData.ReadOutput().power, gData.ReadOutput().power, bData.ReadOutput().power));
        }
        
        protected override void OnRefresh()
        {
            if(enabled)
                UpdateColor();
        }

        private void UpdateColor()
        {
            if (!HasAuthority)
                return;
            
            var color = Color.black;
            if (valuePower.Value > 0)
            {
                color.r = rData.ReadOutput().IsPowered ? 1 : 0;
                color.g = gData.ReadOutput().IsPowered ? 1 : 0;
                color.b = bData.ReadOutput().IsPowered ? 1 : 0;
                color *= Mathf.Pow(2, colorIntensity);
            }
            
            valueColor.Value = color;
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
        public void _UpdateMaterial()
        {
#if UNITY_EDITOR
            if(PrefabStageUtility.GetCurrentPrefabStage() != null)
                return;
#endif

            if(meshRenderer == null) return;
            meshRenderer.materials[materialIndex].SetColor(EmissionColor, valueColor.Value * 1.5f);
        }
        #endregion
    }
}