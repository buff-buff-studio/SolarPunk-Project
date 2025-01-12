using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using AYellowpaper.SerializedCollections;
using TMPro;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Interface.Dialog
{
    [RequireComponent(typeof(TMP_Text))]
    [ExecuteInEditMode]
    public class DialogText : MonoBehaviour
    {
        [Serializable]
        public struct Label
        {
            public string icon;
            public string key;
            public Color color;
        }
        
        private TMP_Text _text;
        private DialogTextPreprocessor _preprocessor;
        
        public TMP_Text Text => _text;
        public SerializedDictionary<string, Label> emojis = new();
        
        [Range(0, 25)]
        public float typingTime = 1000;
        public float MaxTime => _preprocessor.Result.typingMaxTime;
        public DialogTextPreprocessor.ParseResult Result => _preprocessor.Result;
        
        private void OnEnable()
        {
            _text = GetComponent<TMP_Text>();
            _preprocessor = new DialogTextPreprocessor(this);
            _text.textPreprocessor = _preprocessor;
        }
        
        private void OnDisable()
        {
            _text.textPreprocessor = null;
        }

        private void Update()
        {
            _text.ForceMeshUpdate();
            
            var charDataArray = new TMPCharData[_text.textInfo.characterCount];
            for (var i = 0; i < _text.textInfo.characterCount; i++)
            {
                var charInfo = _text.textInfo.characterInfo[i];

                if (!charInfo.isVisible)
                    continue;

                charDataArray[i] = new TMPCharData(_text.textInfo, charInfo, i);
                
                    var timings = _preprocessor.Result.timings[i];
                var anim = _preprocessor.Result.animations[i];
                var progress = timings.GetProgress(typingTime);
                anim.Apply(this, charDataArray[i], i, progress);
            }
            
            foreach (var instance in _preprocessor.Result.tags)
            {
                var start = instance.start;
                var end = instance.end;
                
                for (var i = start; i < end; i++)
                {
                    var charInfo = _text.textInfo.characterInfo[i];

                    if (!charInfo.isVisible)
                        continue;
                    
                    instance.tag.Apply(this, charDataArray[i], i, i - start + instance.rawStart);
                }
            }
            
            _text.UpdateVertexData();
        }
    }
    
    #if UNITY_EDITOR
    [CustomEditor(typeof(DialogText))]
    public class DialogTextEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            var dialogText = (DialogText)target;
            var typingTime = serializedObject.FindProperty("typingTime");
            var labels = serializedObject.FindProperty("emojis");
            var maxTime = dialogText.MaxTime;

            EditorGUILayout.Slider(typingTime, 0, maxTime);
            EditorGUILayout.HelpBox($"Max Time: {maxTime}s", MessageType.Info);
            EditorGUILayout.PropertyField(labels, true);
            
            serializedObject.ApplyModifiedProperties();
        }
    }
    #endif

    public class DialogTextPreprocessor : ITextPreprocessor
    {
        public abstract class Tag
        {
            public virtual bool Closable => false;

            public virtual bool LoadArguments(DialogText text, Arguments arguments)
            {
                return true;
            }
            
            public virtual void OnOpen(DialogText text, ParseState state, int index, int rawIndex) {}
            public virtual void OnClose(DialogText text, ParseState state, int start, int rawStart, int end, int rawEnd) {}
            
            public virtual string GetPrefix(DialogText text) => string.Empty;
            public virtual string GetSuffix(DialogText text) => string.Empty;
            
            public virtual string GetInnerPrefix(DialogText text) => string.Empty;
            public virtual string GetInnerSuffix(DialogText text) => string.Empty;
            
            public virtual void Apply(DialogText text, TMPCharData data, int index, int rawIndex) {}

            public Tag CreateCopy()
            {
                var tag = (Tag)MemberwiseClone();
                return tag;
            }
        }

        public class TypingAnimation
        {
            public virtual void Apply(DialogText text, TMPCharData data, int index, float progress)
            {
                var clamped = Mathf.Clamp(progress, 0, 1);
                data.Scale(Vector3.one * clamped);
            }
        }

        public class TagInstance
        {
            public Tag tag;
            public string name;
            public int start;
            public int rawStart;
            public int end;
            public int rawEnd;
        }

        public class CharTypingTimings
        {
            public float start;
            public float end;
            
            public float GetProgress(float time)
            {
                if (end - start == 0)
                    return 1;
            
                return (time - start) / (end - start);
            }
        }
        
        public class ParseState
        {
            private readonly StringBuilder _result = new();
            private int _discard;

            public bool Parsing { get; set; } = true;
            public int RawLength => _result.Length;
            public int Length => _result.Length - _discard;
            
            public List<TagInstance> openTags = new();
            public List<TagInstance> tags = new();
            
            private readonly List<CharTypingTimings> _timings = new();
            private readonly List<TypingAnimation> _animations = new();
            public float currentTypingTime;
            public TypingAnimation currentTypingAnimation = new();
            public float currentTypingSpeed = 1f;
            
            public void Append(string text, int start, int length)
            {
                //Handle emojis correctly
                for (var i = start; i < start + length; i++)
                {
                    if (i > 0 && char.IsSurrogate(text[i]) && char.IsHighSurrogate(text[i - 1]))
                        _discard++;

                    _result.Append(text[i]);
                    AddTypingTimer(text[i]);
                }
            }
            
            public void Append(string text)
            {
                Append(text, 0, text.Length);
            }
            
            public void AppendRaw(string text, int start, int length)
            {
                _result.Append(text, start, length);
                _discard += length;
            }
            
            public void AppendRaw(string text)
            {
                _result.Append(text);
                _discard += text.Length;
            }
            
            public void AddBlankInternalChar()
            {
                _discard--;
                AddTypingTimer('?');
            }
            
            public void AddTypingTimer(char c)
            {
                var start = currentTypingTime;
                var end = start + 0.05f * currentTypingSpeed;
                _timings.Add(new CharTypingTimings { start = start, end = end});
                _animations.Add(currentTypingAnimation);
                currentTypingTime = end;
            }   
            
            public void AddEmptyTypingTimer()
            {
                _timings.Add(new CharTypingTimings { start = 0, end = 0});
                _animations.Add(currentTypingAnimation);
            }
            
            public void Insert(string text, int position, int start, int length)
            {
                _result.Insert(position, text.ToCharArray(), start, length);

                foreach(var tag in tags)
                {
                    if(tag.start >= position)
                        tag.start += length;

                    if(tag.end >= position)
                        tag.end += length;
                }
            }
            
            public void Insert(string text, int position)
            {
                Insert(text, position, 0, text.Length);
            }
            
            public ParseResult GetResult()
            {
                return new ParseResult
                {
                    text = _result.ToString(),
                    tags = tags.ToArray(),
                    timings = _timings.ToArray(),
                    animations = _animations.ToArray(),
                    typingMaxTime = _timings.Count == 0 ? 0 : _timings.Select(t => Mathf.Max(t.start, t.end)).Max()
                };
            }
        }

        public class ParseResult
        {
            public string text;
            public TagInstance[] tags;
            public CharTypingTimings[] timings;
            public TypingAnimation[] animations;
            public float typingMaxTime;
        }
        
        private const int MaxDepth = 10;
        private static readonly Regex _XMLRegex = new("<(\\/)?([\\w-]*)(?:\\s*(?:([\"\"\']?)([\\w-]*)\\3\\s*=\\s*([\"\"\']?)((?:[^\\s\"\"\'<>]|\\\\.)*)\\5|\\s*([\\w-]+)))*\\s*\\/?>", RegexOptions.Compiled);

        private static readonly Dictionary<string, Tag> _CustomTags = new()
        {
            {"label", new LabelTag()},
            {"pause", new PauseTag()},
            {"erase", new EraseTag()},
            {"rainbow", new RainbowTag()},
        };
        
        public ParseResult Result { get; private set; }
        public DialogText Text { get; private set; }
        
        public DialogTextPreprocessor(DialogText text)
        {
            Text = text;
        }
        
        public string PreprocessText(string text)
        {
            var result = ParseText(text);
            Result = result;
            return result.text;
        }
        
        public ParseResult ParseText(string text)
        {
            var state = new ParseState();
            ParseString(text, 0, state);
            return state.GetResult();
        }

        public virtual void ParseString(string text, int depth, ParseState state)
        {
            if(string.IsNullOrEmpty(text) || depth >= MaxDepth)
                return;

            var parsedUntil = 0;

            while (true)
            {
                var match = _XMLRegex.Match(text, parsedUntil);
                if (!match.Success)
                    break;
                
                var tag = match.Groups[2].Value;       
                var closing = match.Groups[1].Success;
                
                if(tag is "noparse") //Handle no-parse tag
                {
                    //Add text before tag
                    state.Append(text, parsedUntil, match.Index - parsedUntil);
                    state.Parsing = closing;

                    //Skip no-parse tag
                    parsedUntil = match.Index + match.Length;
                }
                else if (state.Parsing)
                {
                    if (_CustomTags.TryGetValue(tag, out var customTag)) //Custom tags
                    {
                        state.Append(text, parsedUntil, match.Index - parsedUntil);
                        parsedUntil = match.Index + match.Length;
                        
                        if (closing)
                        {
                            TagInstance tagInstance;
                            
                            if(tag == "" && state.openTags.Count > 0)
                                tagInstance = state.openTags[^1];
                            else
                                tagInstance = state.openTags.FindLast(t => t.name == tag);
                            
                            if(tagInstance == null)
                            {
                                //Invalid closing tag
                                state.AppendRaw("<noparse>");
                                state.Append(match.Value);
                                state.AppendRaw("</noparse>");
                                continue;
                            }
                            
                            ParseString(tagInstance.tag.GetInnerSuffix(Text), depth + 1, state);
                            tagInstance.end = state.Length;
                            tagInstance.rawEnd = state.RawLength;
                            tagInstance.tag.OnClose(Text, state, tagInstance.start, tagInstance.rawStart, tagInstance.end, tagInstance.rawEnd);
                            state.openTags.Remove(tagInstance);
                            ParseString(tagInstance.tag.GetSuffix(Text), depth + 1, state);
                        }
                        else
                        {
                            var copy = customTag.CreateCopy();
                            var keys = match.Groups[4].Captures;
                            var values = match.Groups[6].Captures;
                            var arguments = new Arguments(keys, values);

                            if (copy.LoadArguments(Text, arguments))
                            {
                                ParseString(copy.GetPrefix(Text), depth + 1, state);
                                copy.OnOpen(Text, state, state.Length, state.RawLength);

                                if (copy.Closable)
                                {
                                    var instance = new TagInstance
                                    {
                                        tag = copy,
                                        start = state.Length,
                                        rawStart = state.RawLength,
                                        name = tag
                                    };
                                
                                    state.openTags.Add(instance);
                                    state.tags.Add(instance);
                                
                                    ParseString(copy.GetInnerPrefix(Text), depth + 1, state);
                                }
                            }
                            else
                            {
                                state.Append(match.Value);
                            }
                        }
                    }
                    else //Assumes that standard tag are valid
                    {
                        if(tag == "sprite") //Add typing info for sprite
                            state.AddBlankInternalChar();
                        
                        state.Append(text, parsedUntil, match.Index - parsedUntil);
                        state.AppendRaw(match.Value);
                        parsedUntil = match.Index + match.Length;
                    }
                }
                else
                {
                    state.Append(text, parsedUntil, match.Index - parsedUntil);
                    state.Append(match.Value);
                    parsedUntil = match.Index + match.Length;
                }
            }
            
            state.Append(text, parsedUntil, text.Length - parsedUntil);

            if(depth != 0)
                return;

            foreach(var tag in state.openTags)
            {
                tag.end = state.Length;
                tag.rawEnd = state.RawLength;
            }
        }
    }
    
    public struct TMPCharData
    {
        public char Character {get;}
        public int Index {get;}
        public int VertexIndex {get;}
        public int VertexCount => 4;
        public TMP_TextInfo TextInfo {get;}
        public TMP_MeshInfo MeshInfo {get; private set;}

        public TMPCharData(TMP_TextInfo textInfo, TMP_CharacterInfo charInfo, int index)
        {
            Character = charInfo.character;
            Index = index;
            VertexIndex = charInfo.vertexIndex;
            TextInfo = textInfo;
            MeshInfo = textInfo.meshInfo[charInfo.materialReferenceIndex];
        }

        #region Vertex Methods
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector3 GetVertexPosition(int index)
        {
            return MeshInfo.vertices[VertexIndex + index];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetVertexPosition(int index, Vector3 position)
        {
            MeshInfo.vertices[VertexIndex + index] = position;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Color GetVertexColor(int index)
        {
            return MeshInfo.colors32[VertexIndex + index];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetVertexColor(int index, Color color)
        {
            MeshInfo.colors32[VertexIndex + index] = color;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float GetVertexAlpha(int index)
        {
            return MeshInfo.colors32[VertexIndex + index].a / 255f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetVertexAlpha(int index, float alpha)
        {
            var color = MeshInfo.colors32[VertexIndex + index];
            color.a = (byte)(alpha * 255);
            MeshInfo.colors32[VertexIndex + index] = color;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void TranslateVertex(int index, Vector3 translation)
        {
            MeshInfo.vertices[VertexIndex + index] += translation;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RotateVertex(int index, Vector3 pivot, float angle)
        {
            var position = GetVertexPosition(index);
            var direction = position - pivot;
            var rotated = Quaternion.Euler(0, 0, angle) * direction;
            MeshInfo.vertices[VertexIndex + index] = pivot + rotated;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ScaleVertex(int index, Vector3 pivot, Vector3 scale)
        {
            var position = GetVertexPosition(index);
            var direction = position - pivot;
            var scaled = new Vector3(direction.x * scale.x, direction.y * scale.y, direction.z * scale.z);
            MeshInfo.vertices[VertexIndex + index] = pivot + scaled;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RotateVertex(int index, float angle)
        {
            RotateVertex(index, GetCenter(), angle);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ScaleVertex(int index, Vector3 scale)
        {
            ScaleVertex(index, GetCenter(), scale);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void MultiplyColorVertex(int index, Color color)
        {
            var current = GetVertexColor(index);
            MeshInfo.colors32[VertexIndex + index] = current * color;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void MultiplyAlphaVertex(int index, float alpha)
        {
            var current = GetVertexColor(index);
            current.a = (byte)(current.a * alpha);
            MeshInfo.colors32[VertexIndex + index] = current;
        }
        #endregion

        #region Methods
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Rotate(float angle, Vector3 pivot)
        {
            var q = Quaternion.Euler(0, 0, angle);
            for (var i = 0; i < 4; i++)
            {
                var position = GetVertexPosition(i);
                position = q * (position - pivot) + pivot;
                SetVertexPosition(i, position);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Rotate(Vector3 angles, Vector3 pivot)
        {
            var q = Quaternion.Euler(angles);
            for (var i = 0; i < 4; i++)
            {
                var position = GetVertexPosition(i);
                position = q * (position - pivot) + pivot;
                SetVertexPosition(i, position);
            }
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Rotate(Quaternion rotation, Vector3 pivot)
        {
            for (var i = 0; i < 4; i++)
            {
                var position = GetVertexPosition(i);
                position = rotation * (position - pivot) + pivot;
                SetVertexPosition(i, position);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Scale(Vector3 scale, Vector3 pivot)
        {
            for (int i = 0; i < 4; i++)
            {
                var position = GetVertexPosition(i);
                position = Vector3.Scale(position - pivot, scale) + pivot;
                SetVertexPosition(i, position);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Translate(Vector3 translation)
        {
            for (int i = 0; i < 4; i++)
            {
                var position = GetVertexPosition(i);
                position += translation;
                SetVertexPosition(i, position);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Rotate(float angle)
        {
            Rotate(angle, GetCenter());
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Scale(Vector3 scale)
        {
            Scale(scale, GetCenter());
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Color GetColor()
        {
            return GetVertexColor(0);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetColor(Color color)
        {
            for (int i = 0; i < 4; i++)
                SetVertexColor(i, color);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float GetAlpha()
        {
            return GetVertexAlpha(0);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetAlpha(float alpha)
        {
            for (int i = 0; i < 4; i++)
                SetVertexAlpha(i, alpha);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void MultiplyColor(Color color)
        {
            for (int i = 0; i < 4; i++)
                MultiplyColorVertex(i, color);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void MultiplyAlpha(float alpha)
        {
            for (int i = 0; i < 4; i++)
                MultiplyAlphaVertex(i, alpha);
        }
        #endregion

        #region Utils Methods
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector3 GetCenter()
        {
            return (GetVertexPosition(0) + GetVertexPosition(2)) / 2;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void GetCharactersBounds(int start, int length, out Vector3 min, out Vector3 max)
        {
            min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            max = new Vector3(-float.MaxValue, -float.MaxValue, -float.MaxValue);
            var textInfo = TextInfo;
            for (var i = start; i < start + length; i++)
            {
                var charData = textInfo.characterInfo[i];
                if (!charData.isVisible)
                    continue;

                var materialIndex = charData.materialReferenceIndex;
                var vertices = textInfo.meshInfo[materialIndex].vertices;
                var vertexIndex = charData.vertexIndex;

                for (int j = 0; j < 4; j++)
                {
                    var vertex = vertices[vertexIndex + j];
                    min = Vector3.Min(min, vertex);
                    max = Vector3.Max(max, vertex);
                }
            }
        }
        #endregion
    }
    
    public class Arguments
    {
        private readonly Dictionary<string, string> _args = new();
        
        public Arguments(IList<Capture> keys, IList<Capture> values)
        {
            if (keys.Count != values.Count)
                throw new ArgumentException("Keys and values must have the same length");

            for (int i = 0; i < keys.Count; i++)
                _args[keys[i].Value.ToLower(CultureInfo.InvariantCulture)] = values[i].Value.ToLower(CultureInfo.InvariantCulture);
        }
        
        public bool Has(string key)
        {
            return _args.ContainsKey(key);
        }

        public bool TryGetString(string key, out string value, string @default = default)
        {
            if (_args.TryGetValue(key, out value))
                return true;

            value = @default;
            return false;
        }
        
        public bool TryGetBool(string key, out bool value, bool @default = default)
        {
            if (_args.TryGetValue(key, out var strValue) && bool.TryParse(strValue, out value))
                return true;

            value = @default;
            return false;
        }
        
        public bool TryGetInt(string key, out int value, int @default = default)
        {
            if (_args.TryGetValue(key, out var strValue) && int.TryParse(strValue, NumberStyles.Any, CultureInfo.InvariantCulture, out value))
                return true;

            value = @default;
            return false;
        }
        
        public bool TryGetFloat(string key, out float value, float @default = default)
        {
            if (_args.TryGetValue(key, out var strValue) && float.TryParse(strValue, NumberStyles.Any, CultureInfo.InvariantCulture, out value))
                return true;

            value = @default;
            return false;
        }
        
        #region Arrays
        public bool TryGetStringArray(string key, out string[] result)
        {
            if (_args.TryGetValue(key, out var strValue))
            {
                result = strValue.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                return true;
            }

            result = Array.Empty<string>();
            return false;
        }
        
        public bool TryGetBoolArray(string key, out bool[] result)
        {
            if (_args.TryGetValue(key, out var strValue))
            {
                var split = strValue.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                var values = new bool[split.Length];

                for (int i = 0; i < split.Length; i++)
                {
                    if (bool.TryParse(split[i], out values[i]))
                        continue;

                    result = Array.Empty<bool>();
                    return false;
                }

                result = values;
                return true;
            }

            result = Array.Empty<bool>();
            return false;
        }
        
        public bool TryGetIntArray(string key, out int[] result)
        {
            if (_args.TryGetValue(key, out var strValue))
            {
                var split = strValue.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                var values = new int[split.Length];

                for (int i = 0; i < split.Length; i++)
                {
                    if (int.TryParse(split[i], out values[i]))
                        continue;

                    result = Array.Empty<int>();
                    return false;
                }

                result = values;
                return true;
            }

            result = Array.Empty<int>();
            return false;
        }
        
        public bool TryGetFloatArray(string key, out float[] result)
        {
            if (_args.TryGetValue(key, out var strValue))
            {
                var split = strValue.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                var values = new float[split.Length];

                for (var i = 0; i < split.Length; i++)
                {
                    if (float.TryParse(split[i], NumberStyles.Any, CultureInfo.InvariantCulture, out values[i]))
                        continue;

                    result = Array.Empty<float>();
                    return false;
                }

                result = values;
                return true;
            }

            result = Array.Empty<float>();
            return false;
        }
        #endregion
    }
    
    //property drawer DialogText.Label
    #if UNITY_EDITOR
    [CustomPropertyDrawer(typeof(DialogText.Label))]
    public class LabelDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            var icon = property.FindPropertyRelative("icon");
            var key = property.FindPropertyRelative("key");
            var color = property.FindPropertyRelative("color");
            
            var distance = 5;
            var width = ( position.width - distance * 2) / 3;

            var iconRect = new Rect(position.x, position.y, width, position.height);
            var keyRect = new Rect(position.x + width + distance, position.y, width, position.height);
            var colorRect = new Rect(position.x + width * 2 + distance * 2, position.y, width, position.height);
            
            EditorGUI.PropertyField(iconRect, icon, GUIContent.none);
            EditorGUI.PropertyField(keyRect, key, GUIContent.none);
            EditorGUI.PropertyField(colorRect, color, GUIContent.none);
            
            EditorGUI.EndProperty();
        }
    }
    #endif
}