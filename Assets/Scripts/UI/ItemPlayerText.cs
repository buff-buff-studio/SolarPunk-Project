using NetBuff.Components;

namespace UI
{
    public class ItemPlayerText : NetworkBehaviour
    {
        public InteractableTextData currentDialog;
        public bool IsDialogPlaying => InteractablePanel.Instance.Index != -1;
        public bool canBeReplayed = true;
        private bool _hasPlayed;
        public void PlayDialog()
        {
            if (!canBeReplayed && _hasPlayed)return;
            
            InteractablePanel.Instance.PlayDialog(this);
            _hasPlayed = true;
            
        }
    }
}