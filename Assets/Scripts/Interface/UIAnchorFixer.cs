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
                fixer.anchorMin = rect.anchorMin;
                fixer.anchorMax = rect.anchorMax;
                fixer.offsetMin = rect.offsetMin;
                fixer.offsetMax = rect.offsetMax;
            }
        }
    }
    #endif
}