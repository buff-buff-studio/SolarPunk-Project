using System;
using System.Collections.Generic;
using NetBuff;
using NetBuff.Components;
using NetBuff.Interface;
using NetBuff.Misc;
using Solis.Circuit.Interfaces;
using Solis.Packets;
using Solis.Player;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Serialization;

namespace Solis.Circuit.Connections
{
    /// <summary>
    /// Represents a physical connection between two circuit plugs.
    /// Renders a line between the two plugs and creates a series of nodes to simulate a physical connection.
    /// </summary>
    [ExecuteInEditMode]
    [RequireComponent(typeof(LineRenderer))]
    public sealed class CircuitPhysicalCableConnection : NetworkBehaviour, ICircuitConnection
    {
        #region Types
        [Serializable]
        public struct Node
        {
            public GameObject gameObject;
            public Rigidbody rigidbody;
            public Joint joint;
        }
        #endregion

        #region Private Static Fields
        #endregion

        #region Inspector Fields
        [Header("SETTINGS")]
        public int minNodeCount = 3;
        public int maxNodeCount = 15;
        public float nodeDistance = 0.25f;
        public float radius = 3;

        [Header("REFERENCES")]
        public GameObject nodePrefab;
        public GameObject prefabShockVFX;
        [SerializeField]
        private CircuitPlug plugBase;
        public GameObject connectorPrefabInput;
        public GameObject connectorPrefabOutput;
        
        [Header("STATE")]
        public List<Node> nodes = new();
        [SerializeField]
        private Rigidbody holder;
        [FormerlySerializedAs("helderJoint")]
        [HideInInspector, SerializeField]
        private HingeJoint holderJoint;
        public GameObject connector;
        #endregion

        #region Private Fields
        [SerializeField, HideInInspector]
        private ParticleSystem shockVFX;
        #endregion
        
        #region Internal Fields
        [NonSerialized]
        private CircuitPlug _plugBase;
        [NonSerialized]
        private CircuitPlug _plugHolder;
        private bool _isValid;
        private LineRenderer _renderer;
        #endregion

        #region Public Properties
        /// <summary>
        /// The last node in the list of nodes.
        /// </summary>
        public Node Head => nodes[^1];
        
        /// <summary>
        /// Rigidbody that is holding the cable.
        /// </summary>
        public Rigidbody Holder
        {
            get => holder;
            set
            {
                if (holderJoint != null)
                {
                    if (Application.isPlaying)
                        Destroy(holderJoint);
                    else
                        DestroyImmediate(holderJoint);
                }

                holderJoint = null;

                holder = value;
                if (Application.isPlaying)
                {
                    if (holder != null)
                    {
                        holderJoint = holder.gameObject.AddComponent<HingeJoint>();
                        holderJoint.anchor = Vector3.zero;
                        holderJoint.connectedBody = Head.rigidbody;
                    }

                    if (HasAuthority)
                        ServerBroadcastPacket(_CreatePacket(), true);
                }

                OnValidate();
            }
        }
        #endregion

        #region ICircuitConnection Properties
        public bool IsValid =>
            _isValid && _plugBase != null && _plugHolder != null && _plugBase.type != _plugHolder.type;

        public CircuitPlug PlugA
        {
            get => plugBase;
            set
            {
                plugBase = value;
                OnValidate();
            }
        }

        public CircuitPlug PlugB
        {
            get => Holder == null ? null : Holder.GetComponentInParent<CircuitPlug>();
            set
            {
                Holder = value.GetComponentInChildren<Rigidbody>();
                OnValidate();
            }
        }
        #endregion

        #region Unity Callbacks
        private void OnEnable()
        {
            _renderer = GetComponent<LineRenderer>();

            if (!Application.isPlaying)
                return;

            if (plugBase == null)
                plugBase = GetComponentInParent<CircuitPlug>();

            InvokeRepeating(nameof(_TickCable), 0, 0.25f);
            PacketListener.GetPacketListener<PlayerInteractPacket>().AddServerListener(_OnPlayerInteract);
            InvokeRepeating(nameof(_ShockEffects), 0f, 0.25f);

            OnValidate();
        }

