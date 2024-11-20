 using System;
 using System.Linq;
 using Solis.Circuit.Connections;
 using Solis.Circuit.Interfaces;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
using System.Collections.Generic;
#endif

namespace Solis.Circuit
{
    /// <summary>
    /// Used to link two circuit components together via a connection
    /// </summary>
    [ExecuteInEditMode]
    public class CircuitPlug : MonoBehaviour
    {
        #region Inspector Fields
        public CircuitPlugType type;
        public bool acceptMultipleConnections;
        public bool inputSelfPowered = false;
        #endregion

        #region Private Fields
        private CircuitComponent _owner;
        private ICircuitConnection[] _connections = Array.Empty<ICircuitConnection>();
        #endregion

        public bool IsDataPlug => gameObject.name.Contains("Data", StringComparison.OrdinalIgnoreCase);

        #region Public Properties
        /// <summary>
        /// The connections that this plug is connected to
        /// </summary>
        public ICircuitConnection[] Connections
        {
            get => _connections;
            set
            {
                _connections = value ?? Array.Empty<ICircuitConnection>();

                if (!Application.isPlaying)
                    return;

                if (Owner != null)
                    Owner.Refresh();
            }
        }
        
        /// <summary>
        /// The connection that this plug is connected to. If there are multiple connections, the first one is returned
        /// </summary>
        public ICircuitConnection Connection
        {
            get => Connections.Length > 0 ? Connections[0] : null;
            set => Connections = new[] { value };
        }

        /// <summary>
        /// Returns the owner of this plug, which is the circuit component that this plug is attached to
        /// </summary>
        public CircuitComponent Owner
        {
            get
            {
                if (_owner == null)
                    _owner = GetComponentInParent<CircuitComponent>();

                return _owner;
            }
        }

        public Color Color
        {
            get
            {
                if (Connection == null) return Color.clear;

                var connection = Connection as MonoBehaviour;
                if (connection!.TryGetComponent(out CircuitWirelessConnection wirelessConnection))
                    return wirelessConnection.color;
                else if (connection!.TryGetComponent(out CircuitStandardCableConnection standardCable))
                    return standardCable.color;

                return Color.clear;
            }
            set
            {
                if (Connection == null) return;

                var connection = Connection as MonoBehaviour;
                if (connection!.TryGetComponent(out CircuitWirelessConnection wirelessConnection))
                    wirelessConnection.color = value;
                else if (connection!.TryGetComponent(out CircuitStandardCableConnection standardCable))
                    standardCable.color = value;
            }
        }
        #endregion

        #region Unity Callbacks
        #if UNITY_EDITOR
        private async void OnEnable()
        {
            if (Application.isPlaying)
                return;

            await Awaitable.NextFrameAsync();

            var plugs = FindObjectsByType<CircuitPlug>(FindObjectsSortMode.None);

            var connectionList = new List<ICircuitConnection>(Connections);
            foreach (var plug in plugs)
            {
                if (plug == this)
                    continue;

                foreach (var connection in plug.Connections)
                {
                    if (connection.PlugA == this || connection.PlugB == this)
                    {
                        if (!connectionList.Contains(connection))
                            connectionList.Add(connection);
                    }
                }
            }

            Connections = connectionList.ToArray();

            foreach (var connection in Connections)
            {
                if (connection.PlugA == null)
                    connection.PlugA = this;
                else if (connection.PlugB == null)
                    connection.PlugB = this;
            }
        }
        #endif

        private void OnDestroy()
        {
            #if UNITY_EDITOR
            Undo.SetCurrentGroupName("Removed object");
            Undo.RecordObject(gameObject, "Removed object");
            var group = Undo.GetCurrentGroup();
            foreach (var connection in Connections)
            {
                var mb = connection as MonoBehaviour;
                if (mb != null)
                    Undo.DestroyObjectImmediate(mb.gameObject);
            }

            Undo.CollapseUndoOperations(group);
            #else
            foreach (var connection in Connections)
            {
                var mb = connection as MonoBehaviour;
                if (mb != null)
                    Destroy(mb.gameObject);
            }
            #endif
        }
        #endregion

        #region Public Methods
        public CircuitData ReadOutput(int connection = 0)
        {
            switch (type)
            {
                case CircuitPlugType.Output:
                    var read = Owner.ReadOutput(this);
                    return read;

                case CircuitPlugType.Input:
                    if (inputSelfPowered)
                    {
                        return new CircuitData(1);
                    }

                    var other = GetOtherPlug(connection);

                    if (ReferenceEquals(other, null))
                        return default;
                    return other.Owner.ReadOutput(other);

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public float ReadInputPower()
        {
            var result = Connections
                .Select((connection, i) => GetOtherPlug(i))
                .Where(other => !ReferenceEquals(other, null))
                .Sum(other => other.Owner.ReadOutput(other).power);

            if (inputSelfPowered)
                result += 1;

            return result;
        }

        public CircuitPlug GetOtherPlug(int connection = 0)
        {
            var con = connection < Connections.Length ? Connections[connection] : null;
            if (con == null)
                return null;
            return this == con.PlugA ? con.PlugB : con.PlugA;
        }
        #endregion
    }
}