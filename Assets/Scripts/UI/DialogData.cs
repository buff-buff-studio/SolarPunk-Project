using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using _Scripts.UI;
using TMPro;
using UI;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;

namespace UI
{
    [Serializable]
    public enum Effects
    {
        Shake,
        Big,
        Small,
        Rainbow,
        Glitch
    }
    [Serializable]
    public enum Emojis
    {
        Lever,
        Button,
        PressurePlate,
        ZipLine,
        RightMouseBtn,
        LeftMouseButton,
        W,
        AKey,
        S,
        D,
        Enter,
        Space,
        Shift,
        Y,
        B,
        X,
        A,
        LJoyStick,
        RJoyStick,
        PalmScanner,
        RobotScanner,
        Plug1, 
        Plug2,
        Socket1,
        Socket2,
        WindTurbine,
        Container,
        Box,
        Frog,
        Nina,
        Ram,
        Fan,
        Stop,
        Alert,
        E
    }

    [Serializable]
    public class EffectsAndWords
    {
        public Effects effects;
        public string word;

        public EffectsAndWords(Effects effect, string word)
        {
            effects = effect;
            this.word = word;
        }
    }
    [Serializable]
    public class DialogStruct
    {
        public CharacterTypeEmote characterType;
        [TextArea]
        public string textValue;
      //  public Emojis[] emojis;
    }
    
    [CreateAssetMenu(fileName = "Dialog", menuName = "Solis/Game/Dialog")]
    public class DialogData : ScriptableObject
    {
        public List<DialogStruct> dialogs;
    }
}

/*
#if UNITY_EDITOR

[CustomPropertyDrawer(typeof(DialogData), true)]
public class DialogDataDrawer : PropertyDrawer
{
    private bool _foldout;

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);
        SerializedObject serializedObject = new SerializedObject(property.objectReferenceValue);
        serializedObject.Update();

        position.height = EditorGUIUtility.singleLineHeight;
        EditorGUI.PropertyField(position, property, label);

        if (property.objectReferenceValue != null)
        {
            position.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
            
            _foldout = EditorGUI.Foldout(position, _foldout, "Details", true);

            if (_foldout)
            {
           
                position.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
                
                SerializedProperty textValueProperty = serializedObject.FindProperty("textValue");
                position.height = EditorGUI.GetPropertyHeight(textValueProperty);
                EditorGUI.PropertyField(position, textValueProperty, new GUIContent("Text Value"));
                position.y += position.height + EditorGUIUtility.standardVerticalSpacing;
                
                SerializedProperty emojisProperty = serializedObject.FindProperty("emojis");
                DrawEmojisArray(position, emojisProperty);

                SerializedProperty characterTypeProperty = serializedObject.FindProperty("characterType");
        
                if (characterTypeProperty != null)
                {
                    EditorGUILayout.PropertyField(characterTypeProperty, new GUIContent("Character Type"));
                }
                serializedObject.ApplyModifiedProperties();
                
            }
        }

        EditorGUI.EndProperty();
    }

    private void DrawEmojisArray(Rect position, SerializedProperty property)
    {
        if (property.isArray)
        {
            float labelWidth = 40f; 
            float fieldWidth = position.width - labelWidth;

            Rect labelRect = new Rect(position.x, position.y, labelWidth, EditorGUIUtility.singleLineHeight);
            Rect fieldRect = new Rect(position.x + labelWidth, position.y, fieldWidth, EditorGUIUtility.singleLineHeight);
            
            EditorGUI.LabelField(labelRect, "Size");
            EditorGUI.PropertyField(fieldRect, property.FindPropertyRelative("Array.size"), GUIContent.none);

            position.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
            
            for (int i = 0; i < property.arraySize; i++)
            {
                SerializedProperty emojiElement = property.GetArrayElementAtIndex(i);
                position.height = EditorGUIUtility.singleLineHeight;
                EditorGUI.PropertyField(position, emojiElement, new GUIContent($"Emoji {i}"));
                position.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
            }
        }
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float height = EditorGUIUtility.singleLineHeight;

        if (property.objectReferenceValue != null)
        {
            height += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

            if (_foldout)
            {
                SerializedObject serializedObject = new SerializedObject(property.objectReferenceValue);
                SerializedProperty textValueProperty = serializedObject.FindProperty("textValue");
                SerializedProperty emojisProperty = serializedObject.FindProperty("emojis");

                height += EditorGUI.GetPropertyHeight(textValueProperty) + EditorGUIUtility.standardVerticalSpacing;

                if (emojisProperty.isArray)
                {
                    height += EditorGUIUtility.singleLineHeight * (emojisProperty.arraySize + 1) + EditorGUIUtility.standardVerticalSpacing * (emojisProperty.arraySize + 1);
                }
            }
        }

        return height;
    }
}
#endif*/