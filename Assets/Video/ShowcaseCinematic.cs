using System;
using Solis.Misc.Multicam;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Video
{
    public class ShowcaseCinematic : MonoBehaviour
    {
        public CinematicController controller;

        private void Update()
        {
            QualitySettings.SetQualityLevel(QualitySettings.count - 1);
            if(Keyboard.current.aKey.wasPressedThisFrame)
                controller.Play();
        }
    }
}