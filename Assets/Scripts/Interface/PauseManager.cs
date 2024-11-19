using System;
using System.Collections;
using System.Collections.Generic;
using NetBuff;
using Solis.Core;
using Solis.Data;
using Solis.Interface.Input;
using Solis.Player;
using Solis.Settings;
using Solis.UI;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PauseManager : WindowManager
{

    #region Public Fields

    [Header("References")]
    public SolisNetworkManager networkManager;
    public GameManager gameManager;
    public GameObject pauseVolume;

    [Header("Players List")]
    public TextMeshProUGUI ninaUsername;
    public TextMeshProUGUI ninaPing;

    public TextMeshProUGUI ramUsername;
    public TextMeshProUGUI ramPing;

    #endregion

    #region Private Fields

    private CanvasGroup pauseMenu;
    private bool isPaused = false;

    private string ninaUsernameText;
    private string ramUsernameText;

    #endregion
    
    public static Action<bool> OnPause;

    private void Awake()
    {
        TryGetComponent(out pauseMenu);

        SetVisible(false);
    }

    private void OnEnable()
    {
        networkManager = NetworkManager.Instance as SolisNetworkManager;
    }

    private void SetVisible(bool visible)
    {
        pauseMenu.alpha = visible ? 1 : 0;
        pauseMenu.interactable = visible;
        pauseMenu.blocksRaycasts = visible;

        DetectServerList();
    }

    private void DetectServerList()
    {
        if(!isPaused) return;

        if (string.IsNullOrEmpty(ninaUsernameText))
        {
            var n = FindFirstObjectByType<PlayerControllerHuman>();
            if (n != null)
            {
                n.username.OnValueChanged += NinaUserUpdate;
                NinaUserUpdate("", n.username.Value);
                ninaPing.text = n.OwnerId == 1 ? "Host" : "";
            }else ninaUsername.text = "Waiting for player...";
        }
        if (string.IsNullOrEmpty(ramUsernameText))
        {
            var r = FindFirstObjectByType<PlayerControllerRobot>();
            if (r != null)
            {
                r.username.OnValueChanged += RAMUserUpdate;
                RAMUserUpdate("", r.username.Value);
                ramPing.text = r.OwnerId == 1 ? "Host" : "";
            }else ramUsername.text = "Waiting for player...";
        }

        if(ramPing.text == "Host") ramPing.transform.parent.SetAsFirstSibling();
        else ninaPing.transform.parent.SetAsFirstSibling();
    }

    private void NinaUserUpdate(string _, string value)
    {
        ninaUsernameText = value;
        ninaUsername.text = value;
    }

    private void RAMUserUpdate(string _, string value)
    {
        ramUsernameText = value;
        ramUsername.text = value;
    }

    private void Update()
    {
        if(gameManager.IsOnLobby || !gameManager.isGameStarted) return;
        if (SolisInput.GetKeyDown("Pause"))
        {
            if (isPaused) ResumeGame();
            else PauseGame();
        }
    }
    
    public void ResumeGame()
    {
        isPaused = false;
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
        pauseVolume.SetActive(isPaused);
        SetVisible(false);
        ChangeWindow(0);
        OnPause?.Invoke(false);
    }

    public void Pause()
    {
        isPaused = true;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        OnPause?.Invoke(true);
    }

    public void Resume()
    {
        isPaused = false;
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
       OnPause?.Invoke(false);
    }
    
    public void PauseGame()
    {
        isPaused = true;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        pauseVolume.SetActive(isPaused);
        SetVisible(true);
        ChangeWindow(0);
        OnPause?.Invoke(true);
    }
    
    public void BugReport()
    {
        Application.OpenURL("https://forms.gle/vmPGyamH8Dcc8S9J8"); 
    }
    
    public void ExitGame()
    {
        networkManager.Close();
    }
}
