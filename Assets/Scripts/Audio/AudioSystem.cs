using System;
using System.Collections.Generic;
using System.Linq;
using Solis.Data;
using Solis.Settings;
using UnityEngine;
using UnityEngine.Audio;

namespace Solis.Audio
{
    /// <summary>
    /// Used to play audio in the game.
    /// </summary>
    public class AudioSystem : MonoBehaviour
    {
        #region Public Static Properties
        /// <summary>
        /// Returns the current instance of the audio system.
        /// </summary>
        public static AudioSystem Instance { get; private set; }
        #endregion
        
        #region Inspector Fields
        [Header("REFERENCES")]
        public AudioPalette[] audioPalettes = Array.Empty<AudioPalette>();

        public AudioMixerGroup vfxMixer;
        public AudioMixerGroup musicMixer;
        public AudioMixerGroup masterMixer;
        public AudioMixerGroup characterMixer;
        public AudioMixerGroup dialogMixer;

        [Header("SETTINGS")]
        public int audioSourcePoolSize = 10;
        public SettingsData settingData;
        #endregion

        #region Private Fields
        [SerializeField, HideInInspector]
        private List<AudioSource> freeAudioSources = new();
        
        [SerializeField, HideInInspector]
        private List<AudioPlayer> audioPlayers = new();
        
        [SerializeField, HideInInspector]
        private List<AudioMusicTransition> musicTransitions = new();
        
        #endregion

        #region Public Properties
        /// <summary>
        /// Returns the source that is playing the current music.
        /// </summary>
        public AudioPlayer MusicPlayer => audioPlayers.FirstOrDefault(player => player.AudioType == AudioType.Music);
        #endregion
        
        #region Unity Callbacks
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            OnSettingsChanged();
            for (var i = 0; i < audioSourcePoolSize; i++)
                _CreateAudioSource(i);
        }

        private void OnEnable()
        {
            Instance = this;
            
            SettingsManager.OnSettingsChanged += OnSettingsChanged;
            
            DontDestroyOnLoad(gameObject);
            OnSettingsChanged();
        }

        private void OnDisable()
        {
            if (Instance == this)
                Instance = null;
            
            SettingsManager.OnSettingsChanged -= OnSettingsChanged;
        }

        private void Update()
        {
            for (var i = audioPlayers.Count - 1; i >= 0; i--)
            {
                var player = audioPlayers[i];

                if (player.AudioSource == null)
                {
                    audioPlayers.RemoveAt(i);
                    continue;
                }

                if (!player.HasEnded) 
                    continue;
                
                player.OnEnd?.Invoke();
                Kill(player);
            }

            var time = Time.deltaTime;
            for (var i = musicTransitions.Count - 1; i >= 0; i--)
            {
                if (musicTransitions[i].Tick(time))
                    musicTransitions.RemoveAt(i);
            }
        }
        
        private void OnSettingsChanged()
        {
            
        }

        private void FixedUpdate()
        {
            var musicVolume = Mathf.Clamp(settingData.sliderItems.GetValueOrDefault("musicVolume", 0) / 100, 0.0001f, 1f);
            musicMixer.audioMixer.SetFloat("musicVolume", Mathf.Log10(musicVolume) * 20);

            var fxVolume = Mathf.Clamp(settingData.sliderItems["sfxVolume"] / 100, 0.0001f, 1f);
            vfxMixer.audioMixer.SetFloat("sfxVolume", Mathf.Log10(fxVolume) * 20);
            
            var masterVolume = Mathf.Clamp(settingData.sliderItems.GetValueOrDefault("masterVolume", 0) / 100, 0.0001f, 1f);
            masterMixer.audioMixer.SetFloat("masterVolume", Mathf.Log10(masterVolume) * 20);
            
            var characterVolume = Mathf.Clamp(settingData.sliderItems.GetValueOrDefault("characterVolume", 0) / 100, 0.0001f, 1f);
            characterMixer.audioMixer.SetFloat("characterVolume", Mathf.Log10(characterVolume) * 20);

            var dialogVolume = Mathf.Clamp(settingData.sliderItems.GetValueOrDefault("dialogVolume", 0) / 100, 0.0001f, 1f);
            dialogMixer.audioMixer.SetFloat("dialogVolume", Mathf.Log10(dialogVolume) * 20);
        }

        #endregion
        
        #region Public Methods
        /// <summary>
        /// Starts playing a music. The transition parameter is the time in seconds that the current music will take to fade out.
        /// </summary>
        /// <param name="audioName"></param>
        /// <param name="transition"></param>
        /// <returns></returns>
        public AudioPlayer PlayMusic(string audioName, float transition = 0)
        {
            var currentMusic = MusicPlayer;
            var player = _CreatePlayer(audioName, AudioType.Music);
            player.Play(true);
            
            if (currentMusic == null)
                return player;
            
            musicTransitions.Add(new AudioMusicTransition(currentMusic, player, transition));
            return player;
        }
        
        /// <summary>
        /// Starts playing a sound effect. The loop parameter is used to determine if the sound will loop or not.
        /// </summary>
        /// <param name="audioName"></param>
        /// <param name="loop"></param>
        /// <returns></returns>
        public AudioPlayer PlayVfx(string audioName, bool loop = false)
        {
            var player = CreateVfx(audioName);
            return player?.Play(loop);
        }

