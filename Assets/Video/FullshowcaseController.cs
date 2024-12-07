using System;
using System.Collections;
using UnityEngine;

namespace Video
{
    public class FullshowcaseController : MonoBehaviour
    {
        public GameObject titleArt;
        public GameObject titleCode;
        public GameObject subtitles;
        public ShowcaseController artController;
        public ShowcaseController codeController;
        
        public Material material;
        public float timeOffset = 10f;
        private static readonly int _TimeOffset = Shader.PropertyToID("_TimeOffset");

        private void OnEnable()
        {
            StartCoroutine(_Do());
        }

        private IEnumerator _Do()
        {
            titleArt.SetActive(true);
            yield return new WaitForSeconds(5f);
            titleArt.SetActive(false);
            
            subtitles.SetActive(true);
            artController.gameObject.SetActive(true);
            while (!artController.IsDone)
                yield return null;
            artController.gameObject.SetActive(false);
            subtitles.SetActive(false);
           
            material.SetFloat(_TimeOffset, material.GetFloat(_TimeOffset) + timeOffset); 
            titleCode.SetActive(true);
            yield return new WaitForSeconds(5f);
            titleCode.SetActive(false);
            
            subtitles.SetActive(true);
            codeController.gameObject.SetActive(true);
            while (!codeController.IsDone)
                yield return null;
            codeController.gameObject.SetActive(false);
            subtitles.SetActive(false);
        }
    }
}