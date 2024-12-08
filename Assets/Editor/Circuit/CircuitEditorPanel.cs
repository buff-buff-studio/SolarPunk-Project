using System;
using System.Collections.Generic;
using System.Linq;
using Solis.Circuit.Interfaces;
using Solis.Circuit;
using Solis.Circuit.Connections;
using UnityEditor;
using UnityEditor.Overlays;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;
using Quaternion = UnityEngine.Quaternion;
using Vector2 = UnityEngine.Vector2;
using Vector3 = UnityEngine.Vector3;

namespace Editor.Circuit
{
    #if UNITY_EDITOR
    [Overlay(typeof(SceneView), "Circuit Tools", true)]
    public class CircuitEditorPanel : Overlay
    {
        private enum CurrentMode
        {
            Normal,
            CreatingConnection,
            EditingStandardConnection
        }

        public enum WireCreationType
        {
            Wireless,
            Standard
        }

        public enum SnapMode
        {
            None,
            GridLocal,
            GridWorld
        }

        #region Static
        private static GUIStyle _styleSceneGUI;
        private static GUIStyle _styleSceneGUIText;
        #endregion

        #region Settings
        private bool _active = true;

        private CurrentMode _currentMode = CurrentMode.Normal;
        private GameObject _previousSelection;
        private WireCreationType _wireCreationType = WireCreationType.Standard;
        private Color _wireCreationColor = Color.black;

        private CircuitPlug _selectedPlug;

        private CircuitStandardCableConnection _selectedStandardConnection;
        #endregion

        public override void OnCreated()
        {
            base.OnCreated();
            SceneView.duringSceneGui += _OnSceneGUI;
        }

        public override void OnWillBeDestroyed()
        {
            SceneView.duringSceneGui -= _OnSceneGUI;
        }

        public override VisualElement CreatePanelContent()
        {
            var root = new VisualElement() { name = "Circuit Tools" };

            //Header
            var header = new Label("Settings")
            {
                style =
                {
                    unityFontStyleAndWeight = FontStyle.Bold, marginLeft = 1,
                    marginTop = 5, marginBottom = 5
                }
            };
            root.Add(header);

            //Active
            var toggle = new Toggle("Active") { value = _active };
            toggle.RegisterValueChangedCallback(evt => _active = evt.newValue);
            root.Add(toggle);
            
            //Wire Creation Type
            var wireCreationType = new EnumField("Wire Creation Type", _wireCreationType);
            wireCreationType.RegisterValueChangedCallback(evt =>
            {
                _wireCreationType = (WireCreationType)evt.newValue;

                displayed = false;
                displayed = true;
            });
            root.Add(wireCreationType);

            //Wire Color
            var colorField = new ColorField("Wire Color");
            colorField.value = _wireCreationColor;
            colorField.RegisterValueChangedCallback(evt => _wireCreationColor = evt.newValue);
            root.Add(colorField);

            if (_selectedStandardConnection != null)
            {
                header = new Label("Connection")
                {
                    style =
                    {
                        unityFontStyleAndWeight = FontStyle.Bold, marginLeft = 1,
                        marginTop = 5, marginBottom = 5
                    }
                };
                root.Add(header);

                var button = new Button(() =>
                {
                    var so2 = new SerializedObject(_selectedStandardConnection);
                    so2.Update();
                    Undo.RecordObject(_selectedStandardConnection, "Straighten Path");

                    var points = _selectedStandardConnection.GetControlPoints();

                    for (var i = 0; i < points.Length; i++)
                    {
                        var curr = points[i];

                        if (i > 0)
                        {
                            var prev = points[i - 1];
                            var prevDir = (prev.position - curr.position).normalized;
                            points[i].leftHandle = prevDir;
                        }

                        if (i < points.Length - 1)
                        {
                            var next = points[i + 1];
                            var nextDir = (next.position - curr.position).normalized;
                            points[i].rightHandle = nextDir;
                        }
                    }

                    _selectedStandardConnection.RefreshVisuals();
                })
                {
                    text = "Straighten Path"
                };
                root.Add(button);

                if (_selectedStandardConnection.selectedControlPointIndices.Count > 0)
                {
                    header = new Label("Control Point")
                    {
                        style =
                        {
                            unityFontStyleAndWeight = FontStyle.Bold, marginLeft = 1,
                            marginTop = 5, marginBottom = 5
                        }
                    };
                    root.Add(header);

                    button = new Button(_HandlesFlat)
                    {
                        text = "Flat Handles"
                    };
                    root.Add(button);

                    var container = new VisualElement
                    {
                        style =
                        {
                            flexDirection = FlexDirection.Row,
                            alignItems = Align.Center,
                            justifyContent = Justify.SpaceBetween
                        }
                    };

                    root.Add(container);

                    button = new Button(() => _HandlesSmooth(true, true))
                    {
                        text = "Smooth Both"
                    };
                    container.Add(button);

                    button = new Button(() => _HandlesSmooth(true, false))
                    {
                        text = "Left Only"
                    };
                    container.Add(button);

                    button = new Button(() => _HandlesSmooth(false, true))
                    {
                        text = "Right Only"
                    };
                    container.Add(button);

                    container = new VisualElement();
                    container.style.flexDirection = FlexDirection.Row;
                    container.style.alignItems = Align.Center;
                    container.style.justifyContent = Justify.SpaceBetween;
                    root.Add(container);

                    button = new Button(() => _HandlesRecalculate(true, true))
                    {
                        text = "Recalc. Both"
                    };
                    container.Add(button);

                    button = new Button(() => _HandlesRecalculate(true, false))
                    {
                        text = "Left Only"
                    };
                    container.Add(button);

                    button = new Button(() => _HandlesRecalculate(false, true))
                    {
                        text = "Right Only"
                    };
                    container.Add(button);
                }
            }

            return root;
        }

