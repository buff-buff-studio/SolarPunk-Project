using System;
using System.IO;
using System.Linq;
using NetBuff;
using NetBuff.Components;
using NetBuff.Interface;
using NetBuff.Misc;
using NetBuff.Relays;
using Solis.Audio.Players;
using Solis.Player;
using Solis.Data;
using Solis.Data.Saves;
using Solis.Interface.Lobby;
using Solis.Misc.Multicam;
using Solis.Misc.Props;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#pragma warning disable CS4014

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
        
        public SolisMusicPlayer lobbyMusic;
        public SolisMusicPlayer gameMusic;
        #endregion

        #region Private Fields
        [SerializeField]
        private Save save = new();

        [SerializeField]
        private bool playedCutscene;

        private bool _loadedLobby;
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

        public bool isGameStarted;
        #endregion

        #region Unity Callbacks
        private void OnEnable()
        {
            PacketListener.GetPacketListener<FadePacket>().AddClientListener(OnReceiveFadePacket);
            
            Instance = this;
#if UNITY_EDITOR
            var scene = SolisNetworkManager.sceneToLoad;
            if(!string.IsNullOrEmpty(scene) && scene != "Null" && scene != "Lobby")
            {
                isGameStarted = true;
                save.data.currentLevel = FindActiveLevel();
            }
#endif
            LoadingLobby(isGameStarted);
        }

        private bool OnReceiveFadePacket(FadePacket arg)
        {
            if (HasAuthority)
                return false;
            
            _ = _Fade(arg.IsIn);
            return true;
        }

        private void Update()
        {
            /*
            if (Input.GetKeyDown(KeyCode.K) && SaveData.currentLevel % 2 == 1)
            {
                SaveData.currentLevel++;
                LoadLevel();
            }
            */

            if (!isGameStarted)
                isGameStarted = !NetworkManager.Instance.LoadedScenes.Contains(registry.sceneLobby.Name);
            
            if (!save.IsSaved)
                return;

            save.playTime += Time.deltaTime;
        }
        
        private void OnDisable()
        {
            PacketListener.GetPacketListener<FadePacket>().RemoveClientListener(OnReceiveFadePacket);
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
            {
                spawnPoint.occupiedBy = -1;
                spawnPoint.playerNameValue.Value = "";
            }
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

            await _FadeGameServer();
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
            if(SaveData.currentLevel >= registry.levels.Length || SaveData.currentLevel < 0)
            {
                Debug.LogWarning("Level index out of range: " + SaveData.currentLevel);
                SaveData.currentLevel = Mathf.Clamp(SaveData.currentLevel, 0, registry.levels.Length - 1);
                return;
            }

            if(MulticamCamera.Instance != null)
                MulticamCamera.Instance.OnChangeScene();

            var manager = NetworkManager.Instance!;
            var levelInfo = registry.levels[save.data.currentLevel];
            var scene = levelInfo.scene.Name;

            #region Prepare
            await _FadeGameServer();
            
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
                        
                        //_FadeGameServer(false);
                    });
                });
            }
            else
            {
                _LoadSceneInternal(scene, (_) =>
                {
                    foreach (var clientId in manager.GetConnectedClients())
                        _RespawnPlayerForClient(clientId);
                    
                    //_FadeGameServer(false);
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

        [ServerOnly]
        public void RespawnAllPlayers()
        {
            foreach (var clientId in NetworkManager.Instance.GetConnectedClients())
            {
                Debug.Log("Respawning player for client: " + clientId);
                _RespawnPlayerForClient(clientId);
            }
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

#if UNITY_EDITOR
        public int FindActiveLevel()
        {
            if(SceneManager.sceneCount < 2) return save.data.currentLevel;
            var scene = SceneManager.GetSceneAt(1).name;
            if(registry.levels.Any(x => x.scene.sceneName == scene))
            {
                var registryLevels = registry.levels.ToList();
                var item = registryLevels.Find(x => x.scene.sceneName == scene);
                Debug.Log("Level found in registry: " + scene + " at index: " + SaveData.currentLevel);
                return registryLevels.IndexOf(item);
            }
            Debug.LogWarning("Scene not found in registry: " + scene);
            return save.data.currentLevel;
        }
#endif
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
                    spawnPoint!.playerNameValue.Value = data.Username;
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
            }
        }

        [ServerOnly]
        private async Awaitable _FadeGameServer()
        {
            await _Fade(true);
            ServerBroadcastPacket(new FadePacket { IsIn = true });
        }
        
        private async Awaitable _Fade(bool @in)
        {
            if (!@in)
                await Awaitable.WaitForSecondsAsync(0.5f);
            
            fadeScreen.gameObject.SetActive(true);
            
            const float fadeTime = 0.25f;
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
            if(!GetSceneName(sceneId).Contains("Core"))
                _Fade(false);
            
            copyCode.gameObject.SetActive(IsOnLobby);
            leaveGame.gameObject.SetActive(!IsOnLobby);
            restartLevel.gameObject.SetActive(!IsOnLobby && IsServer);
            LoadingLobby(IsOnLobby || isGameStarted);
            
            lobbyMusic.gameObject.SetActive(IsOnLobby);
            gameMusic.gameObject.SetActive(!IsOnLobby);
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
            {
                #if UNITY_EDITOR
                SaveData.currentLevel = FindActiveLevel();
                #endif
                LoadLevel();
            }
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

    [Serializable]
    public class FadePacket : IPacket
    {
        public bool IsIn { get; set; }
        
        public void Serialize(BinaryWriter writer)
        {
            writer.Write(IsIn);
        }

        public void Deserialize(BinaryReader reader)
        {
            IsIn = reader.ReadBoolean();
        }
    }
}