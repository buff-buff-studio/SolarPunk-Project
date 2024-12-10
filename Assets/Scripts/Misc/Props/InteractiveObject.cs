using System;
using NetBuff.Components;
using NetBuff.Misc;
using Solis.Data;
using Solis.Packets;
using Solis.Player;
using UnityEngine;

namespace Misc.Props
{
    public class InteractiveObject : NetworkBehaviour
    {
        [Header("SETTINGS")]
        public float radius = 3f;
        [Range(0,1)] [Tooltip("The dot product threshold for player facing the object")]
        public float dotThreshold = 0.5f;
        public CharacterTypeFilter playerTypeFilter = CharacterTypeFilter.Both;

        private protected LayerMask _layerMask;
        private int _originalLayer, _ignoreRaycastLayer = 2;

        protected virtual void OnEnable()
        {
            PacketListener.GetPacketListener<PlayerInteractPacket>().AddServerListener(OnPlayerInteract);
            Debug.Log("Interactive object enabled: " + this);

            _originalLayer = gameObject.layer;
            _layerMask = ~(playerTypeFilter != CharacterTypeFilter.Both
                ? LayerMask.GetMask("Ignore Raycast", "Trigger", "CubeTrigger", "PressurePlate", playerTypeFilter == CharacterTypeFilter.Human ? "Human" : "Robot")
                : LayerMask.GetMask("Ignore Raycast", "Trigger", "CubeTrigger", "PressurePlate", "Human", "Robot"));
        }

        protected virtual void OnDisable()
        {
            PacketListener.GetPacketListener<PlayerInteractPacket>().RemoveServerListener(OnPlayerInteract);
            Debug.Log("Interactive object disabled: " + this);
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
            var dist = Vector3.Distance(networkObject.transform.position, transform.position);
            if (dist > radius) return false;

            // Check if game object has a player controller
            if(!networkObject.TryGetComponent(out player))
                return false;

            // Check if player is allowed to interact with object
            if (!playerTypeFilter.Filter(player.CharacterType))
            {
                Debug.LogWarning($"{player.CharacterType} is not allowed to interact with the object", this);
                return false;
            }

            // Check if player is facing the object
            var directionToTarget = transform.position - player.body.position;
            var dot = Vector3.Dot(player.body.forward, directionToTarget.normalized);
            if (dot < dotThreshold)
            {
                Debug.LogWarning($"{player.CharacterType} is not facing the object, dot: " + dot, this);
                return false;
            }

            //Check if have a wall between player and object
            SetGameLayerRecursive(this.gameObject, _ignoreRaycastLayer);
            Physics.Linecast(networkObject.transform.position, transform.position, out var hit, _layerMask);
            SetGameLayerRecursive(this.gameObject, _originalLayer);
            if (hit.collider != null)
            {
                Debug.Log($"{hit.transform.name} is between the {player.CharacterType} and {this.name}", hit.collider.gameObject);
                return false;
            }

            Debug.LogWarning($"{player.CharacterType} is allowed to interact with the object", this);
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
    }
}
