using System;
using NetBuff.Components;
using NetBuff.Interface;
using NetBuff.Misc;
using Solis.Core;
using UnityEngine;
using Solis.Data;
using Solis.Interface.Input;
using Solis.Misc.Integrations;
using Solis.Packets;
using Random = UnityEngine.Random;

namespace Solis.Player
{
    /// <summary>
    /// Player Lobby Controller. Used to control the player in the lobby
    /// </summary>
    public class PlayerLobby : NetworkBehaviour
    {
        #region Inspector Fields
        [Header("SETTINGS")]
        [SerializeField]
        private CharacterType characterType;
        
        [Header("STATE")]
        public bool mouseIsGrabbed;
        public float rotationSpeed = 0f;
        #endregion

        private float _timeToReset = 0.5f;

        #region Unity Callbacks
        private void Update()
        {
            if (!HasAuthority || !IsOwnedByClient) return;

            if(SolisInput.CurrentInputType == SolisInput.InputType.Keyboard)
            {
                //check if mouse is being dragged over the player, to rotate it
                if (!mouseIsGrabbed)
                {
                    _timeToReset -= Time.deltaTime;
                    if (SolisInput.GetKeyDown("Click"))
                    {
                        var ray = Camera.allCameras[1].ScreenPointToRay(SolisInput.GetVector2("Point"));
                        if (Physics.Raycast(ray, out var hit))
                            if (hit.collider.gameObject == gameObject)
                            {
                                mouseIsGrabbed = true;
                                _timeToReset = Random.Range(2, 6);
                            }
                    }
                }
                else
                {
                    if (SolisInput.GetKeyUp("Click")) mouseIsGrabbed = false;
                    rotationSpeed -= SolisInput.GetVector2("PointDelta").x * 50f;
                }
            }
            else
            {
                var x = SolisInput.GetVector2("PointDelta").x;
                rotationSpeed -= x * 50f;

                if (Mathf.Abs(x) > 0.1f)
                    _timeToReset = Random.Range(2, 6);
                else
                    _timeToReset -= Time.deltaTime;
            }

            if(_timeToReset>0)
                transform.Rotate(new Vector3(0, rotationSpeed * Time.deltaTime, 0));
            else
                transform.rotation = Quaternion.Lerp(transform.rotation, Quaternion.identity, Time.deltaTime);

            rotationSpeed = Mathf.Lerp(rotationSpeed, 0, Time.deltaTime * 5f);
            
            if (!HasAuthority)
                return;

            if (SolisInput.GetKeyDown("ChangeCharacter"))
                ChangeCharacterType();
        }
        #endregion
        
        #region Network Callbacks

        public override void OnSpawned(bool isRetroactive)
        {
            base.OnSpawned(isRetroactive);

            if (SolisInput.CurrentInputType == SolisInput.InputType.Gamepad)
            {
                SolisInput.Instance.RumblePulse(.1f,.2f,.125f);
                //#64C9E2 and #575F41
                SolisInput.GamepadLight(characterType == CharacterType.Robot ? new Color(0.392f, 0.788f, 0.886f) : new Color(0.341f, 0.373f, 0.254f));
            }

            if (!HasAuthority || !IsOwnedByClient) return;
            if(DiscordController.Instance != null)
            {
                DiscordController.LobbyStartTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                DiscordController.Instance!.SetGameActivity(characterType, true,
                    SolisNetworkManager.relayCode);
            }
        }

        public override void OnServerReceivePacket(IOwnedPacket packet, int clientId)
        {
            if (packet is LobbyPlayerActionPacket actionPacket)
            {
                switch (actionPacket.Action)
                {
                    case LobbyPlayerAction.ChangeCharacter:
                        GameManager.Instance.SetCharacterType(clientId,
                            characterType == CharacterType.Human
                                ? CharacterType.Robot
                                : CharacterType.Human);

                        if (!HasAuthority || !IsOwnedByClient) return;
                        if(DiscordController.Instance != null)
                            DiscordController.Instance!.SetGameActivity(characterType, true, SolisNetworkManager.relayCode);
                        break;
                }
            }
        }
        #endregion
        
        #region Public Methods
        /// <summary>
        /// Changes the character type of the player
        /// </summary>
        [RequiresAuthority]
        public void ChangeCharacterType()
        {
            if (!HasAuthority)
                return;

            var packet = new LobbyPlayerActionPacket
            {
                Id = Id,
                Action = LobbyPlayerAction.ChangeCharacter,
            };

            SendPacket(packet);
        }
        #endregion
    }
}