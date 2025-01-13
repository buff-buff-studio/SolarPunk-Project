using AYellowpaper.SerializedCollections;
using UnityEngine;

namespace Solis.i18n
{
    /// <summary>
    /// Represents a language.
    /// </summary>
    public class Language : ScriptableObject
    {
        #region Inspector Fields
        public string internalName;
        public string displayName;
        [SerializeField, HideInInspector]
        public SerializedDictionary<int, string> entries = new();
        #endregion
        
        public string Localize(string key, params object[] args)
        {
            if (!entries.TryGetValue(Hash(key), out var entry)) 
                return $"[missing_{key}]";
            
            return string.Format(entry, args);
        }
        
        /// <summary>
        /// Localizes a string using the given key.
        /// Replaces {0}, {1}, etc. with the given args.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        public string Localize(int key, params object[] args)
        {
            if (!entries.TryGetValue(key, out var entry)) 
                return $"[missing]";
            
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
    }
}