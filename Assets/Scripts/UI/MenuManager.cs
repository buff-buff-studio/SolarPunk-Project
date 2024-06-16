using System;
using System.Collections;
using System.Collections.Generic;
using Solis.Audio;
using TMPro;
using UnityEngine;

namespace Solis.UI
{
    public class MenuManager : WindowManager
    {
        public Transform camTarget;
        public Transform camMainMenu, camOtherMenu;
        
        public TextMeshProUGUI versionText;

        private void Awake()
        {
            versionText.text = $"V: {Application.version}";
            camTarget.position = camMainMenu.position;

            onChangeWindow += ChangeCameraTarget;
        }

        private void ChangeCameraTarget(int index)
        {
            camTarget.position = index switch
            {
                0 or 1 => camMainMenu.position,
                _ => camOtherMenu.position
            };
        }
        
        public void ExitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
        }
#if UNITY_EDITOR
        protected override void OnValidate()
        {
            if (versionText != null) versionText.text = $"V: {Application.version}";
            base.OnValidate();
            if (!Application.isPlaying)
            {
                ChangeCameraTarget(currentIndex);
            }
        }
#endif
    }
}