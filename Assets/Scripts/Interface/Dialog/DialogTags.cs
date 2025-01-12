using System;
using Solis.i18n;
using UnityEngine;

namespace Interface.Dialog
{
    public class LabelTag : DialogTextPreprocessor.Tag
    {
        public string key;

        public override bool LoadArguments(DialogText text, Arguments arguments)
        {
            arguments.TryGetString("", out key, "");
            return true;
        }

        public override string GetPrefix(DialogText text)
        {
            if (text.emojis.TryGetValue(key, out var value))
                return
                    $"<sprite={value.icon}> <color=#{ColorUtility.ToHtmlStringRGBA(value.color)}>{LanguagePalette.Localize($"dialog.label.{value.key}")}</color>";

            return $"[{key}]";
        }
    }

    public class PauseTag : DialogTextPreprocessor.Tag
    {
        public float pause = 0.75f;

        public override bool LoadArguments(DialogText text, Arguments arguments)
        {
            arguments.TryGetFloat("", out pause, 1f);
            return true;
        }

        public override void OnOpen(DialogText text, DialogTextPreprocessor.ParseState state, int index, int rawIndex)
        {
            state.currentTypingTime += pause;
        }
    }

    public class EraseTag : DialogTextPreprocessor.Tag
    {
        public override bool Closable => true;

        public float delay = 0.5f;
        public float timeForChar = 0.025f;

        private DialogTextPreprocessor.CharTypingTimings[] _eraseTimings =
            Array.Empty<DialogTextPreprocessor.CharTypingTimings>();

        private float _eraseStartTime;
        private float _eraseEndTime;
        private int _tagStart;

        private float _startTypingSpeed;
        private DialogTextPreprocessor.TypingAnimation _startTypingAnimation;

        public override bool LoadArguments(DialogText text, Arguments arguments)
        {
            arguments.TryGetFloat("delay", out delay, delay);
            arguments.TryGetFloat("speed", out var speed, 1f);
            timeForChar *= speed;

            return delay >= 0 && speed > 0;
        }

        public override void OnOpen(DialogText text, DialogTextPreprocessor.ParseState state, int index, int rawIndex)
        {
            _tagStart = index;
            _startTypingSpeed = state.currentTypingSpeed;
            _startTypingAnimation = state.currentTypingAnimation;
        }

        public override void OnClose(DialogText text, DialogTextPreprocessor.ParseState state, int start, int rawStart,
            int end, int rawEnd)
        {
            var currentTimer = state.currentTypingTime;
            var length = end - start;

            _eraseStartTime = currentTimer + delay;
            _eraseEndTime = _eraseStartTime + timeForChar * length;
            _eraseTimings = new DialogTextPreprocessor.CharTypingTimings[length];

            for (var i = 0; i < length; i++)
            {
                _eraseTimings[^(i + 1)] = new DialogTextPreprocessor.CharTypingTimings()
                    { start = _eraseStartTime + i * timeForChar, end = _eraseStartTime + (i + 1) * timeForChar };
            }
            
            if (text.typingTime >= _eraseEndTime)
            {
                //Restore state
                state.Insert("</size>", rawEnd);
                state.Insert("<size=0>", rawStart);
                state.currentTypingAnimation = _startTypingAnimation;
                state.currentTypingSpeed = _startTypingSpeed;
            }

            state.currentTypingTime = _eraseEndTime;
        }

        public override void Apply(DialogText text, TMPCharData data, int index, int rawIndex)
        {
            if (text.typingTime < _eraseStartTime || text.typingTime >= _eraseEndTime)
                return;
            
            var progress = (text.typingTime - _eraseTimings[data.Index - _tagStart].start) / (_eraseTimings[data.Index - _tagStart].end - _eraseTimings[data.Index - _tagStart].start);
            var clamp = 1f - Mathf.Clamp01(progress);
            text.Result.animations[data.Index].Apply(text, data, index, clamp);
        }
    }

    public class RainbowTag : DialogTextPreprocessor.Tag
    {
        public override bool Closable => true;
        
        public float speed = 1f;
        public float scale = 0.1f;
        
        public override void Apply(DialogText text, TMPCharData data, int index, int rawIndex)
        {
            var color = Color.HSVToRGB((Time.time * speed + index * scale) % 1, 1, 1);
            data.SetColor(color);
        }
    }
}