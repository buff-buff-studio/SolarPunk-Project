using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AYellowpaper.SerializedCollections;
using Interface;
using Solis.Data;
using Solis.Data.Saves;
using Solis.i18n;
using Solis.Interface.Input;
using Solis.Misc.Integrations;
using Solis.UI;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using Screen = UnityEngine.Screen;

namespace Solis.Settings
{
    public class SettingsManager : WindowManager
    {
        private static string Path => Application.persistentDataPath;
        private static UniversalRenderPipelineAsset _renderPipeline;

        public SettingsTab[] settingsTabs;
        public Sprite selectedTab, unselectedTab;

        [Header("REFERENCES")]
        [SerializeField]
        private SettingsData settingsData;
        [SerializeField]
        private CanvasGroup canvasGroup;

        public Label renderScaleLabel;
        public GameObject discordToggle;
        public Language[] languages;
        public Button saveButton;
        public Button resetButton;

        [Space(10)]
        [Header("ITEMS")]
        [SerializeField] private SerializedDictionary<string, Toggle> boolItems;
        [SerializeField] private SerializedDictionary<string, ArrowItems> intItems;
        [SerializeField] private SerializedDictionary<string, Slider> floatItems;

        private bool IsSettingsOpen => canvasGroup.interactable;

        public string Username
        {
            get => string.IsNullOrEmpty(settingsData.username) ? "<unknown>" : settingsData.username;
            set
            {
                settingsData.username = value;
                Save();
            }
        }

#if UNITY_EDITOR
        public bool tryLocateItems;
        public bool resetItems;        
        public bool renameItems;   
        public bool resetSO;
        public bool updateNavigation;
#endif
        
        public static Action OnSettingsChanged;
        
        private readonly List<Resolution> _supportedResolutions = new List<Resolution>
        {//4k 21:9 to FHD, 4k 16:9 to HD, FHD 4:3 to SD
            new Resolution {width = 3840, height = 1600}, //21:9
            new Resolution {width = 3840, height = 2160}, //16:9
            new Resolution {width = 2560, height = 1080}, //21:9
            new Resolution {width = 1920, height = 1080}, //16:9
            new Resolution {width = 1440, height = 1080}, //4:3
            new Resolution {width = 1280, height = 960}, //4:3
            new Resolution {width = 1280, height = 720}, //16:9
            new Resolution {width = 1024, height = 768}, //4:3
            new Resolution {width = 800, height = 600}, //4:3
        };

        #region Unity Callbacks
        
        private void Awake()
        {
            DetectDisplay();
#if !PLATFORM_STANDALONE_WIN
            discordToggle.SetActive(false);
            boolItems["discord"].isOn = false;
            settingsData.TrySet("discord", false);
#else
            boolItems["discord"].onValueChanged.AddListener(DiscordController.EnableDiscord);
#endif
            Load();
            
            foreach (var i in intItems)
                i.Value.onChangeItem.AddListener(index => { settingsData.arrowItems[i.Key] = index; OnSettingsChanged?.Invoke(); });
            foreach (var f in floatItems)
                f.Value.onValueChanged.AddListener(value => { settingsData.sliderItems[f.Key] = value; OnSettingsChanged?.Invoke(); });
            foreach (var b in boolItems)
                b.Value.onValueChanged.AddListener(value => { settingsData.toggleItems[b.Key] = value; OnSettingsChanged?.Invoke(); });


            intItems["resolution"].SetItems(_supportedResolutions.Select(r => $"{r.width}x{r.height}").ToList());
        }

        protected override void Start()
        {
            base.Start();
            ApplySettings();
        }

        private void OnEnable()
        {
            OnSettingsChanged += ApplySettings;
            SelectableBackground.OnSelected += ScrollRectUpdate;
        }

        private void OnDisable()
        {
            OnSettingsChanged -= ApplySettings;
            SelectableBackground.OnSelected -= ScrollRectUpdate;
        }

