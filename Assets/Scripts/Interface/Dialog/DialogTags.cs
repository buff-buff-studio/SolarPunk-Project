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
        
        public float speed = 0.25f;
        public float scale = 0.002f;
        
        public override void Apply(DialogText text, TMPCharData data, int index, int rawIndex)
        {
            for (var i = 0; i < data.VertexCount; i++)
            {
                var pos = data.GetVertexPosition(i);
                var color = Color.HSVToRGB( Mathf.Repeat(Time.time * speed + (pos.x + pos.y) * scale, 1f), 1, 1);
                data.SetVertexColor(i, color);
            }
        }
    }

    public class GlitchTag : DialogTextPreprocessor.Tag
    {
        public override bool Closable => true;

        public float speed = 5f;
        public float scale = 0.1f;
        public float strength = 3f;

        public float alphaSpeed = 2f;
        public float alphaScale = 3.33f;

        public override void Apply(DialogText text, TMPCharData data, int index, int rawIndex)
        {
            //glitch text, effect, randomly scaling on x and y, and disappearing randomly
            for (var i = 0; i < data.VertexCount; i++)
            {
                var pos = data.GetVertexPosition(i);
                var offset = new Vector3(Mathf.PerlinNoise(pos.x * scale + Time.time * speed, pos.y * scale + Time.time * speed),
                    Mathf.PerlinNoise(pos.y * scale + Time.time * speed, pos.x * scale + Time.time * speed), 0);
                data.SetVertexPosition(i, pos + offset * strength);
            }
            
            if(Mathf.Repeat(Time.time * alphaSpeed + index * alphaScale, 1f) < 0.2f)
                data.SetAlpha(0);
        }
    }

    public class ScreamTag : DialogTextPreprocessor.Tag
    {
        public override bool Closable => true;
        
        public float speed = 10f;
        public float scale = 0.23f;
        public float strength = 10f;

        private DialogTextPreprocessor.TypingAnimation _before;

        public override void OnOpen(DialogText text, DialogTextPreprocessor.ParseState state, int index, int rawIndex)
        {
            _before = state.currentTypingAnimation;
            state.currentTypingAnimation = new ScreamTypingAnimation();
            state.currentTypingSpeed *= 0.5f;
        }

        public override void OnClose(DialogText text, DialogTextPreprocessor.ParseState state, int start, int rawStart, int end, int rawEnd)
        {
            state.currentTypingAnimation = _before;
            state.currentTypingSpeed *= 2f;
        }

        public override void Apply(DialogText text, TMPCharData data, int index, int rawIndex)
        {
            var offset = new Vector3(Mathf.PerlinNoise(index * scale + Time.time * speed, 0),
                Mathf.PerlinNoise(0, index * scale + Time.time * speed), 0);
            
            data.Translate(offset * strength);
        }
    }

    public class ScreamTypingAnimation : DialogTextPreprocessor.TypingAnimation
    {
        public override void Apply(DialogText text, TMPCharData data, int index, float progress)
        {
            var clamped = Mathf.Clamp(progress, 0, 1);
            data.Scale(Vector3.one * Mathf.Lerp(2f, 1f, clamped));
            var color = data.GetColor();
            data.SetColor(new Color(color.r, color.g, color.b,  clamped));
        }
    }

    public class ShakeTag : DialogTextPreprocessor.Tag
    {
        public override bool Closable => true;

        public float speed = 5f;
        public float scale = 0.1f;
        public float strength = 6f;
        
        public override void Apply(DialogText text, TMPCharData data, int index, int rawIndex)
        {
            var offset = new Vector3(Mathf.PerlinNoise(index * scale + Time.time * speed, 0),
                Mathf.PerlinNoise(0, index * scale + Time.time * speed), 0);

            data.Translate(offset * strength);
        }
    }
}