using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Overlays;
using UnityEngine;
using UnityEngine.UIElements;

namespace Editor.Circuit
{
    [Overlay(typeof(SceneView), "Circuit Prefabs")]
    public class CircuitPrefabsPanel : Overlay
    {
        #region Static
        private const int _PREVIEW_SIZE = 50;
        #endregion
        
        private string _currentCategory = "";
        
        public override void OnCreated()
        {
            EditorApplication.hierarchyChanged += _Repaint;
            EditorApplication.playModeStateChanged += _Repaint1;
            EditorApplication.focusChanged += _Repaint2;
            EditorApplication.projectChanged += _Repaint;
        }

        public override void OnWillBeDestroyed()
        {
            EditorApplication.hierarchyChanged -= _Repaint;
            EditorApplication.playModeStateChanged -= _Repaint1;
            EditorApplication.focusChanged -= _Repaint2;
            EditorApplication.projectChanged -= _Repaint;
        }

        private void _Repaint2(bool obj)
        {
            _Repaint();
        }

        private void _Repaint1(PlayModeStateChange obj)
        {
            _Repaint();
        }

        private void _Repaint()
        {
            displayed = false;
            displayed = true;
        }

        public override VisualElement CreatePanelContent()
        {
            var root = new VisualElement();
            var prefabFiles = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs/Circuit/Components" });

            root.Add(new Label("Circuit Prefabs")
            {
                style =
                {
                    unityFontStyleAndWeight = FontStyle.Bold,
                    marginLeft = 1,
                    marginTop = 5, marginBottom = 5
                }
            });
            
            //aDD A DROPDOWN THAT FLEXES THE ENTIRE WIDTH, AND SHOWS THE LIST OF CATEGORIES (BASIC, CONTROLS, GADGETS)
            var folders = new List<string>();
            //GET ALL FOLDERS IN THE PREFABS/CIRCUIT/COMPONENTS
            foreach (var prefabFile in prefabFiles)
            {
                var path = AssetDatabase.GUIDToAssetPath(prefabFile);
                var folder = path.Substring(0, path.LastIndexOf('/'));
                if (!folders.Contains(folder))
                {
                    //add only folder name
                    folders.Add(folder.Substring(folder.LastIndexOf('/') + 1));
                }
            }

            if(string.IsNullOrEmpty(_currentCategory) && folders.Count > 0)
                _currentCategory = folders[0];

            var dropdown = new DropdownField("", folders, folders.IndexOf(_currentCategory))
            {
                style =
                {
                    width = 170,
                    marginLeft = 0,
                    marginBottom = 5
                }
            };
            dropdown.RegisterValueChangedCallback(evt =>
            {
                _currentCategory = evt.newValue;
                _Repaint();
            });
            
            prefabFiles = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs/Circuit/Components/" + _currentCategory });

            root.Add(dropdown);
            var scrollView = new ScrollView();
            VisualElement currentRow = null;

            for (var n = 0; n < prefabFiles.Length; n++)
            {
                if (n % 3 == 0)
                {
                    currentRow = new VisualElement
                    {
                        style =
                        {
                            flexDirection = FlexDirection.Row,
                            alignItems = Align.Center,
                            justifyContent = Justify.FlexStart,
                            width = _PREVIEW_SIZE * 3 + 20,
                            height = _PREVIEW_SIZE + 25
                        }
                    };

                    scrollView.Add(currentRow);
                }

                var path = AssetDatabase.GUIDToAssetPath(prefabFiles[n]);
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var preview = AssetPreview.GetAssetPreview(asset);
                if (preview == null)
                    preview = AssetPreview.GetMiniThumbnail(asset);

                var item = new VisualElement
                {
                    style =
                    {
                        width = _PREVIEW_SIZE,
                        height = _PREVIEW_SIZE + 25
                    }
                };

                if (n % 3 != 0)
                    item.style.marginLeft = 10;

                item.RegisterCallback<MouseLeaveEvent>(evt =>
                {
                    //chec kif mouse 0 is pressed
                    if (evt.button == 0)
                    {
                        if (Event.current.type == EventType.MouseDrag)
                        {
                            DragAndDrop.PrepareStartDrag();
                            DragAndDrop.objectReferences = new Object[] { asset };
                            DragAndDrop.StartDrag("Prefab");
                            Event.current.Use();
                        }
                    }
                });


                var name = new Label(asset.name)
                {
                    style =
                    {
                        unityFontStyleAndWeight = FontStyle.Bold,
                        unityTextAlign = TextAnchor.MiddleCenter,
                        //label wrap text
                        whiteSpace = WhiteSpace.Normal,
                        width = _PREVIEW_SIZE,
                        fontSize = 8
                    }
                };

                var image = new Image
                {
                    image = preview,
                    style =
                    {
                        width = _PREVIEW_SIZE,
                        height = _PREVIEW_SIZE
                    }
                };

                item.Add(image);
                item.Add(name);

                currentRow!.Add(item);
            }

            scrollView.style.maxHeight = 100;
            root.Add(scrollView);
            return root;
        }
    }
}