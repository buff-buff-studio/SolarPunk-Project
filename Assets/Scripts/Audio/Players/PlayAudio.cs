using UnityEngine;

namespace Solis.Audio.Players
{
    public class PlayAudio : MonoBehaviour
    {
        public string audioName;

        public void Play(string audio)
        {
            Debug.Log("PlayAudio");
            var a =  AudioSystem.PlayVfxStatic(audio);
            a.At(transform.position);
        }

        public void PlayAudioName()
        {
          var audio =  AudioSystem.PlayVfxStatic(audioName);
          audio.At(transform.position);
        }
    }
}