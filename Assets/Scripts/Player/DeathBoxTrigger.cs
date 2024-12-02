using NetBuff.Components;
using Solis.Packets;
using Solis.Player;
using Unity.VisualScripting;
using UnityEngine;

namespace Solis.Player
{
    [Icon("Assets/Art/Sprites/Editor/DeathTrigger_ico.png")]
    [RequireComponent(typeof(BoxCollider))]
    public class DeathBoxTrigger : DeathTrigger
    {
#if UNITY_EDITOR
        protected override void OnDrawGizmos()
        {
            Gizmos.color = _type == PlayerControllerBase.Death.Fall
                ? new Color(1, 0, 0, .25f)
                : new Color(1, .25f, 0, .25f);
            Gizmos.DrawCube(transform.position, transform.lossyScale);
            Gizmos.DrawWireCube(transform.position, transform.lossyScale);

            BoxColliderVolume();
        }

        private void BoxColliderVolume()
        {
            if (_collider == null) _collider = GetComponent<BoxCollider>();
            var _boxCollider = (BoxCollider) _collider;

            if (_boxCollider.center != Vector3.zero)
                transform.position += Vector3.Scale(_boxCollider.center, transform.localScale);
            if (_boxCollider.size != Vector3.one)
                transform.localScale = Vector3.Scale(transform.localScale, _boxCollider.size);

            _boxCollider.center = Vector3.zero;
            _boxCollider.size = Vector3.one;
        }

        protected override void OnValidate()
        {
            TryGetComponent(out BoxCollider _collider);
            _collider.isTrigger = true;
            gameObject.tag = "DeathTrigger";
            gameObject.layer = LayerMask.NameToLayer("Trigger");
        }

#endif
    }
}