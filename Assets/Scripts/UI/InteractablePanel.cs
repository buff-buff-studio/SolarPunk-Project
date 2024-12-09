using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Cinemachine;
using DefaultNamespace;
using NetBuff.Components;
using NetBuff.Misc;
using Solis.i18n;
using Solis.Misc.Multicam;
using Solis.Packets;
using Solis.Player;
using Unity.VisualScripting;
using UnityEngine;

namespace UI
{
    public class InteractablePanel : NetworkBehaviour
    {
        private static InteractablePanel _instance;
        public static InteractablePanel Instance => _instance ? _instance : FindFirstObjectByType<InteractablePanel>();

        public static bool IsDialogPlaying;
        public TextScaler textWriterSingle;
        
        [SerializeField]private GameObject orderTextGameObject;
        public ItemPlayerText currentDialog; 
        private int _index;
        [SerializeField] private GameObject nextImage;

        public int Index
        {
            get => _index;
            set
            {
                _index = value;
                UpdateDialog(_index);
            }
        }

        public EmojisData emojisStructure;
        private Animator nextImageAnimator;
         protected void OnEnable()
        {
            PacketListener.GetPacketListener<PlayerInputPackage>().AddServerListener(OnClickDialog);
        }
        protected void OnDisable()
        {
            PacketListener.GetPacketListener<PlayerInputPackage>().RemoveServerListener(OnClickDialog);
        }
        private void Awake()
        {
            /*if (_instance != null)
            {
                Destroy(gameObject);
                return;
            }*/
            
            _instance = this;
            nextImage.TryGetComponent(out nextImageAnimator);
            _index = -1;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            emojisStructure.emojisStructure.ForEach(c => c.EmojiNameDisplay = c.emoji.ToString());
        }
#endif
        

        public void PlayDialog(ItemPlayerText dialogData)
        {
            currentDialog = dialogData;
            Index = 0;
        }

        public bool OnClickDialog(PlayerInputPackage playerInputPackage, int i)
        {
            Debug.Log("ON INPUT");
            if (!IsDialogPlaying) return false;
            if(playerInputPackage.Key != KeyCode.Return) return false;
            if(!orderTextGameObject.activeSelf) return false;
            var player = GetNetworkObject(playerInputPackage.Id);
            var controller = player.GetComponent<PlayerControllerBase>();
            if (controller == null)
                return false;
            if (textWriterSingle.isWriting)
            {
                textWriterSingle.SkipText();
                return true;
            }
            /*if (Index != -1)
            {
                return true;
            }*/
            
            if(currentDialog == null) return false;
            if (Index+ 1 > currentDialog.currentDialog.texts.Count - 1) 
                Index = -1;
            else
                Index++;
            
            return true;
        }

        public void UpdateDialog(int newValue)
        {
            if (newValue == -1) ClosePanel();
            else
            {
                if (nextImage.activeSelf) nextImageAnimator.Play("NextDialogClose");
                IsDialogPlaying = true;
                Debug.Log(Index);
                TypeWriteText(currentDialog.currentDialog.texts[Index], () =>
                {
                    if (nextImage.activeSelf) nextImageAnimator.Play("NextDialogOpen");
                    else nextImage.SetActive(true);
                });
            }
        }
        
        private void ClosePanel()
        {
            textWriterSingle.ClearText();
            IsDialogPlaying = false;
            orderTextGameObject.SetActive(false);
            nextImage.SetActive(false);
        }

        private void TypeWriteText(string dialogData, Action callback)
        {
            var newText = LanguagePalette.Localize(dialogData);
            textWriterSingle.SetText(GetFormattedString(newText), callback);
            orderTextGameObject.SetActive(true);
        }
        public string GetFormattedString(string text)
        {
            effectsAndWords.Clear();
            
            string newText = text;
            foreach (var emojiStructure in emojisStructure.emojisStructure)
            {
                string emojiPlaceholder = $"{{{emojiStructure.emoji.ToString()}}}";
                string value =
                    $"<sprite name=\"{emojiStructure.emojiNameInSpriteEditor}\"> <color=#{emojiStructure.textColor.ToHexString()}>{emojiStructure.emojiNameDisplay}</color>";
                newText = newText.Replace(emojiPlaceholder, value);
            }

            string processedText = ProcessTags(newText);
            Debug.Log(newText);
            textWriterSingle.effectsAndWords = effectsAndWords;
            return processedText;
        }
      
        List<EffectsAndWords> effectsAndWords = new List<EffectsAndWords>();
        
        private string ProcessTags(string textWithTags)
        {
            string processedText = textWithTags;
            
            foreach (var effect in Enum.GetValues(typeof(Effects)))
            {
                string effectName = effect.ToString();
        
             
                MatchCollection matches = GetRegexMatch(effectName, processedText);
                
                foreach (Match match in matches)
                {
                    string contentBetweenTags = match.Groups[1].Value;
                    
                    string nestedProcessedContent = ProcessTags(contentBetweenTags);
                    
                    effectsAndWords.Add(new EffectsAndWords((Effects)effect, nestedProcessedContent));
                    
                    processedText = processedText.Replace(match.Value, nestedProcessedContent);
                }
            }

            return processedText;
        }
        
       
        private MatchCollection GetRegexMatch(string effect, string textValue)
        {
            string pattern = $@"<{effect}>(.*?)<\/{effect}>";

            MatchCollection matches = Regex.Matches(textValue, pattern);
            return matches;
        }
        
        public override void OnSceneLoaded(int sceneId)
        {
            base.OnSceneLoaded(sceneId);
            ClosePanel();
        }
    }
}