using System;
using System.Collections.Generic;
using System.Linq;
using Solis.Circuit.Components;
using Solis.Circuit.Interfaces;
using UnityEditor;
using UnityEngine;

namespace Solis.Circuit
{
    public class CircuitToggleGroup : CircuitComponent
    {
        public CircuitPlug connection;

        private List<CircuitPlug> _connections = new List<CircuitPlug>();
        private int _currentConnectionOn;
        private CircuitPlug CurrentConnectionOn => _currentConnectionOn >= 0 ? _connections[_currentConnectionOn] : null;
        protected internal List<CircuitPlug> Connections => _connections;

        #region Unity Callbacks

        protected override void OnEnable()
        {
            if (connection == null) SetActive(false);
            else if (_connections == null)
            {
                LocateConnections();
                if (_connections == null) SetActive(false);
            }
            _currentConnectionOn = -1;
            OnRefresh();
        }

        private void OnValidate()
        {
            if(Application.isPlaying) return;

            if (connection == null)
            {
                connection = new GameObject("Connection").AddComponent<CircuitPlug>();
                connection.transform.SetParent(transform);
                connection.transform.localPosition = Vector3.zero;
            }
            if(!connection.acceptMultipleConnections)
                connection.acceptMultipleConnections = true;

            LocateConnections();
        }

        internal void LocateConnections()
        {
            Debug.Log("Locating connections");
            _connections.Clear();
            foreach (var t in connection.Connections)
            {
                _connections.Add(t.PlugA == connection ? t.PlugB : t.PlugA);
            }
        }

        #endregion

        public override CircuitData ReadOutput(CircuitPlug plug)
        {
            return new CircuitData();
        }

        public override IEnumerable<CircuitPlug> GetPlugs()
        {
            yield return connection;
        }

        protected override void OnRefresh()
        {
            //TODO:: Remover isso se resolver o BUG de dectar o circuito wifi ao iniciar
            if(_connections.Count != connection.Connections.Length) LocateConnections();
            if (!HasAuthority) return;

            if (connection.Connections.Length == 0)
            {
                _currentConnectionOn = -1;
                Debug.LogWarning("No connections");
                return;
            }

            if (!_connections.Exists(c => c.ReadOutput().power > 0f))
            {
                //_currentConnectionOn = -1;
                Debug.LogWarning("No connections on");
                return;
            }

            var connectionOn = _connections.FindAll(c => c.ReadOutput().power > 0.5f);

            if (connectionOn.Count == 1 && CurrentConnectionOn == connectionOn[0])
            {
                Debug.LogWarning("The same connection is on");
                return;
            }

            connectionOn.Remove(CurrentConnectionOn);
            _currentConnectionOn = _connections.FindIndex(connectionOn[0].Equals);
            Debug.LogWarning($"New connection on {_connections[_currentConnectionOn].Owner.name}");
            foreach (var c in _connections.Where(c => c != CurrentConnectionOn))
            {
                Debug.LogWarning($"Turning off {c.Owner.name}");
                switch (c.Owner)
                {
                    case CircuitLever lever:
                        lever.isOn.Value = false;
                        break;
                    case CircuitTemporizedButton button:
                        button.isOn.Value = false;
                        break;
                    case CircuitPalmScanner scanner:
                        scanner.isOn.Value = false;
                        break;
                    case CircuitValve valve:
                        valve.isOn.Value = false;
                        break;
                }
            }
        }
    }

#if UNITY_EDITOR
    [CustomEditor(typeof(CircuitToggleGroup))]
    public class CircuitToggleGroupEditor : Editor
    {
        private SerializedProperty connection;

        private CircuitToggleGroup toggleGroup;

        private void OnEnable()
        {
            toggleGroup = (CircuitToggleGroup) target;
            connection = serializedObject.FindProperty("connection");
        }
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(connection);
            EditorGUILayout.Space();
            EditorGUI.BeginDisabledGroup(true);
            EditorGUI.TextArea(GUILayoutUtility.GetRect(0, 20), $"Connections: {toggleGroup.Connections.Count}");
            if(toggleGroup.Connections.Count == 0)
                EditorGUILayout.ObjectField("", null, typeof(CircuitComponent), true);
            else
            {
                foreach (var c in toggleGroup.Connections)
                {
                    EditorGUILayout.ObjectField("", c.Owner, typeof(CircuitComponent), true);
                }
            }
            EditorGUI.EndDisabledGroup();
            if (GUILayout.Button("Locate Connections"))
            {
                toggleGroup.LocateConnections();
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
#endif
}