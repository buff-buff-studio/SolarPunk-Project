using System;
using UnityEngine;

namespace Video
{
    public class ShowcaseController : MonoBehaviour
    {
        [Serializable]
        public struct ShowcaseData
        {
            public GameObject o;
            public string name;
            public string description;
            public bool spin;
            public Vector3 spinAxis;
        }
        
        public float spinSpeed = 1f;
        public ShowcaseData[] showcaseData;
        public int currentShowcaseIndex = 0;
        private int _lastShowcaseIndex = -1;
        
        private void Update()
        {
            if (currentShowcaseIndex != _lastShowcaseIndex)
            {
                _lastShowcaseIndex = currentShowcaseIndex;
                for (var i = 0; i < showcaseData.Length; i++)
                {
                    showcaseData[i].o.SetActive(i == currentShowcaseIndex);
                }
            }
            
            if (showcaseData[currentShowcaseIndex].spin)
            {
                showcaseData[currentShowcaseIndex].o.transform.localEulerAngles = showcaseData[currentShowcaseIndex].spinAxis * (Time.time * spinSpeed);
            }
        }
        
    }
}