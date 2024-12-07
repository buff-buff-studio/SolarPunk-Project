using System;
using System.Collections;
using NetBuff;
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
        public NetworkManager manager;

        private void OnEnable()
        {
            manager.StartHost();
            StartCoroutine(_Do());
        }

        private IEnumerator _Do()
        {
            titleArt.SetActive(true);
            yield return new WaitForSeconds(7.5f);
            titleArt.SetActive(false);
            
            subtitles.SetActive(true);
            artController.gameObject.SetActive(true);
            artController.Update();
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
            codeController.Update();
            while (!codeController.IsDone)
                yield return null;
            codeController.gameObject.SetActive(false);
            subtitles.SetActive(false);
        }
    }
}