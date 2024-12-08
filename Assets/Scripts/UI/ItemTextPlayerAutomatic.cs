using Solis.Data;
using Solis.Player;
using UnityEngine;

namespace UI
{
    public class ItemTextPlayerAutomatic : ItemPlayerText
    {
        public CharacterTypeFilter characterTypeFilter = CharacterTypeFilter.Both;
        public bool canRepeat = false;
        private void OnTriggerEnter(Collider col)
        {
            if (!col.CompareTag("Player")) return;
            if (!col.TryGetComponent(out PlayerControllerBase p)) return;
            if(characterTypeFilter.Filter(p.CharacterType))
            {
                Debug.Log("Playing Dialog");
                PlayDialog();
                if (!canRepeat)
                    gameObject.SetActive(false);
            }
        }
    }
}
