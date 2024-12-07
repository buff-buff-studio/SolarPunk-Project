using System;
using NetBuff;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Video
{
    public class ShowcaseController : MonoBehaviour
    {
        [Serializable]
        public struct ShowcaseData
        {
            public GameObject[] o;
            public string name;
            public string description;
            public bool spin;
            public Vector3 spinAxis;
            public float time;
        }
        
        public float spinSpeed = 1f;
        public ShowcaseData[] showcaseData;
        public int currentShowcaseIndex = 0;
        private int _lastShowcaseIndex = -1;
        public TMP_Text text;
        public Material material;
        public float timeOffset = 10f;
        private static readonly int _TimeOffset = Shader.PropertyToID("_TimeOffset");

        private float _timer = 0f;
        
        public bool IsDone { get; private set; }

        public void Next()
        {
            _timer = 0;
            if (currentShowcaseIndex == showcaseData.Length - 1)
                IsDone = true;
            else
                currentShowcaseIndex = (currentShowcaseIndex + 1) % showcaseData.Length;
        }

        public void Update()
        {
            _timer += Time.deltaTime;
            if (_timer > showcaseData[currentShowcaseIndex].time)
            {
                Next();
            }

            if (Keyboard.current.aKey.wasPressedThisFrame)
            {
                Next();
            }
            
            if (currentShowcaseIndex != _lastShowcaseIndex)
            {
                _lastShowcaseIndex = currentShowcaseIndex;
                for (var i = 0; i < showcaseData.Length; i++)
                {
                    foreach(var go in showcaseData[i].o)
                        go.SetActive(i == currentShowcaseIndex);
                }
                
                var title = showcaseData[currentShowcaseIndex].name;
                var desc = showcaseData[currentShowcaseIndex].description;
                text.text = $"<size=70><b>{title}</b></size>\n{desc}";
                
                material.SetFloat(_TimeOffset, material.GetFloat(_TimeOffset) + timeOffset);
            }
            
            if (showcaseData[currentShowcaseIndex].spin)
            {
                foreach(var go in showcaseData[currentShowcaseIndex].o)
                    go.transform.localEulerAngles = showcaseData[currentShowcaseIndex].spinAxis * (Time.time * spinSpeed);
            }
        }
        
    }
}