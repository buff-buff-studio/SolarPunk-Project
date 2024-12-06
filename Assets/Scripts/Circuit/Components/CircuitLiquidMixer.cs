using System;
using System.Collections;
using System.Collections.Generic;
using NetBuff.Misc;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using Random = UnityEngine.Random;

namespace Solis.Circuit.Components
{
    public class CircuitLiquidMixer : CircuitComponent
    {
        public enum Mode
        {
            Error = -2,
            Flushing = -1,
            Idle = 0,
            Filling = 1,
            Heating = 2,
            Mixing = 3,
            Complete = 4
        }

        public enum Error
        {
            None,
            LiquidAOverflow,
            LiquidBOverflow,
            FlushError,
            Overheating,
            CoolingError,
            MixingError,
            StartMixingError
        }

        [Serializable]
        public struct LiquidData
        {
            [Header("INFO")]
            public string name;
            public Color color;
            [Range(1, 10)]
            public float drainSpeed;
            public Vector2 maxAmountRange;
            [Range(0,100)]
            public FloatNetworkValue amount;

            [Header("UI")]
            public Image bar;
            [FormerlySerializedAs("maxAmountIndicator")] public RectTransform amountIndicator;

            [Header("REFERENCES")]
            public CircuitPlug input;

            public bool IsOpen => input.ReadOutput().power > 0;

            public float Amount
            {
                get => amount.Value;
                set => amount.Value = value;
            }
        }

        #region Inspector Fields
        [Header("STATE")]
        public IntNetworkValue mode;
        public Mode currentMode => (Mode) mode.Value;
        public IntNetworkValue error;
        public Error currentError => (Error) error.Value;

        public CircuitPlug startInput;
        public TextMeshProUGUI statusTitle;
        public TextMeshProUGUI statusDescription;
        public TextMeshProUGUI statusInfo;

        [Header("LIQUID")]
        public LiquidData liquidA;
        public LiquidData liquidB;
        public CircuitPlug flushInput;
        public CircuitPlug pipeOutput;
        [Range(1, 10)]
        public float flushSpeed = 1;

        private float _flushStartAmount;
        private bool flushIsOpen => flushInput.ReadOutput().power > 0;

        [Header("HEATING")]
        public CircuitPlug heatingInput;
        public CircuitPlug coolingInput;
        public float heatingSpeed = 1;
        public float heatingTime = 10;
        public Vector2 idealTemperatureRange = new(240, 260);
        private FloatNetworkValue _temperature = new(0);
        private FloatNetworkValue _heatingTimeCounter = new(0);

        /// <summary>
        /// 0 = off, 1 = heating, -1 = cooling
        /// </summary>
        private int _heatingStatus = 0;

        [Header("MIXING")]
        public CircuitPlug mixingInput;
        public float mixingTime = 10;
        public float mixingTimeExceeded = 12;
        private FloatNetworkValue _mixingTimeCounter = new(0);
        private BoolNetworkValue _startMixing = new(false);

        private bool HavePermission = false;

        #endregion

        protected override void OnEnable()
        {
            base.OnEnable();

            WithValues(mode, error, liquidA.amount, liquidB.amount, _temperature, _heatingTimeCounter, _mixingTimeCounter, _startMixing);

            HavePermission = mode.CheckPermission();
            mode.OnValueChanged += (value, newValue) => UpdateText();
            error.OnValueChanged += (value, newValue) => UpdateText();
            liquidA.amount.OnValueChanged += (value, newValue) => UpdateText();
            liquidB.amount.OnValueChanged += (value, newValue) => UpdateText();
            _temperature.OnValueChanged += (value, newValue) => UpdateText();
            _heatingTimeCounter.OnValueChanged += (value, newValue) => UpdateText();
            _mixingTimeCounter.OnValueChanged += (value, newValue) => UpdateText();
            _startMixing.OnValueChanged += (value, newValue) => UpdateText();

            if (HavePermission)
            {
                mode.Value = (int)Mode.Idle;
            }
            statusTitle.text = "Idle";
            statusDescription.text = "Waiting for input";
            statusInfo.text = "";

            SetLiquidIndicator(liquidA.amountIndicator, liquidA.maxAmountRange);
            SetLiquidIndicator(liquidB.amountIndicator, liquidB.maxAmountRange);
        }

