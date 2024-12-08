using System;
using System.Collections.Generic;
using NetBuff.Misc;
using NUnit.Framework;
using Solis.Data;
using Solis.Misc.Multicam;
using Solis.Packets;
using Solis.Player;
using UnityEngine;

namespace Solis.Circuit
{
    [RequireComponent(typeof(BoxCollider))]
    public class CircuitTrigger : CircuitComponent
    {
        [Header("REFERENCES")]
        public BoolNetworkValue isOn = new(false);
        public CircuitPlug output;

        [Header("SETTINGS")]
        public CharacterTypeFilter playerTypeFilter = CharacterTypeFilter.Both;
        public int minPlayers = 2;
        public bool exitTrigger;
        public bool canBeTurnedOff = false;
        private int _triggerCount;
        private static int _playerCount;

#if UNITY_EDITOR
        private BoxCollider _boxCollider;
#endif
        protected override void OnEnable()
        {
            base.OnEnable();
            WithValues(isOn);

            isOn.OnValueChanged += _OnValueChanged;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            TryGetComponent(out _boxCollider);
            _boxCollider.isTrigger = true;
        }
#endif

        private void _OnValueChanged(bool oldvalue, bool newvalue)
        {
            onToggleComponent.Invoke();
            Refresh();
        }

        public override CircuitData ReadOutput(CircuitPlug plug)
        {
            return new CircuitData(isOn.Value);
        }

        public override IEnumerable<CircuitPlug> GetPlugs()
        {
            yield return output;
        }

        protected override void OnRefresh() { }

        private void OnTriggerEnter(Collider other)
        {
            if(isOn.Value)
                return;

            if(_playerCount <= 0)
                _playerCount = FindObjectsByType<PlayerControllerBase>(FindObjectsSortMode.None).Length;

            var controller = other.GetComponent<PlayerControllerBase>();
            if (controller == null)
                return;
            if (!playerTypeFilter.Filter(controller.CharacterType))
                return;

            _triggerCount++;
            _triggerCount = Mathf.Clamp(_triggerCount, 0, _playerCount);
            if (_triggerCount >= minPlayers)
            {
                isOn.Value = true;
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (isOn.Value && !canBeTurnedOff)
                return;

            if(!exitTrigger)
                return;

            var controller = other.GetComponent<PlayerControllerBase>();
            if (controller == null)
                return;
            if (!playerTypeFilter.Filter(controller.CharacterType))
                return;

            _triggerCount--;
            if (_triggerCount <= 0) _triggerCount = 0;
        }
    }
}