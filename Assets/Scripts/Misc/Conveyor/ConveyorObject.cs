using System.Collections;
using System.Collections.Generic;
using NetBuff.Components;
using UnityEngine;

namespace Solis.Misc
{
    [RequireComponent(typeof(NetworkRigidbodyTransform), typeof(Collider))]
    public class ConveyorObject : NetworkBehaviour
    {
        public enum ObjectType
        {
            SmallObject,
            MediumObject,
            LargeObject
        }
        public ObjectType objectType;
    }
}