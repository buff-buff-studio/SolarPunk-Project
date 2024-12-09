using System.Collections.Generic;
using NetBuff.Components;
using Solis.Circuit;
using UnityEngine;

namespace Solis.Misc.Conveyor
{
    public class ConveyorReset : CircuitComponent
    {
        [Header("CIRCUIT")]
        public CircuitPlug input;

        [Header("REFERENCES")]
        public NetworkAnimator[] glassAnimators;
        public float animationWaitDuration = .5f;
        public float animationStartDuration = 1f;
        public float animationTotalDuration = 1f;

        private bool _resetting;
        private float _waitTimer;
        private float _startTimer;
        private float _totalTimer;
        
        private void Update()
        {
            if (!HasAuthority || !_resetting)
                return;

            if(_waitTimer < animationWaitDuration)
            {
                _waitTimer += Time.deltaTime;
                if (_waitTimer >= animationWaitDuration)
                {
                    _startTimer = 0;
                    foreach (var animator in glassAnimators)
                    {
                        animator.SetTrigger("FadeIn");
                    }
                }
            }
            else if(_startTimer < animationStartDuration)
            {
                _startTimer += Time.deltaTime;
                if (_startTimer >= animationStartDuration)
                {
                    _totalTimer = 0;
                    DestroyObjects();
                }
            }
            else if (_totalTimer < animationTotalDuration)
            {
                _totalTimer += Time.deltaTime;
                if (_totalTimer >= animationTotalDuration)
                {
                    foreach (var animator in glassAnimators)
                    {
                        animator.SetTrigger("FadeOut");
                    }
                    _resetting = false;
                }
            }
        }

        private void DestroyObjects()
        {
            var objects = FindObjectsByType<ConveyorObject>(FindObjectsSortMode.None);
            foreach (var obj in objects)
            {
                obj.Despawn();
            }
        }

        public override CircuitData ReadOutput(CircuitPlug plug)
        {
            return new CircuitData();
        }

        public override IEnumerable<CircuitPlug> GetPlugs()
        {
            yield return input;
        }

        protected override void OnRefresh()
        {
            if (!HasAuthority)
                return;
            if (input.ReadOutput().IsPowered)
            {
                _resetting = true;
                _waitTimer = 0;
                _startTimer = 0;
                _totalTimer = 0;
            }
        }
    }
}