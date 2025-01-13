using NetBuff.Components;
using NetBuff.Misc;
using Solis.Data;
using Solis.Packets;
using Solis.Player;
using UnityEngine;

namespace Interface.Dialog.Activators
{
    public class InteractionDialogActivator : NetworkBehaviour
    {
        public CharacterTypeFilter filter = CharacterTypeFilter.Both;
        public DialogData dialogData;
        public float radius = 3f;
        public BoolNetworkValue dialogOpened = new(false, NetworkValue.ModifierType.Everybody);
        public bool isReusable = false;
        
        private void OnDrawGizmos()
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, radius);
        }

        private void OnEnable()
        {
            WithValues(dialogOpened);
            PacketListener.GetPacketListener<PlayerInteractPacket>().AddServerListener(OnPlayerInteract);
        }

        private void OnDisable()
        {
            PacketListener.GetPacketListener<PlayerInteractPacket>().RemoveServerListener(OnPlayerInteract);
        }

        private bool OnPlayerInteract(PlayerInteractPacket arg1, int arg2)
        {
            if (dialogOpened.Value)
                return false;
            
            var player = GetNetworkObject(arg1.Id).GetComponent<PlayerControllerBase>();
            if (player != null && player.HasAuthority && filter.Filter(player.CharacterType) && Vector3.Distance(player.transform.position, transform.position) < radius)
            {
                if(!isReusable)
                    dialogOpened.Value = true;
                DialogController.Instance.OpenDialog(dialogData);
                return true;
            }
            
            return false;
        }
    }
}