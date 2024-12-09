using System;
using NetBuff;
using Solis.Core;
using Solis.Player;
using UnityEngine;

namespace Solis.Misc.Props
{
    /// <summary>
    /// When the player collides with this object, the level will be exited
    /// </summary>
    public class LevelExit : MonoBehaviour
    {
        private PlayerControllerBase playerInsideBox = null;
        private static int _playerCount = -1;
        #region Unity Callbacks

        private void OnTriggerEnter(Collider other)
        {
            if(_playerCount <= 0)
                _playerCount = FindObjectsByType<PlayerControllerBase>(FindObjectsSortMode.None).Length;

            var controller = other.GetComponent<PlayerControllerBase>();
            if (controller == null)
                return;
            
            if(NetworkManager.Instance == null)
                return;

            if (!NetworkManager.Instance.IsServerRunning)
                return;

            var game = GameManager.Instance;
            
            if (game == null)
                return;

            if (_playerCount > 1)
            {
                if (playerInsideBox != null)
                {
                    game.SaveData.currentLevel++;
                    game.LoadLevel();
                }
                else
                {
                    playerInsideBox = controller;
                }
            }
            else
            {
                game.SaveData.currentLevel++;
                game.LoadLevel();
            }
        }

        private void OnTriggerExit(Collider other)
        {
            playerInsideBox = null;
        }

#if UNITY_EDITOR
        private BoxCollider _boxCollider;

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.green;
            Gizmos.matrix = transform.localToWorldMatrix;
            //Gizmos.DrawCube(Vector3.zero, Vector3.one);
            Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.green;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(Vector3.zero, Vector3.one);
        }
#endif

        #endregion
    }
}