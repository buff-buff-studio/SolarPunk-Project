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
        public bool PlayDialog()
        {
            if(!canBeReplayed && _hasPlayed) return false;

            _hasPlayed = true;
            DialogPanel.Instance.PlayDialog(this);
            return true;
        }
    }
}
