using NetBuff.Components;
using Solis.Packets;
using Solis.Player;
using Unity.VisualScripting;
using UnityEngine;

namespace Solis.Player
{
    [Icon("Assets/Art/Sprites/Editor/DeathTrigger_ico.png")]
    [RequireComponent(typeof(Collider))]
    public class DeathTrigger : NetworkBehaviour
    {
        [SerializeField] private protected PlayerControllerBase.Death _type;

#if UNITY_EDITOR
        private protected Collider _collider;

        protected virtual void OnDrawGizmos()
        {
            Gizmos.color = _type == PlayerControllerBase.Death.Fall
                ? new Color(1, 0, 0, .25f)
                : new Color(1, .25f, 0, .25f);

            if(_collider == null) _collider = gameObject.AddComponent<MeshCollider>();

            switch (_collider.GetType().Name)
            {
                case "BoxCollider":
                    Gizmos.DrawCube(transform.position, transform.localScale);
                    Gizmos.DrawWireCube(transform.position, transform.localScale);
                    break;
                case "SphereCollider":
                    Gizmos.DrawSphere(transform.position, transform.localScale.x);
                    break;
                case "CapsuleCollider":
                    var capsule = (CapsuleCollider) _collider;
                    Gizmos.DrawWireCube(transform.position, new Vector3(capsule.radius, capsule.height, capsule.radius));
                    break;
                case "MeshCollider":
                    Gizmos.DrawWireMesh(((MeshCollider) _collider).sharedMesh, transform.position, transform.rotation, transform.localScale);
                    break;
            }
        }

        protected virtual void OnValidate()
        {
            if(Application.isPlaying) return;
            TryGetComponent(out _collider);
            _collider.isTrigger = true;
            gameObject.tag = "DeathTrigger";
            gameObject.layer = LayerMask.NameToLayer("Trigger");
        }

#endif

        private void OnTriggerEnter(Collider col)
        {
            if (col.CompareTag("Player") && col.TryGetComponent(out PlayerControllerBase p))
            {
                if (!p.HasAuthority)
                    return;
                
                Debug.Log($"Player {p.Id} has died by {_type}");
                
                p.SendPacket(new PlayerDeathPacket()
                {
                    Type = _type,
                    Id = p.Id
                });
            }
        }
    }
}