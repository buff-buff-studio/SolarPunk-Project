using System.Collections;
using System.Collections.Generic;
using Solis.Data;
using UnityEngine;

public class RespawnHUD : MonoBehaviour
{
    public static RespawnHUD Instance { get; private set; }

    [SerializeField]
    private Animator _humanRespawnAnimator;
    [SerializeField]
    private Animator _robotRespawnAnimator;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(this);
        }
    }

    public void ShowHUD(CharacterType characterType = CharacterType.Human, float time = 0)
    {
        if (characterType == CharacterType.Human)
        {
            _humanRespawnAnimator.gameObject.SetActive(true);
            _humanRespawnAnimator.SetFloat("Speed", time);
            _humanRespawnAnimator.Play("Respawn");
        }else if (characterType == CharacterType.Robot)
        {
            _robotRespawnAnimator.gameObject.SetActive(true);
            _robotRespawnAnimator.SetFloat("Speed", time);
            _robotRespawnAnimator.Play("Respawn");
        }
    }

    public void CloseHUD()
    {
        _humanRespawnAnimator.gameObject.SetActive(false);
        _robotRespawnAnimator.gameObject.SetActive(false);
    }
}
