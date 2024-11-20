using System.Collections.Generic;
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
        public bool invisibleOnPlay = false;
        #endregion

        #region Unity Callbacks
        protected override void OnEnable()
        {
            base.OnEnable();
            _UpdateMaterial();
            if(invisibleOnPlay)
            {
                transform.GetChild(0).gameObject.SetActive(false);
            }
        }
        
        private void OnValidate()
        {
            materialIndex = Mathf.Clamp(materialIndex, 0, meshRenderer.sharedMaterials.Length - 1);
        }
        #endregion

        #region Abstract Methods Implementation
        public override CircuitData ReadOutput(CircuitPlug plug)
        {
            _UpdateMaterial();
            var power = input.ReadOutput().power;
            return power <= 0 ? new CircuitData(false) : new CircuitData(power, new Vector3(rData.ReadOutput().power, gData.ReadOutput().power, bData.ReadOutput().power));
        }
        
        protected override void OnRefresh()
        {
            
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
        private void _UpdateMaterial()
        {
#if UNITY_EDITOR
            if(PrefabStageUtility.GetCurrentPrefabStage() != null)
                return;
#endif

            if(meshRenderer == null) return;

            var color = Color.black;
            color.r = rData.ReadOutput().IsPowered ? 1 : 0;
            color.g = gData.ReadOutput().IsPowered ? 1 : 0;
            color.b = bData.ReadOutput().IsPowered ? 1 : 0;

            meshRenderer.materials[materialIndex].SetColor(EmissionColor, color*1.5f);
        }
        #endregion
    }
}