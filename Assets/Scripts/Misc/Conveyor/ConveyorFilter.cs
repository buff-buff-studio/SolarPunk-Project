using System;
using UnityEngine;

namespace Solis.Misc.Conveyor
{
    [RequireComponent(typeof(BoxCollider))]
    public class ConveyorFilter : MonoBehaviour
    {
        public FilterType filter;

        private void OnTriggerEnter(Collider col)
        {
            if(col.TryGetComponent(out ConveyorObject conveyorObject))
            {
                if((filter & (FilterType)conveyorObject.objectType) != 0)
                    return;
                Destroy(conveyorObject.gameObject);
            }
        }
    }
}