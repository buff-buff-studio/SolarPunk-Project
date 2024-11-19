using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NetBuff.Components;
using Solis.Circuit;
using Solis.Core;
using Solis.Player;
using UnityEngine;
using UnityEngine.UI;

namespace Solis.Misc
{
    [RequireComponent(typeof(CanvasGroup))]
    public class CheatsManager : NetworkBehaviour
    {
        public static CheatsManager Instance { get; private set; }
        public static bool IsCheatsEnabled { get; private set; }

        private CanvasGroup _canvasGroup;
        private PlayerControllerBase _player;
        private PauseManager _pauseManager;

        [SerializeField]
        private GameObject _noClip;
        [SerializeField]
        private GameObject _nextPhase;
        [SerializeField]
        private GameObject _boxPrefab;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(this);
            }

            _canvasGroup = GetComponent<CanvasGroup>();
            _pauseManager = GetComponentInParent<PauseManager>();
            IsCheatsEnabled = false;

            _nextPhase.SetActive(IsServer);
            _noClip.SetActive(false);

            DisableCheats();
        }

        public void ChangeVisibility()
        {
            _canvasGroup.alpha = IsCheatsEnabled ? 1 : 0;
            _canvasGroup.blocksRaycasts = IsCheatsEnabled;
            _canvasGroup.interactable = IsCheatsEnabled;

            _noClip.SetActive(false);
        }

        public void EnableCheats(PlayerControllerBase player)
        {
            Debug.LogWarning("Cheats are now enabled");

            IsCheatsEnabled = true;
            _player = player;

            ChangeVisibility();
            _pauseManager.PauseGame();
        }

        public void DisableCheats()
        {
            Debug.LogWarning("Cheats are now disabled");

            IsCheatsEnabled = false;

            foreach (var toggle in GetComponentsInChildren<Toggle>())
                toggle.isOn = false;

            ChangeVisibility();
        }

        public void SetGodMode(bool value)
        {
            if (!IsCheatsEnabled) return;

            _player.SetCheatsValue(0, value);
        }

        public void SetFlyMode(bool value)
        {
            if (!IsCheatsEnabled) return;

            _player.SetCheatsValue(1, value);
            _noClip.SetActive(value);
            if (!value)
            {
                _noClip.GetComponentInChildren<Toggle>().isOn = false;
                SetNoClip(false);
            }
        }

        public void SetNoClip(bool value)
        {
            if (!IsCheatsEnabled) return;

            _player.SetCheatsValue(2, value);
        }

        public void RespawnPlayer()
        {
            if (!IsCheatsEnabled) return;

            _player.Respawn();
        }

        public void SpawnBox()
        {
            if (!IsCheatsEnabled) return;

            Debug.Log("Spawning box");
            Spawn(_boxPrefab, _player.transform.position + (_player.transform.up * 3), Quaternion.identity, _boxPrefab.transform.localScale, true);
        }

        public void CircuitBreaker()
        {
            if (!IsCheatsEnabled) return;

            var objs = Physics.SphereCastAll(_player.transform.position, 10, Vector3.up, 0, ~LayerMask.GetMask("Box", "CarriedIgnore", "Human" , "Robot"), QueryTriggerInteraction.Ignore);
            if (objs.Length == 0) return;
            var first = objs.OrderBy(x => Vector3.Distance(x.transform.position, _player.transform.position));
            foreach (var obj in first)
            {
                if (obj.transform.TryGetComponent(out CircuitComponent circuit))
                {
                    circuit.Refresh();
                    break;
                }
            }
        }

        public void NextPhase()
        {
            if (!IsCheatsEnabled) return;

            GameManager.Instance.LoadLevel();
        }
    }
}