using System.Collections.Generic;
using Solis.Circuit;
using UnityEngine;

namespace Solis.Misc.Conveyor
{
    public class SpawnerConveyorObject : CircuitComponent
    {
        public CircuitPlug input;

        public GameObject[] objectsToSpawn;
        public float spawnInterval = 1f;

        private float _timer;
        private int _lastSpawnIndex;

        private bool _isOn;

        private void Update()
        {
            if (!HasAuthority)
                return;
            if (!_isOn) return;

            _timer += Time.deltaTime;
            if (_timer >= spawnInterval)
            {
                _timer = 0;
                var index = Random.Range(0, objectsToSpawn.Length);
                if (index == _lastSpawnIndex)
                    index = (index + 1) % objectsToSpawn.Length;
                _lastSpawnIndex = index;
                var obj = objectsToSpawn[index];
                Spawn(obj, transform.position, transform.rotation, obj.transform.localScale, true);
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
            _isOn = input.ReadOutput().IsPowered;
        }
    }
}