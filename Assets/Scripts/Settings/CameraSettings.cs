using System.Collections;
using System.Collections.Generic;
using Cinemachine;
using Solis.Settings;
using Solis.Data;
using Solis.Misc.Multicam;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class CameraSettings : MonoBehaviour
{
    [SerializeField]
    private CinemachineFreeLook freeLookCamera;
    [SerializeField]
    private List<VolumeProfile> pfxVolumes;
    [SerializeField]
    private SettingsData settingsData;

    private const float CamSenseX = 450;
    private const float CamSenseY = 3.5f;
    
    private bool isPaused;
    private float _senseX, _senseY;
    private bool pfxActive = true;

    private void Awake()
    {
        ApplyCameraSettings();
        SettingsManager.OnSettingsChanged += ApplyCameraSettings;
    }
    
    private void OnEnable()
    {
        SettingsManager.OnSettingsChanged += ApplyCameraSettings;
        
        PauseManager.OnPause += OnPause;
        CinematicController.OnCinematicStarted += OnLevelCutsceneOnOnCinematicStarted;
        CinematicController.OnCinematicEnded += OnLevelCutsceneOnOnCinematicEnded;
    }

    private void OnDestroy()
    {
        SettingsManager.OnSettingsChanged -= ApplyCameraSettings;
        
        PauseManager.OnPause -= OnPause;
        CinematicController.OnCinematicStarted -= OnLevelCutsceneOnOnCinematicStarted;
        CinematicController.OnCinematicEnded -= OnLevelCutsceneOnOnCinematicEnded;
    }

    private void OnDisable()
    {
        SettingsManager.OnSettingsChanged -= ApplyCameraSettings;
        
        PauseManager.OnPause -= OnPause;
        CinematicController.OnCinematicStarted -= OnLevelCutsceneOnOnCinematicStarted;
        CinematicController.OnCinematicEnded -= OnLevelCutsceneOnOnCinematicEnded;
    }
    
    private void ApplyCameraSettings()
    {
        _senseX = settingsData.sliderItems["cameraSensitivity"] * CamSenseX;
        _senseY = settingsData.sliderItems["cameraSensitivity"] * CamSenseY;
        if (isPaused)
        {
            freeLookCamera.m_XAxis.m_MaxSpeed = 0;
            freeLookCamera.m_YAxis.m_MaxSpeed = 0;
        }else
        {
            freeLookCamera.m_XAxis.m_MaxSpeed = _senseX;
            freeLookCamera.m_YAxis.m_MaxSpeed = _senseY;
        }
        freeLookCamera.m_XAxis.m_InvertInput = settingsData.toggleItems["invertXAxis"];
        freeLookCamera.m_YAxis.m_InvertInput = settingsData.toggleItems["invertYAxis"];

        pfxVolumes.ForEach(pfx =>
        {
            if (settingsData.TryGet<bool>("pfx") != pfxActive)
            {
                var value = settingsData.TryGet<bool>("pfx");
                foreach (var volumeComponent in pfx.components)
                {
                    volumeComponent.active = value;
                }
                pfxActive = value;
            }

            if(pfx.TryGet(out MotionBlur motionBlur))
                motionBlur.active = settingsData.TryGet<bool>("motionBlur");
            if(pfx.TryGet(out VolumetricFogVolumeComponent volumetricFog))
                volumetricFog.active = settingsData.TryGet<bool>("volumetricLight");
        });
    }
    
    private void OnLevelCutsceneOnOnCinematicStarted() => freeLookCamera.enabled = false;
    private void OnLevelCutsceneOnOnCinematicEnded()
    {
        freeLookCamera.enabled = true;
    }
    
    private void OnPause(bool isPaused)
    {
        this.isPaused = isPaused;
        if (isPaused)
        {
            freeLookCamera.m_XAxis.m_MaxSpeed = 0;
            freeLookCamera.m_YAxis.m_MaxSpeed = 0;
        }
        else
        {
            freeLookCamera.m_XAxis.m_MaxSpeed = _senseX;
            freeLookCamera.m_YAxis.m_MaxSpeed = _senseY;
        }
    }
}