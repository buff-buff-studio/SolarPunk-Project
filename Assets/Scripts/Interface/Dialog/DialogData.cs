using System;
using Solis.Data;
using UnityEngine;

namespace Interface.Dialog
{
    [CreateAssetMenu(fileName = "DialogData", menuName = "Solis/Dialog/DialogData")]
    public class DialogData : ScriptableObject
    {
        [Serializable]
        public struct Line
        {
            public DialogCharacter character;
            public string textKey;
        }
        
        public Line[] lines = Array.Empty<Line>();
    }
    
    #if UNITY_EDITOR
    [UnityEditor.CustomPropertyDrawer(typeof(DialogData.Line))]
    public class DialogDataLineDrawer : UnityEditor.PropertyDrawer
    {
        public override void OnGUI(Rect position, UnityEditor.SerializedProperty property, GUIContent label)
        {
            position.height = UnityEditor.EditorGUIUtility.singleLineHeight;
            var character = property.FindPropertyRelative("character");
            var textKey = property.FindPropertyRelative("textKey");
            var characterRect = new Rect(position.x, position.y, position.width * 0.25f, position.height);
            var textKeyRect = new Rect(position.x + position.width * 0.275f, position.y, position.width * 0.725f, position.height);
            character.enumValueIndex = (int)(DialogCharacter)UnityEditor.EditorGUI.EnumPopup(characterRect, (DialogCharacter)character.enumValueIndex);
            textKey.stringValue = UnityEditor.EditorGUI.TextField(textKeyRect, textKey.stringValue);
        }
    }
    #endif
}