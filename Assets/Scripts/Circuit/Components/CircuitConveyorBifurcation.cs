using System;
using System.Collections.Generic;
using NetBuff.Misc;
using UnityEngine;

namespace Solis.Circuit.Components
{
    public class CircuitConveyorBifurcation : CircuitComponent
    {
        public CircuitPlug input;
        public CircuitPlug data;
        public CircuitPlug outputA;
        public CircuitPlug outputB;

        public BoolNetworkValue isBifurcationA = new(true);
        public CircuitConveyorBelt bifurcationA;
        public CircuitConveyorBelt bifurcationB;
        public float turnOnYPosition = 0.01f;

        public Transform ConveyorBeltA => bifurcationA.transform.parent;
        public Transform ConveyorBeltB => bifurcationB.transform.parent;

        protected override void OnEnable()
        {
            WithValues(isBifurcationA);
            base.OnEnable();

            isBifurcationA.OnValueChanged += IsBifurcationAOnValueChanged;
        }

        private void IsBifurcationAOnValueChanged(bool oldvalue, bool newvalue)
        {
            ConveyorBeltA.localPosition = new Vector3(ConveyorBeltA.localPosition.x, newvalue ? 0 : -turnOnYPosition, ConveyorBeltA.localPosition.z);
            ConveyorBeltB.localPosition = new Vector3(ConveyorBeltB.localPosition.x, newvalue ? -turnOnYPosition : 0, ConveyorBeltB.localPosition.z);
            bifurcationA.ColliderEnabled = newvalue;
            bifurcationA.connectedBelts.ForEach(belt => belt.ColliderEnabled = newvalue);
            bifurcationB.ColliderEnabled = !newvalue;
            bifurcationB.connectedBelts.ForEach(belt => belt.ColliderEnabled = !newvalue);
        }

        public override CircuitData ReadOutput(CircuitPlug plug)
        {
            if (!input.ReadOutput().IsPowered) return new CircuitData(false);

            if (plug == outputA)
                return new CircuitData(isBifurcationA.Value);
            if (plug == outputB)
                return new CircuitData(!isBifurcationA.Value);

            return new CircuitData(false);
        }

        public override IEnumerable<CircuitPlug> GetPlugs()
        {
            yield return input;
            yield return data;
            yield return outputA;
            yield return outputB;
        }

        protected override void OnRefresh()
        {
            if (!HasAuthority)
                return;

            if(!isBifurcationA.AttachedTo)
                isBifurcationA.AttachedTo = this;

            isBifurcationA.Value = data.ReadOutput().IsPowered;
        }
    }
}