        private void OnDisable()
        {
            CancelInvoke(nameof(_TickCable));

            if (Application.isPlaying)
                CancelInvoke(nameof(_ShockEffects));

            PacketListener.GetPacketListener<PlayerInteractPacket>().RemoveServerListener(_OnPlayerInteract);
        }

        private void OnDrawGizmos()
        {
            if (holder == null)
                return;

            var hp = holder.transform.position;
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(hp, 0.25f);
            Gizmos.DrawLine(hp, transform.position);
        }

        private void FixedUpdate()
        {
            var positions = nodes.ConvertAll(n => n.gameObject.transform.position).ToArray();

            _renderer.positionCount = positions.Length;
            _renderer.SetPositions(positions);
        }

        private void Update()
        {
            #if UNITY_EDITOR
            if (!Application.isPlaying)
                return;
            #endif
            if (nodes.Count < 2)
                return;

            var head = Head.gameObject.transform.position;

            if (holder == null)
            {
                var before = nodes[^2].gameObject.transform.position;
                connector.transform.position = head;
                connector.transform.forward = (head - before).normalized;
            }
            else
            {
                var ht = holder.transform;
                var pos = ht.position;
                var fw = ht.forward;
                var dt = Time.deltaTime * 50f;
                connector.transform.position = Vector3.MoveTowards(connector.transform.position, pos, dt);
                connector.transform.rotation =
                    Quaternion.Lerp(connector.transform.rotation, Quaternion.LookRotation(-fw), dt);
            }
        }

        private void OnValidate()
        {
            var plb = PlugB;

            if (_plugBase != null && _plugBase != plugBase)
                _UnsetFrom(_plugBase);

            if (_plugHolder != null && _plugHolder != plb)
                _UnsetFrom(_plugHolder);

            if (plugBase != null && _plugBase != plugBase)
                _SetTo(plugBase);

            if (plb != null && _plugHolder != plb)
                _SetTo(plb);

            _plugBase = plugBase;
            _plugHolder = plb;
            _isValid = _plugHolder != null && _plugBase != null;
        }
        #endregion

        #region ICircuitConnection Methods
        public void Detach(CircuitPlug plug)
        {
            if (plug == _plugBase)
                _plugBase = null;
            else if (plug == _plugHolder)
                _plugHolder = null;

            OnValidate();
        }
        #endregion

        #region Network Callbacks
        public override void OnClientConnected(int clientId)
        {
            ServerSendPacket(_CreatePacket(), clientId, true);
        }

        public override void OnClientReceivePacket(IOwnedPacket packet)
        {
            #if UNITY_EDITOR
            if (!Application.isPlaying)
                return;
            #endif

            if (packet is PhysicalCableConnectionPacket p)
            {
                Debug.Log($"Received cable connection packet for {p.Id} {HasAuthority} {NetworkManager.Instance.EnvironmentType}");
                
                if (HasAuthority)
                    return;

                Holder = p.Holder == NetworkId.Empty
                    ? null
                    : GetNetworkObject(p.Holder).GetComponentInChildren<Rigidbody>();

                while (nodes.Count > p.Nodes.Length)
                {
                    Destroy(nodes[^1].gameObject);
                    nodes.RemoveAt(nodes.Count - 1);
                }

                for (var i = 0; i < nodes.Count; i++)
                {
                    var h3 = p.Nodes[i];
                    nodes[i].gameObject.transform.position = new Vector3(h3.x, h3.y, h3.z);
                }

                while (nodes.Count < p.Nodes.Length)
                {
                    var h3 = p.Nodes[nodes.Count];
                    _CreateNode(new Vector3(h3.x, h3.y, h3.z), nodes.Count);
                }
            }
        }
        #endregion

