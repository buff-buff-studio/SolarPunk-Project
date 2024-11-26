using System;
using System.Linq;
using NetBuff;
using NetBuff.Components;
using NetBuff.Misc;
using Solis.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Solis.Data;
using Solis.Interface.Input;
using Solis.Misc.Integrations;

namespace Solis.Interface.Lobby
{
    /// <summary>
    /// Controls the lobby screen UI and network logic
    /// </summary>
    public class LobbyScreen : NetworkBehaviour
    {
        #region Public Static Properties
        /// <summary>
        /// Current instance of the lobby screen.
        /// </summary>
        public static LobbyScreen Instance { get; private set; }
        #endregion

        #region Inspector Fields
        [Header("VALUES")]
        public IntNetworkValue playerCount = new(0);

        [Header("REFERENCES")]
        public TMP_Text textPlayerCount;

        public Button buttonStartGame;
        public Button buttonCloseRoom;
        public Button buttonLeaveRoom;

        public GameObject changeCharacter;

        [Header("SAVE")]
        public GameObject saveDisplay;

        public TMP_Text textSaveName;
        public RawImage imageSaveIcon;
        public Texture2D newSaveIcon;
        public SaveListScreen saveListScreen;
        #endregion

        private bool canStartGame = true;

        #region Unity Callbacks
        public void OnEnable()
        {
            Instance = this;

            WithValues(playerCount);

            playerCount.OnValueChanged += (_, newValue) =>
            {
                // Update player count UI
                textPlayerCount.text = $"{newValue}";
                DiscordController.PlayerCount = newValue;
            };

            RefreshSave();
        }

        public void OnDisable()
        {
            Instance = null;
        }

        private void Update()
        {
            if (!Cursor.visible)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }

            if (HasAuthority)
            {
                if(SolisInput.GetKeyDown("Submit") && canStartGame)
                    StartGame();
            }
        }

        #endregion
        
        #region Network Callbacks
        public override void OnSpawned(bool isRetroactive)
        {
            var hasAuthority = HasAuthority;
            buttonStartGame.gameObject.SetActive(hasAuthority);
            buttonCloseRoom.gameObject.SetActive(hasAuthority);
            //changeCharacter.SetActive(hasAuthority);
            buttonLeaveRoom.gameObject.SetActive(!hasAuthority);
            
            if(hasAuthority)
                playerCount.Value = NetworkManager.Instance.GetConnectedClientCount();
            textPlayerCount.text = $"{playerCount.Value}";
            
            var camTransform =  Camera.main!.transform;
            camTransform.position = new Vector3(0, 1, -10);
            camTransform.rotation = Quaternion.identity;
        }
        
        public override void OnClientConnected(int clientId)
        {
            playerCount.Value = NetworkManager.Instance.GetConnectedClientCount();
            UpdateRoom();
        }

        public override void OnClientDisconnected(int clientId)
        {
            playerCount.Value--;
            UpdateRoom();
        }
        #endregion

        #region Lobby Controls
        /// <summary>
        /// Starts the game.
        /// </summary>
        public void StartGame()
        {
            GameManager.Instance.StartGame();
        }

        /// <summary>
        /// Leaves or closes the room, and its network connections.
        /// </summary>
        public void LeaveOrCloseRoom()
        {
            NetworkManager.Instance.Close();
        }

        public void InviteFriends()
        {
            DiscordController.Instance.SendInvite();
        }

        /// <summary>
        /// Refreshes the save display.
        /// </summary>
        public void RefreshSave()
        {
            var auth = HasAuthority;
            saveDisplay.SetActive(auth);
            if (!auth)
                return;

            var save = GameManager.Instance.Save;
            textSaveName.text = string.IsNullOrEmpty(save.name) ? "New Save" : save.name;
            imageSaveIcon.texture = save.preview == null ? newSaveIcon : save.preview;
        }

        /// <summary>
        /// Opens the save list screen.
        /// </summary>
        public void OpenSaveListScreen()
        {
            saveListScreen.gameObject.SetActive(true);
        }
        
        /// <summary>
        /// Updates room situation.
        /// Called only on server.
        /// </summary>
        [ServerOnly]
        public void UpdateRoom()
        { 
            if (!HasAuthority)
                return;

            canStartGame = true;

            var sessions = NetworkManager.Instance.GetAllSessionData<SolisSessionData>();
            // ReSharper disable once PossibleMultipleEnumeration
            var humanCount = sessions.Count(s => s.PlayerCharacterType == CharacterType.Human);
            // ReSharper disable once PossibleMultipleEnumeration
            var robotCount = sessions.Count(s => s.PlayerCharacterType == CharacterType.Robot);

            canStartGame &= humanCount == 1 && robotCount == 1;
            buttonStartGame.interactable = canStartGame;
#if UNITY_EDITOR
            buttonStartGame.interactable = true;
#endif
        }
        #endregion
    }
}