        private void Update()
        {
            if(!IsSettingsOpen) return;

            if (SolisInput.GetKeyDown("NavigateRight"))
            {
                var i = currentIndex + 1;
                if (i >= settingsTabs.Length)
                    i = 0;
                Debug.Log($"Navigate Right: {i}");
                ChangeWindow(i);
            }else if (SolisInput.GetKeyDown("NavigateLeft"))
            {
                var i = currentIndex - 1;
                if (i < 0)
                    i = settingsTabs.Length - 1;
                ChangeWindow(i);
            }
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            if(tryLocateItems)
            {
                var toggles = GetComponentsInChildren<Toggle>(false);
                var arrows = GetComponentsInChildren<ArrowItems>(false);
                var sliders = GetComponentsInChildren<Slider>(false);
                var b = boolItems.Keys.ToList();
                var i = intItems.Keys.ToList();
                var f = floatItems.Keys.ToList();
                foreach (var item in b)
                {
                    boolItems[item] = Array.Find(toggles, t => t.name == item);
                }
                foreach (var item in i)
                {
                    intItems[item] = Array.Find(arrows, a => a.name == item);
                }
                foreach (var item in f)
                {
                    floatItems[item] = Array.Find(sliders, s => s.name == item);
                }
                
                tryLocateItems = false;
            }
            if (resetItems)
            {
                boolItems.Clear();
                intItems.Clear();
                floatItems.Clear();

                foreach (var b in settingsData.toggleItems)
                    boolItems.Add(b.Key, null);
                foreach (var i in settingsData.arrowItems)
                    intItems.Add(i.Key, null);
                foreach (var f in settingsData.sliderItems)
                    floatItems.Add(f.Key, null);
                
                resetItems = false;
            }
            if (renameItems)
            {
                foreach (var item in boolItems)
                {
                    item.Value.gameObject.name = item.Key;
                    item.Value.transform.parent.name = item.Key + " (Toggle)";
                }
                
                foreach (var item in intItems)
                {
                    item.Value.gameObject.name = item.Key;
                    item.Value.transform.parent.name = item.Key + " (Arrow)";
                }
                
                foreach (var item in floatItems)
                {
                    item.Value.gameObject.name = item.Key;
                    item.Value.transform.parent.name = item.Key + " (Slider)";
                }
                
                renameItems = false;
            }
            if (resetSO)
            {
                settingsData.toggleItems.Clear();
                settingsData.arrowItems.Clear();
                settingsData.sliderItems.Clear();
                
                foreach (var item in boolItems)
                    settingsData.toggleItems.Add(item.Key, false);
                foreach (var item in intItems)
                    settingsData.arrowItems.Add(item.Key, 0);
                foreach (var item in floatItems)
                    settingsData.sliderItems.Add(item.Key, 0);
                
                resetSO = false;
            }

            if (updateNavigation)
            {
                for(int i = 0; i < settingsTabs.Length; i++)
                {
                    var content = windows[i].transform.GetChild(0).GetChild(0);
                    for (int j = 1; j < content.childCount; j++)
                    {
                        var selectable = content.GetChild(j).GetComponentInChildren<Selectable>();
                        var sUp = content.GetChild(j - 1).GetComponentInChildren<Selectable>();
                        var sDown = j + 1 < content.childCount ? content.GetChild(j + 1).GetComponentInChildren<Selectable>() : saveButton;
                        Debug.Log($"New navigation for {content.GetChild(j).name} is: Up: {sUp.name} Down: {sDown.name}", selectable);
                        selectable.navigation = new Navigation
                        {
                            mode = Navigation.Mode.Explicit,
                            selectOnUp = sUp,
                            selectOnDown = sDown
                        };
                    }
                }
                updateNavigation = false;
            }

            if(!Application.isPlaying)
            {
                OnTabSelected(currentIndex);
            }
            
            base.OnValidate();
        }
#endif

        #endregion

        private void ScrollRectUpdate(RectTransform lookAt)
        {
            windows[currentIndex].TryGetComponent(out ScrollRect scrollRect);
            if (scrollRect.verticalScrollbar.gameObject.activeSelf)
                scrollRect.SnapToSelected(lookAt.parent as RectTransform);
        }

