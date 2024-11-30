using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Cinemachine;
using DefaultNamespace;
using NetBuff.Components;
using NetBuff.Misc;
using Solis.i18n;
using Solis.Misc.Multicam;
using Solis.Packets;
using Solis.Player;
using TMPro;
using UI;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

[Serializable]
public enum Emotion
{
    Happy,
    Neutral,
    Confused
}
public enum CharacterTypeEmote
{
    Nina = 0,
    RAM = 1,
    Diluvio = 2,
    None = 3
}

[Serializable]
public struct CharacterTypeAndImages
{
    public CharacterTypeEmote characterType;
    public Sprite image;
}
[Serializable]
public class EmojisStructure
{
#if UNITY_EDITOR
    [HideInInspector]
    public string EmojiNameDisplay;
#endif
    public Emojis emoji;
    public string emojiNameDisplay;
    public string emojiNameInSpriteEditor;
    public Color textColor;
}

namespace _Scripts.UI
{
    
    public class DialogPanel : NetworkBehaviour
    {
        private static DialogPanel _instance;
        public static DialogPanel Instance => _instance ? _instance : FindFirstObjectByType<DialogPanel>();

        public static bool IsDialogPlaying;
        public TextScaler textWriterSingle;
        
        [SerializeField]private GameObject orderTextGameObject;
        [SerializeField] private Image characterImage;
        [SerializeField] private TMP_Text characterName;
        [SerializeField] private List<CharacterTypeAndImages> characterTypesAndEmotions;
        
        public NetworkBehaviourNetworkValue<DialogPlayerBase> currentDialog = new(); 
        public IntNetworkValue index;
        public List<EmojisStructure> emojisStructure = new List<EmojisStructure>();
        public IntNetworkValue charactersReady;
        
        [SerializeField]private List<int> hasSkipped = new List<int>();
        [SerializeField] private GameObject nextImage;
        [SerializeField] private TextMeshProUGUI playersText;
        
        private Animator nextImageAnimator;
        
        #region MonoBehaviour

        protected void OnEnable()
        {
            WithValues(charactersReady,index, currentDialog);
            
            PacketListener.GetPacketListener<PlayerInputPackage>().AddServerListener(OnClickDialog);
            index.OnValueChanged += UpdateDialog;
            charactersReady.OnValueChanged += UpdateText;

        }
        protected void OnDisable()
        {
            PacketListener.GetPacketListener<PlayerInputPackage>().RemoveServerListener(OnClickDialog);
            index.OnValueChanged -= UpdateDialog;
            charactersReady.OnValueChanged -= UpdateText;
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
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            emojisStructure.ForEach(c => c.EmojiNameDisplay = c.emoji.ToString());
        }
#endif
        #endregion
        

        public void PlayDialog(DialogPlayerBase dialogData)
        {
            currentDialog.Value = dialogData;
            index.Value = 0;

            MulticamCamera.Instance!.SetDialogueFocus(
                currentDialog.Value.currentDialog.dialogs[index.Value].characterType);
        }

        public bool OnClickDialog(PlayerInputPackage playerInputPackage, int i)
        {
            if (!IsDialogPlaying) return false;
            if(playerInputPackage.Key != KeyCode.Return) return false;
            if(hasSkipped.Contains(i)) return false;
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
            if (index.Value != -1)
            {
                hasSkipped.Add(i);
                charactersReady.Value++;
                playersText.text = charactersReady.Value + "/2";
#if UNITY_EDITOR
                if (hasSkipped.Count == 0) return true;
#else
                if (hasSkipped.Count < 2) return true;
#endif
            }
            
            if(currentDialog == null) return false;
            if (index.Value + 1 > currentDialog.Value.currentDialog.dialogs.Count - 1) 
                index.Value = -1;
            else
                index.Value++;

            hasSkipped.Clear();
            charactersReady.Value = 0;
            return true;
        }
        private void UpdateText(int oldvalue, int newvalue)
        {
            playersText.text = charactersReady.Value + "/2";
        }

        public void UpdateDialog(int oldValue, int newValue)
        {
            if (newValue == -1) ClosePanel();
            else
            {
                if (nextImage.activeSelf) nextImageAnimator.Play("NextDialogClose");
                IsDialogPlaying = true;
                TypeWriteText(currentDialog.Value.currentDialog.dialogs[index.Value], () =>
                {
                    if (nextImage.activeSelf) nextImageAnimator.Play("NextDialogOpen");
                    else nextImage.SetActive(true);
                });
            }
        }
        
        private void ClosePanel()
        {
            playersText.text = "0/2";
            if (IsServer)
            {
                charactersReady.Value = 0;
                hasSkipped.Clear();
            }
            textWriterSingle.ClearText();
            IsDialogPlaying = false;
            orderTextGameObject.SetActive(false);
            nextImage.SetActive(false);
            if(!CinematicController.IsPlaying)
                MulticamCamera.Instance!.ChangeCameraState(MulticamCamera.CameraState.Gameplay,
                CinemachineBlendDefinition.Style.EaseInOut, 1);
        }

        private void TypeWriteText(DialogStruct dialogData, Action callback)
        {
            characterImage.gameObject.SetActive(false);
            EnterImage(dialogData.characterType);
            var newText = LanguagePalette.Localize(dialogData.textValue);
            Debug.Log(newText);
            textWriterSingle.SetText(GetFormattedString(newText), callback);
            orderTextGameObject.SetActive(true);
        }
        public string GetFormattedString(string text)
        {
            effectsAndWords.Clear();
            
            /*
            for (int i = 0; i < emojis.Length; i++)
            {
                EmojisStructure emojisStructure = DialogPanel.Instance.emojisStructure.First(c => c.emoji == emojis[i]);
                var field = emojisStructure.emojiNameDisplay;
                string value = $"<sprite name=\"{emojisStructure.emojiNameInSpriteEditor}\"> <color=#{emojisStructure.textColor.ToHexString()}>{field}</color>";
                _instancedValues.Add(value);
            }
            */
            
            /*
            string pattern = @"\{\{(\w+)\}\}";
            string newText = Regex.Replace(text, pattern, match =>
            {
                string emojiName = match.Groups[1].Value;

                // Procura na estrutura de emojis
                var emojiStructure = emojisStructure.FirstOrDefault(c => c.emoji.ToString() == emojiName);

                if (emojiStructure != null)
                {
                    Debug.Log("Emoji encontrado: " + emojiStructure.emojiNameInSpriteEditor);

                    // Retorna a substituição com o sprite e a cor do texto
                    return $"<sprite name=\"{emojiStructure.emojiNameInSpriteEditor}\"><color=#{emojiStructure.textColor.ToHexString()}>{emojiStructure.emojiNameDisplay}</color>";
                }

                // Caso não encontre, mantém o texto original
                return match.Value;
            });*/
            
            string newText = text;
            foreach (var emojiStructure in emojisStructure)
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

        private void EnterImage(CharacterTypeEmote characterType)
        {
            characterImage.gameObject.SetActive(true);
            var choosed = characterTypesAndEmotions.FirstOrDefault(c => c.characterType == characterType);
            var sprite = choosed.image;
            characterImage.sprite = sprite;
            characterName.text = characterType.ToString();
            MulticamCamera.Instance!.SetDialogueFocus(characterType);
        }

        public override void OnSceneLoaded(int sceneId)
        {
            base.OnSceneLoaded(sceneId);
            ClosePanel();
        }
    }
}