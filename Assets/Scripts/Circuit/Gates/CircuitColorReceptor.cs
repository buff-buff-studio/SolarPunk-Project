using System.Collections.Generic;
using TMPro;
using UnityEngine;

#if UNITY_EDITOR
using Solis.Circuit.Connections;
using UnityEditor.SceneManagement;
#endif

namespace Solis.Circuit.Gates
{
    /// <summary>
    /// Basic gate component that can be used to create simple logic circuits.
    /// </summary>
    public class CircuitColorReceptor : CircuitComponent
    {
        private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

        #region Inspector Fields
        [Header("REFERENCES")]
        public CircuitPlug input;
        public CircuitPlug output;
        public Renderer[] meshRenderers;
        public int[] materialsIndex;

        [Header("SETTINGS")]
        public Vector3 colorPassword;
        public bool invisibleOnPlay = false;
        #endregion

        private Vector3 _dataColor;

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
            for (int i = 0; i < meshRenderers.Length; i++)
            {
                materialsIndex[i] = Mathf.Clamp(materialsIndex[i], 0, meshRenderers[i].sharedMaterials.Length - 1);
            }

            _UpdateMaterial();
        }
        #endregion

        #region Abstract Methods Implementation
        public override CircuitData ReadOutput(CircuitPlug plug)
        {
            if(input.ReadOutput().power > 0)
            {
                _dataColor = (Vector3)input.ReadOutput().additionalData;
                if(_dataColor == colorPassword)
                {
                    return new CircuitData(true);
                }
            }

            return new CircuitData(false);
        }
        
        protected override void OnRefresh()
        {
            
        }

        public override IEnumerable<CircuitPlug> GetPlugs()
        {
            yield return input;
            yield return output;
        }
        #endregion

        #region Private Methods
        private void _UpdateMaterial()
        {
            var color = new Color(colorPassword.x, colorPassword.y, colorPassword.z);

#if UNITY_EDITOR
            if(PrefabStageUtility.GetCurrentPrefabStage() != null)
                return;

            input.Color = color;
            output.Color = color;
#endif
            if(!Application.isPlaying) return;
            
            if(meshRenderers.Length == 0)
                return;
            if(meshRenderers.Length != materialsIndex.Length)
                return;

            for (int i = 0; i < meshRenderers.Length; i++)
            {
                if(meshRenderers[i] == null) continue;
                var materials = meshRenderers[i].sharedMaterials;
                if(materials.Length <= materialsIndex[i]) continue;
                var material = materials[materialsIndex[i]];
                material.SetColor(EmissionColor, color);
            }
        }
        #endregion
    }
}