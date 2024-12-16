using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Cinemachine;
using DefaultNamespace;
using NetBuff.Components;
using NetBuff.Misc;
using Solis.Audio;
using Solis.i18n;
using Solis.Misc.Multicam;
using Solis.Packets;
using Solis.Player;
using TMPro;
using UI;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

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
        [SerializeField] private GameObject characterInfo;
        [SerializeField] private TMP_Text characterName;
        [SerializeField] private List<CharacterTypeAndImages> characterTypesAndEmotions;
        
        public NetworkBehaviourNetworkValue<DialogPlayerBase> currentDialog = new(); 
        public IntNetworkValue index;
        public EmojisData emojisStructure;
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
            //emojisStructure.emojisStructure.ForEach(c => c.EmojiNameDisplay = c.emoji.ToString());
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

        private AudioPlayer _audioPlayer;

        public void UpdateDialog(int oldValue, int newValue)
        {
            if (newValue == -1) ClosePanel();
            else
            {
                if (nextImage.activeSelf) nextImageAnimator.Play("NextDialogClose");
                IsDialogPlaying = true;
                var character = currentDialog.Value.currentDialog.dialogs[index.Value].characterType;

                if (character != CharacterTypeEmote.None)
                {
                    var stringAudio = character == CharacterTypeEmote.Diluvio
                        ? character.ToString()
                        : character.ToString() + Random.Range(0, 3);
                    _audioPlayer = AudioSystem.PlayDialogStatic(stringAudio, true);
                }
                TypeWriteText(currentDialog.Value.currentDialog.dialogs[index.Value], () =>
                {
                    if (nextImage.activeSelf) nextImageAnimator.Play("NextDialogOpen");
                    else nextImage.SetActive(true);
                   KillAudio();
                });
            }
        }

        public void KillAudio()
        {
            if(_audioPlayer != null) AudioSystem.Instance.Kill(_audioPlayer);
        }
        private void ClosePanel()
        {
            IsDialogPlaying = false;
            playersText.text = "0/2";
            if (IsServer)
            {
                charactersReady.Value = 0;
                hasSkipped.Clear();
            }
            textWriterSingle.ClearText();
            orderTextGameObject.SetActive(false);
            nextImage.SetActive(false);
            if(!CinematicController.IsPlaying)
                MulticamCamera.Instance!.ChangeCameraState(MulticamCamera.CameraState.Gameplay,
                CinemachineBlendDefinition.Style.EaseInOut, 1);
            
            KillAudio();
            textWriterSingle.KillAudio();
        }

        private void TypeWriteText(DialogStruct dialogData, Action callback)
        {
            characterInfo.gameObject.SetActive(false);
            EnterImage(dialogData.characterType);
            var newText = LanguagePalette.Localize(dialogData.textValue);
            textWriterSingle.SetText(GetFormattedString(newText), callback);
            orderTextGameObject.SetActive(true);
        }
        public string GetFormattedString(string text)
        {
            effectsAndWords.Clear();
            
            var newText = text;
            foreach (var emojiStructure in emojisStructure.emojisStructure)
            {
                var emojiPlaceholder = $"{{{emojiStructure.emoji.ToString()}}}";
                var localized = LanguagePalette.Localize($"dialog.emoji.{emojiStructure.emojiNameDisplay}");
                var value =
                    $"<sprite name=\"{emojiStructure.emojiNameInSpriteEditor}\"> <color=#{emojiStructure.textColor.ToHexString()}>{localized}</color>";

                newText = newText.Replace(emojiPlaceholder, value);
            }

            var processedText = ProcessTags(newText);
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
            if (characterType == CharacterTypeEmote.None)
            {
                return;
            }
            characterInfo.gameObject.SetActive(true);
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