using NetBuff.Misc;
using Solis.Data;
using Solis.Packets;
using Solis.Player;
using UnityEditor;
using UnityEngine;

namespace UI
{
    public class DialogPlayerClick : DialogPlayerBase
    { 
        public float radius = 2;
        public float minDistance = 1.24f;

        private Vector3 _objectCenter;
        private int _originalLayer, _ignoreRaycastLayer = 2;
        protected void OnEnable()
        {
            PacketListener.GetPacketListener<PlayerInteractPacket>().AddServerListener(OnClickDialog);
            _objectCenter = GetComponentInChildren<Collider>().bounds.center;
            _originalLayer = gameObject.layer;
        }
    
        protected void OnDisable()
        {
            PacketListener.GetPacketListener<PlayerInteractPacket>().RemoveServerListener(OnClickDialog);
        }
        
        private bool OnClickDialog(PlayerInteractPacket arg1, int arg2)
        {
            if (IsDialogPlaying) return false;
            /*if (!PlayerChecker(arg1))
        {
            Debug.Log("Click");
            return false;
        }*/
        
            
            PlayDialog();
            return true;
        }

        private bool PlayerChecker(PlayerInteractPacket arg1)
        {
            // Check if player is within radius
            var networkObject = GetNetworkObject(arg1.Id);
            var dist = Vector3.Distance(networkObject.transform.position, _objectCenter);
            if (dist > radius) return false;

            // Check if game object has a player controller
            if(!networkObject.TryGetComponent(out PlayerControllerBase player))
                return false;

            // Check if player is facing the object
            var directionToTarget = _objectCenter - player.body.position;
            var dot = Vector3.Dot(player.body.forward, directionToTarget.normalized);
            if (dot < (dist <= minDistance ? 0.2f : 0.5f))
            {
                Debug.Log("Player is not facing the object, dot: " + dot);
                return false;
            }

            //Check if have a wall between player and object
            SetGameLayerRecursive(this.gameObject, _ignoreRaycastLayer);
            Physics.Linecast(networkObject.transform.position, _objectCenter, out var hit,
                ~(LayerMask.GetMask("Ignore Raycast", player.CharacterType == CharacterType.Human ? "Human" : "Robot")));
            SetGameLayerRecursive(this.gameObject, _originalLayer);
            if (hit.collider != null)
            {
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
    
#if UNITY_EDITOR
    
        [CustomEditor(typeof(DialogPlayerClick)),CanEditMultipleObjects]
        public class DialogPlayerEditor : Editor
        {
            private DialogPlayerClick targetClass;

            private void OnEnable()
            {
                targetClass = target as DialogPlayerClick;
            }

            void OnSceneGUI()
            {
                var transform = targetClass.transform;
                targetClass.radius = Handles.RadiusHandle(
                    transform.rotation, 
                    transform.position, 
                    targetClass.radius);
            }
        }
#endif
    }
}
