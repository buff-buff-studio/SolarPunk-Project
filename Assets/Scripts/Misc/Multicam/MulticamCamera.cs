using System;
using System.Collections.Generic;
using Cinemachine;
using UnityEngine;
using NetBuff.Components;
using NetBuff.Misc;
using Solis.Data;
using Solis.Packets;
using Solis.Player;
using TMPro;

namespace Solis.Misc.Multicam
{
    public class MulticamCamera : NetworkBehaviour
    {
        public enum CameraState
        {
            Gameplay,
            Cinematic,
            Dialogue
        }

        public static MulticamCamera Instance { get; private set; }

        #region Inspector Fields
        [Header("REFERENCES")]
        public Camera mainCamera;
        public CameraState state;
        public Transform target;

        [Header("GAMEPLAY")]
        public CinemachineFreeLook gameplayCamera;
        protected internal bool PlayerFound;

        [Header("CINEMATIC")]
        public CinemachineVirtualCamera cinematicCamera;
        public GameObject cinematicCanvas;
        public TextMeshProUGUI skipCinematicText;
        private List<int> _hasSkipped = new List<int>();

        [Header("DIALOGUE")]
        public CinemachineVirtualCamera dialogueCamera;

        private CinemachineBrain _cinemachineBrain;

        #endregion

        private Transform ram, nina, diluvio;

        #region Unity Callbacks
        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(this);

            _cinemachineBrain = mainCamera.GetComponent<CinemachineBrain>();
        }

        private void OnEnable()
        {
            PacketListener.GetPacketListener<PlayerInputPackage>().AddServerListener(OnInput);
        }

        private void OnDisable()
        {
            PacketListener.GetPacketListener<PlayerInputPackage>().RemoveServerListener(OnInput);
        }

        #endregion

        private bool OnInput(PlayerInputPackage arg1, int arg2)
        {
            Debug.Log($"MulticamCamera: OnInput {arg1.Key}");
            if (state == CameraState.Cinematic)
            {
                if (arg1.Key != KeyCode.Return) return false;
                if (_hasSkipped.Contains(arg2)) return false;
                _hasSkipped.Add(arg2);
                skipCinematicText.text = $"{_hasSkipped.Count}/2";

#if UNITY_EDITOR
                if (_hasSkipped.Count >= 1)
                {
                    CinematicController.Instance.Stop();
                    return true;
                }
#endif
                
                if (_hasSkipped.Count < 2) return false;
                CinematicController.Instance.Stop();
                return true;
            }

            return false;
        }

        #region Public Methods

        public void ChangeCameraState(CameraState newState, CinemachineBlendDefinition.Style blend = CinemachineBlendDefinition.Style.Cut, float blendTime = 0)
        {
            Debug.Log($"Changing camera state to {newState}");
            _cinemachineBrain.m_DefaultBlend = new CinemachineBlendDefinition(blend, blendTime);

            gameplayCamera.gameObject.SetActive(newState == CameraState.Gameplay);
            dialogueCamera.gameObject.SetActive(newState == CameraState.Dialogue);

            if(cinematicCamera != null) cinematicCamera.gameObject.SetActive(newState == CameraState.Cinematic);
            else if(newState == CameraState.Cinematic)
            {
                Debug.LogError("Cinematic camera is not set");
                ChangeCameraState(CameraState.Gameplay);
                return;
            }

            if(newState == CameraState.Cinematic)
            {
                _hasSkipped.Clear();
                skipCinematicText.text = "0/2";
                cinematicCanvas.SetActive(true);
            }else cinematicCanvas.SetActive(false);

            state = newState;
        }
        public Transform SetPlayerTarget(Transform follow, Transform lookAt)
        {
            gameplayCamera.Follow = follow;
            gameplayCamera.LookAt = lookAt;
            PlayerFound = true;

            if(!cinematicCamera)
            {
                ChangeCameraState(CameraState.Gameplay);
                Debug.Log("Gameplay camera is on");
            }
            return mainCamera.transform;
        }

        public void SetCinematic(CinemachineVirtualCamera cinematic, bool changeState = false)
        {
            cinematicCamera = cinematic;

            if(changeState)
            {
                ChangeCameraState(CameraState.Cinematic);
                CinematicController.Instance.Play(0);
            }
            else
            {
                Debug.Log("Cinematic camera is set");
                ChangeCameraState(CameraState.Gameplay);
            }
        }

        public void SetDialogueFocus(CharacterTypeEmote type)
        {
            switch (type)
            {
                case CharacterTypeEmote.Nina:
                    if(!nina)
                    {
                        var player = FindFirstObjectByType<PlayerControllerHuman>();
                        if (player) nina = player.dialogueLookAt;
                        else
                        {
                            Debug.LogError("Nina is not found to focus on dialogue");
                            dialogueCamera.LookAt = gameplayCamera.LookAt;
                            dialogueCamera.Follow = gameplayCamera.Follow;
                            break;
                        }
                    }
                    dialogueCamera.LookAt = nina;
                    dialogueCamera.Follow = nina;
                    break;
                case CharacterTypeEmote.RAM:
                    if(!ram)
                    {
                        var player = FindFirstObjectByType<PlayerControllerRobot>();
                        if (player) ram = player.dialogueLookAt;
                        else
                        {
                            Debug.LogError("RAM is not found to focus on dialogue");
                            dialogueCamera.LookAt = gameplayCamera.LookAt;
                            dialogueCamera.Follow = gameplayCamera.Follow;
                            break;
                        }
                    }
                    dialogueCamera.LookAt = ram;
                    dialogueCamera.Follow = ram;
                    break;
                case CharacterTypeEmote.Diluvio:
                    if(!diluvio)
                    {
                        var player = FindFirstObjectByType<PlayerControllerRobot>();
                        if (player) diluvio = player.diluvioPosition;
                        else
                        {
                            Debug.LogError("Diluvio is not found to focus on dialogue");
                            dialogueCamera.LookAt = gameplayCamera.LookAt;
                            dialogueCamera.Follow = gameplayCamera.Follow;
                            break;
                        }
                    }
                    dialogueCamera.LookAt = diluvio;
                    dialogueCamera.Follow = diluvio;
                    break;
                default:
                    Debug.LogError("This focus on dialogue is not implemented yet, the camera will follow the player instead.");
                    dialogueCamera.LookAt = gameplayCamera.LookAt;
                    dialogueCamera.Follow = gameplayCamera.Follow;
                    break;
            }

            ChangeCameraState(CameraState.Dialogue, CinemachineBlendDefinition.Style.EaseInOut, 1);
        }

        #endregion
    }
}