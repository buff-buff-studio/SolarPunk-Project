using System;
using Solis.i18n;
using TMPro;
using UnityEngine;

namespace Interface
{
    /// <summary>
    /// Automatically localizes a TextMeshPro label.
    /// </summary>
    [RequireComponent(typeof(TMP_Text))]
    [DisallowMultipleComponent]
    public class Label : MonoBehaviour
    {
        #region Public Fields
        public bool resize = false;
        #endregion

        #region Private Fields
        private TMP_Text _text;
        private string _buffer;
        private bool _translated;

        private string _prefix;
        private string _suffix;
        #endregion

        /// <summary>
        /// Updates the buffer of the label without localizing it.
        /// </summary>
        public string SetBuffer
        {
            set => _buffer = value;
        }

        /// <summary>
        /// Updates the prefix of the label without localizing it.
        /// </summary>
        public string SetPrefix
        {
            set => _prefix = value;
        }

        /// <summary>
        /// Updates the suffix of the label without localizing it.
        /// </summary>
        public string SetSuffix
        {
            set => _suffix = value;
        }

        #region Unity Callbacks

        private void Awake()
        {
            if (TryGetComponent(out _text))
            {
                _buffer = _text.text;
                _prefix = "";
                _suffix = "";

                _translated = true;
                LanguagePalette.OnLanguageChanged += _Localize;
            }else Debug.LogError("Label component requires a TextMeshPro component", this);
        }

        private void OnEnable()
        {
            if (_text)
                _Localize();
            else
                Debug.LogError("Label component requires a TextMeshPro component", this);
        }

        private void OnDisable()
        {
            _text.text = _buffer;
            LanguagePalette.OnLanguageChanged -= _Localize;
        }

        /// <summary>
        /// Updates the buffer and disable the localization.
        /// </summary>
        /// <param name="text"></param>
        public void SetText(string text)
        {
            _buffer = text;
            _translated = false;
            _Localize();
        }

        /// <summary>
        /// Updates the buffer of the label and localizes it.
        /// </summary>
        /// <param name="buffer"></param>
        public void Localize(string buffer)
        {
            if(_buffer == buffer)
                return;

            _translated = true;
            _buffer = buffer;
            _Localize();
        }

        /// <summary>
        /// Updates the prefix of the label and localizes it.
        /// </summary>
        /// <param name="prefix"></param>
        public void Prefix(string prefix)
        {
            if (_prefix == prefix)
                return;

            _prefix = prefix;
            _Localize();
        }

        /// <summary>
        /// Updates the suffix of the label and localizes it.
        /// </summary>
        /// <param name="suffix"></param>
        public void Suffix(string suffix)
        {
            if (_suffix == suffix)
                return;

            _suffix = suffix;
            _Localize();
        }

        public void UpdateLabel()
        {
            _Localize();
        }
        #endregion

        #region Private Methods
        private void _Localize()
        {
            if (!_text)
                if (!TryGetComponent(out _text))
                    return;

            var middle = _translated ? (!string.IsNullOrWhiteSpace(_buffer) ? LanguagePalette.Localize(_buffer) : _buffer) : _buffer;
            _text.text = $"{_prefix}{middle}{_suffix}";

            if (resize)
                _Resize();
        }

        private void _Resize()
        {
            _text.ForceMeshUpdate();
            _text.rectTransform.sizeDelta = new Vector2(_text.renderedWidth, _text.renderedHeight);
        }
        #endregion
    }
}