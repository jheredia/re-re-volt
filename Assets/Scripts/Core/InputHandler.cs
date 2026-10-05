using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Users;
using Vehicle;

namespace Core
{
    /// <summary>Reads only this player's explicitly paired device. No global-device fallback.</summary>
    [RequireComponent(typeof(VehicleController))]
    public sealed class InputHandler : MonoBehaviour
    {
        private VehicleController vehicle;
        private InputUser user;
        private RaceManager race;
        private RacerState racer;
        private int joinedFrame;
        private InputDevice lastConnectedDevice;

        public event Action FireRequested;
        public event Action PauseRequested;
        public event Action LeaveRequested;
        public event Action ConfirmRequested;
        public event Action BackRequested;
        public event Action<int> HandlingModelCycleRequested;
        public event Action<int> VehicleFamilyCycleRequested;
        public event Action VisualStyleCycleRequested;
        public bool GameplaySuspended { get; set; }
        public bool IsKeyboardPlayer { get; private set; }

        public InputDevice PairedDevice
        {
            get
            {
                if (!user.valid) return null;
                foreach (InputDevice device in user.pairedDevices)
                    if ((device is Gamepad || device is Keyboard) && device.added && device.enabled) return device;
                return null;
            }
        }

        public Gamepad PairedGamepad => PairedDevice as Gamepad;
        public bool IsConnected => PairedDevice != null;

        private void Awake()
        {
            vehicle = GetComponent<VehicleController>();
            vehicle.SetInputEnabled(false);
        }

        private void OnEnable() => InputSystem.onDeviceChange += OnDeviceChange;

        private void OnDeviceChange(InputDevice device, InputDeviceChange change)
        {
            if (device == lastConnectedDevice &&
                (change == InputDeviceChange.Removed || change == InputDeviceChange.Disabled))
            {
                lastConnectedDevice = null;
                vehicle.SetInputEnabled(false);
            }
        }

        public void Initialize(InputUser pairedUser, RaceManager raceManager, RacerState racerState)
        {
            if (!pairedUser.valid || pairedUser.pairedDevices.Count != 1 ||
                !(pairedUser.pairedDevices[0] is Gamepad || pairedUser.pairedDevices[0] is Keyboard) ||
                raceManager == null || racerState == null)
                throw new ArgumentException("Local input requires one paired Gamepad or Keyboard and a registered racer.");
            user = pairedUser;
            IsKeyboardPlayer = pairedUser.pairedDevices[0] is Keyboard;
            race = raceManager;
            racer = racerState;
            joinedFrame = Time.frameCount;
            lastConnectedDevice = null;
        }

