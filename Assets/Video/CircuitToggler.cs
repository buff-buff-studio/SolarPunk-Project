using Solis.Circuit;
using UnityEngine;

namespace Video
{
    public class CircuitToggler : MonoBehaviour
    {
        public CircuitInteractive interactive;
        public float interval = 1f;
        
        public void Start()
        {
            InvokeRepeating(nameof(ToggleCircuit), interval, interval);
        }
        
        public void ToggleCircuit()
        {
            interactive.isOn.Value = !interactive.isOn.Value;
        }
    }
}