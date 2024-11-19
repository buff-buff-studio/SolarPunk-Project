using System;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Solis.Misc.Grass
{
    [Serializable]
    public struct GrassVolumeMap
    {
        public bool use;
        public Texture2D map;
        
        #if UNITY_EDITOR
        public Vector2Int size;
        #endif
    }
    
    [ExecuteInEditMode]
    public class GrassVolume : MonoBehaviour
    {
        private static readonly int HeightMap = Shader.PropertyToID("_HeightMap");
        private static readonly int HeightMapMax = Shader.PropertyToID("_HeightMapMax");
        private static readonly int GrassMap = Shader.PropertyToID("_GrassMap");

        public Vector3 size = new(100, 10, 100);
        public int subdivisions = 10;

        private Mesh _mesh;
        public Material material;

        [Header("HEIGHT MAP")] 
        public GrassVolumeMap heightMap;

        [Header("VISIBILITY MAP")] 
        public GrassVolumeMap visibilityMap;
        
        private void OnEnable()
        {
            _UpdateMesh();
        }

        private void OnDisable()
        {
            _mesh = null;
        }

        private void OnValidate()
        {
            _UpdateMesh();
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireCube(transform.position + size / 2, size);
        }

        private void OnDrawGizmosSelected()
        {
            var divs = Mathf.Max(1, subdivisions);
            var pos = transform.position;
            Gizmos.color = Color.gray;
            for (var i = 0; i <= divs; i++)
            {
                Gizmos.DrawLine(
                    pos + new Vector3(0, 0, i * size.z / divs),
                    pos + new Vector3(size.x, 0, i * size.z / divs)
                );
                Gizmos.DrawLine(
                    pos + new Vector3(i * size.x / divs, 0, 0),
                    pos + new Vector3(i * size.x / divs, 0, size.z)
                );
            }
        }

        private void Update()
        {
            var mpb = new MaterialPropertyBlock();
            if (heightMap.use && heightMap.map != null)
            {
                mpb.SetTexture(HeightMap, heightMap.map);
                mpb.SetFloat(HeightMapMax, size.y);
            }

            if (visibilityMap.use && visibilityMap.map != null)
                mpb.SetTexture(GrassMap, visibilityMap.map);

            Graphics.DrawMesh(_mesh, transform.localToWorldMatrix, material, gameObject.layer, null, 0, mpb);
        }

        private void _UpdateMesh()
        {
            var divs = Mathf.Max(1, subdivisions);
            
            var triangles = new int[divs * divs * 6];
            var vertices = new Vector3[(divs + 1) * (divs + 1)];
            var uvs = new Vector2[vertices.Length];
            
            for (var i = 0; i <= divs; i++)
            {
                for (var j = 0; j <= divs; j++)
                {
                    vertices[i * (divs + 1) + j] = new Vector3(
                        j * size.x / divs,
                        0,
                        i * size.z / divs
                    );
                    
                    uvs[i * (divs + 1) + j] = new Vector2(
                        (float) j / divs,
                        (float) i / divs
                    );
                }
            }
            
            for (var i = 0; i < divs; i++)
            {
                for (var j = 0; j < divs; j++)
                {
                    triangles[(i * divs + j) * 6 + 2] = i * (divs + 1) + j;
                    triangles[(i * divs + j) * 6 + 1] = i * (divs + 1) + j + 1;
                    triangles[(i * divs + j) * 6 + 0] = (i + 1) * (divs + 1) + j;
                    triangles[(i * divs + j) * 6 + 5] = i * (divs + 1) + j + 1;
                    triangles[(i * divs + j) * 6 + 4] = (i + 1) * (divs + 1) + j + 1;
                    triangles[(i * divs + j) * 6 + 3] = (i + 1) * (divs + 1) + j;
                }
            }
            
            if (_mesh == null)
            {
                _mesh = new Mesh
                {
                    hideFlags = HideFlags.HideAndDontSave
                };
            }
            
            _mesh.Clear();
            
            _mesh.vertices = vertices;
            _mesh.triangles = triangles;
            _mesh.uv = uvs;

            _mesh.RecalculateNormals();
            _mesh.RecalculateBounds();
        }

        #if UNITY_EDITOR
        public void AutoGenerateMaps(bool height, bool visibility)
        {
            var size = 512;
            
            //Do raycast to get height
            var heightMapTexture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var visibilityMapTexture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            
            for (var i = 0; i < size; i++)
            {
                for (var j = 0; j < size; j++)
                {
                    var ray = new Ray(
                        transform.position + new Vector3(i * this.size.x / size, 100, j * this.size.z / size),
                        Vector3.down
                    );
                    if (Physics.Raycast(ray, out var hit))
                    {
                        var h = (hit.point.y - transform.position.y) / this.size.y;
                        if(height)
                            heightMapTexture.SetPixel(i, j, new Color(h, h, h, 1));
                        if(visibility)
                            visibilityMapTexture.SetPixel(i, j, new Color(1, 1, 1, 1));
                    }
                    else
                    {
                        if(height)
                            heightMapTexture.SetPixel(i, j, new Color(0, 0, 0, 0));
                        if(visibility)
                            visibilityMapTexture.SetPixel(i, j, new Color(0, 0, 0, 0));
                    }
                }
            }
            
            if(height)
            {
                heightMapTexture.Apply();
                var heightMapPath = EditorUtility.SaveFilePanelInProject("Save Height Map", "HeightMap", "png", "Save Height Map");
                if (!string.IsNullOrEmpty(heightMapPath))
                {
                    System.IO.File.WriteAllBytes(heightMapPath, heightMapTexture.EncodeToPNG());
                    AssetDatabase.Refresh();
                    
                    //set texture import settings to normal map
                    var importer = (TextureImporter) AssetImporter.GetAtPath(heightMapPath);
                    importer.textureType = TextureImporterType.NormalMap;
                    importer.SaveAndReimport();
                    
                    heightMap.map = AssetDatabase.LoadAssetAtPath<Texture2D>(heightMapPath);
                    
                }
            }
            
            if(visibility)
            {
                visibilityMapTexture.Apply();
                var visibilityMapPath = EditorUtility.SaveFilePanelInProject("Save Visibility Map", "VisibilityMap", "png", "Save Visibility Map");
                if (!string.IsNullOrEmpty(visibilityMapPath))
                {
                    System.IO.File.WriteAllBytes(visibilityMapPath, visibilityMapTexture.EncodeToPNG());
                    AssetDatabase.Refresh();
                    
                    //set texture import settings to normal map
                    var importer = (TextureImporter) AssetImporter.GetAtPath(visibilityMapPath);
                    importer.textureType = TextureImporterType.NormalMap;
                    importer.SaveAndReimport();
                    
                    visibilityMap.map = AssetDatabase.LoadAssetAtPath<Texture2D>(visibilityMapPath);
                }
            }
        }
        #endif
    }
    
    #if UNITY_EDITOR
    [CustomEditor(typeof(GrassVolume))]
    public class GrassVolumeEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            var volume = target as GrassVolume;
            if (volume == null)
            {
                return;
            }
            
            serializedObject.Update();

            DrawDefaultInspector();
            
            if (GUILayout.Button("Auto Generate Height Map"))
            {
                volume.AutoGenerateMaps(true, false);
            }
            
            if (GUILayout.Button("Auto Generate Visibility Map"))
            {
                volume.AutoGenerateMaps(false, true);
            }
            
            serializedObject.ApplyModifiedProperties();
        }
    }
    
    [CustomPropertyDrawer(typeof(GrassVolumeMap))]
    public class GrassVolumeMapDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var use = property.FindPropertyRelative("use");
            var map = property.FindPropertyRelative("map");
            var size = property.FindPropertyRelative("size");
            
            var rect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            EditorGUI.PropertyField(rect, use, new GUIContent("Use"));
            
            if (use.boolValue)
            {
                rect.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
                EditorGUI.PropertyField(rect, map, new GUIContent("Map"));
                
                var mapValue = map.objectReferenceValue as Texture2D;
                if (mapValue == null)
                {
                    rect.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
                    _SizeField(rect, property);
                    //EditorGUI.PropertyField(rect, size, new GUIContent("Size"));
                }
                
                rect.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
                if (mapValue == null)
                {
                    if (GUI.Button(rect, "Create"))
                    {
                        var path = EditorUtility.SaveFilePanelInProject("Save Height Map", "HeightMap", "png", "Save Height Map");
                        if (!string.IsNullOrEmpty(path))
                        {
                            var texture = new Texture2D(size.vector2IntValue.x, size.vector2IntValue.y, TextureFormat.RGBA32, false);
                            var colors = new Color[size.vector2IntValue.x * size.vector2IntValue.y];
                            for (var i = 0; i < colors.Length; i++)
                            {
                                colors[i] = Color.white;
                            }
                            texture.SetPixels(colors);
                            texture.Apply();
                            System.IO.File.WriteAllBytes(path, texture.EncodeToPNG());
                            AssetDatabase.Refresh();
                            
                            var importer = (TextureImporter) AssetImporter.GetAtPath(path);
                            importer.textureType = TextureImporterType.NormalMap;
                            importer.SaveAndReimport();
                            
                            map.objectReferenceValue = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                        }
                    }
                }
                else
                {
                    if (GUI.Button(rect, "Clear"))
                    {
                        map.objectReferenceValue = null;
                    }
                }
            }
        }
        
        //draw size as dropdown (128, 256, 512, 1024, 2048, 4096)
        private static void _SizeField(Rect rect, SerializedProperty property)
        {
            var size = property.FindPropertyRelative("size");
            var sizes = new[] {128, 256, 512, 1024, 2048, 4096};
            var labels = new[] {"128", "256", "512", "1024", "2048", "4096"};
            var index = Array.IndexOf(sizes, size.vector2IntValue.x);
            if (index == -1)
            {
                index = 3;
            }
            index = EditorGUI.Popup(rect, "Size", index, labels);
            size.vector2IntValue = new Vector2Int(sizes[index], sizes[index]);
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var use = property.FindPropertyRelative("use");
            var map = property.FindPropertyRelative("map");
            
            var height = EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
            
            if (use.boolValue)
            {
                height += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
                height += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
                var mapValue = map.objectReferenceValue as Texture2D;
                if (mapValue == null)
                {
                    height += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
                }
                height += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
            }
            
            return height;
        }
    }
    #endif
}