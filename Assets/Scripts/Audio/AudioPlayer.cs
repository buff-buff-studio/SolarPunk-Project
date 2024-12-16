using System;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Solis.Audio
{
    /// <summary>
    /// Represents an audio player that can play audio clips.
    /// Provides methods to control the audio player.
    /// </summary>
    [Serializable]
    public class AudioPlayer
    {
        #region Public Properties
        /// <summary>
        /// Represents the mode of the audio player
        /// </summary>
        public AudioPlayerMode Mode { get; private set; } = AudioPlayerMode.Normal;
        
        /// <summary>
        /// Represents the type of the audio player
        /// </summary>
        public AudioType AudioType { get; private set; }
        
        /// <summary>
        /// Represents the audio clip of the audio player
        /// </summary>
        public AudioClip AudioClip { get; private set; }
        
        /// <summary>
        /// Represents the audio source of the audio player
        /// </summary>
        public AudioSource AudioSource { get; private set; }
        
        /// <summary>
        /// Represents the audio system of the audio player
        /// </summary>
        public AudioSystem AudioSystem { get; private set; }
        
        /// <summary>
        /// Callback when the audio player ends. Not called if the audio player is looping
        /// </summary>
        public Action OnEnd { get; set; }
        
        /// <summary>
        /// Current volume of the audio player
        /// </summary>
        public float Volume { get; private set; }
        
        /// <summary>
        /// Returns true if the audio source is playing
        /// </summary>
        public bool IsPlaying => AudioSource.isPlaying;
        
        /// <summary>
        /// Returns true if the audio source is paused
        /// </summary>
        public bool IsPaused => !AudioSource.isPlaying;
        
        /// <summary>
        /// Returns true if the audio source has ended and is not looping
        /// </summary>
        public bool HasEnded => AudioSource.time >= AudioClip.length && !AudioSource.loop;
        #endregion
        
        #region Constructors
        public AudioPlayer(AudioType audioType, AudioClip audioClip, AudioSource audioSource, AudioSystem audioSystem, float volume)
        {
            AudioType = audioType;
            AudioClip = audioClip;
            AudioSource = audioSource;
            AudioSystem = audioSystem;
            Volume = volume;
            
            audioSource.clip = audioClip;
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// Starts playing the audio clip
        /// </summary>
        /// <param name="loop"></param>
        /// <returns></returns>
        public AudioPlayer Play(bool loop = false)
        {
            AudioSource.loop = loop;
            AudioSource.Play();
            return this;
        }

        /// <summary>
        /// Restarts the audio clip
        /// </summary>
        /// <returns></returns>
        public AudioPlayer Restart()
        {
            AudioSource.time = 0;
            AudioSource.Play();
            return this;
        }
        
        /// <summary>
        /// Stops the audio clip
        /// </summary>
        /// <returns></returns>
        public AudioPlayer Stop()
        {
            AudioSource.Stop();
            return this;
        }
        
        /// <summary>
        /// Pauses the audio clip
        /// </summary>
        /// <returns></returns>
        public AudioPlayer Pause()
        {
            AudioSource.Pause();
            return this;
        }
        
        /// <summary>
        /// Sets the audio source to a fixed position
        /// </summary>
        /// <param name="position"></param>
        /// <returns></returns>
        public AudioPlayer At(Vector3 position)
        {
            Mode = AudioPlayerMode.Location;
            var transform = AudioSource.transform;
            transform.parent = AudioSystem.transform;
            transform.position = position;
            AudioSource.spatialBlend = 1;
            return this;
        }
        
        /// <summary>
        /// Sets the audio source to follow a target.
        /// Target is the parent and offset is the local position
        /// </summary>
        /// <param name="target"></param>
        /// <param name="offset"></param>
        /// <returns></returns>
        public AudioPlayer Following(Transform target, Vector3 offset)
        {
            Mode = AudioPlayerMode.Target;
            var transform = AudioSource.transform;
            transform.SetParent(target);
            transform.localPosition = offset;
            return this;
        }
        
        /// <summary>
        /// Sets the audio source to follow a target
        /// </summary>
        /// <param name="target"></param>
        /// <returns></returns>
        public AudioPlayer Following(Transform target)
        {
            return Following(target, Vector3.zero);
        }
        
        /// <summary>
        /// Sets the volume of the audio source
        /// </summary>
        /// <param name="volume"></param>
        /// <returns></returns>
        public AudioPlayer SetVolume(float volume)
        {
            AudioSource.volume = Volume = volume;
            return this;
        }
        
        /// <summary>
        /// Kills the audio source, removing it from the audio system
        /// </summary>
        public void Kill()
        {
            AudioSystem.Kill(this);
        }
        #endregion
    }
    
    /// <summary>
    /// Used internally to transition between music tracks
    /// </summary>
    [Serializable]
    public class AudioMusicTransition
    {
        #region Public Properties
        /// <summary>
        /// The audio player that the transition is coming from
        /// </summary>
        public AudioPlayer From {get; private set;}
        
        /// <summary>
        /// The audio player that the transition is going to
        /// </summary>
        public AudioPlayer To {get; private set;}
        
        /// <summary>
        /// Current time of the transition
        /// </summary>
        public float Time {get; private set;}
        
        /// <summary>
        /// Total duration of the transition
        /// </summary>
        public float Duration {get; private set;}
        #endregion
        
        #region Constructors
        public AudioMusicTransition(AudioPlayer from, AudioPlayer to, float duration)
        {
            From = from;
            To = to;
            Duration = duration;
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// Ticks the transition, updating the volume of the audio players
        /// </summary>
        /// <param name="deltaTime"></param>
        /// <returns></returns>
        public bool Tick(float deltaTime)
        {
            Time += deltaTime;
            var t = Mathf.Clamp01(Time / Duration);
            
            From.AudioSource.volume = (1 - t);
            To.AudioSource.volume = t;

            if (Time < Duration)
                return false;
            
            From.Kill();
            return true;
        }
        #endregion
    }
    
    /// <summary>
    /// Represents the type of the audio
    /// </summary>
    [Serializable]
    public enum AudioType
    {
        /// <summary>
        /// Represents a sound effect
        /// </summary>
        Vfx,
        
        /// <summary>
        /// Represents a music track
        /// </summary>
        Music,
        Character
    }
    
    /// <summary>
    /// Represents the mode of the audio player
    /// </summary>
    [Serializable]
    public enum AudioPlayerMode
    {
        /// <summary>
        /// The audio player is in normal mode.
        /// The audio source is exactly at the camera position.
        /// </summary>
        Normal,
        
        /// <summary>
        /// The audio player is in location mode.
        /// The audio source is at a fixed position.
        /// </summary>
        Location,
        
        /// <summary>
        /// The audio player is in target mode.
        /// The audio source is following a target.
        /// </summary>
        Target,
    }
    
    /// <summary>
    /// Represents an audio clip with a volume
    /// </summary>
    [Serializable]
    public class Audio
    {
        #region Inspector Fields
        public AudioClip clip;
        public float volume = 1f;
        #endregion
    }
    
    #if UNITY_EDITOR
    [CustomPropertyDrawer(typeof(Audio))]
    public class AudioDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            
            var clipRect = new Rect(position.x, position.y, position.width - 30, position.height);
            EditorGUI.PropertyField(clipRect, property.FindPropertyRelative("clip"), GUIContent.none);
            
            var volumeRect = new Rect(position.x + position.width - 30, position.y, 30, position.height);
            EditorGUI.PropertyField(volumeRect, property.FindPropertyRelative("volume"), GUIContent.none);
            
            EditorGUI.EndProperty();
        }
    }
    #endif
}