        private void _OnSceneGUI(SceneView obj)
        {
            if (!displayed || !_active)
                return;

            _styleSceneGUI ??= new GUIStyle(EditorStyles.helpBox)
            {
                alignment = TextAnchor.UpperLeft,
                normal =
                {
                    textColor = Color.black
                },
                hover =
                {
                    textColor = Color.black
                }
            };

            _styleSceneGUIText ??= new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.UpperCenter,
                normal =
                {
                    textColor = Color.black
                },
                hover =
                {
                    textColor = Color.black
                }
            };

            _styleSceneGUIText.fontSize = 10;

            if (Event.current.type == EventType.DragPerform)
            {
                if (DragAndDrop.objectReferences.Length == 1)
                {
                    if (Event.current.control)
                    {
                        var ray = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);
                        if (Physics.Raycast(ray, out var hit))
                        {
                            var prefab = DragAndDrop.objectReferences[0];
                            if (PrefabUtility.GetCorrespondingObjectFromSource(hit.collider.gameObject) == prefab)
                            {
                                Object.DestroyImmediate(hit.collider.gameObject);
                                
                                ray.origin = hit.point + ray.direction * 0.02f;
                                if (Physics.Raycast(ray, out var hit2))
                                {
                                    hit = hit2;
                                }
                                else
                                    return;
                            }

                            Event.current.Use();
                            
                            var go = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
                            go!.transform.position = hit.point;

                            var ax = Mathf.Abs(hit.normal.x);
                            var ay = Mathf.Abs(hit.normal.y);
                            var az = Mathf.Abs(hit.normal.z);

                            if (ax < 0.1f && az < 0.1f)
                            {
                                go.transform.up = new Vector3(0, Mathf.Sign(hit.normal.y), 0);
                                var euler = go.transform.eulerAngles;
                                go.transform.eulerAngles = new Vector3(euler.x,
                                    SceneView.lastActiveSceneView.camera.transform.eulerAngles.y, euler.z);
                            }
                            else if (ax < 0.01f || ay < 0.01f || az < 0.01f)
                            {
                                var q = Quaternion.LookRotation(Vector3.up, hit.normal);
                                go.transform.rotation = q;
                            }
                            else
                            {
                                go.transform.up = hit.normal;
                            }

                            Undo.RegisterCreatedObjectUndo(go, "Create Object");
                        }
                    }
                }
            }

