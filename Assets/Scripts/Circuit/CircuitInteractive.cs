using System;
using System.Collections.Generic;
using System.Linq;
using NetBuff.Misc;
using NUnit.Framework;
using Solis.Data;
using Solis.Packets;
using Solis.Player;
using UnityEngine;
using UnityEngine.Serialization;

namespace Solis.Circuit
{
    public abstract class CircuitInteractive : CircuitComponent
    {
        #region Inspector Fields
        [Header("SETTINGS")]
        public float radius = 3f;
        public float minDistance = 1.24f;
        [UnityEngine.Range(0,1)] [Tooltip("The dot product threshold for player facing the object")]
        public float dotThreshold = 0.5f;
        public CharacterTypeFilter playerTypeFilter = CharacterTypeFilter.Both;
        public BoolNetworkValue isOn = new(false);
        #endregion

        private List<Collider> _colliders = new List<Collider>();
        private LayerMask _layerMask;
        private int _originalLayer, _ignoreRaycastLayer = 2;
        private Vector3 _objectCenter;
        [SerializeField] [Tooltip("The parent object that will be ignored by the raycast, don't add the parent object to the list")]
        private List<Collider> ignoreColliders = new List<Collider>();

        private protected PlayerControllerBase _lastPlayerInteracted;

        protected override void OnEnable()
        {
            base.OnEnable();

            WithValues(isOn);
            isOn.OnValueChanged += _OnValueChanged;

            _objectCenter = GetComponentInChildren<Collider>().bounds.center;
            _originalLayer = gameObject.layer;
            PacketListener.GetPacketListener<PlayerInteractPacket>().AddServerListener(OnPlayerInteract);
            _layerMask = ~(playerTypeFilter != CharacterTypeFilter.Both
                ? LayerMask.GetMask("Ignore Raycast", playerTypeFilter == CharacterTypeFilter.Human ? "Human" : "Robot")
                : LayerMask.GetMask("Ignore Raycast", "Human", "Robot"));
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            PacketListener.GetPacketListener<PlayerInteractPacket>().RemoveServerListener(OnPlayerInteract);
        }

        protected virtual bool OnPlayerInteract(PlayerInteractPacket arg1, int arg2)
        {
            return PlayerChecker(arg1, out var player);
        }

        protected bool PlayerChecker(PlayerInteractPacket arg1, out PlayerControllerBase player)
        {
            Debug.Log("Checking player");
            
            player = null;
            // Check if player is within radius
            var networkObject = GetNetworkObject(arg1.Id);
            var dist = Vector3.Distance(networkObject.transform.position, _objectCenter);
            if (dist > radius) return false;

            Debug.Log("Player is within radius: " + dist);
            // Check if game object has a player controller
            if(!networkObject.TryGetComponent(out player))
                return false;

            // Check if player is allowed to interact with object
            if (!playerTypeFilter.Filter(player.CharacterType))
                return false;

            _lastPlayerInteracted = player;

            // Check if player is facing the object
            var directionToTarget = _objectCenter - player.body.position;
            var dot = Vector3.Dot(player.body.forward, directionToTarget.normalized);
            if (dot < (dist <= minDistance ? dotThreshold/2 : dotThreshold))
            {
                Debug.Log("Player is not facing the object, dot: " + dot);
                return false;
            }

            //Check if have a wall between player and object
            SetGameLayerRecursive(this.gameObject, _ignoreRaycastLayer);
            Physics.Linecast(networkObject.transform.position, _objectCenter, out var hit, _layerMask);
            SetGameLayerRecursive(this.gameObject, _originalLayer);
            if (hit.collider != null)
            {
                if (hit.collider.transform == gameObject.transform.parent) return true;
                if (ignoreColliders.Contains(hit.collider)) return true;

                Debug.Log($"{hit.transform.name} is between the {player.CharacterType} and {this.name}", hit.collider.gameObject);
                return false;
            }

            return true;
        }

        private void SetGameLayerRecursive(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform child in go.transform)
            {
                SetGameLayerRecursive(child.gameObject, layer);
            }
        }

        public override CircuitData ReadOutput(CircuitPlug plug)
        {
            throw new System.NotImplementedException();
        }

        public override IEnumerable<CircuitPlug> GetPlugs()
        {
            throw new System.NotImplementedException();
        }

        protected override void OnRefresh()
        {
            throw new System.NotImplementedException();
        }

        protected virtual void _OnValueChanged(bool old, bool @new)
        {
            Refresh();
            onToggleComponent?.Invoke();
            _lastPlayerInteracted = null;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (_lastPlayerInteracted == null) return;

            Gizmos.color = Color.blue;
            Gizmos.DrawRay(_lastPlayerInteracted.body.position, _lastPlayerInteracted.body.forward * radius);
        }

        protected virtual void OnValidate()
        {
            if(Application.isPlaying) return;
            _colliders.Clear();
            _colliders.AddRange(GetComponentsInChildren<Collider>());
            if (_colliders.Count == 0)
            {
                Debug.LogError("No colliders found in children", this);
            }

        }
#endif
    }
}