        /// <summary>
        /// Creates a sound effect player, without playing it.
        /// </summary>
        /// <param name="audioName"></param>
        /// <returns></returns>
        public AudioPlayer CreateVfx(string audioName)
        {
            return _CreatePlayer(audioName, AudioType.Vfx);
        }

        /// <summary>
        /// Starts playing a sound effect. The loop parameter is used to determine if the sound will loop or not.
        /// </summary>
        /// <param name="audioName"></param>
        /// <param name="loop"></param>
        /// <returns></returns>
        public AudioPlayer PlayDialog(string audioName, bool loop = false)
        {
            var player = CreateDialog(audioName);
            return player?.Play(loop);
        }

        /// <summary>
        /// Creates a sound effect player, without playing it.
        /// </summary>
        /// <param name="audioName"></param>
        /// <returns></returns>
        public AudioPlayer CreateDialog(string audioName)
        {
            return _CreatePlayer(audioName, AudioType.Dialog);
        }
        
        /// <summary>
        /// Creates a sound effect player, without playing it.
        /// </summary>
        /// <param name="audioName"></param>
        /// <returns></returns>
        public AudioPlayer CreateCharacter(string audioName)
        {
            return _CreatePlayer(audioName, AudioType.Character);
        }
        
        
        /// <summary>
        /// Kills a player, stopping the audio and returning the audio source to the pool.
        /// </summary>
        /// <param name="player"></param>
        public void Kill(AudioPlayer player)
        {
            player.Stop();
            
            var t = player.AudioSource.transform;
            t.parent = transform;
            t.localPosition = Vector3.zero;
            t.gameObject.SetActive(false);
            
            audioPlayers.Remove(player);
            freeAudioSources.Add(player.AudioSource);
        }
        
        /// <summary>
        /// Kills all players, stopping all audio and returning all audio sources to the pool.
        /// </summary>
        public void KillAll()
        {
            for (var i = audioPlayers.Count - 1; i >= 0; i--)
            {
                Kill(audioPlayers[i]);
            }
        }
        
        /// <summary>
        /// Kills all sound effects players, stopping all sound effects and returning all audio sources to the pool.
        /// </summary>
        public void KillAllVfx()
        {
            for (var i = audioPlayers.Count - 1; i >= 0; i--)
            {
                if (audioPlayers[i].AudioType == AudioType.Vfx)
                    Kill(audioPlayers[i]);
            }
        }
        #endregion
        
        #region Private Methods
        private AudioSource _GetFreeAudioSource()
        {
            if (freeAudioSources.Count == 0)
            {
                _CreateAudioSource(-1);
            }

            var audioSource = freeAudioSources[0];
            freeAudioSources.RemoveAt(0);
            audioSource.gameObject.SetActive(true);
            
            //Reset values
            audioSource.spatialBlend = 0;
            audioSource.volume = 1;
            audioSource.pitch = 1;
            audioSource.loop = false;
            
            return audioSource;
        }
        
        private void _CreateAudioSource(int n)
        {
            var go = new GameObject($"AudioSource {n}");
            go.SetActive(false);
            go.transform.SetParent(transform);
            var audioSource = go.AddComponent<AudioSource>();
            audioSource.spatialBlend = 0;
            audioSource.transform.localPosition = Vector3.zero;
            freeAudioSources.Add(audioSource);
        }
        
        private AudioPlayer _CreatePlayer(string audioName, AudioType type)
        {
            foreach (var palette in audioPalettes)
            {
                if (palette.TryGetAudio(audioName, out var clip))
                {
                    return _CreatePlayer(clip.clip, type, clip.volume);
                }
            }
            
            return null;
        }
        
        private AudioPlayer _CreatePlayer(AudioClip clip, AudioType type, float volume = 1)
        {
            var source = _GetFreeAudioSource();
            
            var player = new AudioPlayer(type, clip, source, this, volume);
            audioPlayers.Add(player);

            AudioMixerGroup group = type switch
            {
                AudioType.Vfx => vfxMixer,
                AudioType.Music => musicMixer,
                AudioType.Character => characterMixer,
                AudioType.Dialog => dialogMixer,
                _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
            };

            player.AudioSource.outputAudioMixerGroup = group;
            
            return player;
        }
        #endregion

        #region Public Static Methods
        /// <summary>
        /// Starts playing a music. The transition parameter is the time in seconds that the current music will take to fade out.
        /// </summary>
        /// <param name="audioName"></param>
        /// <param name="transition"></param>
        /// <returns></returns>
        public static AudioPlayer PlayMusicStatic(string audioName, float transition = 0)
        {
            return Instance.PlayMusic(audioName, transition);
        }
        
        /// <summary>
        /// Starts playing a sound effect. The loop parameter is used to determine if the sound will loop or not.
        /// </summary>
        /// <param name="audioName"></param>
        /// <param name="loop"></param>
        /// <returns></returns>
        public static AudioPlayer PlayVfxStatic(string audioName, bool loop = false)
        {
            return Instance.PlayVfx(audioName, loop);
        }

        public static AudioPlayer PlayDialogStatic (string audioName, bool loop = false)
        {
            return Instance.PlayDialog(audioName, loop);
        }
        
        /// <summary>
        /// Creates a sound effect player, without playing it.
        /// </summary>
        /// <param name="audioName"></param>
        /// <returns></returns>
        public static AudioPlayer CreateVfxStatic(string audioName)
        {
            return Instance.CreateVfx(audioName);
        }
        #endregion
    }
}