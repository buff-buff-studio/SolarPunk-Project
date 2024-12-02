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
        public GameObject cloudPrefab;
        public Vector3 cloudOffset;
        public float minDistanceToSpecial = 1f;

        private bool _isSpecialBlocked;
        private BoolNetworkValue _isSpecialOn = new(false);
        private float _specialTimer;

        [ColorUsage(false, true)]
        public Color specialOnColor, specialOffColor;

        private static readonly int EmissionColor2 = Shader.PropertyToID("_EmissionColor_2");

        protected override void OnEnable()
        {
            base.OnEnable();
            WithValues(isRespawning, isPaused, username, _isSpecialOn);
            _isSpecialOn.OnValueChanged += _OnSpecialValueChanged;
            _isSpecialBlocked = false;
        }

        private void _OnSpecialValueChanged(bool oldvalue, bool newvalue)
        {
            renderer.material.SetColor(EmissionColor2, newvalue ? specialOnColor : specialOffColor);
        }

        protected override void _Timer()
        {
            base._Timer();

            if(_flyMode || _isSpecialBlocked) return;
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
            if(_flyMode || _isSpecialBlocked)
            {
                _specialTimer = specialCooldown / 2;
                _isSpecialOn.Value = false;
                return;
            }

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

                animator.SetTrigger("ShootCloud");
                _isSpecialOn.Value = false;
                velocity = new Vector3(0, 1, 0);
                IsJumping = true;
                state = State.SpawnCloud;
            }
        }

        public void SpawnCloud()
        {
            state = State.Normal;
            _isJumpCut = true;
            _specialTimer = specialCooldown;
            Spawn(cloudPrefab, transform.position + cloudOffset, body.rotation);
        }

        private void OnTriggerEnter(Collider col)
        {
            if (col.CompareTag("SpecialController"))
            {
                _isSpecialBlocked = true;
                _specialTimer = specialCooldown / 2;
                _isSpecialOn.Value = false;
                renderer.material.SetColor(EmissionColor2, specialOffColor);
                Debug.Log("Nina Special is blocked");
            }
        }

        private void OnTriggerExit(Collider col)
        {
            if (col.CompareTag("SpecialController"))
            {
                _isSpecialBlocked = false;
                Debug.Log("Nina Special is unblocked");
            }
        }
    }

    //I only wanted to be part of something

}