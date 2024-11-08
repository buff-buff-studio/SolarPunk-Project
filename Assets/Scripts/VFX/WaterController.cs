using System;
using UnityEngine;

namespace VFX
{
    [ExecuteInEditMode]
    public class WaterController : MonoBehaviour
    {
        public Vector2 flowingDirection;
        
        private static readonly int _FlowingDirection = Shader.PropertyToID("_waterFlowDirection");
        private static readonly int _WaterDirtyRegion = Shader.PropertyToID("_waterDirtyRegion");
        private static readonly int _WaterFlowingRegion = Shader.PropertyToID("_waterFlowingRegion");
        private static readonly int _WaterPipeExit = Shader.PropertyToID("_waterPipeExit");

        public WaterDirtyRegion[] dirtyRegions = Array.Empty<WaterDirtyRegion>();
        public WaterFlowingRegion[] flowingRegions = Array.Empty<WaterFlowingRegion>();
        public WaterPipeExit[] pipeExits = Array.Empty<WaterPipeExit>();

        private void Update()
        {
            Shader.SetGlobalVector(_FlowingDirection, flowingDirection);
            _DirtyRegions();
            _FlowingRegions();
            _PipeExits();
        }

        private void _DirtyRegions()
        {
            var array = Shader.GetGlobalFloatArray(_WaterDirtyRegion);
            if (array == null)
                array = new float[41];

            array[0] = dirtyRegions.Length;
            for (var i = 0; i < dirtyRegions.Length; i++)
            {
                var go = dirtyRegions[i];
                var pos = go.transform.position;
                var idx = i * 4 + 1;
                array[idx] = pos.x;
                array[idx + 1] = pos.z;
                array[idx + 2] = go.radius;
                array[idx + 3] = go.transitionRadius;
            }

            Shader.SetGlobalFloatArray(_WaterDirtyRegion, array);
        }
        
        private void _FlowingRegions()
        {
            var array = Shader.GetGlobalFloatArray(_WaterFlowingRegion);
            if (array == null)
                array = new float[61];

            array[0] = flowingRegions.Length;
            for (var i = 0; i < flowingRegions.Length; i++)
            {
                var go = flowingRegions[i];
                var pos = go.transform.position;
                var fw = go.transform.forward;
                var idx = i * 6 + 1;
                array[idx] = pos.x;
                array[idx + 1] = pos.z;
                array[idx + 2] = go.radius;
                array[idx + 3] = go.transitionRadius;
                array[idx + 4] = fw.x * go.speed;
                array[idx + 5] = fw.z * go.speed;
            }

            Shader.SetGlobalFloatArray(_WaterFlowingRegion, array);
        }
        
        private void _PipeExits()
        {
            var array = Shader.GetGlobalFloatArray(_WaterPipeExit);
            if (array == null)
                array = new float[21];

            var l = 0;
            for (var i = 0; i < pipeExits.Length; i++)
            {
                if (pipeExits[i] is null)
                    continue;
                if (pipeExits[i].gameObject.activeInHierarchy == false)
                    continue;

                var go = pipeExits[i];
                var pos = go.transform.position;
                var idx = i * 4 + 1;
                array[idx] = pos.x;
                array[idx + 1] = pos.z;
                array[idx + 2] = go.radius;
                array[idx + 3] = go.strength;
                l++;
            }

            array[0] = l;

            Shader.SetGlobalFloatArray(_WaterPipeExit, array);
        }
    }
}