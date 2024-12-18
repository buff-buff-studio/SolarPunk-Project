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

        #region Unity Callbacks
        private void OnEnable()
        {
            if (TryGetComponent(out _text))
            {
                _buffer = _text.text;
                _prefix = "";
                _suffix = "";

                LanguagePalette.OnLanguageChanged += _Localize;
                _Localize();
            }else Debug.LogError("Label component requires a TextMeshPro component", this);
        }

        private void OnDisable()
        {
            _text.text = _buffer;
            LanguagePalette.OnLanguageChanged -= _Localize;
        }

        public void SetText(string text)
        {
            _buffer = text;
            _text.text = _prefix + _buffer + _suffix;
            _translated = false;
        }

        public void Localize(string buffer)
        {
            if(_buffer == buffer)
                return;

            _translated = true;
            _buffer = buffer;
            _Localize();
        }

        public void Prefix(string prefix)
        {
            if (_prefix == prefix)
                return;

            _prefix = prefix;
            _Localize();
        }

        public void Suffix(string suffix)
        {
            if (_suffix == suffix)
                return;

            _suffix = suffix;
            _Localize();
        }
        #endregion

        #region Private Methods
        private void _Localize()
        {
            if (!_translated)
            {
                _text.text = _prefix + _buffer + _suffix;
                return;
            }

            _text.text = $"{_prefix}{LanguagePalette.Localize(_buffer)}{_suffix}";

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