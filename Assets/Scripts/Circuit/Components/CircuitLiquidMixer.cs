using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

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
            public string name;
            public Color color;
            public Image bar;
            public CircuitPlug input;
            [Range(1, 10)]
            public float drainSpeed;
            [Range(0,100)]
            public float amount;
            public Vector2 maxAmountRange;

            public bool IsOpen => input.ReadOutput().power > 0;
        }

        #region Inspector Fields
        [Header("STATE")]
        public Mode mode = Mode.Filling;
        public Error error = Error.None;

        public CircuitPlug startInput;
        public TextMeshProUGUI statusTitle;
        public TextMeshProUGUI statusDescription;

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
        private float _temperature = 0;
        private float _heatingTimeCounter = 0;

        /// <summary>
        /// 0 = off, 1 = heating, -1 = cooling
        /// </summary>
        private int _heatingStatus = 0;

        [Header("MIXING")]
        public CircuitPlug mixingInput;
        public float mixingTime = 10;
        public float mixingTimeExceeded = 12;
        private float _mixingTimeCounter = 0;
        private bool _startMixing = false;

        #endregion

        private void Update()
        {
            if (!HasAuthority)
                return;

            switch (mode)
            {
                case Mode.Error:
                    statusTitle.text = "Error";
                    statusDescription.text = error switch
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
                    if (liquidA.amount > 0 || liquidB.amount > 0)
                    {
                        liquidA.amount = Mathf.Max(liquidA.amount - (Time.deltaTime * flushSpeed), 0);
                        liquidB.amount = Mathf.Max(liquidB.amount - (Time.deltaTime * flushSpeed), 0);

                        statusTitle.text = "Flushing";
                        var flushAmount = ((liquidA.amount + liquidB.amount) / 2);
                        statusDescription.text = $"{(int)((flushAmount / _flushStartAmount) * 100)}%";
                        liquidA.bar.fillAmount = liquidA.amount / 100;
                        liquidB.bar.fillAmount = liquidB.amount / 100;
                    }
                    else
                    {
                        statusTitle.text = "Flush Complete";
                        statusDescription.text = "Close the flush valve";
                    }
                    break;
                case Mode.Idle:
                    statusTitle.text = "Idle";
                    statusDescription.text = "Waiting for input";

                    liquidA.bar.fillAmount = liquidA.amount / 100;
                    liquidB.bar.fillAmount = liquidB.amount / 100;
                    break;
                case Mode.Filling:
                    if(liquidA.IsOpen)
                        liquidA.amount = Mathf.Min(liquidA.amount + (Time.deltaTime * liquidA.drainSpeed), 100);
                    if(liquidB.IsOpen)
                        liquidB.amount = Mathf.Min(liquidB.amount + (Time.deltaTime * liquidB.drainSpeed), 100);

                    statusTitle.text = "Filling";
                    statusDescription.text = $"{(int)liquidA.amount}% - {(int)liquidB.amount}%";
                    liquidA.bar.fillAmount = liquidA.amount / 100;
                    liquidB.bar.fillAmount = liquidB.amount / 100;

                    if(liquidA.amount > liquidA.maxAmountRange.y || liquidB.amount > liquidB.maxAmountRange.y)
                    {
                        mode = Mode.Error;
                        error = liquidA.amount > liquidA.maxAmountRange.y ? Error.LiquidAOverflow : Error.LiquidBOverflow;
                    }
                    break;
                case Mode.Heating:
                    _temperature += _heatingStatus * (heatingSpeed * Time.deltaTime);
                    _heatingTimeCounter += Time.deltaTime;

                    statusTitle.text = "Heating";
                    statusDescription.text = $"{(int)_temperature}°C - {(int)_heatingTimeCounter-heatingTime}s";

                    if (_heatingTimeCounter >= heatingTime)
                    {
                        if (_temperature > idealTemperatureRange.y)
                        {
                            mode = Mode.Error;
                            error = Error.Overheating;
                        }
                        else if (_temperature < idealTemperatureRange.x)
                        {
                            mode = Mode.Error;
                            error = Error.CoolingError;
                        }
                        else
                        {
                            mode = Mode.Mixing;
                        }
                    }

                    break;
                case Mode.Mixing:
                    statusTitle.text = "Mixing";
                    if (_startMixing)
                    {
                        _mixingTimeCounter += Time.deltaTime;
                        statusDescription.text = $"{(int)_mixingTimeCounter}s";
                        if (_mixingTimeCounter >= mixingTimeExceeded)
                        {
                            mode = Mode.Error;
                            error = Error.MixingError;
                            _startMixing = false;
                            _mixingTimeCounter = 0;
                        }
                    }
                    else
                    {
                        statusDescription.text = "Waiting for start mixing";
                    }
                    break;
                case Mode.Complete:
                    statusTitle.text = "Complete";
                    statusDescription.text = "Liquid is released";
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

        private bool IsBetween(float value, float min, float max)
        {
            return value >= min && value <= max;
        }

        #region Abstract Methods Implementation
        public override CircuitData ReadOutput(CircuitPlug plug)
        {
            return new CircuitData(mode == Mode.Complete);
        }

        protected override void OnRefresh()
        {
            switch (mode)
            {
                case Mode.Error:
                    if(!liquidA.IsOpen && !liquidB.IsOpen && flushIsOpen)
                    {
                        mode = Mode.Flushing;
                        _flushStartAmount = (liquidA.amount + liquidB.amount) / 2;
                    }
                    break;
                case Mode.Flushing:
                    if (liquidA.IsOpen || liquidB.IsOpen)
                    {
                        mode = Mode.Error;
                        error = Error.FlushError;
                    }else if(!flushIsOpen)
                    {
                        if (liquidA.amount <= 0 && liquidB.amount <= 0)
                            mode = Mode.Idle;
                        else
                        {
                            mode = Mode.Error;
                            error = Error.FlushError;
                        }
                    }
                    break;
                case Mode.Idle:
                    if(startInput.ReadOutput().power > 0)
                    {
                        mode = Mode.Filling;
                    }
                    else if (flushIsOpen)
                    {
                        mode = Mode.Flushing;
                        _flushStartAmount = (liquidA.amount + liquidB.amount) / 2;
                    }
                    else if(liquidA.IsOpen || liquidB.IsOpen)
                    {
                        mode = Mode.Error;
                        error = Error.StartMixingError;
                    }else if(mixingInput.ReadOutput().power > 0)
                    {
                        mode = Mode.Error;
                        error = Error.StartMixingError;
                    }
                    break;
                case Mode.Filling:
                    if (flushIsOpen)
                    {
                        mode = Mode.Flushing;
                        _flushStartAmount = (liquidA.amount + liquidB.amount) / 2;
                    }
                    else if (IsBetween(liquidA.amount, liquidA.maxAmountRange.x, liquidA.maxAmountRange.y) &&
                             IsBetween(liquidB.amount, liquidB.maxAmountRange.x, liquidB.maxAmountRange.y))
                    {
                        mode = Mode.Heating;
                        _temperature = 30;
                    }
                    else if(liquidA.amount > liquidA.maxAmountRange.y || liquidB.amount > liquidB.maxAmountRange.y)
                    {
                        mode = Mode.Error;
                        error = liquidA.amount > liquidA.maxAmountRange.y ? Error.LiquidAOverflow : Error.LiquidBOverflow;
                    }
                    break;
                case Mode.Heating:
                    if (flushIsOpen)
                    {
                        mode = Mode.Flushing;
                        _flushStartAmount = (liquidA.amount + liquidB.amount) / 2;
                    }
                    else if (liquidA.IsOpen || liquidB.IsOpen)
                    {
                        mode = Mode.Error;
                        error = liquidA.IsOpen ? Error.LiquidAOverflow : Error.LiquidBOverflow;
                    }
                    var cooling = coolingInput.ReadOutput().power > 0;
                    var heating = heatingInput.ReadOutput().power > 0;
                    //if cooling and heating are on = 0, if cooling is on = -1, if heating is on = 1, if both are off = 0
                    _heatingStatus = cooling && heating ? 0 : heating ? 1 : cooling ? -1 : 0;
                    break;
                case Mode.Mixing:
                    if (flushIsOpen)
                    {
                        mode = Mode.Flushing;
                        _flushStartAmount = (liquidA.amount + liquidB.amount) / 2;
                    }
                    else if (liquidA.IsOpen || liquidB.IsOpen)
                    {
                        mode = Mode.Error;
                        error = liquidA.IsOpen ? Error.LiquidAOverflow : Error.LiquidBOverflow;
                    }

                    if (mixingInput.ReadOutput().power > 0)
                    {
                        _startMixing = true;
                        _mixingTimeCounter = 0;
                    }
                    else if (_startMixing)
                    {
                        if (IsBetween(_mixingTimeCounter, mixingTime, mixingTimeExceeded))
                        {
                            mode = Mode.Complete;
                            Debug.Log("Mixing complete");
                        }
                        else
                        {
                            Debug.Log("Mixing error");
                            mode = Mode.Error;
                            error = Error.MixingError;
                        }
                    }
                    break;
                case Mode.Complete:
                    if(flushIsOpen)
                    {
                        mode = Mode.Flushing;
                        _flushStartAmount = (liquidA.amount + liquidB.amount) / 2;
                    }
                    else if (liquidA.IsOpen || liquidB.IsOpen)
                    {
                        mode = Mode.Error;
                        error = liquidA.IsOpen ? Error.LiquidAOverflow : Error.LiquidBOverflow;
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