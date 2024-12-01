using System.Collections.Generic;
using UnityEngine;

namespace UI
{
    [CreateAssetMenu(fileName = "EmojiData", menuName = "Solis/Game/Dialog/EmojiData")]
    public class EmojisData : ScriptableObject
    {
        public List<EmojisStructure> emojisStructure;
    }
}