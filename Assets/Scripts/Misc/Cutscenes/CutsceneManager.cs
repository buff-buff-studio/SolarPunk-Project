using System;
using System.Collections.Generic;
using NetBuff;
using NetBuff.Components;
using NetBuff.Interface;
using NetBuff.Misc;
using Solis.Audio;
using Solis.Core;
using Solis.Data;
using Solis.Interface.Input;
using Solis.Packets;
using Solis.Player;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Solis.Misc.Cutscenes
{
    /// <summary>
    /// Used to manage the cutscene state and skip the cutscene when all players have finished it.
    /// </summary>
    public class CutsceneManager : NetworkBehaviour
    {
        #region Inspector Fields
        [Header("REFERENCES")]
        public TMP_Text labelDone;
        public Animator skipAnimator;
        public CutsceneObject cutscene;
        public bool isEnding = false;
        
        [Header("STATE")]
        [ServerOnly]
        public List<int> doneList = new();
        public IntNetworkValue doneCount = new(0);
        public IntNetworkValue playerCount = new(0);
        #endregion

        #region Unity Callbacks
        private void OnEnable()
        {
            WithValues(doneCount, playerCount);
            
            doneCount.OnValueChanged += _OnDoneCountChanged;
            playerCount.OnValueChanged += _OnDoneCountChanged;
            cutscene.OnCutsceneEnd += MarkFinished;

            PacketListener.GetPacketListener<PlayerInputPacket>().AddServerListener(OnSkip);
            
            _OnDoneCountChanged(0, doneCount.Value);
            cutscene.Play();
        }
        
        private void OnDisable()
        {
            doneCount.OnValueChanged -= _OnDoneCountChanged;
            playerCount.OnValueChanged -= _OnDoneCountChanged;
            cutscene.OnCutsceneEnd -= MarkFinished;
            PacketListener.GetPacketListener<PlayerInputPacket>().RemoveServerListener(OnSkip);
        }

        private void Update()
        {
            if(SolisInput.GetKeyDown("Skip"))
            {
                var packet = new PlayerInputPacket
                    { Key = KeyCode.Return, Id = Id, CharacterType = CharacterType.Human };
                if(!HasAuthority)
                    SendPacket(packet, true);
                else
                    OnSkip(packet, 0);
            }
        }

        #endregion
        
        #region Network Callbacks
        public override void OnSpawned(bool isRetroactive)
        {
            if (!HasAuthority)
                return;
            
            playerCount.Value = NetworkManager.Instance.Transport.GetClientCount();
        }

        public override void OnClientConnected(int clientId)
        {
            playerCount.Value = NetworkManager.Instance.Transport.GetClientCount();
        }
        
        public override void OnClientDisconnected(int clientId)
        {
            playerCount.Value = NetworkManager.Instance.Transport.GetClientCount();
            doneList.Remove(clientId);
        }

        public override void OnServerReceivePacket(IOwnedPacket packet, int clientId)
        {
            if (packet is CutsceneStatePacket csp)
            {
                if (csp.FinishedCount == 1)
                {
                    if (!doneList.Contains(clientId))
                    {
                        doneList.Add(clientId);
                        doneCount.Value = doneList.Count;
                    }
                }
                else if(doneList.Contains(clientId))
                {
                    doneList.Remove(clientId);
                    doneCount.Value = doneList.Count;
                }

                if(doneList.Count >=  NetworkManager.Instance.Transport.GetClientCount())
                {
                    // All players have finished the cutscene
                    _OnEveryoneFinished();
                }
            }
        }

        private bool OnSkip(PlayerInputPacket arg1, int arg2)
        {
            Debug.Log($"Cutscene: OnInput {arg1.Key}");
            if (!doneList.Contains(arg2))
            {
                doneList.Add(arg2);
                doneCount.Value = doneList.Count;
            }

            if(doneList.Count >=  NetworkManager.Instance.Transport.GetClientCount())
            {
                // All players have finished the cutscene
                _OnEveryoneFinished();
            }

            return false;
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// Marks the cutscene as finished for the current player.
        /// Called on the client side.
        /// </summary>
        [ClientOnly]
        public void MarkFinished()
        {
            //skipAnimator.SetTrigger("Skip");
            var packet = new CutsceneStatePacket
            {
                Id = Id,
                FinishedCount = 1
            };
            ClientSendPacket(packet);
        }
        #endregion

        #region Private Methods
        private void _OnDoneCountChanged(int oldvalue, int newvalue)
        {
            labelDone.text = $"{doneCount.Value} / {playerCount.Value}";
        }
        
        private void _OnEveryoneFinished()
        {
            if(!isEnding)
            {
                if(HasAuthority) GameManager.Instance.SaveData.currentLevel++;
                GameManager.Instance.LoadLevel();
            }
            else GameManager.Instance.ButtonLeaveGame();
        }
        #endregion
    }
}