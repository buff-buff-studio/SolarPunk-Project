using NetBuff;
using UnityEngine;

//#if UNITY_EDITOR
using UnityEngine.SceneManagement;
using Solis.Core;
using Solis.Data;
//#endif

namespace Solis.Misc
{
    /// <summary>
    /// This class is used to initialize the game.
    /// It is used to load the core scene and initialize the network manager.
    /// </summary>
    public class GameInitializerHelper : MonoBehaviour
    {
        #region Inspector Fields
        //#if UNITY_EDITOR
        public CharacterType defaultType = CharacterType.Human;
        //#endif
        #endregion
        
        #region Unity Callbacks
        private void OnEnable()
        {
            if (NetworkManager.Instance != null)
            {
                Destroy(gameObject);
                #if UNITY_EDITOR
                return;
                #endif
            }
            
            //#if UNITY_EDITOR
            SolisNetworkManager.usingRelay = false;
            SolisNetworkManager.sceneToLoad = gameObject.scene.name;
            SolisNetworkManager.defaultType = defaultType;
            SceneManager.LoadScene("Core");
            //#endif
        }
        #endregion
    }
}