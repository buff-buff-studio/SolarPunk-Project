using System;
using System.Collections.Generic;
using System.Linq;
using NetBuff.Misc;
using Solis.Circuit;
using Solis.Packets;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

namespace Solis.Misc.Conveyor
{
    public class ConveyorDetector : CircuitComponent
    {
        [Serializable]
        public struct ObjectData
        {
            public ObjectType type;
            public Sprite sprite;
        }

        public CircuitPlug inputReset;
        public CircuitPlug output;
        public CircuitPlug outputIsOn;
        public CircuitPlug outputError;

        public Image filterImage;

        public List<ObjectData> filterList;
        public IntNetworkValue filterIndex = new IntNetworkValue(0);
        public BoolNetworkValue started = new BoolNetworkValue(false);
        public BoolNetworkValue finished = new BoolNetworkValue(false);
        public BoolNetworkValue error = new BoolNetworkValue(false);

        protected override void OnEnable()
        {
            base.OnEnable();
            WithValues(filterIndex, finished, error, started);
            filterIndex.OnValueChanged += _OnFilterIndexChanged;

            PacketListener.GetPacketListener<ShuffleConveyorPacket>().AddClientListener(RandomizeFilter);
            ShuffleFilter();
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            filterIndex.OnValueChanged -= _OnFilterIndexChanged;
        }

        private void _OnFilterIndexChanged(int oldvalue, int newvalue)
        {
            filterImage.sprite = filterList[newvalue].sprite;
        }

        private void OnTriggerEnter(Collider col)
        {
            if(!HasAuthority && finished.Value) return;

            if(col.TryGetComponent(out ConveyorObject conveyorObject))
            {
                if (filterList[filterIndex.Value].type.Equals(conveyorObject.objectType))
                {
                    if(filterIndex.Value < filterList.Count - 1)
                    {
                        filterIndex.Value++;
                    }
                    else
                    {
                        finished.Value = true;
                        Refresh();
                    }
                }
                else
                {
                    error.Value = true;
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
            });
        }

        private bool RandomizeFilter(ShuffleConveyorPacket arg)
        {
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
            if(error.Value)
            {
                if(inputReset.ReadOutput().IsPowered)
                {
                    error.Value = false;
                    ShuffleFilter();
                }
            }else if(!started.Value)
            {
                if(inputReset.ReadOutput().IsPowered)
                {
                    started.Value = true;
                }
            }
        }
    }
}