        private void ApplySettings()
        {
            try
            {
                PFXUpdate();

                LanguagePalette.Instance.currentLanguage = languages[settingsData.arrowItems["language"]];
                LanguagePalette.OnLanguageChanged?.Invoke();

                QualitySettings.vSyncCount = settingsData.toggleItems["vsync"] ? 1 : 0;
                QualitySettings.SetQualityLevel(settingsData.arrowItems["graphics"]);
                Screen.fullScreen = settingsData.toggleItems["fullscreen"];

                _renderPipeline = QualitySettings.GetRenderPipelineAssetAt(settingsData.arrowItems["graphics"]) as UniversalRenderPipelineAsset;
                if (_renderPipeline != null)
                {
                    _renderPipeline.renderScale = settingsData.sliderItems["renderScale"]/10;
                    renderScaleLabel.Localize("settings.renderScale");
                    renderScaleLabel.Suffix($" ({_renderPipeline.upscalingFilter.ToString()})");
                    //Debug.Log($"Render Scale: {_renderPipeline.renderScale}");
                }
#if !UNITY_EDITOR
                if (!Screen.fullScreen) return;
#endif
                if (settingsData.arrowItems["resolution"] < 0 || settingsData.arrowItems["resolution"] >= _supportedResolutions.Count)
                {
                    settingsData.arrowItems["resolution"] = GetScreenResolution();
                    intItems["resolution"].currentIndex = settingsData.arrowItems["resolution"];
                }
                Screen.SetResolution(_supportedResolutions[settingsData.arrowItems["resolution"]].width, _supportedResolutions[settingsData.arrowItems["resolution"]].height, settingsData.toggleItems["fullscreen"]);
                //Debug.Log($"Settings applied: {QualitySettings.GetQualityLevel()} {Screen.width}x{Screen.height} {Screen.fullScreen} - VSync: {QualitySettings.vSyncCount}");
            }
            catch (Exception e)
            {
                if(e is KeyNotFoundException keyException)
                {
                    Debug.LogWarning($"Key {keyException.Message} not found in settings data, adding to settings data", this);
                    var key = keyException.Message.Split('\'');
                    if(intItems.ContainsKey(key[1]))
                    {
                        settingsData.arrowItems.Add(key[1], intItems[key[1]].currentIndex);
                        Debug.LogWarning($"Key {key[1]} added to settings data", this);
                        Save();
                    }
                    else if(floatItems.ContainsKey(key[1]))
                    {
                        settingsData.sliderItems.Add(key[1], floatItems[key[1]].value);
                        Debug.LogWarning($"Key {key[1]} added to settings data", this);
                        Save();
                    }
                    else if(boolItems.ContainsKey(key[1]))
                    {
                        settingsData.toggleItems.Add(key[1], boolItems[key[1]].isOn);
                        Debug.LogWarning($"Key {key[1]} added to settings data", this);
                        Save();
                    }
                    else
                    {
                        Debug.LogError($"Key {key[1]} not found in settings items, maybe the key is wrong", this);
                    }
                }
            }
        }

        private void PFXUpdate()
        {
            if (!settingsData.toggleItems["pfx"])
            {
                boolItems["motionBlur"].isOn = false;
                boolItems["motionBlur"].interactable = false;
                boolItems["volumetricLight"].isOn = false;
                boolItems["volumetricLight"].interactable = false;
                settingsData.toggleItems["motionBlur"] = false;
                settingsData.toggleItems["volumetricLight"] = false;
            }
            else
            {
                boolItems["motionBlur"].interactable = true;
                boolItems["volumetricLight"].interactable = true;
            }
        }

        private void Load()
        {
            if (File.Exists(Path + "/game.config"))
            {
                Debug.Log("Loading settings...");
                settingsData.LoadFromJson(File.ReadAllText(Path + "/game.config"));
                SetItems();
            }else
            {
                Debug.Log("No settings file found, creating new one...");
                this.Save();
            }
        }

        public void Save()
        {
            if (!File.Exists(Path + "/game.config"))
            {
                Debug.Log("Creating new settings file...");
                File.Create(Path + "/game.config").Dispose();
                ResetToDefault();
            }
            Debug.Log("Saving settings...");
            File.WriteAllText(Path + "/game.config", JsonUtility.ToJson(settingsData, true));
            OnSettingsChanged?.Invoke();
            ApplySettings();
        }

        private void SetItems()
        {
            foreach (var t in settingsData.toggleItems)
                boolItems[t.Key].isOn = t.Value;
            foreach (var a in settingsData.arrowItems)
                intItems[a.Key].currentIndex = a.Value;
            foreach (var s in settingsData.sliderItems)
                floatItems[s.Key].value = (float)s.Value;

            ApplySettings();
            OnSettingsChanged?.Invoke();
        }

