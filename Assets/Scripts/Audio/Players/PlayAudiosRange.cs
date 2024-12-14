
using System;
using System.Collections;
using System.Collections.Generic;
using NetBuff.Misc;
using Solis.Audio;
using Solis.Packets;
using Solis.Player;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Audio.Players
{
    public class PlayAudiosRange : MonoBehaviour
    {
        [SerializeField]private string[] _audios;
        private AudioPlayer _audioPlayer;
        private Coroutine _routine;
        private float _currentTime;
        private float _maxTimeInRoutine = 5f;

        private void OnEnable()
        {
            PlayerControllerBase.onRespawn += StopAudio;
            StopAudio();
        }
        
        private void OnDisable()
        {
            PlayerControllerBase.onRespawn -= StopAudio;
            StopAudio();
        }


        private void Update()
        {
            if (_routine != null)
            {
                _currentTime += Time.deltaTime;
                if(_currentTime >= _maxTimeInRoutine)
                    StopAudio();
            }
        }

        public void Play(string audio)
        {
            Debug.Log("PlayAudio");
            AudioSystem.Instance.PlayVfx(audio);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Player")) return;
            _currentTime = 0;
            _routine = StartCoroutine(PlayAudioName());
        }

        private void OnTriggerExit(Collider other)
        {
          //  if (!other.CompareTag("Player")) return;
          if(_routine != null) StopCoroutine(_routine);
        }

        private void StopAudio()
        {
            if(_routine != null) StopCoroutine(_routine);
            if(_audioPlayer != null) AudioSystem.Instance?.Kill(_audioPlayer);
            _routine = null;
        }


        public IEnumerator PlayAudioName()
        {
            _audioPlayer = AudioSystem.Instance.PlayVfx(_audios[Random.Range(0, _audios.Length)]);
            if(_audioPlayer != null) _audioPlayer.At(transform.position);
            yield return new WaitForSeconds(Random.Range(2, 4));
            _routine =  StartCoroutine(PlayAudioName());
        }
    }
}
