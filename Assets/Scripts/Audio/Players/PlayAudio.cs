using UnityEngine;

namespace Solis.Audio.Players
{
    public class PlayAudio : MonoBehaviour
    {
        public string audioName;

        public void Play(string audio)
        {
            Debug.Log("PlayAudio");
            AudioSystem.Instance.PlayVfx(audio);
        }

        public void PlayAudioName()
        {
          var audio =  AudioSystem.PlayVfxStatic(audioName);
          audio.At(transform.position);
        }
    }
}