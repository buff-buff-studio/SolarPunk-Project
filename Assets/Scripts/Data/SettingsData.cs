using AYellowpaper.SerializedCollections;
using UnityEngine;
using UnityEngine.Serialization;

namespace Solis.Data
{
    [CreateAssetMenu(fileName = "Settings", menuName = "Solis/Settings", order = 0)]
    public class SettingsData : ScriptableObject
    {
        public string username = "<unknown>";

        [FormerlySerializedAs("boolItems")] 
        public SerializedDictionary<string, bool> toggleItems;
        
        [FormerlySerializedAs("intItems")] 
        public SerializedDictionary<string, int> arrowItems;
        
        [FormerlySerializedAs("floatItems")] 
        public SerializedDictionary<string, float> sliderItems;

        public void LoadFromJson(string json)
        {
            JsonUtility.FromJsonOverwrite(json, this);
        }

        public T TryGet<T>(string key)
        {
            if (typeof(T) == typeof(bool))
            {
                if (toggleItems.TryGetValue(key, out var value))
                    return (T) (object) value;
            }
            else if (typeof(T) == typeof(int))
            {
                if (arrowItems.TryGetValue(key, out var value))
                    return (T) (object) value;
            }
            else if (typeof(T) == typeof(float))
            {
                if (sliderItems.TryGetValue(key, out var value))
                    return (T) (object) value;
            }
            Debug.LogWarning($"Key {key} not found in settings data", this);
            return default;
        }

        public T TrySet<T>(string key, T value)
        {
            if (typeof(T) == typeof(bool))
            {
                toggleItems[key] = (bool) (object) value;
            }
            else if (typeof(T) == typeof(int))
            {
                arrowItems[key] = (int) (object) value;
            }
            else if (typeof(T) == typeof(float))
            {
                sliderItems[key] = (float) (object) value;
            }
            Debug.LogWarning($"Key {key} not found in settings data", this);
            return default;
        }
    }
}