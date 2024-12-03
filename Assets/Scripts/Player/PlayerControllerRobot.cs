using System.IO;
using NetBuff.Interface;
using NetBuff.Misc;
using Solis.Circuit.Interfaces;
using Solis.Data;
using Solis.Interface.Input;
using UnityEngine;
using UnityEngine.UIElements;

namespace Solis.Player
{
    /// <summary>
    /// Player Controller for the Robot Character
    /// </summary>
    [Icon("Assets/Art/Sprites/Editor/PlayerControllerRobot_ico.png")]
    public class PlayerControllerRobot : PlayerControllerBase, IMagneticObject, IHeavyObject
    {
        #region Grappling Hook
        [Header("GRAPPLING HOOK")]
        public float grapplingMaxDistance = 30f;
        public LayerMask grapplingHookMask;
        public LineRenderer grapplingLine;

        public Transform attachedTo;
        public Vector3 attachedToLocalPoint;

        [Range(0,2)]
        public float grapplingVelocity = 20f;
        public float grapplingAnimationTime = 1f;
        public FloatNetworkValue grapplingHook = new(0f);
        public Vector3NetworkValue grapplingHookPosition = new(Vector3.zero);
        #endregion
        
        public override CharacterType CharacterType => CharacterType.Robot;
        public Transform diluvioPosition;
        private float _grapplingAnimationTimer;
        private bool _isHooking;

        protected override void OnEnable()
        {
            base.OnEnable();
            
            WithValues(isRespawning, isPaused, username, grapplingHookPosition, grapplingHook);
            grapplingHook.OnValueChanged += (old, @new) => grapplingLine.enabled = @new > 0;
        }

        protected override void _Timer()
        {
            base._Timer();

            if(!attachedTo && _isHooking) return;

            if (_grapplingAnimationTimer > 0)
                _grapplingAnimationTimer -= Time.deltaTime;
            else
            {
                _isHooking = true;
                _grapplingAnimationTimer = 0;
            }
        }
        
        #region Grappling Hook
        
        protected override void GrapplingHook()
        {
            if (SolisInput.GetKeyDown("GrapplingHook"))
            {
                //raycast grappling hook
                var camRay = Camera.main.ScreenPointToRay(new Vector3(Screen.width / 2f, Screen.height / 2f, 0));
                var ray = new Ray(transform.position + camRay.direction, camRay.direction);
                if (Physics.Raycast(ray, out var hit, 100, grapplingHookMask))
                {
                    Debug.Log("Distance: " + Vector3.Distance(transform.position, hit.point));
                    if(grapplingMaxDistance < Vector3.Distance(transform.position, hit.point))
                        return;

                    animator.SetBool("Hooking", true);
                    attachedTo = hit.transform;
                    attachedToLocalPoint = attachedTo.InverseTransformPoint(attachedTo.childCount > 0 ? hit.transform.GetChild(0).position : hit.point);
                    state = State.GrapplingHook;
                    _isHooking = false;
                    _grapplingAnimationTimer = grapplingAnimationTime;

                    SetFocus(false);
                    SendPacket(new PlayerGrapplingHookPacket
                    {
                        Id = Id,
                        Grappled = true
                    });
                }
                else
                {
                    attachedTo = null;
                }
            }
        }

        protected override void ExitGrapplingHook()
        {
            var delta = grapplingHookPosition.Value - transform.position;
            var direction = delta.normalized;
            var v = 10 * grapplingHook.Value * direction;
            
            velocity = v;
            state = State.Normal;

            animator.SetBool("Hooking", false);
            SendPacket(new PlayerGrapplingHookPacket
            {
                Id = Id,
                Grappled = false
            });

            grapplingHook.Value = 0f;
            _isHooking = false;
        }

        protected override void HandleGrapplingHook()
        {
            if (!_isHooking) return;

            var deltaTime = Time.fixedDeltaTime;
            grapplingHook.Value = Mathf.Lerp(grapplingHook.Value,  1f, deltaTime * grapplingVelocity);
            grapplingHookPosition.Value = attachedTo.TransformPoint(attachedToLocalPoint);

            HandleGrapplingHookRemote();

            var delta = grapplingHookPosition.Value - grapplingLine.transform.position;
            var direction = delta.normalized;
            var v = 10 * grapplingHook.Value * direction;

            controller.Move(v * deltaTime);
            velocity = Vector3.zero;
                
            if (delta.magnitude < 0.5f)
            {
                ExitGrapplingHook();
            }
            else
            {
                var targetRotation = Quaternion.LookRotation(delta);
                var targetEuler = targetRotation.eulerAngles;

                transform.eulerAngles = new Vector3(0,
                    Mathf.LerpAngle(transform.eulerAngles.y, 0, Time.deltaTime * 20), 0);
                body.localEulerAngles = new Vector3(0,
                    Mathf.LerpAngle(body.localEulerAngles.y, targetEuler.y, deltaTime * 20), 0);
            }
        }

        protected override void HandleGrapplingHookRemote()
        {
            var start = grapplingLine.transform.position;
            grapplingLine.SetPositions(new[] {Vector3.Lerp(start, grapplingHookPosition.Value, grapplingHook.Value), start});
        }
        #endregion

        #region IMagneticObject Implementation
        public void Magnetize(GameObject magnet, Transform anchor)
        {
            magnetAnchor = anchor;
            state = State.Magnetized;
        }

        public void Demagnetize(GameObject magnet, Transform anchor)
        {
            magnetAnchor = null;
        }

        public Transform GetCurrentAnchor()
        {
            return magnetAnchor;
        }

        public GameObject GetGameObject()
        {
            return gameObject;
        }

        public bool CanBeMagnetized()
        {
            return true;
        }

        #endregion
    }

    public class PlayerGrapplingHookPacket : IOwnedPacket
    {
        public NetworkId Id { get; set; }
        public bool Grappled { get; set; }
        
        public void Serialize(BinaryWriter writer)
        {
            Id.Serialize(writer);
            writer.Write(Grappled);
        }

        public void Deserialize(BinaryReader reader)
        {
            Id = NetworkId.Read(reader);
            Grappled = reader.ReadBoolean();
        }
    }
}