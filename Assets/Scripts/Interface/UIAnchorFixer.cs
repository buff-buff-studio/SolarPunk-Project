using UnityEngine;

namespace Interface
{
    [RequireComponent(typeof(RectTransform))]
    public class UIAnchorFixer : MonoBehaviour
    {
        private RectTransform _rectTransform;
        
        public Vector2 anchorMin;
        public Vector2 anchorMax;
        public Vector2 offsetMin;
        public Vector2 offsetMax;

        private void OnEnable()
        {
            _rectTransform = GetComponent<RectTransform>();
        }

        private void FixedUpdate()
        {
            Apply();
        }

        public void Apply()
        {
            _rectTransform.anchorMin = anchorMin;
            _rectTransform.anchorMax = anchorMax;
            
            _rectTransform.offsetMin = offsetMin;
            _rectTransform.offsetMax = offsetMax;
        }
    }
    
    #if UNITY_EDITOR
    [UnityEditor.CustomEditor(typeof(UIAnchorFixer))]
    public class UIAnchorFixerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            var fixer = (UIAnchorFixer) target;
            if (GUILayout.Button("Bake"))
            {
                var rect = fixer.GetComponent<RectTransform>();
                var propAnchorMin = serializedObject.FindProperty("anchorMin");
                var propAnchorMax = serializedObject.FindProperty("anchorMax");
                var propOffsetMin = serializedObject.FindProperty("offsetMin");
                var propOffsetMax = serializedObject.FindProperty("offsetMax");
                
                propAnchorMin.vector2Value = rect.anchorMin;
                propAnchorMax.vector2Value = rect.anchorMax;
                propOffsetMin.vector2Value = rect.offsetMin;
                propOffsetMax.vector2Value = rect.offsetMax;
                
                serializedObject.ApplyModifiedProperties();
            }
            
            
        }
    }
    #endif
}