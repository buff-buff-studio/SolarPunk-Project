using Solis.Circuit;
using Solis.Circuit.Connections;
using UnityEditor;
using UnityEngine;

namespace NetBuff.Editor
{
#if UNITY_EDITOR
    [InitializeOnLoad]
    public static class CircuitEditorTools
    {
        [MenuItem("Tools/Solis/Check Empty Circuits", priority = 1)]
        public static void CheckCircuit()
        {
            int wCount = 0;
            var wireless = GameObject.FindObjectsByType<CircuitWirelessConnection>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var connection in wireless)
            {
                if (connection.PlugA == null && connection.PlugB == null)
                {
                    wCount++;
                    Object.DestroyImmediate(connection.gameObject);
                }else if (connection.PlugA == null || connection.PlugB == null)
                {
                    if (connection.PlugA == null)
                    {
                        var nullType = connection.PlugB.type == CircuitPlugType.Input ? "Output" : "Input";
                        Debug.LogWarning($"Wireless connection {connection.name} is missing a {nullType}", connection);
                    }
                    else
                    {
                        var nullType = connection.PlugA.type == CircuitPlugType.Input ? "Output" : "Input";
                        Debug.LogWarning($"Wireless connection {connection.name} is missing a {nullType}", connection);
                    }
                }
            }
            int pCount = 0;
            var physical = GameObject.FindObjectsByType<CircuitStandardCableConnection>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var connection in physical)
            {
                if (connection.PlugA == null && connection.PlugB == null)
                {
                    pCount++;
                    Undo.DestroyObjectImmediate(connection.gameObject);
                }else if (connection.PlugA == null || connection.PlugB == null)
                {
                    if (connection.PlugA == null)
                    {
                        var nullType = connection.PlugB.type == CircuitPlugType.Input ? "Output" : "Input";
                        Debug.LogWarning($"Wireless connection {connection.name} is missing a {nullType}", connection);
                    }
                    else
                    {
                        var nullType = connection.PlugA.type == CircuitPlugType.Input ? "Output" : "Input";
                        Debug.LogWarning($"Wireless connection {connection.name} is missing a {nullType}", connection);
                    }
                }
            }
            Debug.Log($"Deleted {wCount} empty wireless connections and {pCount} empty physical connections.");
        }
    }
#endif
}