using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.DualShock;

namespace Solis.Interface.Input
{
    /// <summary>
    /// Manages input from keyboard and gamepad, including handling input actions and gamepad-specific features like rumble and light bar color.
    /// </summary>
    [RequireComponent(typeof(PlayerInput))]
    public class SolisInput : MonoBehaviour
    {
        /// <summary>
        /// Enum representing the type of input device.
        /// </summary>
        public enum InputType
        {
            Keyboard,
            Gamepad
        }

        [SerializeField] private PlayerInput _input;
        private Coroutine _rumbleCoroutine;

        /// <summary>
        /// Singleton instance of SolisInput.
        /// </summary>
        public static SolisInput Instance { get; private set; }

        /// <summary>
        /// Current input type (Keyboard or Gamepad).
        /// </summary>
        public static InputType CurrentInputType;

        /// <summary>
        /// Event triggered when an input action occurs.
        /// </summary>
        public static UnityAction<string, object> OnInputAction;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(this);
            }
            else Destroy(this);
        }

        private void OnEnable()
        {
            _input.ActivateInput();
            CurrentInputType = _input.currentControlScheme switch
            {
                "Keyboard" => InputType.Keyboard,
                "Gamepad" => InputType.Gamepad,
                _ => InputType.Keyboard
            };

            _input.onActionTriggered += OnActionTriggered;
            _input.onControlsChanged += OnControlsChanged;
        }

        private void OnDisable()
        {
            _input.DeactivateInput();
            _input.onActionTriggered -= OnActionTriggered;
        }

        /// <summary>
        /// Handles input action events and invokes the OnInputAction event.
        /// </summary>
        /// <param name="obj">The input action context.</param>
        private void OnActionTriggered(InputAction.CallbackContext obj)
        {
            OnInputAction?.Invoke(obj.action.name, obj.ReadValueAsObject());
        }

        /// <summary>
        /// Handles control scheme changes and updates the current input type.
        /// </summary>
        /// <param name="obj">The PlayerInput object.</param>
        private void OnControlsChanged(PlayerInput obj)
        {
            CurrentInputType = obj.currentControlScheme switch
            {
                "Keyboard" => InputType.Keyboard,
                "Gamepad" => InputType.Gamepad,
                _ => InputType.Keyboard
            };
            Debug.Log("Controls changed to: " + obj.currentControlScheme);
        }

        /// <summary>
        /// Gets the Vector2 value of the specified input action.
        /// </summary>
        /// <param name="actionName">The name of the input action.</param>
        /// <returns>The Vector2 value of the input action.</returns>
        public static Vector2 GetVector2(string actionName)
        {
            return Instance._input.actions[actionName].ReadValue<Vector2>();
        }

        /// <summary>
        /// Checks if the specified input action is currently pressed.
        /// </summary>
        /// <param name="actionName">The name of the input action.</param>
        /// <returns>True if the input action is pressed, false otherwise.</returns>
        public static bool GetKey(string actionName)
        {
            return Instance._input.actions[actionName].IsPressed();
        }

        /// <summary>
        /// Checks if the specified input action was pressed in the current frame.
        /// </summary>
        /// <param name="actionName">The name of the input action.</param>
        /// <returns>True if the input action was pressed in the current frame, false otherwise.</returns>
        public static bool GetKeyDown(string actionName)
        {
            return Instance._input.actions[actionName].WasPressedThisFrame();
        }

        /// <summary>
        /// Checks if the specified input action was released in the current frame.
        /// </summary>
        /// <param name="actionName">The name of the input action.</param>
        /// <returns>True if the input action was released in the current frame, false otherwise.</returns>
        public static bool GetKeyUp(string actionName)
        {
            return Instance._input.actions[actionName].WasReleasedThisFrame();
        }

        /// <summary>
        /// Sets the light bar color of a DualShock/DualSense gamepad.
        /// </summary>
        /// <param name="col">The color to set the light bar to.</param>
        public static void GamepadLight(Color col)
        {
            if (CurrentInputType == InputType.Gamepad)
            {
                var gamepad = (DualShockGamepad)Gamepad.all.ToList().Find(x => x is DualShockGamepad);
                if (gamepad == null)
                {
                    Debug.Log("No DualShock/DualSense Gamepad found, your gamepad is a: " + Gamepad.current.name +
                              " instead.");
                    return;
                }

                gamepad.SetLightBarColor(col);
            }
        }

        /// <summary>
        /// Initiates a rumble pulse on the gamepad.
        /// </summary>
        /// <param name="low">The low-frequency motor speed.</param>
        /// <param name="high">The high-frequency motor speed.</param>
        /// <param name="duration">The duration of the rumble pulse.</param>
        public void RumblePulse(float low, float high, float duration)
        {
            if (CurrentInputType == InputType.Gamepad)
            {
                var gamepad = Gamepad.current;
                if (gamepad == null) return;

                gamepad.SetMotorSpeeds(low, high);
                _rumbleCoroutine = StartCoroutine(StopRumble(gamepad, duration));
            }
        }

        /// <summary>
        /// Stops the rumble on the gamepad after a specified duration.
        /// </summary>
        /// <param name="gamepad">The gamepad to stop the rumble on.</param>
        /// <param name="duration">The duration to wait before stopping the rumble.</param>
        /// <returns>An IEnumerator for the coroutine.</returns>
        private IEnumerator StopRumble(Gamepad gamepad, float duration)
        {
            yield return new WaitForSeconds(duration);
            gamepad.SetMotorSpeeds(0, 0);
        }
    }
}