        #region Private Methods
        private bool _OnPlayerInteract(PlayerInteractPacket packet, int client)
        {
            var player = GetNetworkObject(packet.Id).gameObject.GetComponent<PlayerControllerBase>();
            var hand = player.GetComponentInChildren<Rigidbody>();

            if (Holder == hand)
            {
                Holder = null;

                var transform1 = hand.transform;

                var sockets = FindObjectsByType<CircuitSocket>(FindObjectsSortMode.None);
            
                var closestSocket = default(CircuitSocket);
                var closestDistance = 2.5f;
                
                foreach (var socket in sockets)
                {
                    if (socket != null && socket.outlet.Connection == null && PlugA != socket.outlet)
                    {
                        if (socket.outlet.type == PlugA.type)
                            continue;

                        if (socket.GetComponentInChildren<ICircuitConnection>() != null)
                            return false;
                        
                        var distance = Vector3.Distance(transform1.position, socket.transform.position);
                        
                        //do raycast and check if it's has view to the socket
                        if (distance > 0.5f && Physics.Raycast(transform1.position, socket.transform.position - transform1.position, out var hit, distance - 0.5f))
                            continue;

                        if (distance < closestDistance)
                        {
                            closestSocket = socket;
                            closestDistance = distance;
                        }
                    }
                }

                if (closestSocket != null)
                    Holder = closestSocket.GetComponentInChildren<Rigidbody>();


                player.PlayInteraction(InteractionType.Cable);
                ServerBroadcastPacket(new InteractObjectPacket()
                {
                    Id = player.Id,
                    Interaction = InteractionType.Cable
                });
                return true;
            }


            if (Vector3.Distance(player.transform.position, Head.gameObject.transform.position) < radius)
            {
                Holder = hand;
                player.PlayInteraction(InteractionType.Cable);
                ServerBroadcastPacket(new InteractObjectPacket()
                {
                    Id = player.Id,
                    Interaction = InteractionType.Cable
                });
                return true;
            }

            return false;
        }
        
        private void _UnsetFrom(CircuitPlug plug)
        {
            plug.Connections = Array.FindAll(plug.Connections, c => !ReferenceEquals(c, this));

            if (!Application.isPlaying)
                return;

            if (plug == _plugHolder && _plugBase != null)
                _plugBase.Owner.Refresh();
        }

        private void _SetTo(CircuitPlug plug)
        {
            if (plug.acceptMultipleConnections)
            {
                if (Array.FindIndex(plug.Connections, c => ReferenceEquals(c, this)) == -1)
                {
                    var connections = new ICircuitConnection[plug.Connections.Length + 1];
                    Array.Copy(plug.Connections, connections, plug.Connections.Length);
                    connections[^1] = this;
                    plug.Connections = connections;
                }
            }
            else
            {
                if (plug.Connection != null && !ReferenceEquals(plug.Connection, this))
                    plug.Connection.Detach(plug);

                plug.Connection = this;
            }
        }

        private void _ShockEffects()
        {
            if(PlugA == null)
                return;

            if (PlugA.type is not CircuitPlugType.Output && !IsValid)
            {
                if (shockVFX != null)
                    shockVFX.Stop();
                return;
            }

            var power = PlugA.ReadOutput().power;

            if (power < 0.5f)
            {
                if (shockVFX != null)
                    shockVFX.Stop();
                return;
            }

            if (shockVFX == null)
                shockVFX = Instantiate(prefabShockVFX, transform).GetComponent<ParticleSystem>();

            var pos = nodes[UnityEngine.Random.Range(0, nodes.Count)].gameObject.transform.position;
            shockVFX.transform.position = pos;
            shockVFX.Play();
        }