        private void Update()
        {
            if (!HavePermission)
                return;

            switch (currentMode)
            {
                case Mode.Error:
                    break;

                case Mode.Flushing:
                    if (liquidA.Amount > 0 || liquidB.Amount > 0)
                    {
                        liquidA.Amount = Mathf.Max(liquidA.Amount - (Time.deltaTime * flushSpeed), 0);
                        liquidB.Amount = Mathf.Max(liquidB.Amount - (Time.deltaTime * flushSpeed), 0);
                    }
                    else if (!flushIsOpen)
                    {
                        mode.Value = (int)Mode.Idle;
                    }
                    break;

                case Mode.Idle:
                    liquidA.bar.fillAmount = liquidA.Amount / 100;
                    liquidB.bar.fillAmount = liquidB.Amount / 100;
                    break;

                case Mode.Filling:
                    if(liquidA.IsOpen)
                        liquidA.Amount = Mathf.Min(liquidA.Amount + (Time.deltaTime * liquidA.drainSpeed), 100);
                    if(liquidB.IsOpen)
                        liquidB.Amount = Mathf.Min(liquidB.Amount + (Time.deltaTime * liquidB.drainSpeed), 100);

                    if(liquidA.Amount > liquidA.maxAmountRange.y || liquidB.Amount > liquidB.maxAmountRange.y)
                    {
                        mode.Value = (int)Mode.Error;
                        error.Value = (int)(liquidA.Amount > liquidA.maxAmountRange.y
                            ? Error.LiquidAOverflow
                            : Error.LiquidBOverflow);
                    }
                    break;

                case Mode.Heating:
                    _temperature.Value += _heatingStatus * (heatingSpeed * Time.deltaTime);
                    _heatingTimeCounter.Value += Time.deltaTime;
                    if (_heatingTimeCounter.Value >= heatingTime)
                    {
                        if (_temperature.Value > idealTemperatureRange.y)
                        {
                            mode.Value = (int)Mode.Error;
                            error.Value = (int)Error.Overheating;
                        }
                        else if (_temperature.Value < idealTemperatureRange.x)
                        {
                            mode.Value = (int)Mode.Error;
                            error.Value = (int)Error.CoolingError;
                        }
                        else
                        {
                            mode.Value = (int)Mode.Mixing;
                        }
                    }
                    break;

                case Mode.Mixing:
                    if (_startMixing.Value)
                    {
                        _mixingTimeCounter.Value += Time.deltaTime;
                        if (_mixingTimeCounter.Value >= mixingTimeExceeded)
                        {
                            mode.Value = (int)Mode.Error;
                            error.Value = (int)Error.MixingError;
                            _startMixing.Value = false;
                            _mixingTimeCounter.Value = 0;
                        }
                    }
                    break;

                case Mode.Complete:
                    break;

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            mixingTimeExceeded = Mathf.Max(mixingTimeExceeded, mixingTime);
        }
#endif

        private void UpdateText()
        {
            switch (currentMode)
            {
                case Mode.Error:
                    statusTitle.text = "Error";
                    statusInfo.text = "";
                    statusDescription.text = currentError switch
                    {
                        Error.LiquidAOverflow => "Liquid A overflowed, close the valve A and B and flush the system",
                        Error.LiquidBOverflow => "Liquid B overflowed, close the valve A and B and flush the system",
                        Error.FlushError => "Flush process has been interrupted, close the valve A and B and flush the system again",
                        Error.Overheating => "The system overheated, flush the system and try again",
                        Error.CoolingError => "The system is too cold, flush the system and try again",
                        Error.MixingError => "The mixing process has been interrupted or took too long, flush the system and try again",
                        Error.StartMixingError => "The system is not ready to start the process, close the valves and try again",
                        _ => "Unknown error"
                    };
                    break;

                case Mode.Flushing:
                    if (liquidA.Amount > 0 || liquidB.Amount > 0)
                    {
                        statusTitle.text = "Flushing";
                        var flushAmount = ((liquidA.Amount + liquidB.Amount) / 2);
                        statusDescription.text = $"{(int)((flushAmount / _flushStartAmount) * 100)}%";
                        statusInfo.text = $"{liquidA.Amount:F1}% - {liquidB.Amount:F1}%";

                        liquidA.bar.fillAmount = liquidA.Amount / 100;
                        liquidB.bar.fillAmount = liquidB.Amount / 100;
                    }
                    break;

                case Mode.Idle:
                    statusTitle.text = "Idle";
                    statusDescription.text = "";
                    statusInfo.text = "Press the start button to begin";

                    liquidA.bar.fillAmount = liquidA.Amount / 100;
                    liquidB.bar.fillAmount = liquidB.Amount / 100;
                    break;

                case Mode.Filling:
                    statusTitle.text = "Filling";
                    statusDescription.text = "The liquid must be in the ideal quantity";
                    statusInfo.text = $"{liquidA.Amount:F1}% - {liquidB.Amount:F1}%";
                    liquidA.bar.fillAmount = liquidA.Amount / 100;
                    liquidB.bar.fillAmount = liquidB.Amount / 100;

                    break;
                case Mode.Heating:
                    var color = _temperature.Value > idealTemperatureRange.y ? Color.red : _temperature.Value < idealTemperatureRange.x ? Color.blue : Color.green;

                    statusTitle.text = "Heating";
                    statusDescription.text = $"{Mathf.Floor(Mathf.Abs(_heatingTimeCounter.Value-heatingTime))}s";
                    statusInfo.text = $"<color=#{ColorUtility.ToHtmlStringRGB(color)}>{Mathf.Floor(_temperature.Value)}°C</color>";


                    break;

                case Mode.Mixing:
                    statusTitle.text = "Mixing";
                    if (_startMixing.Value)
                    {
                        statusDescription.text = $"Don't stop mixing for {mixingTime}s";
                        statusInfo.text = $"{(int)_mixingTimeCounter.Value}s";
                    }
                    else
                    {
                        statusDescription.text = "Waiting for start mixing";
                        statusInfo.text = "";
                    }
                    break;

                case Mode.Complete:
                    statusTitle.text = "Complete";
                    statusDescription.text = "Liquid is released";
                    statusInfo.text = "";
                    break;

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        void SetLiquidIndicator(RectTransform indicator, Vector2 maxAmountRange)
        {
            indicator.offsetMin = new Vector2(indicator.offsetMin.x, maxAmountRange.x / 100);
            indicator.offsetMax = new Vector2(indicator.offsetMax.x, -(1 - maxAmountRange.y / 100));
        }

        private bool IsBetween(float value, float min, float max)
        {
            return value >= min && value <= max;
        }

        #region Abstract Methods Implementation
        public override CircuitData ReadOutput(CircuitPlug plug)
        {
            return new CircuitData(currentMode == Mode.Complete);
        }

        protected override void OnRefresh()
        {
            if(!HavePermission)
                return;

            switch (currentMode)
            {
                case Mode.Error:
                    if(!liquidA.IsOpen && !liquidB.IsOpen && flushIsOpen)
                    {
                        mode.Value = (int)Mode.Flushing;
                        _flushStartAmount = (liquidA.Amount + liquidB.Amount) / 2;
                    }
                    break;
                case Mode.Flushing:
                    if (liquidA.IsOpen || liquidB.IsOpen)
                    {
                        mode.Value = (int)Mode.Error;
                        error.Value = (int)Error.FlushError;
                    }else if(!flushIsOpen)
                    {
                        if (liquidA.Amount <= 0 && liquidB.Amount <= 0)
                            mode.Value = (int)Mode.Idle;
                    }
                    break;
                case Mode.Idle:
                    if(startInput.ReadOutput().power > 0)
                    {
                        mode.Value = (int)Mode.Filling;
                    }
                    else if (flushIsOpen)
                    {
                        mode.Value = (int)Mode.Flushing;
                        _flushStartAmount = (liquidA.Amount + liquidB.Amount) / 2;
                    }
                    else if(liquidA.IsOpen || liquidB.IsOpen)
                    {
                        mode.Value = (int)Mode.Error;
                        error.Value = (int)Error.StartMixingError;
                    }else if(mixingInput.ReadOutput().power > 0)
                    {
                        mode.Value = (int)Mode.Error;
                        error.Value = (int)Error.StartMixingError;
                    }
                    break;
                case Mode.Filling:
                    if (flushIsOpen)
                    {
                        mode.Value = (int)Mode.Flushing;
                        _flushStartAmount = (liquidA.Amount + liquidB.Amount) / 2;
                    }
                    else if (IsBetween(liquidA.Amount, liquidA.maxAmountRange.x, liquidA.maxAmountRange.y) &&
                             IsBetween(liquidB.Amount, liquidB.maxAmountRange.x, liquidB.maxAmountRange.y))
                    {
                        mode.Value = (int)Mode.Heating;
                        var temp = 0f;
                        while(true)
                        {
                            temp = Random.Range(idealTemperatureRange.x / 1.5f, idealTemperatureRange.y * 1.5f);
                            if(!IsBetween(temp, idealTemperatureRange.x-30, idealTemperatureRange.y+30))
                                break;
                        }
                        _temperature.Value = temp;
                    }
                    else if(liquidA.Amount > liquidA.maxAmountRange.y || liquidB.Amount > liquidB.maxAmountRange.y)
                    {
                        mode.Value = (int)Mode.Error;
                        error.Value = (int)(liquidA.Amount > liquidA.maxAmountRange.y
                            ? Error.LiquidAOverflow
                            : Error.LiquidBOverflow);
                    }
                    break;
                case Mode.Heating:
                    if (flushIsOpen)
                    {
                        mode.Value = (int)Mode.Flushing;
                        _flushStartAmount = (liquidA.Amount + liquidB.Amount) / 2;
                    }
                    else if (liquidA.IsOpen || liquidB.IsOpen)
                    {
                        mode.Value = (int)Mode.Error;
                        error.Value = (int)(liquidA.IsOpen ? Error.LiquidAOverflow : Error.LiquidBOverflow);
                    }
                    var cooling = coolingInput.ReadOutput().power > 0;
                    var heating = heatingInput.ReadOutput().power > 0;
                    //if cooling and heating are on = 0, if cooling is on = -1, if heating is on = 1, if both are off = 0
                    _heatingStatus = cooling && heating ? 0 : heating ? 1 : cooling ? -1 : 0;
                    break;
                case Mode.Mixing:
                    if (flushIsOpen)
                    {
                        mode.Value = (int)Mode.Flushing;
                        _flushStartAmount = (liquidA.Amount + liquidB.Amount) / 2;
                    }
                    else if (liquidA.IsOpen || liquidB.IsOpen)
                    {
                        mode.Value = (int)Mode.Error;
                        error.Value = (int)(liquidA.IsOpen ? Error.LiquidAOverflow : Error.LiquidBOverflow);
                    }

                    if (mixingInput.ReadOutput().power > 0)
                    {
                        _startMixing.Value = true;
                        _mixingTimeCounter.Value = 0;
                    }
                    else if (_startMixing.Value)
                    {
                        if (IsBetween(_mixingTimeCounter.Value, mixingTime, mixingTimeExceeded))
                        {
                            mode.Value = (int)Mode.Complete;
                            Debug.Log("Mixing complete");
                        }
                        else
                        {
                            Debug.Log("Mixing error");
                            mode.Value = (int)Mode.Error;
                            error.Value = (int)Error.MixingError;
                        }
                    }
                    break;
                case Mode.Complete:
                    if(flushIsOpen)
                    {
                        mode.Value = (int)Mode.Flushing;
                        _flushStartAmount = (liquidA.Amount + liquidB.Amount) / 2;
                    }
                    else if (liquidA.IsOpen || liquidB.IsOpen)
                    {
                        mode.Value = (int)Mode.Error;
                        error.Value = (int)(liquidA.IsOpen ? Error.LiquidAOverflow : Error.LiquidBOverflow);
                    }
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public override IEnumerable<CircuitPlug> GetPlugs()
        {
            yield return startInput;
            yield return liquidA.input;
            yield return liquidB.input;
            yield return flushInput;
            yield return pipeOutput;
            yield return heatingInput;
            yield return coolingInput;
            yield return mixingInput;
        }
        #endregion
    }
}