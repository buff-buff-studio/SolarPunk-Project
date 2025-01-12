using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cinemachine;
using NetBuff;
using NetBuff.Components;
using NetBuff.Interface;
using NetBuff.Misc;
using Solis.i18n;
using Solis.Misc.Multicam;
using Solis.Packets;
using TMPro;
using UnityEngine;

namespace Interface.Dialog
{
    public class DialogController : NetworkBehaviour
    {
        public TMP_Text textSkipCount;
        public DialogText textText;
        public TMP_Text textSpeaker;
        public CanvasGroup canvasGroup;

        public static DialogController Instance { get; private set; }
        public bool IsDialogActive => isDialogActive.Value;
        
        public BoolNetworkValue isDialogActive = new(false, NetworkValue.ModifierType.Everybody);
        public IntNetworkValue currentOwnershipId = new(-1, NetworkValue.ModifierType.Everybody);
        public IntNetworkValue clientCount = new(0, NetworkValue.ModifierType.Server); 
        public IntNetworkValue skipFlag = new(0, NetworkValue.ModifierType.Everybody);
        public BoolNetworkValue canSkip = new(false, NetworkValue.ModifierType.Everybody);

        [SerializeField]
        private DialogData currentDialog;
        [SerializeField]
        private int currentDialogIndex = -1;
        
        [ServerOnly]
        public List<int> typingClients = new();
        
        public bool playTyping = true;
        
        private Coroutine _currentCoroutine;
        
        public void OnEnable()
        {
            WithValues(isDialogActive, currentOwnershipId, clientCount, skipFlag, canSkip);

            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
            }
            else
            {
                Instance = this;
                PacketListener.GetPacketListener<PlayerInputPacket>().AddServerListener(_OnPlayerInput);
                skipFlag.OnValueChanged += _OnSkipFlagChanged;
                clientCount.OnValueChanged += _OnClientCountChanged;
                canSkip.OnValueChanged += _OnCanSkipChanged;

                if (IsServer)
                    clientCount.Value = NetworkManager.Instance.GetConnectedClientCount();
            }
        }
        
        public void OnDisable()
        {
            if(Instance == this)
            {
                Instance = null;
                PacketListener.GetPacketListener<PlayerInputPacket>().RemoveServerListener(_OnPlayerInput);
                skipFlag.OnValueChanged -= _OnSkipFlagChanged;
                clientCount.OnValueChanged -= _OnClientCountChanged;
                canSkip.OnValueChanged -= _OnCanSkipChanged;
            }
        }
        
        private void _OnClientCountChanged(int oldvalue, int newvalue)
        {
            var count = _UpdateSkipCount();
            if (currentOwnershipId.Value == NetworkManager.Instance.LocalClientIds[0] && count >= clientCount.Value)
                _NextLine();
        }

        private void _OnSkipFlagChanged(int oldvalue, int newvalue)
        {
            var count = _UpdateSkipCount();
            
            if(currentOwnershipId.Value == NetworkManager.Instance.LocalClientIds[0] && count >= clientCount.Value)
                _NextLine();
        }
        
        private void _OnCanSkipChanged(bool oldvalue, bool newvalue)
        {
            textSkipCount.gameObject.SetActive(newvalue);
        }

        private bool _OnPlayerInput(PlayerInputPacket packet, int client)
        {
            if (packet.Key == KeyCode.Return && isDialogActive.Value)
            {
                if (typingClients.Contains(client))
                {
                    typingClients.Remove(client);
                    ServerSendPacket(new DialogSkipTypingPacket
                    {
                        Id = Id
                    }, client);
                }
                else
                    skipFlag.Value |= 1 << Array.IndexOf(NetworkManager.Instance.GetConnectedClients().ToArray(), client);
                return true;
            }
            
            return false;
        }

        public void OpenDialog(DialogData data)
        {
            if (isDialogActive.Value)
                return;

            //Lock the dialog
            isDialogActive.Value = true;
            currentOwnershipId.Value = NetworkManager.Instance.LocalClientIds[0];

            if (data == null)
            {
                CloseDialog();
                return;
            }
            
            //Set local data
            currentDialog = data;
            currentDialogIndex = -1;

            _NextLine();
        }
        
        public void CloseDialog(bool force = false)
        {
            if(force || currentOwnershipId.Value == NetworkManager.Instance.LocalClientIds[0])
            {
                isDialogActive.Value = false;
                currentOwnershipId.Value = -1;
                currentDialog = null;
                currentDialogIndex = -1;
                
                ClientSendPacket(new DialogClosePacket
                {
                    Id = Id
                }, true);
            }
        }

        public override void OnClientConnected(int clientId)
        {
            if (!IsServer)
                return;
            
            clientCount.Value = NetworkManager.Instance.GetConnectedClientCount();
        }

        public override void OnClientDisconnected(int clientId)
        {
            if (!IsServer)
                return;
            
            clientCount.Value = NetworkManager.Instance.GetConnectedClientCount();
            
            if (currentOwnershipId.Value != clientId) 
                return;
            
            currentOwnershipId.Value = -1;
            CloseDialog(true);
        }

        public override void OnServerReceivePacket(IOwnedPacket packet, int clientId)
        {
            if (packet is DialogShowPacket dialogShowPacket)
                ServerBroadcastPacket(dialogShowPacket, true);
            
            if (packet is DialogClosePacket dialogClosePacket)
                ServerBroadcastPacket(dialogClosePacket, true);

            if (packet is DialogTypingPacket dialogTypingPacket)
            {
                if (dialogTypingPacket.IsTyping)
                {
                    if (!typingClients.Contains(clientId))
                        typingClients.Add(clientId);
                }
                else
                {
                    typingClients.Remove(clientId);
                }
            }
        }

