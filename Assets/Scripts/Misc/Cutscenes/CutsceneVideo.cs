using System;
using System.Collections;
using System.Collections.Generic;
using Interface;
using UnityEngine;
using UnityEngine.Video;

public class CutsceneVideo : CutsceneObject
{
    [System.Serializable]
    public struct VideoSubtitle
    {
        public float time;
        public string text;
    }

    public VideoPlayer videoPlayer;
    public Label labelSubtitle;
    public List<VideoSubtitle> subtitles = new();
    public int subtitleIndex = 0;

    private bool _isPlaying = false;

    public override void Play()
    {
        base.Play();
        videoPlayer.Play();
        _isPlaying = true;
    }

    private void FixedUpdate()
    {
        if (!_isPlaying)
            return;

        if (subtitles.Count > subtitleIndex && videoPlayer.time >= subtitles[subtitleIndex].time)
        {
            labelSubtitle.Localize(subtitles[subtitleIndex].text);
            subtitleIndex++;
        }

        if (!videoPlayer.isPlaying && videoPlayer.time > .1f)
        {
            _isPlaying = false;
            Stop();
        }
    }
}
