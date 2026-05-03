using UnityEngine;
using UnityEngine.InputSystem;
using Vehicle;

namespace Core
{
/// <summary>
/// Reads player input and writes normalized values onto VehicleController.
///
/// Two input paths, mutually exclusive:
///   A) New Input System callbacks  — used when a PlayerInput component is
///      present and a binding fires (gamepad or remapped keys).
///   B) Legacy keyboard polling     — fallback when no PlayerInput is found,
///      useful for quick in-editor testing without any Input System setup.
///
/// Path A takes full ownership once detected: the callback sets a flag that
/// suppresses the keyboard poll for the rest of that frame.
/// </summary>
[RequireComponent(typeof(Vehicle.VehicleController))]
    public class InputHandler : MonoBehaviour
    {
        private VehicleController vehicle;
        private PlayerInput playerInput;

        // Cached input values
        private float throttle;
        private float steer;
        private float brake;

        // Flag to detect if we've received input from the new Input System this frame
        private bool callbackReceivedThisFrame = false;

        void Awake()
        {
            TryGetComponent(out vehicle);
            if (vehicle == null)
            {
                Debug.LogError("InputHandler requires a VehicleController component on the same GameObject.");
                return;
            }

            TryGetComponent(out playerInput);
        }

        void Update()
        {
            if (!GameManager.Instance.IsRacing)
            {
                ClearInputs();
                return;
            }

            if (playerInput == null || !callbackReceivedThisFrame)
            {
                ReadKeyboardInput();
            }

            vehicle.ThrottleInput = throttle;
            vehicle.SteerInput = steer;
            vehicle.BrakeInput = brake;

            // Reset callback flag for the next frame
            callbackReceivedThisFrame = false;
        }

        // ── Keyboard Fallback (new Input System API) ───────────────────────────
        private void ReadKeyboardInput()
        {
            var kb = Keyboard.current;
            if (kb == null) return;
    
            // Throttle
            throttle = 0f;
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed)   throttle =  1f;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed) throttle = -1f;
    
            // Steer
            steer = 0f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) steer =  1f;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed)  steer = -1f;
    
            // Brake / Drift
            brake = kb.spaceKey.isPressed ? 1f : 0f;
    
            // Reset vehicle
            if (kb.rKey.wasPressedThisFrame)
                vehicle.ResetVehicle();
    
            // Pause
            if (kb.escapeKey.wasPressedThisFrame)
                TogglePause();
        }

        #region Input System callbacks
        // Input System callbacks (set up via PlayerInput component)
        public void OnThrottle(InputValue value)
        {
            throttle = value.Get<float>();
            callbackReceivedThisFrame = true;
        }

        public void OnSteer(InputValue value)
        {
            steer = value.Get<float>();
            callbackReceivedThisFrame = true;
        }

        public void OnBrake(InputValue value)
        {
            brake = value.Get<float>();
            callbackReceivedThisFrame = true;
        }

        public void OnFireWeapon(InputValue value)
        {
            if (value.isPressed)
            {
                // vehicle.FireWeapon();
            }
        }

        public void OnResetVehicle(InputValue value)
        {
            callbackReceivedThisFrame = true;
            if (value.isPressed)
            {
                vehicle.ResetVehicle();
            }
        }

        public void OnPause(InputValue value)
        {
            callbackReceivedThisFrame = true;
            if (value.isPressed)
            {
                TogglePause();
            }
        }
        # endregion
    
        

        # region Helpers
        // Helpers

        private void ClearInputs()
        {
            throttle = brake = steer = 0f;
            vehicle.ThrottleInput = vehicle.SteerInput = vehicle.BrakeInput = 0f;
        }

        private void TogglePause()
        {
            if (GameManager.Instance.IsPaused)
            {
                GameManager.Instance.ResumeGame();
            }
            else
            {
                GameManager.Instance.PauseGame();
            }
        }
        #endregion
    }   
}
