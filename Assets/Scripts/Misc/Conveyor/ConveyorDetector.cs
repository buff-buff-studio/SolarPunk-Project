using System;
using System.Collections.Generic;
using System.Linq;
using NetBuff.Misc;
using Solis.Circuit;
using Solis.Packets;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

namespace Solis.Misc.Conveyor
{
    public class ConveyorDetector : CircuitComponent
    {
        [Serializable]
        public class ObjectData
        {
            public ObjectType type;
            public Sprite sprite;
        }

        [Header("CIRCUIT")]
        public CircuitPlug inputReset;
        public CircuitPlug output;
        public CircuitPlug outputIsOn;
        public CircuitPlug outputError;

        [Header("HUD")]
        public GameObject startPage;
        public GameObject loadingPage;
        public GameObject finishPage;
        public GameObject errorPage;
        public Image filterImage;
        public Image[] loadedImages;
        public TextMeshProUGUI loadCountText;

        [Header("SETTINGS")]
        public List<ObjectData> filterList;
        public IntNetworkValue filterIndex = new IntNetworkValue(0);
        public BoolNetworkValue started = new BoolNetworkValue(false);
        public BoolNetworkValue finished = new BoolNetworkValue(false);
        public BoolNetworkValue error = new BoolNetworkValue(false);

        private IntNetworkValue _mode = new IntNetworkValue(0);
        private IntNetworkValue _loadCount = new IntNetworkValue(-1);

        private Color _unloadedColor = new Color(0.35f, 0.35f, 0.35f, 1);
        private Color _loadedColor = new Color(0.2f, .93f, 1, 1);

        protected override void OnEnable()
        {
            base.OnEnable();
            WithValues(filterIndex, finished, error, started, _loadCount, _mode);
            filterIndex.OnValueChanged += _OnFilterIndexChanged;
            _loadCount.OnValueChanged += _OnLoadCountChanged;
            _mode.OnValueChanged += _OnModeChanged;

            startPage.SetActive(true);
            loadingPage.SetActive(false);
            finishPage.SetActive(false);
            errorPage.SetActive(false);

            PacketListener.GetPacketListener<ShuffleConveyorPacket>().AddClientListener(RandomizeFilter);
            ShuffleFilter();
            if(HasAuthority)
                _loadCount.Value = 0;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            filterIndex.OnValueChanged -= _OnFilterIndexChanged;
        }


        private void _OnModeChanged(int oldvalue, int newvalue)
        {
            switch (newvalue)
            {
                case 0:
                    startPage.SetActive(true);
                    loadingPage.SetActive(false);
                    finishPage.SetActive(false);
                    errorPage.SetActive(false);
                    break;
                case 1:
                    startPage.SetActive(false);
                    loadingPage.SetActive(true);
                    finishPage.SetActive(false);
                    errorPage.SetActive(false);
                    break;
                case 2:
                    startPage.SetActive(false);
                    loadingPage.SetActive(false);
                    finishPage.SetActive(true);
                    errorPage.SetActive(false);
                    break;
                case 3:
                    startPage.SetActive(false);
                    loadingPage.SetActive(false);
                    finishPage.SetActive(false);
                    errorPage.SetActive(true);
                    break;
            }
        }

        private void _OnLoadCountChanged(int oldvalue, int newvalue)
        {
            if(_mode.Value != 1) return;
            loadCountText.text = $"{newvalue}/{loadedImages.Length}";
            for (int i = 0; i < loadedImages.Length; i++)
            {
                loadedImages[i].color = i < newvalue ? _loadedColor : _unloadedColor;
            }
        }
        private void _OnFilterIndexChanged(int oldvalue, int newvalue)
        {
            filterImage.sprite = filterList[newvalue].sprite;
        }

        private void OnTriggerEnter(Collider col)
        {
            if(!HasAuthority || finished.Value) return;

            if(col.TryGetComponent(out ConveyorObject conveyorObject))
            {
                if (filterList[filterIndex.Value].type.Equals(conveyorObject.objectType))
                {
                    if(filterIndex.Value < 2)
                    {
                        filterIndex.Value++;
                        _loadCount.Value++;
                    }
                    else
                    {
                        started.Value = false;
                        finished.Value = true;
                        _mode.Value = 2;
                        Refresh();
                    }
                }
                else
                {
                    error.Value = true;
                    _mode.Value = 3;
                    Refresh();
                }
            }
        }

        #region Shuffle

        public void ShuffleFilter()
        {
            if(!HasAuthority) return;

            var shuffle = Random.Range(12345, 543210).ToString("000000");
            RandomizeFilter(shuffle);
            SendPacket(new ShuffleConveyorPacket()
            {
                Id = this.Id,
                ShuffleValue = shuffle
            }, true);
        }

        private bool RandomizeFilter(ShuffleConveyorPacket arg)
        {
            if(HasAuthority) return false;

            RandomizeFilter(arg.ShuffleValue);
            return true;
        }

        public void RandomizeFilter(string shuffle)
        {
            var sChar = shuffle.ToCharArray().Select(c => (int)c).ToArray();
            for (int i = 0; i < filterList.Count; i++)
            {
                var newIndex = Mathf.Clamp(sChar[i], 0, filterList.Count - 1);
                (filterList[i], filterList[newIndex]) = (filterList[newIndex], filterList[i]);
            }
            filterIndex.Value = 0;
            if(_mode.Value != 1) return;
            if(filterImage.sprite != null)
                filterImage.sprite = filterList[0].sprite;
        }

        #endregion

        public override CircuitData ReadOutput(CircuitPlug plug)
        {
            if (plug == output)
            {
                return new CircuitData(finished.Value);
            }
            else if (plug == outputIsOn)
            {
                return new CircuitData(started.Value && !error.Value);
            }
            else
            {
                return new CircuitData(error.Value);
            }
        }

        public override IEnumerable<CircuitPlug> GetPlugs()
        {
            yield return output;
            yield return outputError;
            yield return inputReset;
            yield return outputIsOn;
        }

        protected override void OnRefresh()
        {
            if(finished.Value) return;

            if(error.Value)
            {
                if(inputReset.ReadOutput().IsPowered)
                {
                    error.Value = false;
                    _loadCount.Value = 0;
                    _mode.Value = 1;
                    ShuffleFilter();
                }
            }else if(!started.Value)
            {
                if(inputReset.ReadOutput().IsPowered)
                {
                    started.Value = true;
                    _loadCount.Value = 0;
                    _mode.Value = 1;
                    ShuffleFilter();
                }
            }
        }
    }
}