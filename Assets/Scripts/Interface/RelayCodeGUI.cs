using System;
using System.Collections;
using System.Collections.Generic;
using Interface;
using NetBuff;
using NetBuff.Components;
using NetBuff.Relays;
using Solis.Core;
using UnityEngine;
using UnityEngine.Serialization;

public class RelayCodeGUI : NetworkBehaviour
{
    public RelayNetworkManager relayNetworkManager;

    public string code = "";
    public Label relayCodeLobby;
    public Label relayCodeGame;

    public string Code
    {
        get => code;
        set
        {
            code = value;
            UpdateLabel();
        }
    }

    public override void OnSpawned(bool isRetroactive)
    {
        base.OnSpawned(isRetroactive);
        relayNetworkManager ??= (RelayNetworkManager) NetworkManager.Instance;

        if (relayNetworkManager)
            UpdateLabel();
        else this.enabled = false;
    }

    public override void OnSceneLoaded(int sceneId)
    {
        var lobby = GameManager.Instance.IsOnLobby;
        relayCodeLobby.gameObject.SetActive(lobby);
        relayCodeGame.gameObject.SetActive(!lobby);

        if (relayNetworkManager)
            UpdateLabel();
        else this.enabled = false;
    }

    private void UpdateLabel()
    {
        var hasRelay = relayNetworkManager.Transport.Type != NetworkTransport.EnvironmentType.None;
        var suffix = hasRelay ? ($" {code}") : "";
        var localizationKey = hasRelay ? "code.title" : "code.norelay";

        relayCodeGame.SetSuffix = suffix;
        relayCodeLobby.SetSuffix = suffix;

        relayCodeGame.SetBuffer = localizationKey;
        relayCodeLobby.SetBuffer = localizationKey;

        relayCodeGame.UpdateLabel();
        relayCodeLobby.UpdateLabel();

        this.enabled = hasRelay;
    }
}
