using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Users;
using Vehicle;

namespace Core
{
    /// <summary>Reads only this player's explicitly paired Gamepad. No global-device fallback.</summary>
    [RequireComponent(typeof(VehicleController))]
    public sealed class InputHandler : MonoBehaviour
    {
        private VehicleController vehicle;
        private InputUser user;
        private RaceManager race;
        private RacerState racer;
        private int joinedFrame;
        private Gamepad lastConnectedPad;

        public event Action FireRequested;
        public event Action PauseRequested;
        public event Action LeaveRequested;
        public bool GameplaySuspended { get; set; }

        public Gamepad PairedGamepad
        {
            get
            {
                if (!user.valid) return null;
                foreach (InputDevice device in user.pairedDevices)
                    if (device is Gamepad pad && pad.added && pad.enabled) return pad;
                return null;
            }
        }

        public bool IsConnected => PairedGamepad != null;

        private void Awake()
        {
            vehicle = GetComponent<VehicleController>();
            vehicle.SetInputEnabled(false);
        }

        private void OnEnable() => InputSystem.onDeviceChange += OnDeviceChange;

        private void OnDeviceChange(InputDevice device, InputDeviceChange change)
        {
            if (device == lastConnectedPad &&
                (change == InputDeviceChange.Removed || change == InputDeviceChange.Disabled))
            {
                lastConnectedPad = null;
                vehicle.SetInputEnabled(false);
            }
        }

        public void Initialize(InputUser pairedUser, RaceManager raceManager, RacerState racerState)
        {
            if (!pairedUser.valid || pairedUser.pairedDevices.Count != 1 ||
                !(pairedUser.pairedDevices[0] is Gamepad) || raceManager == null || racerState == null)
                throw new ArgumentException("Local input requires one paired Gamepad and a registered racer.");
            user = pairedUser;
            race = raceManager;
            racer = racerState;
            joinedFrame = Time.frameCount;
            lastConnectedPad = null;
        }

        private void Update()
        {
            Gamepad pad = PairedGamepad;
            if (pad != lastConnectedPad)
            {
                // Reconnected devices may reuse controls with cached values from an old state buffer.
                // Refresh once on reconnect so a now-neutral trigger cannot retain its former value.
                if (pad != null) pad.ApplyParameterChanges();
                lastConnectedPad = pad;
            }
            bool driving = pad != null && race != null && race.RaceRunning && !racer.IsFinished &&
                           !GameplaySuspended && Time.timeScale > 0f;
            vehicle.SetInputEnabled(driving);
            if (pad == null || race == null || Time.frameCount == joinedFrame) return;

            // Pause remains available while gameplay is suspended or timeScale is zero.
            if (pad.startButton.wasPressedThisFrame)
            {
                PauseRequested?.Invoke();
                vehicle.SetInputEnabled(false);
                return;
            }
            if (race.CurrentState == RaceManager.RaceState.Idle && pad.selectButton.wasPressedThisFrame)
            {
                LeaveRequested?.Invoke();
                return;
            }
            if (!driving) return;

            vehicle.ThrottleInput = pad.rightTrigger.ReadValue() - pad.leftTrigger.ReadValue();
            vehicle.SteerInput = pad.leftStick.x.ReadValue();
            vehicle.BrakeInput = pad.buttonSouth.isPressed ? 1f : 0f;
            if (pad.buttonWest.wasPressedThisFrame) FireRequested?.Invoke();
            if (pad.buttonNorth.wasPressedThisFrame) vehicle.ResetVehicle();
        }

        private void OnDisable()
        {
            InputSystem.onDeviceChange -= OnDeviceChange;
            lastConnectedPad = null;
            if (vehicle != null) vehicle.SetInputEnabled(false);
        }
    }
}
