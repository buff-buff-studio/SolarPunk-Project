using System;
using NetBuff.Components;
using NetBuff.Misc;
using Solis.Data;
using TMPro;
using UnityEngine;

namespace Solis.Misc.Props
{
    /// <summary>
    /// Defines a spawn point in the lobby.
    /// </summary>
    public class LobbySpawnPoint : NetworkBehaviour
    {
        #region Inspector Fields
        [ServerOnly]
        [SerializeField, HideInInspector]
        public int occupiedBy = -1;
        [ServerOnly]
        [SerializeField]
        public CharacterTypeFilter playerTypeFilter = CharacterTypeFilter.Both;
        public TextMeshPro playerName;
        public StringNetworkValue playerNameValue = new(string.Empty);
        #endregion

        private void Start()
        {
            WithValues(playerNameValue);
            playerNameValue.OnValueChanged += (oldValue, newValue) => playerName.text = newValue;
        }
    }
}