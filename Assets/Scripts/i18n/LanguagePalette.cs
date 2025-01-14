using System;
using UnityEngine;

namespace Solis.i18n
{
    /// <summary>
    /// Used to store the language entries for the game.
    /// </summary>
    [CreateAssetMenu(menuName = "Solis/i18n/Language Palette", fileName = "LanguagePalette")]
    public class LanguagePalette : ScriptableObject
    {
        #region Private Static Fields
        private static LanguagePalette _instance;
        #endregion
        
        #region Public Static Properties
        /// <summary>
        /// Returns the instance of the LanguageManager.
        /// </summary>
        public static LanguagePalette Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = Resources.Load<LanguagePalette>("LanguagePalette");
                }
                
                return _instance;
            }
        }
        
        /// <summary>
        /// Called when the language is changed.
        /// </summary>
        public static Action OnLanguageChanged { get; set; }
        #endregion

        #region Inspector Fields
        public Language[] languages;
        public Language currentLanguage;
        #endregion
        
        #region Public Static Methods
        
        /// <summary>
        /// Localizes a string using the given key.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        public static string Localize(int key, params object[] args)
        {
            var currentLanguage = Instance.currentLanguage;
            if (!currentLanguage.entries.TryGetValue(key, out var entry)) 
                return $"[missing]";
            
            return string.Format(entry, args);
        }
        
        /// <summary>
        /// Localizes a string using the given key.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        public static string Localize(string key, params object[] args)
        {
            var currentLanguage = Instance.currentLanguage;
           
            #if UNITY_EDITOR
            if (currentLanguage == null)
                currentLanguage = Instance.currentLanguage = Resources.Load<Language>("Languages/EnUs");
            #endif
            
            if (!currentLanguage.entries.TryGetValue(Hash(key), out var entry)) 
                return $"[missing_{key}]";
            
            return string.Format(entry, args);
        }
        
        /// <summary>
        /// Hashes a string to an integer, for faster lookups.
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public static int Hash(string key)
        {
            return Animator.StringToHash(key);   
        }
        #endregion
    }
}