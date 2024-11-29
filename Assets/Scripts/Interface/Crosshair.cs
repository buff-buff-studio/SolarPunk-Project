using System;
using Misc.Props;
using NetBuff.Components;
using Solis.Circuit;
using Solis.Data;
using Solis.Interface.Input;
using Solis.Misc.Props;
using UnityEngine;
using UnityEngine.UI;

namespace Solis.Interface
{
    public class Crosshair : MonoBehaviour
    {
        public static Crosshair Instance { get; private set; }

        public Image crosshairImage;
        public CharacterType characterType;

        private bool _isCrosshairEnabled = false;

        [Header("Crosshairs")]
        public Sprite defaultCrosshair;
        public Sprite ninaCrosshair;
        public Sprite ramCrosshair;
        public Sprite blockCrosshair;
        public Sprite graplingCrosshair;

        private Transform _cam;
        private Transform _lastHitObject;

        private Sprite GetCharacterCrosshair => characterType == CharacterType.Human ? ninaCrosshair : ramCrosshair;

        private void OnEnable()
        {
            _cam = Camera.main?.transform;
            SetCrosshaiActive(false);
            Instance = this;
        }
        
        private void OnDisable()
        {
            Instance = null;
        }

        private void Update()
        {
            if (!_isCrosshairEnabled) return;

            if(SolisInput.GetVector2("Look").magnitude < 0.05f) return;

            UpdateCrosshair();
        }

        private void UpdateCrosshair()
        {
            if (Physics.Raycast(_cam.position, _cam.forward, out var hitInfo, 40))
            {
                if(_lastHitObject == hitInfo.transform) return;
                _lastHitObject = hitInfo.transform;

                Debug.Log("Focused on " + hitInfo.transform.name, hitInfo.transform);

                if (hitInfo.transform.CompareTag("Grappling"))
                {
                    crosshairImage.sprite = characterType == CharacterType.Robot ? graplingCrosshair : blockCrosshair;
                    Debug.Log("Focused on grappling");
                    return;
                }

                if (hitInfo.transform.CompareTag("Interactive"))
                {
                    Debug.Log("Focused on interactive tag");
                    var hit = hitInfo.transform;
                    if(!hit.TryGetComponent(out CircuitInteractive circuitInteractive))
                    {
                        var childs = hit.childCount;
                        if (childs > 0) circuitInteractive = hitInfo.transform.GetComponentInChildren<CircuitInteractive>();
                        if (!circuitInteractive)
                        {
                            while (true)
                            {
                                hit = hit.parent;
                                if (hit.CompareTag("Interactive"))
                                {
                                    if (hit.TryGetComponent(out circuitInteractive)) break;
                                }
                                else break;
                            }
                        }
                    }
                    if (circuitInteractive)
                    {
                        crosshairImage.sprite = crosshairImage.sprite = circuitInteractive.playerTypeFilter.Filter(characterType)
                            ? GetCharacterCrosshair
                            : blockCrosshair;
                        Debug.Log("Focused on circuit interactive", circuitInteractive);
                        return;
                    }
                }

                if(hitInfo.transform.CompareTag("Carryable"))
                {
                    Debug.Log("Focused on carryable tag");

                    var hit = hitInfo.transform;
                    if(!hit.TryGetComponent(out InteractiveObject interactiveObject))
                    {
                        var childs = hit.childCount;
                        if (childs > 0) interactiveObject = hitInfo.transform.GetComponentInChildren<InteractiveObject>();
                        if (!interactiveObject)
                        {
                            while (true)
                            {
                                hit = hit.parent;
                                if (hit.CompareTag("Carryable"))
                                {
                                    if (hit.TryGetComponent(out interactiveObject)) break;
                                }
                                else break;
                            }
                        }
                    }
                    if (interactiveObject)
                    {
                        crosshairImage.sprite = interactiveObject.playerTypeFilter.Filter(characterType)
                            ? GetCharacterCrosshair
                            : blockCrosshair;
                        Debug.Log("Focused on interactive object", interactiveObject);
                        return;
                    }
                }

                crosshairImage.sprite = defaultCrosshair;
            }
        }

        public void SetCrosshaiActive(bool enabled)
        {
            crosshairImage.gameObject.SetActive(enabled);
            _isCrosshairEnabled = enabled;

            if (enabled)
            {
                _lastHitObject = null;
                UpdateCrosshair();
            }
        }
    }
}