        private void _TickCable()
        {
            if (!Application.isPlaying) 
                return;

            if (nodes.Count == 0)
            {
                var pos = transform.position;
                for (var i = 0; i < minNodeCount; i++)
                {
                    _CreateNode(pos, i);
                    pos += Vector3.forward * nodeDistance;
                }

                if (connector != null)
                    DestroyImmediate(connector);

                connector = Instantiate(
                    PlugA.type == CircuitPlugType.Input ? connectorPrefabOutput : connectorPrefabInput, Vector3.one,
                    Quaternion.identity, transform);
                Holder = holder;
            }

            if (nodes.Count > minNodeCount || Holder != null)
            {
                //check is head is under -15 y height
                if (Head.gameObject.transform.position.y < -15)
                {
                    for (var i = 1; i < nodes.Count - 1; i++)
                    {
                        var next = nodes[i + 1];
                        var prev = nodes[i - 1];
                        
                        next.joint.connectedBody = prev.rigidbody;
                        Destroy(nodes[i].gameObject);
                        nodes.RemoveAt(i);
                        break;
                    }
                }
                
                if (holderJoint != null)
                {
                    var target = holderJoint.transform.position;

                    for (var i = 1; i < nodes.Count; i++)
                    {
                        var node = nodes[i];
                        var prevNode = nodes[i - 1];

                        var distance = Vector3.Distance(node.gameObject.transform.position,
                            prevNode.gameObject.transform.position);

                        if (distance >= nodeDistance * 2)
                        {
                            if (nodes.Count < maxNodeCount)
                            {
                                var pos = Head.gameObject.transform.position;
                                var p = pos + (target - pos).normalized * nodeDistance;
                                _CreateNode(p, nodes.Count);
                            }

                            break;
                        }
                    }

                    while (nodes.Count < maxNodeCount &&
                           Vector3.Distance(Head.gameObject.transform.position, target) >= nodeDistance)
                    {
                        var pos = Head.gameObject.transform.position;
                        var p = pos + (target - pos).normalized * nodeDistance;
                        _CreateNode(p, nodes.Count);
                    }
                }

                //Delete nodes if not needed
                for (var i = 1; i < nodes.Count - 1; i++)
                {
                    var next = nodes[i + 1];
                    var prev = nodes[i - 1];

                    var distance = Vector3.Distance(next.gameObject.transform.position,
                        prev.gameObject.transform.position);

                    if (distance < nodeDistance * 2)
                    {
                        next.joint.connectedBody = prev.rigidbody;
                        Destroy(nodes[i].gameObject);
                        nodes.RemoveAt(i);
                        break;
                    }
                }
            }
        }

        private void _CreateNode(Vector3 position, int index)
        {
            var go = Instantiate(nodePrefab, position, Quaternion.identity, transform);
            var node = new Node
            {
                gameObject = go,
                rigidbody = go.GetComponent<Rigidbody>(),
                joint = go.GetComponent<Joint>()
            };

            var hasPrev = index < nodes.Count;
            var prevOnPos = hasPrev ? nodes[index] : default;

            nodes.Insert(index, node);

            if (nodes.Count > 1)
            {
                var prevNode = nodes[index - 1];
                var cj = (node.joint as SpringJoint)!;
                cj.anchor = Vector3.zero;

                if (hasPrev)
                {
                    prevOnPos.joint.connectedBody = node.rigidbody;
                }

                cj.autoConfigureConnectedAnchor = false;
                cj.connectedAnchor = new Vector3(0, 0, 0);
                cj.connectedBody = prevNode.rigidbody;
                cj.minDistance = 0;
                cj.maxDistance = nodeDistance;
                cj.spring = 1000f;
                cj.enableCollision = false;
                cj.tolerance = 0;
            }
            else
            {
                node.rigidbody.isKinematic = true;
            }

            if (index != nodes.Count - 1) return;
            if (holderJoint == null) return;

            holderJoint.connectedBody = node.rigidbody;
        }

        private PhysicalCableConnectionPacket _CreatePacket()
        {
            return new PhysicalCableConnectionPacket
            {
                Id = Id,
                Holder = holder == null ? NetworkId.Empty : holder.GetComponentInParent<NetworkIdentity>().Id,
                Nodes = nodes.ConvertAll(n =>
                {
                    var pos = n.gameObject.transform.position;
                    return new half3((half)pos.x, (half)pos.y, (half)pos.z);
                }).ToArray()
            };
        }
        #endregion
    }
}