        private void Update()
        {
            InputDevice device = PairedDevice;
            if (device != lastConnectedDevice)
            {
                // Reconnected devices may reuse controls with cached values from an old state buffer.
                // Refresh once on reconnect so a now-neutral trigger cannot retain its former value.
                if (device != null) device.ApplyParameterChanges();
                lastConnectedDevice = device;
            }
            bool driving = device != null && race != null && race.RaceRunning && !racer.IsFinished &&
                           !GameplaySuspended && Time.timeScale > 0f;
            vehicle.SetInputEnabled(driving);
            if (device == null || race == null || Time.frameCount == joinedFrame) return;
            Gamepad pad = device as Gamepad;
            Keyboard keyboard = device as Keyboard;
            bool back = keyboard != null ? keyboard.escapeKey.wasPressedThisFrame : pad.buttonEast.wasPressedThisFrame;
            bool confirm = keyboard != null ? keyboard.enterKey.wasPressedThisFrame : pad.buttonSouth.wasPressedThisFrame;
            bool visualStyle = keyboard != null
                ? keyboard.vKey.wasPressedThisFrame
                : pad.rightStickButton.wasPressedThisFrame;

            // The presentation authority applies the lobby/manual-pause gate. Input stays device-owned.
            if (visualStyle) VisualStyleCycleRequested?.Invoke();

            // Menu commands share the same ownership/events as driving. One press has one state-specific meaning.
            if (race.CurrentState == RaceManager.RaceState.Idle)
            {
                int familyDirection = GetVehicleFamilyCycleDirection(keyboard, pad);
                int modelDirection = GetHandlingModelCycleDirection(keyboard, pad);
                if (familyDirection != 0) VehicleFamilyCycleRequested?.Invoke(familyDirection);
                else if (modelDirection != 0) HandlingModelCycleRequested?.Invoke(modelDirection);
                else if (keyboard != null ? back : pad.selectButton.wasPressedThisFrame) LeaveRequested?.Invoke();
                else if (confirm) ConfirmRequested?.Invoke();
                return;
            }
            if (race.CurrentState == RaceManager.RaceState.Finished)
            {
                if (back) BackRequested?.Invoke();
                else if (confirm) ConfirmRequested?.Invoke();
                return;
            }

            // Pause remains available while gameplay is suspended or timeScale is zero.
            if (keyboard != null ? back : pad.startButton.wasPressedThisFrame)
            {
                PauseRequested?.Invoke();
                vehicle.SetInputEnabled(false);
                return;
            }
            if (!driving) return;

            if (keyboard != null)
            {
                vehicle.ThrottleInput = Axis(keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed,
                    keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed);
                vehicle.SteerInput = Axis(keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed,
                    keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed);
                vehicle.BrakeInput = keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed ? 1f : 0f;
                if (keyboard.spaceKey.wasPressedThisFrame) FireRequested?.Invoke();
                if (keyboard.rKey.wasPressedThisFrame) vehicle.ResetVehicle();
            }
            else
            {
                vehicle.ThrottleInput = pad.rightTrigger.ReadValue() - pad.leftTrigger.ReadValue();
                vehicle.SteerInput = pad.leftStick.x.ReadValue();
                vehicle.BrakeInput = pad.buttonSouth.isPressed ? 1f : 0f;
                if (pad.buttonWest.wasPressedThisFrame) FireRequested?.Invoke();
                if (pad.buttonNorth.wasPressedThisFrame) vehicle.ResetVehicle();
            }
        }

        private static float Axis(bool positive, bool negative) => (positive ? 1f : 0f) - (negative ? 1f : 0f);

        private static int GetHandlingModelCycleDirection(Keyboard keyboard, Gamepad pad)
        {
            bool previous = keyboard != null
                ? keyboard.leftArrowKey.wasPressedThisFrame || keyboard.qKey.wasPressedThisFrame
                : pad.dpad.left.wasPressedThisFrame || pad.leftShoulder.wasPressedThisFrame;
            bool next = keyboard != null
                ? keyboard.rightArrowKey.wasPressedThisFrame || keyboard.eKey.wasPressedThisFrame
                : pad.dpad.right.wasPressedThisFrame || pad.rightShoulder.wasPressedThisFrame;
            return Axis(next, previous) > 0f ? 1 : Axis(next, previous) < 0f ? -1 : 0;
        }

        private static int GetVehicleFamilyCycleDirection(Keyboard keyboard, Gamepad pad)
        {
            bool previous = keyboard != null
                ? keyboard.upArrowKey.wasPressedThisFrame || keyboard.wKey.wasPressedThisFrame
                : pad.dpad.up.wasPressedThisFrame;
            bool next = keyboard != null
                ? keyboard.downArrowKey.wasPressedThisFrame || keyboard.sKey.wasPressedThisFrame
                : pad.dpad.down.wasPressedThisFrame;
            return Axis(next, previous) > 0f ? 1 : Axis(next, previous) < 0f ? -1 : 0;
        }

        private void OnDisable()
        {
            InputSystem.onDeviceChange -= OnDeviceChange;
            lastConnectedDevice = null;
            if (vehicle != null) vehicle.SetInputEnabled(false);
        }
    }
}
