using UnityEngine;
using UnityEngine.InputSystem;

namespace Video
{
    public class MenuCameraAnim : MonoBehaviour
    {
        public GameObject virtualCamera;
        
        public void Update()
        {
            if(Keyboard.current.enterKey.wasPressedThisFrame)
                virtualCamera.SetActive(true);
        }
    }
}