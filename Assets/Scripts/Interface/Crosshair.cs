using System;
using UnityEngine;

namespace Solis.Interface
{
    public class Crosshair : MonoBehaviour
    {
        public static Crosshair Instance { get; private set; }
        
        public GameObject[] crosshairs;

        private void OnEnable()
        {
            Instance = this;
        }
        
        private void OnDisable()
        {
            Instance = null;
        }
        
        public void SetCrosshairEnabled(bool enabled)
        {
            foreach (var crosshair in crosshairs)
            {
                crosshair.SetActive(enabled);
            }
        }
    }
}