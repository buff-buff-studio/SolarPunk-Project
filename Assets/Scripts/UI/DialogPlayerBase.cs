using _Scripts.UI;
using NetBuff.Components;

namespace UI
{
    public class DialogPlayerBase : NetworkBehaviour
    {
        public DialogData currentDialog;
        public static bool IsDialogPlaying => DialogPanel.Instance.index.Value != -1;
        public bool canBeReplayed = true;
        private bool _hasPlayed;
        public void PlayDialog()
        {
            if (!canBeReplayed && _hasPlayed)return;
            
            _hasPlayed = true;
            DialogPanel.Instance.PlayDialog(this);
        }
    }
}
