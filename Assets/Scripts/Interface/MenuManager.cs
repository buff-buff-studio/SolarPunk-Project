using System;
using System.Collections;
using System.Collections.Generic;
using Solis.Audio;
using Solis.i18n;
using Solis.Misc.Integrations;
using TMPro;
using UnityEngine;

namespace Solis.Interface
{
    public class MenuManager : WindowManager
    {
        public static bool ShowError { get; set; }
        public static string ErrorMessage { get; set; }
        
        public Transform camTarget;
        public Transform camMainMenu, camOtherMenu;
        
        public TextMeshProUGUI versionText;

        public TMP_Text networkErrorDescription;

        private void Awake()
        {
            versionText.text = $"V: {Application.version}";
            camTarget.position = camMainMenu.position;

            onChangeWindow += ChangeCameraTarget;
        }

        protected override void Start()
        {
            if (ShowError)
            {
                startIndex = 6;
                networkErrorDescription.text = LanguagePalette.Localize(ErrorMessage);
                ShowError = false; 
            }
                
            base.Start();
            DiscordController.Instance!.SetMenuActivity();
        }

        private void ChangeCameraTarget(int index)
        {
            camTarget.position = index switch
            {
                0 or 1 => camMainMenu.position,
                _ => camOtherMenu.position
            };
        }

        public void OpenURL(string url)
        {
            Application.OpenURL(url);
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