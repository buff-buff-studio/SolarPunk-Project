using System;
using System.Collections.Generic;
using Cinemachine;
using NetBuff.Components;
using NetBuff.Misc;
using Solis.Packets;
using UnityEngine;
using UnityEngine.SceneManagement;

#if UNITY_EDITOR
using Solis.Core;
using Solis.Interface.Input;
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace Solis.Misc.Multicam
{
    [RequireComponent(typeof(Animation))]
    [DisallowMultipleComponent]
    [Icon("Assets/Editor/Multicam/Icons/CinematicControllerIcon.png")]
    public class CinematicController : NetworkBehaviour
    {
        public static CinematicController Instance { get; private set; }

        [Header("REFERENCES")]
        [SerializeField]
        internal CinemachineVirtualCamera virtualCamera;
        [SerializeField]
        internal new Animation animation;
        [SerializeField]
        private Transform _follow, _lookAt;

        public Transform Camera => virtualCamera.transform;

        [Space] [Header("SETTINGS")] [SerializeField]
        private bool playOnAwake = true;

        [Space]
        [Header("CINEMATIC")]
        public int currentRoll;
        [SerializeField] internal List<CinematicRoll> rolls;
        public CinematicRoll CurrentRoll => rolls[currentRoll];
        public List<string> GetRollsName => rolls.ConvertAll(roll => roll.name);

        public static bool IsPlaying = false;
        public static event Action OnCinematicStarted;
        public static event Action OnCinematicEnded;

        private bool _isPaused = false;

#if UNITY_EDITOR
        protected internal bool NeedToBake = false;
#endif

        private void Awake()
        {
            Instance = this;
            currentRoll = 0;

            TryGetComponent(out animation);
            rolls.ForEach(roll =>
            {
                animation.AddClip(roll.clip, roll.name);
            });
            TryFindTarget();
            virtualCamera.m_Follow = _follow;
            virtualCamera.m_LookAt = _lookAt;

            MulticamCamera.Instance!.SetCinematic(virtualCamera, playOnAwake);
        }

        private void OnEnable()
        {
            PauseManager.OnPause += isPaused => { _isPaused = isPaused; };
            PacketListener.GetPacketListener<PlayCutscenePacket>().AddClientListener(PlayByPacket);
        }

        private void OnDisable()
        {
            PauseManager.OnPause -= isPaused => { _isPaused = isPaused; };
            PacketListener.GetPacketListener<PlayCutscenePacket>().RemoveClientListener(PlayByPacket);
        }

        private void Update()
        {
            if(!IsPlaying) return;

#if UNITY_EDITOR
            if (!MulticamCamera.Instance.PlayerFound && SolisInput.GetKeyDown("Skip"))
                Stop();
#endif

            if (!animation.isPlaying) Stop();

            if (!CurrentRoll.CurrentFrame.invoked)
            {
                CurrentRoll.CurrentFrame.onFrameShow?.Invoke();
                CurrentRoll.CurrentFrame.invoked = true;
            }
        }

        public void Play()
        { Play(currentRoll);}

        private bool PlayByPacket(PlayCutscenePacket arg1)
        {
            if (arg1.CutsceneIndex < 0 || arg1.CutsceneIndex >= rolls.Count) return false;
            Debug.Log($"Received Play Cutscene Packet: {arg1.CutsceneIndex}");
            Play(arg1.CutsceneIndex);
            return true;
        }

        public void Play(int roll)
        {
            currentRoll = roll;
            CurrentRoll.currentFrame = 0;
            CurrentRoll.framing.ForEach(frame => { frame.invoked = false; });
            animation.clip = animation.GetClip(rolls[currentRoll].name);
            animation.Play();

            MulticamCamera.Instance.ChangeCameraState(MulticamCamera.CameraState.Cinematic, CurrentRoll.blend, CurrentRoll.blendTime);
            IsPlaying = true;

            OnCinematicStarted?.Invoke();
        }

        public void Stop()
        {
#if UNITY_EDITOR
            if (!MulticamCamera.Instance.PlayerFound)
            {
                Debug.LogWarning("No player found. Respawn all players, if you have permission.");
                if (IsServer)
                    GameManager.Instance.RespawnAllPlayers();
            }
#endif

            MulticamCamera.Instance.ChangeCameraState(MulticamCamera.CameraState.Gameplay, CurrentRoll.blend, CurrentRoll.blendTime);

            animation.Stop();
            IsPlaying = false;
            OnCinematicEnded?.Invoke();
        }

        public void Reset()
        {
            Stop();
            Play(currentRoll);
        }

        public void OnFrameChange(int frame)
        {
            CurrentRoll.currentFrame = frame;
        }

        private void TryFindTarget()
        {
            if(_follow == null)
            {
                _follow = transform.Find("Follow");
                if(_follow == null)
                {
                    _follow = new GameObject("Follow").transform;
                    _follow.SetParent(this.transform);
                }
            }
            if(_lookAt == null)
            {
                _lookAt = transform.Find("LookAt");
                if(_lookAt == null)
                {
                    _lookAt = new GameObject("LookAt").transform;
                    _lookAt.SetParent(this.transform);
                }
            }

            _follow.position = CurrentRoll.GetFollow();
            _lookAt.position = CurrentRoll.GetLookAt();
        }

#if UNITY_EDITOR
        public void AddFrame()
        {
            if (rolls == null) rolls = new List<CinematicRoll>();
            if (rolls.Count == 0) rolls.Add(new CinematicRoll {name = "Roll 1"});
            if (rolls[currentRoll].framing == null) rolls[currentRoll].framing = new List<CinematicFrame>();
            rolls[currentRoll].framing.Add(new CinematicFrame(SceneView.lastActiveSceneView.camera));
            CurrentRoll.currentFrame = CurrentRoll.framing.Count - 1;

            EditorSceneManager.MarkSceneDirty(this.gameObject.scene);

            NeedToBake = true;
        }

        public void UpdateFrame(CameraTransition transition, CameraMovement movement)
        {
            var updatedFrame = new CinematicFrame(SceneView.lastActiveSceneView.camera);
            updatedFrame.behaviour.transition = transition;
            updatedFrame.behaviour.movement = movement;

            CurrentRoll.framing[CurrentRoll.currentFrame] = updatedFrame;

            EditorSceneManager.MarkSceneDirty(this.gameObject.scene);

            NeedToBake = true;
        }

        public AnimationClip BakeAnimation()
        {
            if (CurrentRoll.clip == null)
            {
                var c = new AnimationClip
                {
                    name = $"{rolls[currentRoll].name} - {SceneManager.GetActiveScene().name}"
                };

                if(!AssetDatabase.IsValidFolder($"Assets/Animations/Cinematic/{SceneManager.GetActiveScene().name}"))
                    AssetDatabase.CreateFolder("Assets/Animations/Cinematic", SceneManager.GetActiveScene().name);

                AssetDatabase.CreateAsset(c, $"Assets/Animations/Cinematic/{SceneManager.GetActiveScene().name}/{c.name}.anim");
                CurrentRoll.clip = c;
            }
            var clip = CurrentRoll.clip;

            var followPos = new []
            {
                new AnimationCurve(),
                new AnimationCurve(),
                new AnimationCurve()
            };
            var lookAtPos = new []
            {
                new AnimationCurve(),
                new AnimationCurve(),
                new AnimationCurve()
            };
            var events = new List<AnimationEvent>();

            var s = 0f;
            for (var i = 0; i < CurrentRoll.framing.Count; i++)
            {
                var frame = CurrentRoll.framing[i];
                var transition = frame.transition;
                var sStart = (frame.transitionDuration == 0 || transition == CameraTransition.Instant) ? s : s + frame.transitionDuration;
                var sEnd = sStart + (frame.duration <= 0 ? 0.017f : frame.duration - 0.017f);

                if (frame.movement == CameraMovement.Transition)
                {
                    sStart -= 0.017f;
                    followPos[0].AddKey(sStart, frame.follow.x);
                    followPos[1].AddKey(sStart, frame.follow.y);
                    followPos[2].AddKey(sStart, frame.follow.z);

                    lookAtPos[0].AddKey(sStart, frame.lookAt.x);
                    lookAtPos[1].AddKey(sStart, frame.lookAt.y);
                    lookAtPos[2].AddKey(sStart, frame.lookAt.z);
                }
                else
                {
                    foreach (var keyframe in AnimationCurve
                                 .Linear(sStart, frame.follow.x, sEnd, frame.follow.x).keys)
                        followPos[0].AddKey(keyframe);
                    foreach (var keyframe in AnimationCurve
                                 .Linear(sStart, frame.follow.y, sEnd, frame.follow.y).keys)
                        followPos[1].AddKey(keyframe);
                    foreach (var keyframe in AnimationCurve
                                 .Linear(sStart, frame.follow.z, sEnd, frame.follow.z).keys)
                        followPos[2].AddKey(keyframe);

                    foreach (var keyframe in AnimationCurve
                                 .Linear(sStart, frame.lookAt.x, sEnd, frame.lookAt.x).keys)
                        lookAtPos[0].AddKey(keyframe);
                    foreach (var keyframe in AnimationCurve
                                 .Linear(sStart, frame.lookAt.y, sEnd, frame.lookAt.y).keys)
                        lookAtPos[1].AddKey(keyframe);
                    foreach (var keyframe in AnimationCurve
                                 .Linear(sStart, frame.lookAt.z, sEnd, frame.lookAt.z).keys)
                        lookAtPos[2].AddKey(keyframe);
                }

                if(frame.onFrameShow.GetPersistentEventCount() > 0)
                {
                    events.Add(new AnimationEvent
                    {
                        functionName = "OnFrameChange",
                        time = sStart,
                        intParameter = i
                    });
                }
                s += frame.duration + frame.transitionDuration;
            }

            clip.ClearCurves();
            clip.SetCurve("Follow", typeof(Transform), "localPosition.x", followPos[0]);
            clip.SetCurve("Follow", typeof(Transform), "localPosition.y", followPos[1]);
            clip.SetCurve("Follow", typeof(Transform), "localPosition.z", followPos[2]);

            clip.SetCurve("LookAt", typeof(Transform), "localPosition.x", lookAtPos[0]);
            clip.SetCurve("LookAt", typeof(Transform), "localPosition.y", lookAtPos[1]);
            clip.SetCurve("LookAt", typeof(Transform), "localPosition.z", lookAtPos[2]);

            AnimationUtility.SetAnimationEvents(clip, events.ToArray());

            clip.legacy = true;

            EditorSceneManager.MarkSceneDirty(this.gameObject.scene);
            return clip;
        }

        public void SetCameraToCurrentFrame()
        {
            if(Application.isPlaying) return;
            Debug.Log("Setting Camera to Current Frame");
            TryFindTarget();
            virtualCamera.m_Follow = _follow;
            virtualCamera.m_LookAt = _lookAt;
        }

        private void OnValidate()
        {
            if (Application.isPlaying) return;

            NeedToBake = true;
            currentRoll = Mathf.Clamp(currentRoll, 0, rolls.Count - 1);
            rolls?.ForEach(roll =>
            {
                roll.currentFrame = Mathf.Clamp(roll.currentFrame, 0, roll.framing.Count - 1);
            });
        }
#endif
        private static void OnOnCinematicStarted()
        {
            OnCinematicStarted?.Invoke();
        }
    }

#if UNITY_EDITOR
    [UnityEditor.CustomEditor(typeof(CinematicController))]
    public class LevelCutsceneEditor : UnityEditor.Editor
    {
        private SerializedProperty virtualCamera;
        private SerializedProperty animation;
        private SerializedProperty follow;
        private SerializedProperty lookAt;

        private SerializedProperty playOnAwake;

        private SerializedProperty currentRoll;
        private SerializedProperty rolls;

        private CinematicController _cController;

        private void OnEnable()
        {
            _cController = (CinematicController)target;

            virtualCamera = serializedObject.FindProperty("virtualCamera");
            animation = serializedObject.FindProperty("animation");
            follow = serializedObject.FindProperty("_follow");
            lookAt = serializedObject.FindProperty("_lookAt");

            playOnAwake = serializedObject.FindProperty("playOnAwake");
            currentRoll = serializedObject.FindProperty("currentRoll");
            rolls = serializedObject.FindProperty("rolls");
        }
        public override void OnInspectorGUI()
        {
            if(_cController.rolls.Count > 0)
            {
                if (_cController.rolls.Exists(x => x.clip == null))
                {
                    EditorGUILayout.HelpBox(
                        "You have rolls without baked animations.\nPlease bake all animations before playing.",
                        MessageType.Error);
                    if (GUILayout.Button("Bake All Animations"))
                    {
                        var cRoll = _cController.currentRoll;
                        for (var i = 0; i < _cController.rolls.Count; i++)
                        {
                            _cController.currentRoll = i;
                            _cController.BakeAnimation();
                            serializedObject.ApplyModifiedProperties();
                            serializedObject.Update();
                            EditorUtility.SetDirty(_cController);
                        }

                        _cController.currentRoll = cRoll;
                        _cController.NeedToBake = false;
                    }
                }
                else if (_cController.NeedToBake)
                {
                    EditorGUILayout.HelpBox(
                        "You probably have animations that need to be baked.\nWithout baking, the camera will not move.",
                        MessageType.Warning);
                    if (GUILayout.Button("Bake All Animations"))
                    {
                        var cRoll = _cController.currentRoll;
                        for (var i = 0; i < _cController.rolls.Count; i++)
                        {
                            _cController.currentRoll = i;
                            _cController.BakeAnimation();
                            serializedObject.ApplyModifiedProperties();
                            serializedObject.Update();
                            EditorUtility.SetDirty(_cController);
                        }

                        _cController.currentRoll = cRoll;
                        _cController.NeedToBake = false;
                    }

                    if (GUILayout.Button("Dismiss"))
                    {
                        _cController.NeedToBake = false;
                    }
                }
            }
            if(Application.isPlaying)
            {
                if (GUILayout.Button("Play")) _cController.Reset();
                if (GUILayout.Button("Stop")) _cController.Stop();
            }

            base.OnInspectorGUI();
        }

        private void OnSceneGUI()
        {
            if(_cController.rolls.Count == 0) return;

            EditorGUI.BeginChangeCheck();
            var frame = _cController.CurrentRoll.framing[_cController.CurrentRoll.currentFrame];
            var newFollowPos = Handles.PositionHandle(frame.follow, Quaternion.identity);
            var newLookAtPos = Handles.RotationHandle(Quaternion.LookRotation(frame.lookAt - frame.follow), frame.follow);

            Handles.color = Color.green;
            Handles.DrawWireDisc(newFollowPos, newLookAtPos * Vector3.forward, 0.5f);
            Handles.color = Color.red;
            Handles.DrawDottedLine(newFollowPos, frame.lookAt, 5);
            Handles.color = Color.yellow;
            Handles.DrawWireCube(frame.lookAt, Vector3.one * 0.5f);

            if (EditorGUI.EndChangeCheck())
            {
                _cController.CurrentRoll.framing[_cController.CurrentRoll.currentFrame].follow = newFollowPos;
                Physics.Raycast(newFollowPos, newLookAtPos * Vector3.forward, out var hit, 1000, ~LayerMask.GetMask("InvisibleWall", "Ignore Raycast"));
                _cController.CurrentRoll.framing[_cController.CurrentRoll.currentFrame].lookAt = hit.point;
                _cController.SetCameraToCurrentFrame();
            }
        }
    }
#endif
}