        public override void OnClientReceivePacket(IOwnedPacket packet)
        {
            if (packet is DialogShowPacket dialogShowPacket)
            {
                if (_currentCoroutine != null)
                    StopCoroutine(_currentCoroutine);
                
                _currentCoroutine = StartCoroutine(_DisplayLine(dialogShowPacket.Character, dialogShowPacket.TextKey));
            }

            if (packet is DialogClosePacket)
            {
                if (_currentCoroutine != null)
                    StopCoroutine(_currentCoroutine);
                
                _currentCoroutine = StartCoroutine(_CloseDialog());
            }
            
            if (packet is DialogSkipTypingPacket)
                playTyping = false;
        }
        
        private void _NextLine()
        {
            if(currentOwnershipId.Value == NetworkManager.Instance.LocalClientIds[0])
            {
                currentDialogIndex++;
                skipFlag.Value = 0;
                canSkip.Value = false;
                                
                if (currentDialogIndex >= currentDialog.lines.Length)
                {
                    CloseDialog();
                    return;
                }
                
                ClientSendPacket(new DialogShowPacket
                {
                    Id = Id,
                    Character = currentDialog.lines[currentDialogIndex].character,
                    TextKey = currentDialog.lines[currentDialogIndex].textKey
                }, true);
            }
        }
        
        private int _UpdateSkipCount()
        {
            var count = 0;
            for (var i = 0; i < 32; i++)
            {
                if ((skipFlag.Value & (1 << i)) != 0)
                    count++;
            }
            
            textSkipCount.text = LanguagePalette.Localize("dialog.skip", count + "/" + clientCount.Value);
            return count;
        }

        private IEnumerator _DisplayLine(DialogCharacter character, string textKey)
        {
            //Fade out
            while (canvasGroup.alpha > 0)
            {
                canvasGroup.alpha -= Time.deltaTime * 2;
                yield return null;
            }
            
            //Set text
            textSpeaker.text = LanguagePalette.Localize("dialog.character." + character.ToString().ToLowerInvariant());
            textText.Text.text = LanguagePalette.Localize("dialog." + textKey);
            textText.typingTime = 0;
            playTyping = true;
            
            MulticamCamera.Instance.SetDialogueFocus(character);
            
            ClientSendPacket(new DialogTypingPacket
            {
                Id = Id,
                IsTyping = true
            }, true);
            
            //Fade in
            while (canvasGroup.alpha < 1)
            {
                canvasGroup.alpha += Time.deltaTime * 4;
                yield return null;
            }
            
            if (currentOwnershipId.Value == NetworkManager.Instance.LocalClientIds[0])
                canSkip.Value = true;

            while (playTyping && textText.typingTime < textText.MaxTime)
            {
                textText.typingTime += Time.deltaTime;
                yield return null;
            }
            
            textText.typingTime = textText.MaxTime;
            
            if (playTyping)
                ClientSendPacket(new DialogTypingPacket
                {
                    Id = Id,
                    IsTyping = false
                }, true);

            _currentCoroutine = null;
        }
        
        private IEnumerator _CloseDialog()
        {
            if (canvasGroup.alpha > 0)
            {
                //Fade out
                while (canvasGroup.alpha > 0)
                {
                    canvasGroup.alpha -= Time.deltaTime * 4;
                    yield return null;
                }
            }
            
            if(!CinematicController.IsPlaying)
                MulticamCamera.Instance!.ChangeCameraState(MulticamCamera.CameraState.Gameplay,
                    CinemachineBlendDefinition.Style.EaseInOut, 1);
            
            _currentCoroutine = null;
        }
    }

    public class DialogShowPacket : IOwnedPacket
    {
        public NetworkId Id { get; set; }
        public DialogCharacter Character { get; set; }
        public string TextKey { get; set; }
        
        public void Serialize(BinaryWriter writer)
        {
            Id.Serialize(writer);
            writer.Write((int)Character);
            writer.Write(TextKey);
        }

        public void Deserialize(BinaryReader reader)
        {
            Id = NetworkId.Read(reader);
            Character = (DialogCharacter)reader.ReadInt32();
            TextKey = reader.ReadString();
        }
    }
    
    public class DialogClosePacket : IOwnedPacket
    {
        public NetworkId Id { get; set; }
        
        public void Serialize(BinaryWriter writer)
        {
            Id.Serialize(writer);
        }

        public void Deserialize(BinaryReader reader)
        {
            Id = NetworkId.Read(reader);
        }
    }
    
    public class DialogTypingPacket : IOwnedPacket
    {
        public NetworkId Id { get; set; }
        public bool IsTyping { get; set; }
        
        public void Serialize(BinaryWriter writer)
        {
            Id.Serialize(writer);
            writer.Write(IsTyping);
        }
        
        public void Deserialize(BinaryReader reader)
        {
            Id = NetworkId.Read(reader);
            IsTyping = reader.ReadBoolean();
        }
    }
    
    public class DialogSkipTypingPacket : IOwnedPacket
    {
        public NetworkId Id { get; set; }
        
        public void Serialize(BinaryWriter writer)
        {
            Id.Serialize(writer);
        }
        
        public void Deserialize(BinaryReader reader)
        {
            Id = NetworkId.Read(reader);
        }
    }
}