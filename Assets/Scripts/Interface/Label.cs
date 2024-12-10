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
        #endregion

        #region Unity Callbacks
        private void OnEnable()
        {
            _text = GetComponent<TMP_Text>();
            _buffer = _text.text;
            
            LanguagePalette.OnLanguageChanged += _Localize;
            _Localize();
        }

        private void OnDisable()
        {
            _text.text = _buffer;
            LanguagePalette.OnLanguageChanged -= _Localize;
        }

        public void Localize(string buffer)
        {
            _buffer = buffer;
            _Localize();
        }
        #endregion

        #region Private Methods
        private void _Localize()
        {
            _text.text = LanguagePalette.Localize(_buffer);

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