            switch (_currentMode)
            {
                case CurrentMode.Normal:
                    _DoNormal();

                    if (Selection.activeGameObject != null)
                    {
                        var connection = Selection.activeGameObject.GetComponent<CircuitStandardCableConnection>();
                        if (connection != null)
                        {
                            _currentMode = CurrentMode.EditingStandardConnection;
                            _selectedStandardConnection = connection;
                            _selectedStandardConnection.selectedControlPointIndices.Clear();
                            _RefreshFields();
                        }
                    }

                    break;
                case CurrentMode.CreatingConnection:
                    _DoConnectionCreation();
                    break;

                case CurrentMode.EditingStandardConnection:
                    _DoEditingStandardConnection();
                    break;
            }
        }

        #region Modes
        private void _DoNormal()
        {
            const int width = 100;
            const int height = 50;
            const int buttonSize = 20;
            
            CircuitComponent[] circuitComponents;
            
            if (PrefabStageUtility.GetCurrentPrefabStage() != null)
            {
                var prefab = PrefabStageUtility.GetCurrentPrefabStage().prefabContentsRoot;
                circuitComponents = prefab.GetComponentsInChildren<CircuitComponent>();
            }
            else
            {
                circuitComponents = Object.FindObjectsByType<CircuitComponent>(FindObjectsSortMode.None);
            }
            
            var camTransform = SceneView.lastActiveSceneView.camera.transform;
            var camPos = camTransform.position;
            var camDir = camTransform.forward;
            var mousePos = Event.current.mousePosition;

            CircuitPlug hoveredPlug = null;
            var hoveredDistance = float.MaxValue;
            
            foreach (var component in circuitComponents)
            {
                var plugs = component.GetPlugs();
                
                foreach (var plug in plugs)
                {
                    if (plug == null)
                        continue;

                    var transform = plug.transform;
                    var position = transform.position;

                    var screenPos = HandleUtility.WorldToGUIPoint(position);
                    var dot = Vector3.Dot(camDir, (position - camPos).normalized);
                    if (dot < 0)
                        continue;

                    var rect = new Rect(screenPos.x - width / 2f, screenPos.y - height - 10, width, height);

                    var physicalCableHolder = plug.GetComponentInChildren<Rigidbody>();

                    if (physicalCableHolder != null)
                    {
                        var hRect = new Rect(screenPos.x - width / 2f, screenPos.y - height / 2f - 10, width,
                            height / 2f);

                        if (!hRect.Contains(mousePos))
                            continue;
                    }
                    else if (!rect.Contains(mousePos))
                        continue;

                    var distance = Vector2.Distance(mousePos, screenPos);
                    if (distance < hoveredDistance)
                    {
                        hoveredDistance = distance;
                        hoveredPlug = plug;
                    }
                }
            }
            
            foreach (var component in circuitComponents)
            { 
                var plugs = component.GetPlugs();
                
                foreach (var plug in plugs)
                {
                    if (plug == null)
                        continue;

                    var transform = plug.transform;
                    var position = transform.position;
                    var distance = HandleUtility.DistanceToCircle(position, 0.15f);

                    switch (Event.current.type)
                    {
                        case EventType.MouseDown:
                            if (distance == 0)
                            {
                                var go = plug.gameObject;
                                Selection.activeGameObject = go;
                                EditorGUIUtility.PingObject(go);

                                Event.current.Use();
                            }

                            break;

                        case EventType.Repaint:
                            if (distance == 0)
                                Handles.color = Color.white;
                            else
                            {
                                if(plug.IsDataPlug)
                                    Handles.color = Color.cyan;
                                else
                                    Handles.color = plug.type is CircuitPlugType.Input ? Color.green : Color.red;
                            }
                            _DrawSelector(position, 0.15f, plug.acceptMultipleConnections);
                            break;

                        case EventType.MouseMove:
                            HandleUtility.Repaint();
                            break;
                    }
                    
                    if(plug != hoveredPlug)
                        continue;

                    var screenPos = HandleUtility.WorldToGUIPoint(position);
                    var dot = Vector3.Dot(camDir, (position - camPos).normalized);
                    if (dot < 0)
                        continue;

                    var plugType = plug.IsDataPlug ? "Data" : plug.type.ToString();
                    var physicalCableHolder = plug.GetComponentInChildren<Rigidbody>();
                    
                    if (physicalCableHolder != null)
                    {
                        var hRect = new Rect(screenPos.x - width / 2f, screenPos.y - height/2f - 10, width, height/2f);
          
                        Handles.BeginGUI();
                        GUI.Box(hRect, GUIContent.none, _styleSceneGUI);
                        GUI.Label(hRect, $"Physical Connector\nType: {plugType}", _styleSceneGUIText);
                        Handles.EndGUI();
                        continue;
                    }
                    
                    var rect = new Rect(screenPos.x - width / 2f, screenPos.y - height - 10, width, height);
    
                    Handles.BeginGUI();
                    GUI.Box(rect, GUIContent.none, _styleSceneGUI);
                    var acceptMultipleConnections = plug.acceptMultipleConnections;
                    
                    var isPhysicalCable =  plug.GetComponentInChildren<CircuitPhysicalCableConnection>();
                    

                    if (isPhysicalCable)
                    {
                        var valid = isPhysicalCable.Holder != null && isPhysicalCable.PlugA != null;
                        var text = acceptMultipleConnections
                            ? $"Type: {plugType}\nConnections: {plug.Connections.Length}"
                            : $"Type: {plugType}\nConnected: {(valid ? "Yes" : "No")}";
                        GUI.Label(rect, text, _styleSceneGUIText);

                        var buttonRect = new Rect(rect.x + (width - buttonSize * 4f + 10) / 2f,
                            rect.y + height - buttonSize - 5, buttonSize, buttonSize);
                        
                        var physicalCable = plug.GetComponentInChildren<CircuitPhysicalCableConnection>();

                        GUI.enabled = !valid;
                        if (GUI.Button(buttonRect, "+"))
                        {
                            _currentMode = CurrentMode.CreatingConnection;
                            _selectedPlug = plug;
                            _previousSelection = Selection.activeGameObject;
                            Selection.activeGameObject = null;
                            _RefreshFields();
                        }
                        GUI.enabled = true;

                        buttonRect.x += buttonSize + 5;
                        if (GUI.Button(buttonRect, "S"))
                        {
                            Selection.activeGameObject = physicalCable.gameObject;
                        }

                        buttonRect.x += buttonSize + 5;
                        GUI.enabled = valid;
                        if (GUI.Button(buttonRect, "-"))
                        {
                            var so = new SerializedObject(physicalCable);
                            so.Update();
                            Undo.RecordObject(physicalCable, "Remove Connection");

                            if (Application.isPlaying)
                            {
                                physicalCable.Holder = null;
                            }
                            else
                                so.FindProperty("holder").objectReferenceValue = null;
                            so.ApplyModifiedProperties();
                        }

                        GUI.enabled = true;
                    }
                    else
                    {
                        var text = acceptMultipleConnections
                            ? $"Type: {plugType}\nConnections: {plug.Connections.Length}"
                            : $"Type: {plugType}\nConnected: {(plug.Connections.Length == 0 ? "No" : "Yes")}";
                        GUI.Label(rect, text, _styleSceneGUIText);
                        
                        var buttonRect = new Rect(rect.x + (width - buttonSize * 4f + 10) / 2f,
                        rect.y + height - buttonSize - 5, buttonSize, buttonSize);
                        
                        GUI.enabled = plug.Connections.Length == 0 || acceptMultipleConnections;
                        if (GUI.Button(buttonRect, "+"))
                        {
                            _currentMode = CurrentMode.CreatingConnection;
                            _selectedPlug = plug;
                            _previousSelection = Selection.activeGameObject;
                            Selection.activeGameObject = null;
                            _RefreshFields();
                        }
                        GUI.enabled = true;

                        buttonRect.x += buttonSize + 5;
                        if (GUI.Button(buttonRect, "S"))
                        {
                            var selected = Selection.activeGameObject;
                            var current = selected == null ? null : selected.GetComponent<ICircuitConnection>();

                            var connections = plug.Connections;
                            var index = current == null ? -1 : Array.IndexOf(connections, current);
                            var next = connections.Length > 0 ? (index + 1) % connections.Length : 0;

                            if (next < 0 || next >= connections.Length)
                                return;
                            Selection.activeGameObject = (connections[next] as MonoBehaviour)!.gameObject;
                        }

                        buttonRect.x += buttonSize + 5;
                        GUI.enabled = plug.Connection is MonoBehaviour;
                        if (GUI.Button(buttonRect, "-"))
                        {
                            Undo.DestroyObjectImmediate((plug.Connection as MonoBehaviour)!.gameObject);
                        }

                        GUI.enabled = true;
                    }
                    
                    Handles.EndGUI();
                }
            }

            if (Event.current.type == EventType.MouseMove)
                HandleUtility.Repaint();
        }

        private void _DoConnectionCreation()
        {
            if (_selectedPlug == null || Selection.activeGameObject != null)
            {
                _ExitToNormalMode();
                return;
            }
            
            var circuitComponents = Object.FindObjectsByType<CircuitComponent>(FindObjectsSortMode.None);
            var eligiblePlugs = new List<CircuitPlug>();
            
            var physicalCable = _selectedPlug.GetComponentInChildren<CircuitPhysicalCableConnection>();
            var isPhysicalCable =  physicalCable != null;

            foreach (var component in circuitComponents)
            {
                var plugs = component.GetPlugs();
                foreach (var plug in plugs)
                {
                    if (plug == _selectedPlug)
                        continue;

                    if (plug.type == _selectedPlug.type)
                        continue;

                    if (isPhysicalCable)
                    {
                        var physicalCableHolder = plug.GetComponentInChildren<Rigidbody>();
                        if (physicalCableHolder == null)
                            continue;
                    }
                    else
                    {
                        try
                        {
                            var pc = plug.GetComponentInChildren<CircuitPhysicalCableConnection>();
                            if (pc != null)
                                continue;

                            var physicalCableHolder = plug.GetComponentInChildren<Rigidbody>();
                            if (physicalCableHolder != null)
                                continue;
                        }
                        catch (Exception e)
                        {
                            Debug.LogError(e, component.gameObject);
                            throw;
                        }
                    }
                    
                    
                    eligiblePlugs.Add(plug);
                }
            }

            var from = _selectedPlug.transform.position;
            var to = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition).origin;

            switch (Event.current.type)
            {
                case EventType.Repaint:
                    Handles.color = Color.blue;
                    _DrawSelector(from, 0.2f, _selectedPlug.acceptMultipleConnections);

                    var textPosition = new Vector2(Screen.width / 2f, 20);
                    _DrawMessageBox("Press ESC to cancel", textPosition);

                    foreach (var plug in eligiblePlugs)
                    {
                        var position = plug.transform.position;

                        if (_selectedPlug.Connections.Any(connection =>
                                connection.PlugA == plug || connection.PlugB == plug))
                        {
                            _DrawMessageBox("Already connected",
                                HandleUtility.WorldToGUIPoint(plug.transform.position) - new Vector2(0, 20));
                        }
                        else
                        {
                            if (plug.Connections.Length > 0 && !plug.acceptMultipleConnections)
                            {
                                _DrawMessageBox("Will replace connection",
                                    HandleUtility.WorldToGUIPoint(plug.transform.position) - new Vector2(0, 20));

                                Handles.color = Color.red;
                            }
                            else
                            {
                                Handles.color = plug.IsDataPlug ? Color.magenta : Color.yellow;
                            }

                            var distance = HandleUtility.DistanceToCircle(position, 0.25f);
                            if (distance == 0)
                            {
                                Handles.color = Color.white;
                                _DrawSelector(position, 0.3f, plug.acceptMultipleConnections);
                                to = position;
                            }
                            else
                            {
                                _DrawSelector(position, 0.25f, plug.acceptMultipleConnections);
                            }
                        }
                    }

                    if (isPhysicalCable ? physicalCable.IsValid : (_selectedPlug.Connections.Length > 0 && !_selectedPlug.acceptMultipleConnections))
                        _DrawMessageBox("Will remove connection",
                            HandleUtility.WorldToGUIPoint(_selectedPlug.transform.position) - new Vector2(0, 20));

                    Handles.color = Color.blue;
                    Handles.DrawDottedLine(from, to, 5);
                    break;

                case EventType.KeyDown:
                    if (Event.current.keyCode == KeyCode.Escape)
                    {
                        _ExitToNormalMode();
                    }

                    break;

                case EventType.MouseMove:
                    HandleUtility.Repaint();
                    break;

                case EventType.MouseDown:
                    if (Event.current.button == 0)
                    {
                        foreach (var plug in eligiblePlugs)
                        {
                            var position = plug.transform.position;

                            if (_selectedPlug.Connections.Any(connection =>
                                    connection.PlugA == plug || connection.PlugB == plug))
                                continue;

                            var distance = HandleUtility.DistanceToCircle(position, 0.25f);
                            if (distance == 0)
                            {
                                if (isPhysicalCable)
                                {
                                    //begin undo group
                                    Undo.IncrementCurrentGroup();
                                    var group = Undo.GetCurrentGroup();
                                    Undo.SetCurrentGroupName("Create Connection");
                                    
                                    var holder =  plug.GetComponentInChildren<Rigidbody>();
                                    
                                    var allPhysicalCables = Object.FindObjectsByType<CircuitPhysicalCableConnection>(FindObjectsSortMode.None);
                                    foreach (var pc in allPhysicalCables)
                                    {
                                        if (pc.Holder == holder)
                                        {
                                            var so1 = new SerializedObject(pc);
                                            so1.Update();
                                            Undo.RecordObject(physicalCable, "Remove Connection");

                                            if (!Application.isPlaying)
                                                so1.FindProperty("holder").objectReferenceValue = null;
                                            pc.Holder = null;
                                            so1.ApplyModifiedProperties();
                                        }
                                    }
                                    
                                    var so = new SerializedObject(physicalCable);
                                    so.Update();
                                    Undo.RecordObject(physicalCable, "Create Connection");

                                    if (Application.isPlaying)
                                    {
                                        physicalCable.Holder = holder;
                                    }
                                    else
                                        so.FindProperty("holder").objectReferenceValue = holder;
                                    so.ApplyModifiedProperties();
                                    
                                    Undo.CollapseUndoOperations(group);
                                }
                                else
                                {
                                    var replace = plug.Connections.Length > 0 && !plug.acceptMultipleConnections;

                                    if (replace)
                                    {
                                        foreach (var connection in plug.Connections)
                                        {
                                            var mb = connection as MonoBehaviour;
                                            if (mb == null)
                                                continue;
                                            Undo.DestroyObjectImmediate(mb.gameObject);
                                        }
                                    }

                                    replace = _selectedPlug.Connections.Length > 0 &&
                                              !_selectedPlug.acceptMultipleConnections;
                                    if (replace)
                                    {
                                        foreach (var connection in _selectedPlug.Connections)
                                        {
                                            var mb = connection as MonoBehaviour;
                                            if (mb == null)
                                                continue;
                                            Undo.DestroyObjectImmediate(mb.gameObject);
                                        }
                                    }

                                    _CreateConnection(_selectedPlug, plug);
                                }
                            }
                        }

                        Event.current.Use();

                        _ExitToNormalMode();
                    }

                    break;
            }
        }

        private void _DoEditingStandardConnection()
        {
            if (Selection.activeGameObject == null ||
                _selectedStandardConnection.gameObject != Selection.activeGameObject)
            {
                _previousSelection = Selection.activeGameObject;
                _ExitToNormalMode();
                return;
            }

            var controlPoints = _selectedStandardConnection.GetControlPoints();
            var transform = _selectedStandardConnection.transform;
            int i;

            var textPosition = new Vector2(Screen.width / 2f, 20);
            _DrawMessageBox("Editing Cable", textPosition);

            var controlsBoxPosition = new Vector2(Screen.width / 2f, 75);
            _DrawMessageBox(
                "Shift: Show Handles\nControl: Snap To Grid\nLeft Arrow: Previous Control Point\nRight: Next Control Point\nA: Select All\nBackspace: Delete Control Point",
                controlsBoxPosition);

            var isOverSomePoint = false;
            for (i = 0; i < controlPoints.Length; i++)
            {
                var controlPoint = controlPoints[i];
                var position = controlPoint.position;
                var worldPosition = transform.TransformPoint(position);
                var distance = HandleUtility.DistanceToCircle(worldPosition, 0.25f);
                
                if (i > 0)
                {
                    var prev = controlPoints[i - 1];
                    var prevPos = transform.TransformPoint(prev.position);
                    Handles.color = Color.yellow;
                    Handles.DrawLine(prevPos, worldPosition);
                }

                if (distance == 0)
                {
                    Handles.color = Color.white;
                    _DrawSelector(worldPosition, 0.25f, false);
                    isOverSomePoint = true;
                }
                else if (_selectedStandardConnection.selectedControlPointIndices.Contains(i))
                {
                    Handles.color = Color.yellow;
                    _DrawSelector(worldPosition, 0.25f, false);
                }
                else
                {
                    Handles.color = Color.blue;
                    _DrawSelector(worldPosition, 0.25f, false);
                }

                if (_selectedStandardConnection.selectedControlPointIndices.Count > 0 &&
                    _selectedStandardConnection.selectedControlPointIndices[^1] == i)
                {
                    var leftHandlePos = worldPosition + controlPoint.leftHandle;
                    var rightHandlePos = worldPosition + controlPoint.rightHandle;

                    Handles.color = Color.magenta;
                    Handles.DrawDottedLine(leftHandlePos, rightHandlePos, 5f);
                    Handles.color = Color.magenta;
                    var newLeftHandlePosition =
                        Handles.FreeMoveHandle(leftHandlePos, 0.15f, Vector3.zero, Handles.CircleHandleCap);
                    var newRightHandlePosition =
                        Handles.FreeMoveHandle(rightHandlePos, 0.15f, Vector3.zero, Handles.CircleHandleCap);

                    if (newLeftHandlePosition != leftHandlePos)
                    {
                        var so = new SerializedObject(_selectedStandardConnection);
                        so.Update();
                        Undo.RecordObject(_selectedStandardConnection, "Move Left Handle");

                        controlPoint.leftHandle = newLeftHandlePosition - worldPosition;
                        _selectedStandardConnection.RefreshVisuals();
                    }

                    if (newRightHandlePosition != rightHandlePos)
                    {
                        var so = new SerializedObject(_selectedStandardConnection);
                        so.Update();
                        Undo.RecordObject(_selectedStandardConnection, "Move Right Handle");

                        controlPoint.rightHandle = newRightHandlePosition - worldPosition;
                        _selectedStandardConnection.RefreshVisuals();
                    }

                    if (Event.current.shift || _selectedStandardConnection.selectedControlPointIndices.Count > 1)
                    {
                        var pos = Handles.PositionHandle(worldPosition, Quaternion.identity);
                        if (pos != worldPosition)
                        {
                            var so = new SerializedObject(_selectedStandardConnection);
                            so.Update();
                            Undo.RecordObject(_selectedStandardConnection, "Move Control Point");
                            controlPoint.position = transform.InverseTransformPoint(pos);

                            var offset = pos - worldPosition;

                            foreach (var index in _selectedStandardConnection.selectedControlPointIndices)
                            {
                                if (index == i)
                                    continue;

                                var point = controlPoints[index];
                                point.position += offset;
                            }

                            _selectedStandardConnection.RefreshVisuals();
                        }
                    }
                    else
                    {
                        if (Event.current.type == EventType.MouseDrag && Event.current.button == 0)
                        {
                            var so = new SerializedObject(_selectedStandardConnection);
                            so.Update();
                            Undo.RecordObject(_selectedStandardConnection, "Move Control Point");
                            controlPoint.position = _Snap(transform.InverseTransformPoint(_RaycastPosition(20)),
                                transform);

                            _selectedStandardConnection.RefreshVisuals();
                            Event.current.Use();
                        }
                    }
                }
            }

            var canCreateNewPoint = false;
            var createNewPointPosition = Vector3.zero;
            var createNewPointIndex = 0;

            if (!isOverSomePoint)
            {
                var positions = controlPoints.Select(point => transform.TransformPoint(point.position)).ToArray();

                createNewPointPosition = _ClosestPointOnLineSegments(positions, out var before);
                createNewPointIndex = before;

                Handles.color = Color.green;

                var distance = HandleUtility.DistanceToCircle(createNewPointPosition, 0.25f);
                if (distance == 0)
                {
                    Handles.color = Color.white;
                    _DrawSelector(createNewPointPosition, 0.25f, false);
                    canCreateNewPoint = true;
                }
            }

            switch (Event.current.type)
            {
                case EventType.MouseDown:
                    if (Event.current.button == 0)
                    {
                        if (canCreateNewPoint)
                        {
                            var so = new SerializedObject(_selectedStandardConnection);
                            so.Update();

                            var dir = (controlPoints[createNewPointIndex + 1].position -
                                       controlPoints[createNewPointIndex].position).normalized;

                            Undo.RecordObject(_selectedStandardConnection, "Add Control Point");
                            _selectedStandardConnection.controlPoints.Insert(createNewPointIndex,
                                new CircuitStandardCableConnection.ControlPoint
                                {
                                    position = transform.InverseTransformPoint(createNewPointPosition),
                                    leftHandle = -dir,
                                    rightHandle = dir
                                });
                            
                            _selectedStandardConnection.RefreshVisuals();
                            _selectedStandardConnection.selectedControlPointIndices =
                                new List<int> { createNewPointIndex + 1 };
                            _RefreshFields();
                            Event.current.Use();
                        }
                        else
                            for (i = 0; i < controlPoints.Length; i++)
                            {
                                var controlPoint = controlPoints[i];
                                var position = controlPoint.position;
                                var worldPosition = transform.TransformPoint(position);
                                var distance = HandleUtility.DistanceToCircle(worldPosition, 0.25f);

                                if (distance == 0)
                                {
                                    var so = new SerializedObject(_selectedStandardConnection);
                                    so.Update();
                                    Undo.RecordObject(_selectedStandardConnection, "Changed Selection");

                                    if (Event.current.control)
                                    {
                                        if (_selectedStandardConnection.selectedControlPointIndices.Contains(i))
                                            _selectedStandardConnection.selectedControlPointIndices.Remove(i);
                                        else
                                            _selectedStandardConnection.selectedControlPointIndices.Add(i);
                                    }
                                    else
                                        _selectedStandardConnection.selectedControlPointIndices = new List<int> { i };
                                    
                                    _RefreshFields();
                                    Event.current.Use();
                                }
                            }
                    }

                    break;

                case EventType.MouseMove:
                    HandleUtility.Repaint();
                    break;

                case EventType.KeyDown:
                    if (Event.current.keyCode == KeyCode.LeftArrow)
                    {
                        if (_selectedStandardConnection.selectedControlPointIndices.Count == 0)
                        {
                            _selectedStandardConnection.selectedControlPointIndices = new List<int>
                            {
                                0
                            };
                        }
                        else
                        {
                            _selectedStandardConnection.selectedControlPointIndices = new List<int>
                            {
                                _selectedStandardConnection.selectedControlPointIndices[^1] - 1
                            };
                        }

                        if (_selectedStandardConnection.selectedControlPointIndices[^1] < 0)
                            _selectedStandardConnection.selectedControlPointIndices[^1] = controlPoints.Length - 1;

                        var selectedControlPoint =
                            controlPoints[_selectedStandardConnection.selectedControlPointIndices[^1]];
                        var selectedPosition = transform.TransformPoint(selectedControlPoint.position);
                        var direction = SceneView.lastActiveSceneView.camera.transform.rotation;
                        SceneView.lastActiveSceneView.LookAt(selectedPosition, direction, 1.5f);
                        Event.current.Use();
                        _RefreshFields();
                    }
                    else if (Event.current.keyCode == KeyCode.RightArrow)
                    {
                        if (_selectedStandardConnection.selectedControlPointIndices.Count == 0)
                        {
                            _selectedStandardConnection.selectedControlPointIndices = new List<int>
                            {
                                0
                            };
                        }
                        else
                        {
                            _selectedStandardConnection.selectedControlPointIndices = new List<int>
                            {
                                _selectedStandardConnection.selectedControlPointIndices[^1] + 1
                            };
                        }

                        if (_selectedStandardConnection.selectedControlPointIndices[^1] >= controlPoints.Length)
                            _selectedStandardConnection.selectedControlPointIndices[^1] = 0;

                        var selectedControlPoint =
                            controlPoints[_selectedStandardConnection.selectedControlPointIndices[^1]];
                        var selectedPosition = transform.TransformPoint(selectedControlPoint.position);
                        var direction = SceneView.lastActiveSceneView.camera.transform.rotation;
                        SceneView.lastActiveSceneView.LookAt(selectedPosition, direction, 1.5f);
                        Event.current.Use();
                        _RefreshFields();
                    }
                    else if (Event.current.keyCode == KeyCode.Backspace)
                    {
                        var so = new SerializedObject(_selectedStandardConnection);
                        so.Update();
                        Undo.RecordObject(_selectedStandardConnection, "Remove Control Point");

                        var indexes = _selectedStandardConnection.selectedControlPointIndices.OrderByDescending(j => j)
                            .ToArray();

                        foreach (var index in indexes)
                        {
                            if (index == 0 || index == controlPoints.Length - 1)
                                continue;

                            _selectedStandardConnection.controlPoints.RemoveAt(index - 1);
                        }

                        _selectedStandardConnection.RefreshVisuals();
                        _selectedStandardConnection.selectedControlPointIndices =
                            new List<int>(new[] { indexes[^1] - 1 });
                        _RefreshFields();
                    }
                    else if (Event.current.keyCode == KeyCode.A)
                    {
                        if (_selectedStandardConnection.selectedControlPointIndices.Count == controlPoints.Length)
                        {
                            _selectedStandardConnection.selectedControlPointIndices.Clear();
                        }
                        else
                        {
                            _selectedStandardConnection.selectedControlPointIndices =
                                new List<int>(Enumerable.Range(0, controlPoints.Length));
                        }

                        _RefreshFields();
                        Event.current.Use();
                    }

                    break;
            }
        }
        #endregion

        #region Utils
        private Vector3 _Snap(Vector3 point, Transform transform)
        {
            if (EditorSnapSettings.snapEnabled ? !Event.current.control : Event.current.control)
            {
                point = transform.TransformPoint(point);

                var grid = EditorSnapSettings.gridSize;
                point.x = Mathf.Round(point.x / grid.x) * grid.x;
                point.y = Mathf.Round(point.y / grid.y) * grid.y;
                point.z = Mathf.Round(point.z / grid.z) * grid.z;

                point = transform.InverseTransformPoint(point);
            }


            return point;
        }

        private Vector3 _RaycastPosition(float maxDistance)
        {
            var ray = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);
            if (Physics.Raycast(ray, out var hit, maxDistance))
            {
                return hit.point - ray.direction * 0.125f;
            }

            return ray.origin + ray.direction * maxDistance;
        }

        private void _DrawSelector(Vector3 position, float radius, bool isCube)
        {
            if (isCube)
                Handles.CubeHandleCap(0, position, Quaternion.identity, radius, EventType.Repaint);
            else
                Handles.SphereHandleCap(0, position, Quaternion.identity, radius, EventType.Repaint);
        }

        private void _DrawMessageBox(string message, Vector2 position)
        {
            Handles.BeginGUI();
            var textSize = _styleSceneGUIText.CalcSize(new GUIContent(message));
            var textPosition = new Vector2(position.x - textSize.x / 2f, position.y - textSize.y / 2f);
            var rect = new Rect(textPosition.x - 5, textPosition.y - 5, textSize.x + 10, textSize.y + 10);
            GUI.Box(rect, GUIContent.none, _styleSceneGUI);
            GUI.Label(new Rect(textPosition, textSize), message, _styleSceneGUIText);
            Handles.EndGUI();
        }

        private void _ExitToNormalMode()
        {
            _currentMode = CurrentMode.Normal;

            Selection.activeGameObject = _previousSelection;

            _previousSelection = null;
            _selectedPlug = null;

            if (_selectedStandardConnection != null)
                _selectedStandardConnection.selectedControlPointIndices.Clear();
            _selectedStandardConnection = null;
            _RefreshFields();
        }

        private void _CreateConnection(CircuitPlug a, CircuitPlug b)
        {
            switch (_wireCreationType)
            {
                case WireCreationType.Standard:
                    var stndrd = new GameObject("Connection").AddComponent<CircuitStandardCableConnection>();
                    stndrd.PlugA = a;
                    stndrd.PlugB = b;
                    stndrd.color = _wireCreationColor;
                    stndrd.material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/Cable.mat");
                    stndrd.prefabShockVFX =
                        AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/VFX/VFX Shock.prefab");
                    Undo.RegisterCreatedObjectUndo(stndrd.gameObject, "Create Connection");

                    stndrd.RefreshVisuals();
                    break;

                case WireCreationType.Wireless:
                    var wrlss = new GameObject("Connection").AddComponent<CircuitWirelessConnection>();
                    wrlss.PlugA = a;
                    wrlss.PlugB = b;
                    wrlss.color = _wireCreationColor;
                    Undo.RegisterCreatedObjectUndo(wrlss.gameObject, "Create Connection");
                    break;
            }
        }

        private void _HandlesFlat()
        {
            var so2 = new SerializedObject(_selectedStandardConnection);
            so2.Update();
            Undo.RecordObject(_selectedStandardConnection, "Flat Handles");

            var points = _selectedStandardConnection.GetControlPoints();

            foreach (var index in _selectedStandardConnection.selectedControlPointIndices)
            {
                if (index == 0 || index == points.Length - 1)
                    continue;

                var point = points[index];

                var oldLeft = point.leftHandle + point.position;
                var oldRight = point.rightHandle + point.position;

                var prev = points[index - 1];
                var next = points[index + 1];

                var dirLeft = (prev.position - point.position).normalized;
                var dirRight = (next.position - point.position).normalized;

                var plane = -Vector3.Cross(dirLeft, dirRight).normalized;

                oldLeft = _GetPointOnPlane(oldLeft, point.position, plane);
                oldRight = _GetPointOnPlane(oldRight, point.position, plane);

                point.leftHandle = oldLeft - point.position;
                point.rightHandle = oldRight - point.position;
            }

            _selectedStandardConnection.RefreshVisuals();
        }

        private void _HandlesSmooth(bool left, bool right)
        {
            var so2 = new SerializedObject(_selectedStandardConnection);
            so2.Update();
            Undo.RecordObject(_selectedStandardConnection, "Smooth Handles");

            var points = _selectedStandardConnection.GetControlPoints();

            foreach (var index in _selectedStandardConnection.selectedControlPointIndices)
            {
                if (index == 0 || index == points.Length - 1)
                    continue;

                var point = points[index];

                var prev = points[index - 1];
                var next = points[index + 1];
                var dirLeft = (prev.position - point.position).normalized;
                var dirRight = (next.position - point.position).normalized;

                if (left)
                    point.leftHandle = (dirLeft - dirRight / 2f).normalized;
                if (right)
                    point.rightHandle = (dirRight - dirLeft / 2f).normalized;
            }

            _selectedStandardConnection.RefreshVisuals();
        }

        private void _HandlesRecalculate(bool left, bool right)
        {
            var so2 = new SerializedObject(_selectedStandardConnection);
            so2.Update();
            Undo.RecordObject(_selectedStandardConnection, "Rectangle Handles");
            var points = _selectedStandardConnection.GetControlPoints();

            foreach (var index in _selectedStandardConnection.selectedControlPointIndices)
            {
                var point = points[index];

                if (index > 0)
                {
                    var prev = points[index - 1];
                    var dirLeft = (prev.position - point.position).normalized;
                    if (left)
                        point.leftHandle = dirLeft;
                }

                if (index < points.Length - 1)
                {
                    var next = points[index + 1];
                    var dirRight = (next.position - point.position).normalized;
                    if (right)
                        point.rightHandle = dirRight;
                }
            }
            
            _selectedStandardConnection.RefreshVisuals();
        }

        private Vector3 _GetPointOnPlane(Vector3 point, Vector3 planePos, Vector3 planeNormal)
        {
            var d = Vector3.Dot(planeNormal, planePos);
            var t = (d - Vector3.Dot(planeNormal, point)) / Vector3.Dot(planeNormal, planeNormal);
            return point + t * planeNormal;
        }

        private void _RefreshFields()
        {
            displayed = false;
            displayed = true;
        }

        private static Vector3 _ClosestPointOnLineSegments(Vector3[] vertices, out int before)
        {
            var maxDistance = 10f;
            const float minDistance = 0.5f;
            const int steps = 15;

            var ray = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);

            if (Physics.Raycast(ray, out var hit, maxDistance))
                maxDistance = hit.distance;

            var minDist = float.MaxValue;
            var closest = Vector3.zero;
            before = -1;

            for (int step = 0; step < steps; step++)
            {
                var t = step / (float)steps;
                var p = ray.origin + ray.direction * Mathf.Lerp(minDistance, maxDistance, t);
                var c = _ClosestPointOnLineSegments(vertices, p, out var b);
                var dist = (c - p).sqrMagnitude;
                if (dist < minDist)
                {
                    minDist = dist;
                    closest = c;
                    before = b;
                }
            }

            return closest;
        }

        private static Vector3 _ClosestPointOnLineSegments(Vector3[] vertices, Vector3 p, out int before)
        {
            var minDist = float.MaxValue;
            var closest = Vector3.zero;
            before = -1;
            for (var i = 0; i < vertices.Length - 1; i++)
            {
                var a = vertices[i];
                var b = vertices[i + 1];
                var c = _ClosestPointOnLineSegment(a, b, p);
                var dist = (c - p).sqrMagnitude;
                if (dist < minDist)
                {
                    minDist = dist;
                    closest = c;
                    before = i;
                }
            }

            return closest;
        }

        private static Vector3 _ClosestPointOnLineSegment(Vector3 a, Vector3 b, Vector3 p)
        {
            var ap = p - a;
            var ab = b - a;
            var magnitude = ab.sqrMagnitude;
            var abap = Vector3.Dot(ap, ab);
            var t = abap / magnitude;
            if (t < 0)
                return a;
            if (t > 1)
                return b;
            return a + ab * t;
        }
        #endregion
    }
    #endif
}