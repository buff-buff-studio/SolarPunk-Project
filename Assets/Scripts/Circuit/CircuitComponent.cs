using System.Collections.Generic;
using NetBuff.Components;
using UnityEngine;
using UnityEngine.Events;

namespace Solis.Circuit
{
    /// <summary>
    /// Base class for all circuit components.
    /// </summary>
    public abstract class CircuitComponent : NetworkBehaviour
    {
        #region Private Static Fields
        private static int _currentUpdateID;
        #endregion

        #region Private Fields
        private int _lastUpdateId = -1;
        [Space(10)]
        [SerializeField]protected UnityEvent onToggleComponent;
        #endregion

        #region Unity Callbacks
        protected virtual void OnEnable()
        {
            OnRefresh();
        }

        protected virtual void OnDisable()
        {
        }
        #endregion

        #region Public Abstract Methods
        /// <summary>
        /// Reads the output of the circuit component based on the plug.
        /// </summary>
        /// <param name="plug"></param>
        /// <returns></returns>
        public abstract CircuitData ReadOutput(CircuitPlug plug);
        
        /// <summary>
        /// Returns all plugs on the circuit component.
        /// </summary>
        /// <returns></returns>
        public abstract IEnumerable<CircuitPlug> GetPlugs();
        #endregion

        #region Protected Abstract Methods
        protected abstract void OnRefresh();
        #endregion

        #region Public Methods
        /// <summary>
        /// Refreshes the circuit component and all subsequent components.
        /// </summary>
        public void Refresh()
        {
            _currentUpdateID++;
            _Refresh();
        }

        public static void DrawWireCapsule(Vector3 _pos, Quaternion _rot, float _radius, float _height, Color _color = default(Color))
        {
            if (_color != default(Color))
                UnityEditor.Handles.color = _color;
            Matrix4x4 angleMatrix = Matrix4x4.TRS(_pos, _rot, UnityEditor.Handles.matrix.lossyScale);
            using (new UnityEditor.Handles.DrawingScope(angleMatrix))
            {
                var pointOffset = (_height - (_radius * 2)) / 2;

                //draw sideways
                UnityEditor.Handles.DrawWireArc(Vector3.up * pointOffset, Vector3.left, Vector3.back, -180, _radius);
                UnityEditor.Handles.DrawLine(new Vector3(0, pointOffset, -_radius), new Vector3(0, -_pos.y, -_radius));
                UnityEditor.Handles.DrawLine(new Vector3(0, pointOffset, _radius), new Vector3(0, -_pos.y, _radius));
                //UnityEditor.Handles.DrawWireArc(Vector3.down * pointOffset, Vector3.left, Vector3.back, 180, _radius);
                //draw frontways
                UnityEditor.Handles.DrawWireArc(Vector3.up * pointOffset, Vector3.back, Vector3.left, 180, _radius);
                UnityEditor.Handles.DrawLine(new Vector3(-_radius, pointOffset, 0), new Vector3(-_radius, -_pos.y, 0));
                UnityEditor.Handles.DrawLine(new Vector3(_radius, pointOffset, 0), new Vector3(_radius, -_pos.y, 0));
                //UnityEditor.Handles.DrawWireArc(Vector3.down * pointOffset, Vector3.back, Vector3.left, -180, _radius);
                //draw center
                UnityEditor.Handles.DrawWireDisc(Vector3.up * pointOffset, Vector3.up, _radius);
                UnityEditor.Handles.DrawWireDisc(Vector3.down * pointOffset, Vector3.up, _radius);

            }
        }

        #endregion

        #region Private Methods
        private void _Refresh()
        {
            if (_lastUpdateId == _currentUpdateID)
            {
                Debug.LogWarning(
                    $"CircuitComponent: Refresh called multiple times for {gameObject.name} at the same circuit. Maybe a loop?");
                return;
            }

            _lastUpdateId = _currentUpdateID;
            
            OnRefresh();

            foreach (var plug in GetPlugs())
            {
                if (plug.type == CircuitPlugType.Input)
                    continue;

                for (var i = 0; i < plug.Connections.Length; i++)
                {
                    var otherPlug = plug.GetOtherPlug(i);

                    if (otherPlug == null)
                        continue;

                    var owner = otherPlug.Owner;

                    if (owner == null)
                        continue;

                    owner._Refresh();
                }
            }
        }
        #endregion
    }
}