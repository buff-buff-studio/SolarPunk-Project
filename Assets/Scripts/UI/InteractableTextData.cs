using System;
using System.Collections.Generic;
using UnityEngine;

namespace UI
{
    [CreateAssetMenu(fileName = "InteractableText", menuName = "Solis/Game/Dialog/InteractableText")]
    public class InteractableTextData : ScriptableObject
    {
        public List<string> texts;
    }
}