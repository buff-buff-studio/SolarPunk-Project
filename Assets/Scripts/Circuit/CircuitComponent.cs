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

        #endregion

        #region Private Methods
        private void _Refresh()
        {
            if (_lastUpdateId == _currentUpdateID)
            {
                Debug.LogWarning(
                    $"CircuitComponent: Refresh called multiple times for {gameObject.name} at the same circuit. Maybe a loop?", gameObject);
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