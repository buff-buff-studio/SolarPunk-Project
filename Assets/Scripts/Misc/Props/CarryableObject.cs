using System;
using Misc.Props;
using NetBuff.Components;
using NetBuff.Interface;
using NetBuff.Misc;
using Solis.Circuit;
using Solis.Circuit.Interfaces;
using Solis.Data;
using Solis.Packets;
using Solis.Player;
using UnityEngine;

namespace Solis.Misc.Props
{
    [RequireComponent(typeof(Rigidbody))]
    public class CarryableObject : InteractiveObject, ICarryableObject
    {
        #region Inspector Fields
        [Header("REFERENCES")]
        public BoolNetworkValue isOn = new(false);
        public NetworkRigidbodyTransform networkRigidbodyTransform;

        private PlayerControllerBase playerHolding;
        private Rigidbody rb;
        
        #endregion

        #region Private Fields

        private Vector3 _initialPosition;
        private Collider _collider;
        private ICarryableObject _carryableObjectImplementation;

        private static readonly Collider[] _objects = new Collider[10];
        private GameObject _boxPlace;

        public Bounds objectSize;

        #endregion

        #region Unity Callbacks

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            _collider = GetComponentInChildren<Collider>();
            _initialPosition = transform.position;
            objectSize = _collider.GetComponentInChildren<MeshRenderer>().bounds;
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            WithValues(isOn);
            
            InvokeRepeating(nameof(_PosCheck), 0, 1f);
            isOn.OnValueChanged += _OnValueChanged;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            CancelInvoke(nameof(_PosCheck));
            isOn.OnValueChanged -= _OnValueChanged;
        }

        private void Update()
        {
            if(!isOn.Value && !playerHolding) return;
            
            var ht = playerHolding.handPosition;
            var pos = ht.position;
            var fw = ht.forward;
            var dt = Time.deltaTime * 50f;
            transform.position = Vector3.MoveTowards(transform.position, pos, dt);
            transform.rotation = Quaternion.Lerp(transform.rotation, Quaternion.LookRotation(fw), dt);
        }

        private void OnTriggerEnter(Collider col)
        {
            if (col.CompareTag("DeathTrigger"))
            {
                _Reset();
            }
        }

        #endregion

        #region Network Callbacks
        
        public override void OnClientReceivePacket(IOwnedPacket packet)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
                return;
#endif

            if (packet is CarryableObjectGrabPacket grabPacket)
            {
                if (HasAuthority)
                    return;

                if (grabPacket.HandId == null)
                    playerHolding = null;
                else
                {
                    NetworkId.TryParse(grabPacket.HandId, out var handId);
                    playerHolding = GetNetworkObject((NetworkId)handId).GetComponent<PlayerControllerBase>();
                }
            }
            else if(packet is PlayerDeathPacket deathPacket)
            {
                if (HasAuthority)
                    return;

                if (playerHolding && playerHolding.Id == deathPacket.Id && isOn.Value)
                {
                    _Reset();
                }
            }else if (packet is SnapSyncPacket snapSyncPacket)
            {
                if (HasAuthority)
                    return;

                transform.position = snapSyncPacket.Position;
                transform.eulerAngles = snapSyncPacket.Rotation;
            }
        }
        
        #endregion
        
        private void _PosCheck()
        {
            if (transform.position.y < -15)
            {
                _Reset();
            }
        }
        
        private void _Reset()
        {
            transform.position = _initialPosition + Vector3.up;
            transform.rotation = Quaternion.identity;
            rb.velocity = Vector3.zero;
            if (playerHolding)
            {
                playerHolding.carriedObject = null;
                playerHolding = null;
            }
            isOn.Value = false;
        }

        private void _OnValueChanged(bool old, bool @new)
        {
            _Refresh();
        }

        private void _Refresh()
        {
            var pBody = playerHolding ? playerHolding.body : null;
            _collider.excludeLayers = !isOn.Value
                ? 0
                : LayerMask.GetMask("Trigger", playerHolding!.CharacterType == CharacterType.Human ? "Human" : "Robot");
            
            if (isOn.Value)
            {
                if (playerHolding)
                {
                    transform.position = playerHolding.handPosition.position;
                    transform.rotation = playerHolding.handPosition.rotation;

                    if (TryGetComponent(out MagneticProp prop))
                        prop.cantBeMagnetized.Value = true;
                    
                    if(_boxPlace!= null) _boxPlace.SetActive(true);
                }
            }
            else if(pBody)
            {
                var pPos = pBody.position;
                var newPos = new Vector3(pPos.x, transform.position.y, pPos.z);
                transform.position = newPos + (pBody.forward*1.25f);
                playerHolding.carriedObject = null;
            }
            else
            {
                Debug.Log("Demagnetized");
                if (TryGetComponent(out MagneticProp prop))
                    prop.cantBeMagnetized.Value = false;
            }

            playerHolding = isOn.Value ? playerHolding : null;
            
            networkRigidbodyTransform.enabled = !isOn.Value;
            rb.isKinematic = isOn.Value;
            rb.velocity = Vector3.zero;
        }

        protected override bool OnPlayerInteract(PlayerInteractPacket arg1, int arg2)
        {
            PlayerControllerBase player;

            if (isOn.Value)
            {
                if(!GetNetworkObject(arg1.Id).TryGetComponent(out player))
                    return false;

                if (playerHolding != player)
                    return false;

                playerHolding = null;
                isOn.Value = false;
                player.carriedObject = null;
                CheckIfThereIsPlace();
                ServerBroadcastPacket(new CarryableObjectGrabPacket
                {
                    Id = this.Id,
                    HandId = ""
                });

                return true;
            }

            if (!PlayerChecker(arg1, out player))
                return false;

            if(player.carriedObject)
                return false;

            playerHolding = player;
            isOn.Value = true;
            player.carriedObject = this;
            ServerBroadcastPacket(new CarryableObjectGrabPacket
            {
                Id = this.Id,
                HandId = player.Id.ToString()
            });

            return true;
        }

        private void CheckIfThereIsPlace()
        {
            var count = Physics.OverlapBoxNonAlloc(transform.position, new Vector3(0.35f, 0.35f, 0.35f), _objects,
                Quaternion.identity);
            for (int i = 0;i < count;i++)
            {
                if (_objects[i].transform.CompareTag("BoxPlace"))
                {
                    if(Vector3.Distance(_objects[i].transform.position ,transform.position) <= 1)
                    {
                        if (!_objects[i].gameObject.activeInHierarchy) return;
                        
                        Snap(_objects[i].gameObject);
                        SnapSyncPacket p = new SnapSyncPacket();
                        p.Id = Id;
                        p.Position = transform.position;
                        p.Rotation = transform.eulerAngles;
                        Sync(p);
                        return;
                    }
                }
            }
        }
        private void Snap(GameObject boxPlace)
        {
            transform.position = boxPlace.transform.position;
            transform.rotation = Quaternion.identity;
            //rb.isKinematic = true;
            boxPlace.gameObject.SetActive(false);
            _boxPlace = boxPlace.gameObject;
        }
        
        private void Sync(SnapSyncPacket p)
        {
             SendPacket(p);
        }


        public GameObject GetGameObject()
        {
            return gameObject;
        }

        #if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(Vector3.zero, objectSize.extents);
        }
        #endif
    }
}