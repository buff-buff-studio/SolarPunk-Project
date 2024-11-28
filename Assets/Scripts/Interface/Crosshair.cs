using System;
using Solis.Circuit;
using Solis.Data;
using UnityEngine;
using UnityEngine.UI;

namespace Solis.Interface
{
    public class Crosshair : MonoBehaviour
    {
        public static Crosshair Instance { get; private set; }

        public Image crosshairImage;
        public CharacterType characterType;

        private bool _isCrosshairEnabled = true;

        [Header("Crosshairs")]
        public Sprite defaultCrosshair;
        public Sprite ninaCrosshair;
        public Sprite ramCrosshair;
        public Sprite blockCrosshair;
        public Sprite graplingCrosshair;

        private Transform _cam;

        private void OnEnable()
        {
            _cam = Camera.main?.transform;
            Instance = this;
        }
        
        private void OnDisable()
        {
            Instance = null;
        }

        private void FixedUpdate()
        {
            if (!_isCrosshairEnabled) return;

            if (Physics.Raycast(_cam.position, _cam.forward, out var hitInfo, 100))
            {
                if (hitInfo.transform.gameObject.layer == LayerMask.NameToLayer("Grapling") && characterType == CharacterType.Robot)
                {
                    crosshairImage.sprite = graplingCrosshair;
                    return;
                }
                else if (hitInfo.transform.gameObject.layer == LayerMask.NameToLayer("Interactive"))
                {
                    if (hitInfo.transform.TryGetComponent(out CircuitInteractive interactive))
                    {
                        crosshairImage.sprite = interactive.playerTypeFilter.Filter(characterType) ? (characterType == CharacterType.Human ? ninaCrosshair : ramCrosshair) : blockCrosshair;
                        return;
                    }
                }
                crosshairImage.sprite = defaultCrosshair;
            }
        }

        public void SetCrosshairEnabled(bool enabled)
        {
            crosshairImage.gameObject.SetActive(enabled);
            _isCrosshairEnabled = enabled;
        }
    }
}