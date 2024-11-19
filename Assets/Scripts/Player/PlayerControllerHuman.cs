using NetBuff.Misc;
using Solis.Data;
using Solis.Interface.Input;
using UnityEngine;
using UnityEngine.Serialization;

namespace Solis.Player
{
    /// <summary>
    /// Player Controller for the Human Character
    /// </summary>
    [Icon("Assets/Art/Sprites/Editor/PlayerControllerHuman_ico.png")]
    public class PlayerControllerHuman : PlayerControllerBase
    {
        public override CharacterType CharacterType => CharacterType.Human;

        [Space]
        [Header("SPECIAL")]
        public float specialCooldown = 5f;
        private BoolNetworkValue _isSpecialOn = new(false);
        private float _specialTimer;
        public GameObject cloudPrefab;
        public Vector3 cloudOffset;
        public float minDistanceToSpecial = 1f;

        [ColorUsage(false, true)]
        public Color specialOnColor, specialOffColor;

        private static readonly int EmissionColor2 = Shader.PropertyToID("_EmissionColor_2");

        protected override void OnEnable()
        {
            base.OnEnable();
            WithValues(isRespawning, isPaused, username, grapplingHookPosition, grapplingHook, _isSpecialOn);
            _isSpecialOn.OnValueChanged += _OnSpecialValueChanged;
        }

        private void _OnSpecialValueChanged(bool oldvalue, bool newvalue)
        {
            renderer.material.SetColor(EmissionColor2, newvalue ? specialOnColor : specialOffColor);
        }

        protected override void _Timer()
        {
            base._Timer();
            if (_specialTimer > 0)
            {
                _specialTimer -= Time.deltaTime;
                if (_specialTimer <= 0)
                {
                    _specialTimer = 0;
                    if (_isSpecialOn.CheckPermission())
                        _isSpecialOn.Value = true;
                }
            }
        }

        protected override void _Special()
        {
            if (SolisInput.GetKeyDown("Jump") && !IsGrounded)
            {
                if (_specialTimer > 0)
                {
                    Debug.Log("Nina Special on cooldown");
                    return;
                }
                if(Physics.Raycast(transform.position, Vector3.down, out var hit, minDistanceToSpecial))
                {
                    Debug.Log("Nina is too close to the ground, can't use special");
                    return;
                }

                _specialTimer = specialCooldown;
                _isSpecialOn.Value = false;
                Spawn(cloudPrefab, transform.position + cloudOffset, body.rotation);
            }
        }
    }

    //I only wanted to be part of something

}