using NetBuff.Components;
using NetBuff.Misc;
using Solis.Player;
using UnityEngine;

namespace Interface.Dialog.Activators
{
    public class TriggerDialogActivator : NetworkBehaviour
    {
        public DialogData dialogData;

        public BoolNetworkValue dialogOpened = new(false, NetworkValue.ModifierType.Everybody);

        private void OnEnable()
        {
            WithValues(dialogOpened);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (dialogOpened.Value)
                return;
            
            var player = other.GetComponent<PlayerControllerBase>();
            if (player != null && player.HasAuthority)
            {
                dialogOpened.Value = true;
                DialogController.Instance.OpenDialog(dialogData);
            }
        }
    }
}