        private void OnTabSelected(int index)
        {
            Debug.Log($"Selected tab {index}");
            for (var i = 0; i < settingsTabs.Length; i++)
            {
                if (i == index)
                {
                    settingsTabs[i].SelectTab(selectedTab);
                }
                else
                {
                    settingsTabs[i].DeselectTab(unselectedTab);
                }

                var navigation = settingsTabs[i].button.navigation;
                navigation.selectOnDown = settingsTabs[index].firstObject.GetComponent<Selectable>();
                settingsTabs[i].button.navigation = navigation;
            }
            var saveNavigation = saveButton.navigation;
            var resetNavigation = resetButton.navigation;
            saveNavigation.selectOnUp = settingsTabs[index].lastObject;
            resetNavigation.selectOnUp = settingsTabs[index].lastObject;
            saveButton.navigation = saveNavigation;
            resetButton.navigation = resetNavigation;
        }
        
        public override void ChangeWindow(int index)
        {
            base.ChangeWindow(index);
            OnTabSelected(index);
        }

        public void ResetToDefault()
        {
            Debug.Log("Resetting settings to default...");

            //Video
            settingsData.arrowItems["resolution"] = GetScreenResolution();
            settingsData.sliderItems["renderScale"] = 10;
            settingsData.arrowItems["graphics"] = 1;
            settingsData.toggleItems["fullscreen"] = true;
            settingsData.toggleItems["vsync"] = true;
            settingsData.toggleItems["pfx"] = true;
            settingsData.toggleItems["motionBlur"] = true;
            settingsData.toggleItems["volumetricLight"] = false;
            
            //Gameplay
            settingsData.arrowItems["language"] = 0;
            settingsData.sliderItems["cameraSensitivity"] = 1;
            settingsData.toggleItems["invertXAxis"] = false;
            settingsData.toggleItems["invertYAxis"] = true;

#if !PLATFORM_STANDALONE_WIN
            settingsData.toggleItems["discord"] = false;
#else
            settingsData.toggleItems["discord"] = true;
#endif
            
            //Sound
            settingsData.sliderItems["masterVolume"] = 100;
            settingsData.sliderItems["musicVolume"] = 50;
            settingsData.sliderItems["characterVolume"] = 50;
            settingsData.sliderItems["sfxVolume"] = 50;
            settingsData.sliderItems["dialogVolume"] = 50;
            
            SetItems();
        }
        
        public int GetScreenResolution()
        {
            var display = UnityEngine.Device.Screen.mainWindowDisplayInfo;
            if(_supportedResolutions.Exists(r => r.width == display.width && r.height == display.height))
                return _supportedResolutions.FindIndex(r => r.width == display.width && r.height == display.height);
            else if(_supportedResolutions.Exists(r => r.width == display.width))
                return _supportedResolutions.FindIndex(r => r.width == display.width);
            else if(_supportedResolutions.Exists(r => r.height == display.height))
                return _supportedResolutions.FindIndex(r => r.height == display.height);
            else return _supportedResolutions.FindIndex(r => r is { width: 1920, height: 1080 });
        }

        public void DetectDisplay()
        {
            //Sort width first then height
            _supportedResolutions.Sort((r1, r2) => r1.width.CompareTo(r2.width) == 0 ? r1.height.CompareTo(r2.height) : r1.width.CompareTo(r2.width));
            Debug.Log($"Supported Resolutions: {string.Join(", ", _supportedResolutions.Select(r => $"{r.width}x{r.height}"))}");
            var display = UnityEngine.Device.Screen.mainWindowDisplayInfo;
            if (!_supportedResolutions.Exists(r => r.width == display.width && r.height == display.height))
            {
                //Add in list on order
                _supportedResolutions.Add(new Resolution {width = display.width, height = display.height});
                _supportedResolutions.Sort((r1, r2) => r1.width.CompareTo(r2.width) == 0 ? r1.height.CompareTo(r2.height) : r1.width.CompareTo(r2.width));

                Debug.Log($"Added new resolution: {display.width}x{display.height}");
                Debug.Log($"New Supported Resolutions: {string.Join(", ", _supportedResolutions.Select(r => $"{r.width}x{r.height}"))}");
            }
        }

        [Serializable]
        public struct SettingsTab
        {
            public Button button;
            public Image image;
            public TextMeshProUGUI text;
            public GameObject firstObject;
            public Selectable lastObject;
            
            public void SelectTab(Sprite spr)
            {
                image.sprite = spr;
                text.color = Color.black;
                text.fontSize = 45;
                button.interactable = false;
                if(Application.isPlaying)
                    EventSystem.current!.SetSelectedGameObject(firstObject);
            }
            
            public void DeselectTab(Sprite spr)
            {
                image.sprite = spr;
                text.color = Color.white;
                text.fontSize = 40;
                button.interactable = true;
            }
        }
    }
}