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
        public bool deliverPower = false;
        public bool canChange = true;
        public bool invisibleOnPlay = false;
        public bool canTurnOff = true;
        #endregion

        #region Unity Callbacks
        protected override void OnEnable()
        {
            base.OnEnable();
            if(invisibleOnPlay)
            {
                transform.GetChild(0).gameObject.SetActive(false);
            }
        }
        #endregion

        #region Abstract Methods Implementation
        public override CircuitData ReadOutput(CircuitPlug plug)
        {
            //return new CircuitData(deliverPower.Value ? 1 : 0);

            var result = data.ReadInputPower();

            if (canTurnOff || !deliverPower)
            {
                if (canChange && result > 0)
                {
                    deliverPower = !deliverPower;
                    canChange = false;
                }
                else if (result <= 0)
                {
                    canChange = true;
                }
            }

            return new CircuitData(deliverPower ? 1 : 0);
        }

        protected override void OnRefresh()
        { }

        public override IEnumerable<CircuitPlug> GetPlugs()
        {
            yield return data;
            yield return output;
        }
        #endregion
    }
}