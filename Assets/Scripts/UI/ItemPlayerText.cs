using NetBuff.Components;
using UnityEngine;

namespace UI
{
    public class ItemPlayerText : NetworkBehaviour
    {
        public InteractableTextData currentDialog;
        public bool IsDialogPlaying => InteractablePanel.Instance.Index != -1;
        public void PlayDialog()
        {
            InteractablePanel.Instance.PlayDialog(this);
        }
    }
}