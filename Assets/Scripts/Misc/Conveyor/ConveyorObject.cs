using System;
using NetBuff.Components;
using UnityEngine;

namespace Solis.Misc.Conveyor
{
    [Flags]
    public enum FilterType : int
    {
        Cardboard = 1,
        WoodCrate = 2,
        MetalCrate = 4,
        HeavyCrate = 8,
        PlasticDrum = 16,
        MetalBarrel = 32,
    }

    public enum ObjectType : int
    {
        Cardboard = 1,
        WoodCrate = 2,
        MetalCrate = 4,
        HeavyCrate = 8,
        PlasticDrum = 16,
        MetalBarrel = 32,
    }

    [RequireComponent(typeof(NetworkRigidbodyTransform), typeof(Collider))]
    public class ConveyorObject : NetworkBehaviour
    {
        public ObjectType objectType;

        private void OnEnable()
        {
            InvokeRepeating(nameof(_PosCheck), 0, 1f);
        }

        private void OnDisable()
        {
            CancelInvoke(nameof(_PosCheck));
        }

        private void FixedUpdate()
        {
            if (!HasAuthority)
                return;

            if(Physics.Raycast(transform.position, Vector3.down, out var hit, 1f))
            {
                if (hit.collider.CompareTag("Conveyor"))
                {
                    var conveyor = hit.transform;

                    var localPos = conveyor.InverseTransformPoint(transform.position);
                    localPos.x = 0;
                    var newPos = conveyor.TransformPoint(localPos);

                    transform.position = Vector3.Lerp(transform.position, newPos, Time.fixedDeltaTime * 3.5f);
                }
            }
        }

        private void OnTriggerEnter(Collider col)
        {
            if (col.CompareTag("DeathTrigger"))
            {
                Destroy(gameObject);
            }
        }

        private void _PosCheck()
        {
            if (transform.position.y < -15)
            {
                Destroy(gameObject);
            }
        }
    }
}