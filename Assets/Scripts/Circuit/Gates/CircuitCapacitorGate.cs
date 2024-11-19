using System.Collections.Generic;
using NetBuff.Misc;
using Solis.Misc.Multicam;
using UnityEngine;

namespace Solis.Circuit.Gates
{
    /// <summary>
    /// Basic gate component that can be used to create simple logic circuits.
    /// </summary>
    public class CircuitCapacitorGate : CircuitComponent
    {
        #region Inspector Fields
        [Header("REFERENCES")]
        public CircuitPlug data;
        public CircuitPlug output;

        [Header("SETTINGS")] 
        public BoolNetworkValue deliverPower = new(false);
        public bool canChange = true;
        public bool invisibleOnPlay = false;
        public bool canTurnOff = true;
        #endregion

        #region Unity Callbacks
        protected override void OnEnable()
        {
            base.OnEnable();
            WithValues(deliverPower);
            if(invisibleOnPlay)
            {
                transform.GetChild(0).gameObject.SetActive(false);
            }
        }
        #endregion

        #region Abstract Methods Implementation
        public override CircuitData ReadOutput(CircuitPlug plug)
        {
            return new CircuitData(deliverPower.Value ? 1 : 0);
        }

        protected override void OnRefresh()
        {
            if (!HasAuthority) return;
            if (output.Connections.Length <= 0) return;

            if(!canTurnOff && deliverPower.Value)
                return;

            if(canChange && data.ReadInputPower() > .5f)
            {
                deliverPower.Value = !deliverPower.Value;
                canChange = false;
            }
            else if(data.ReadInputPower() <= .1f)
            {
                canChange = true;
            }
        }

        public override IEnumerable<CircuitPlug> GetPlugs()
        {
            yield return data;
            yield return output;
        }
        #endregion
    }
}