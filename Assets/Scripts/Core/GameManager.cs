﻿using System;
using System.Linq;
using NetBuff;
using NetBuff.Components;
using NetBuff.Misc;
using NetBuff.Relays;
using Solis.Player;
using Solis.Data;
using Solis.Data.Saves;
using Solis.Interface.Lobby;
using Solis.Misc.Props;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Solis.Core
{
    /// <summary>
    /// Main game manager class that handles game logic and player spawning.
    /// </summary>
    public class GameManager : NetworkBehaviour
    {
        #region Public Static Properties
        /// <summary>
        /// Returns the instance of the GameManager.
        /// </summary>
        public static GameManager Instance { get; private set; }
        #endregion
        
        #region Inspector Fields
        [Header("REFERENCES")]
        public GameRegistry registry;
        public CanvasGroup fadeScreen;
        public Button leaveGame;
        public Button restartLevel;
        public Button copyCode;
        public GameObject lobbyLoadingScene;
        public GameObject loadingCanvas;
        
        [Header("PREFABS")]
        public GameObject playerHumanLobbyPrefab;
        public GameObject playerRobotLobbyPrefab;
        public GameObject playerHumanGamePrefab;
        public GameObject playerRobotGamePrefab;

        [Header("SETTINGS")]
        public string[] persistentScenes = { "Core" };
        #endregion

        #region Private Fields
        [SerializeField]
        private Save save = new();

        [SerializeField]
        private bool playedCutscene;

        private bool _loadedLobby = false;
        #endregion

        #region Public Properties
        /// <summary>
        /// Returns true if the game is currently in the lobby.
        /// </summary>
        public bool IsOnLobby => SceneManager.GetSceneByName(registry.sceneLobby.Name).isLoaded;
        
        /// <summary>
        /// Returns the save instance.
        /// </summary>
        public Save Save => save;

        /// <summary>
        /// Returns current save data.
        /// </summary>
        public SaveData SaveData
        {
            get => save.data;
            set => save.data = value;
        }
        
        /// <summary>
        /// Returns the current level info.
        /// </summary>
        public LevelInfo CurrentLevel => save.data.currentLevel < 0 ? null : registry.levels[save.data.currentLevel];

        //[HideInInspector] 
        public bool isGameStarted = false;
        #endregion

        #region Unity Callbacks
        private void OnEnable()
        {
            Instance = this;
#if UNITY_EDITOR
            var scene = SolisNetworkManager.sceneToLoad;
            if(scene != null && scene != "" && scene != "Null" && scene != "Lobby")
                isGameStarted = true;
#endif
            LoadingLobby(isGameStarted);
        }

        private void Update()
        {
            if(Input.GetKeyDown(KeyCode.K))
                ButtonRestartLevel();

            if (!isGameStarted)
                isGameStarted = !NetworkManager.Instance.LoadedScenes.Contains(registry.sceneLobby.Name);
            
            if (!save.IsSaved)
                return;

            save.playTime += Time.deltaTime;
        }

        private void OnDisable()
        {
            Instance = null;
        }
        
        private void OnApplicationQuit()
        {
            if (save.IsSaved)
                save.SaveData(null);
        }
        #endregion

        #region Network Callbacks
        public override void OnClientConnected(int clientId)
        {
            if (!HasAuthority)
                return;
            
            //If it's the cutscene scene, don't spawn the player
            if (NetworkManager.Instance.LoadedScenes.Contains(registry.sceneCutscene.Name))
                return;
            
            _RespawnPlayerForClient(clientId);
        }

        public override void OnClientDisconnected(int clientId)
        {
            var spawnPoints = FindObjectsByType<LobbySpawnPoint>(FindObjectsSortMode.InstanceID)
                .Where(x => x.occupiedBy == clientId).ToArray();
            foreach (var spawnPoint in spawnPoints)
                spawnPoint.occupiedBy = -1;
        }
        #endregion

        #region Public Methods - Game
        /// <summary>
        /// Starts the game.
        /// Called only on the server.
        /// </summary>
        [ServerOnly]
        public void StartGame()
        {
            isGameStarted = true;
            save.SaveData(null);
            LoadLevel();
        }
        
        /// <summary>
        /// Returns to the lobby.
        /// Called only on the server.
        /// </summary>
        [ServerOnly]
        public async void ReturnToLobby()
        {
            var manager = NetworkManager.Instance!;
            
            foreach (var s in manager.LoadedScenes)
            {
                if (Array.IndexOf(persistentScenes, s) == -1 && s != name)
                    manager.UnloadScene(s);
            }
            
            await _Fade(true);
            var waiting = true;
            
            if (!manager.IsSceneLoaded(registry.sceneLobby.Name))
                manager.LoadScene(registry.sceneLobby.Name).Then((_) =>
                {
                    foreach (var clientId in manager.GetConnectedClients())
                        _RespawnPlayerForClient(clientId);
                    
                    waiting = false;
                });
            
            while (waiting)
                await Awaitable.EndOfFrameAsync();
            await _Fade(false);
        }

        /// <summary>
        /// Loads the specified level.
        /// Can be used to restart the current level.
        /// Called only on the server.
        /// </summary>
        [ServerOnly]
        public async void LoadLevel()
        {
            var manager = NetworkManager.Instance!;
            var levelInfo = registry.levels[save.data.currentLevel];
            var scene = levelInfo.scene.Name;

            #region Prepare
            await _Fade(true);
            
            //Unload other scenes
            foreach (var s in manager.LoadedScenes)
            {
                if (Array.IndexOf(persistentScenes, s) == -1  && s != name)
                    manager.UnloadScene(s);
            }
            #endregion

            #region Cutscene
            //Load the cutscene if it has one
            if (levelInfo.hasCutscene && !playedCutscene)
            {
                if (IsOnLobby)
                {
                    manager.UnloadScene(registry.sceneLobby.Name).Then((_) =>
                    {
                        _LoadSceneInternal(registry.sceneCutscene.Name);
                    });
                }
                else
                {
                    _LoadSceneInternal(registry.sceneCutscene.Name);
                }

                playedCutscene = true;
                return;
            }
            playedCutscene = false;
            #endregion

            #region Level
            //Load the level
            if (IsOnLobby)
            {
                manager.UnloadScene(registry.sceneLobby.Name).Then((_) =>
                {
                    _LoadSceneInternal(scene, (_) =>
                    {
                        foreach (var clientId in manager.GetConnectedClients())
                            _RespawnPlayerForClient(clientId);
                    });
                });
            }
            else
            {
                _LoadSceneInternal(scene, (_) =>
                {
                    foreach (var clientId in manager.GetConnectedClients())
                        _RespawnPlayerForClient(clientId);
                });
            }
            #endregion
        }
        
        /// <summary>
        /// Called when the player exits the game.
        /// </summary>
        public void OnExitGame()
        {
            if (save.IsSaved)
                save.SaveData(null);
        }
        #endregion

        #region Public Methods - Player
        /// <summary>
        /// Changes the character type of the player.
        /// Called only on the server.
        /// </summary>
        /// <param name="client"></param>
        /// <param name="type"></param>
        [ServerOnly]
        public void SetCharacterType(int client, CharacterType type)
        {
            if (NetworkManager.Instance.TryGetSessionData<SolisSessionData>(client, out var data))
            {
                data.PlayerCharacterType = type;
                _RespawnPlayerForClient(client);
                if (LobbyScreen.Instance != null)
                    LobbyScreen.Instance.UpdateRoom();
            }
        }
        
        /// <summary>
        /// Respawns the player.
        /// Called only on the server.
        /// </summary>
        /// <param name="client"></param>
        [ServerOnly]
        public void RespawnPlayer(int client)
        {
            _RespawnPlayerForClient(client); 
        }
        #endregion

        #region Public Methods - Save
        /// <summary>
        /// Prepares the save data, creates a new save if it doesn't exist.
        /// Called only on the server.
        /// </summary>
        [ServerOnly]
        public void PrepareSaveData()
        {
            if (!save.IsSaved)
                save.New();
        }
        #endregion

        #region Private Methods
        [ServerOnly]
        private void _RespawnPlayerForClient(int clientId)
        {
            if (NetworkManager.Instance.TryGetSessionData<SolisSessionData>(clientId, out var data))
            {
                #region Remove Existing
                var existingPlayer = FindObjectsByType<PlayerLobby>(FindObjectsSortMode.None)
                    .FirstOrDefault(x => x.OwnerId == clientId);
                
                if (existingPlayer != null)
                {
                    existingPlayer.ForceSetOwner(-1);
                    existingPlayer.Identity.Despawn();
                }
                
                var existingPlayerController = FindObjectsByType<PlayerControllerBase>(FindObjectsSortMode.None)
                    .FirstOrDefault(x => x.OwnerId == clientId);

                if (existingPlayerController != null)
                    return;
                
                #endregion

                #region Spawn New
                var spawnPos = Vector3.zero;
                if (IsOnLobby)
                {
                    var spawnPoint = FindObjectsByType<LobbySpawnPoint>(FindObjectsSortMode.InstanceID)
                        .FirstOrDefault(x => (x.occupiedBy == -1 || x.occupiedBy == clientId) && x.playerTypeFilter.Filter(data.PlayerCharacterType));

                    spawnPoint!.occupiedBy = clientId;
                    spawnPos = spawnPoint.transform.position;
                }
                else
                {
                    var spawnPoints = FindObjectsByType<SpawnPoint>(FindObjectsSortMode.InstanceID).Where(x => x.characterType == data.PlayerCharacterType).ToArray();
                    if(spawnPoints.Length > 0)
                        spawnPos = spawnPoints[clientId % spawnPoints.Length].transform.position;
                }

                var prefab = data.PlayerCharacterType == CharacterType.Human
                    ? (IsOnLobby ? playerHumanLobbyPrefab : playerHumanGamePrefab)
                    : (IsOnLobby ? playerRobotLobbyPrefab : playerRobotGamePrefab);
                
                Spawn(prefab, spawnPos, Quaternion.identity, Vector3.one, true, clientId);
                #endregion
            }
        }

        private async void _LoadSceneInternal(string scene, Action<int> then = null)
        {
            var manager = NetworkManager.Instance!;
            isGameStarted = scene != registry.sceneLobby.Name;
            if (!manager.IsSceneLoaded(scene))
            {
                manager.LoadScene(scene).Then(then);
                await _Fade(false);
            }
            else
            {
                var waiting = true;
                manager.UnloadScene(scene).Then((_) =>
                {
                    manager.LoadScene(scene).Then((x) =>
                    {
                        waiting = false;
                        then?.Invoke(x);
                    });
                });
                
                while (waiting)
                    await Awaitable.EndOfFrameAsync();
                await _Fade(false);
            }
        }

        private async Awaitable _Fade(bool @in)
        {
            fadeScreen.gameObject.SetActive(true);
            
            const float fadeTime = 0.5f;
            var time = 0f;
            var from = fadeScreen.alpha;
            var target = @in ? 1f : 0f;
            
            while (time < fadeTime)
            {
                time += Time.deltaTime;
                fadeScreen.alpha = Mathf.Lerp(from, target, time / fadeTime);
                await Awaitable.EndOfFrameAsync();
            }
            
            fadeScreen.alpha = target;
            fadeScreen.gameObject.SetActive(@in);
        }
        #endregion

        public override void OnSceneLoaded(int sceneId)
        {
            copyCode.gameObject.SetActive(IsOnLobby);
            leaveGame.gameObject.SetActive(!IsOnLobby);
            restartLevel.gameObject.SetActive(!IsOnLobby && IsServer);
            LoadingLobby(IsOnLobby || isGameStarted);
        }
        
        public void ButtonLeaveGame()
        {
            if (IsServer)
            {
                foreach (var clientId in NetworkManager.Instance.GetConnectedClients())
                    NetworkManager.Instance.Transport.ServerDisconnect(clientId, "closing");
                
            }
            
            NetworkManager.Instance.Close();
        }

        public void ButtonRestartLevel()
        {
            if (IsServer)
                LoadLevel();
        }
        
        public void ButtonCopyCode()
        {
            var o = FindFirstObjectByType<RelayNetworkManagerGUI>();
            if (o != null)
            {
                GUIUtility.systemCopyBuffer = o.code;
            }
        }

        private void LoadingLobby(bool isDone)
        {
            if(_loadedLobby) return;
            _loadedLobby = isDone;
            lobbyLoadingScene.SetActive(!isDone);
            loadingCanvas.SetActive(!isDone);
        }
    }
}