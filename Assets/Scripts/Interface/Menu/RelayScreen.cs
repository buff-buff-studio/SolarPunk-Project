using System;
using Solis.Core;
using Solis.Misc.Integrations;
using Solis.Settings;
using TMPro;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace Interface
{
    public class RelayScreen : MonoBehaviour
    {
        #region Inspector Fields
        public static RelayScreen Instance { get; private set; }

        [Header("REFERENCES")]
        public SettingsManager settingsManager;
        public TMP_InputField inputFieldUsername;
        public TMP_Text textLoggedIn;
        public TMP_InputField inputRelayCode;
        public Button joinButton;
        #endregion

        #region Private Fields

        private TMP_Text _joinButtonText;
        private readonly Color _interactableColor = new Color(0.9490196f, 0.8313726f, 0.4745098f, 1);
        private readonly Color _nonInteractableColor = new Color(0.6039216f, 0.5294118f, 0.3176471f, 1);

        private string _firstUsername;

        #endregion


        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(this);
        }

        private async void OnEnable()
        {
            _joinButtonText = joinButton.GetComponentInChildren<TMP_Text>();
            joinButton.interactable = !string.IsNullOrEmpty(inputRelayCode.text);
            _joinButtonText.color = joinButton.interactable ? _interactableColor : _nonInteractableColor;

            await UnityServices.InitializeAsync();
            if (!AuthenticationService.Instance.IsSignedIn)
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }

        public void GenerateUsername()
        {
            settingsManager.Username = "<unknown>";
            if(settingsManager.Username != "<unknown>")
                inputFieldUsername.text = settingsManager.Username;
            else
            {
                if (DiscordController.IsConnected &&
                    !string.IsNullOrEmpty(DiscordController.Username))
                {
                    inputFieldUsername.text = DiscordController.Username;
                }
                else
                {
#if PLATFORM_STANDALONE_WIN
                    inputFieldUsername.text = System.Environment.UserName;
#else
                    inputFieldUsername.text = SystemInfo.deviceName;
#endif
                    if (string.IsNullOrEmpty(inputFieldUsername.text) ||
                        string.IsNullOrWhiteSpace(inputFieldUsername.text) || inputFieldUsername.text == "<unknown>")
                        inputFieldUsername.text = $"Player {UnityEngine.Random.Range(0, 1000)}";
                }

                if (inputFieldUsername.text.Length < 4)
                    for (var i = 0; i < 4 - inputFieldUsername.text.Length; i++)
                        inputFieldUsername.text += "_";

                settingsManager.Username = inputFieldUsername.text;
            }

#if UNITY_EDITOR
            inputFieldUsername.text += " (UNITY)";
#endif
            _firstUsername = inputFieldUsername.text;

            //resize the input field to fit the text
            ResizeUsername();
            //resize the logged in text to fit the text
            textLoggedIn.ForceMeshUpdate();
            textLoggedIn.rectTransform.sizeDelta = new Vector2(textLoggedIn.preferredWidth, textLoggedIn.rectTransform.sizeDelta.y);
        }

        public void ResizeUsername()
        {
            inputFieldUsername.ForceLabelUpdate();
            inputFieldUsername.GetComponent<RectTransform>().sizeDelta =
                new Vector2(inputFieldUsername.preferredWidth, inputFieldUsername.GetComponent<RectTransform>().sizeDelta.y);

            var h = inputFieldUsername.GetComponentInParent<HorizontalLayoutGroup>();
            h.enabled = false;
            h.enabled = true;
        }

        public void EndEditUsername()
        {
            if (string.IsNullOrEmpty(inputFieldUsername.text) ||
                string.IsNullOrWhiteSpace(inputFieldUsername.text) ||
                inputFieldUsername.text.Length < 4)
                inputFieldUsername.text = _firstUsername;

            settingsManager.Username = inputFieldUsername.text;
            ResizeUsername();
        }

        public void JoinCodeChanged()
        {
            joinButton.interactable = inputRelayCode.text.Length > 4;
            _joinButtonText.color = joinButton.interactable ? _interactableColor : _nonInteractableColor;
        }

        public void Create()
        {
            SolisNetworkManager.username = inputFieldUsername.text;
            SolisNetworkManager.isJoining = false;
            SolisNetworkManager.usingRelay = true;

            SceneManager.LoadScene("Core");
        }

        public void Join(string user, string code)
        {
            inputFieldUsername.text = user;
            inputRelayCode.text = code;
            Join();
        }

        public void Join()
        {
            SolisNetworkManager.username = inputFieldUsername.text;
            SolisNetworkManager.isJoining = true;
            SolisNetworkManager.usingRelay = true;
            SolisNetworkManager.relayCode = inputRelayCode.text;

            SceneManager.LoadScene("Core");
        }
    }
}