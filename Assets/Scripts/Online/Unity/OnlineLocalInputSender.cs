using System;
using System.Collections.Generic;
using ReReVolt.Online.Contracts;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Users;

namespace ReReVolt.Online.Unity
{
    /// <summary>
    /// Owns this peer's physical devices for one locked round. It only emits intent;
    /// the host validates ownership, applies controls, and runs all vehicle physics.
    /// </summary>
    public sealed class OnlineLocalInputSender : MonoBehaviour
    {
        private sealed class Binding
        {
            public uint RacerId;
            public InputUser User;
            public Keyboard Keyboard;
            public Gamepad Gamepad;
            public InputEdges PendingEdges;
        }

        private const double SendInterval = 1.0 / 30.0;
        private readonly List<Binding> bindings = new List<Binding>(4);
        private OnlineLobbyProtocol protocol;
        private OnlineRaceMessageRouting routing;
        private PeerInputFrame frame;
        private ulong sequence;
        private double nextSendAt;
        private bool controlsSuspended;

        public int LocalRacerCount => bindings.Count;

        /// <summary>Local menus send neutral input without pausing the host or another peer.</summary>
        public void SetControlsSuspended(bool suspended)
        {
            controlsSuspended = suspended;
            if (suspended)
                foreach (Binding binding in bindings) binding.PendingEdges = InputEdges.None;
        }

        /// <summary>
        /// The maximum number of independent local racers that can be bound now.
        /// A solo racer can use keyboard or gamepad; each additional racer needs
        /// another unpaired device. This is advisory until round lock, where
        /// Initialize performs the same check and owns the actual pairing.
        /// </summary>
        public static int AvailableRacerCapacity()
        {
            int count = 0;
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.added && keyboard.enabled &&
                !InputUser.FindUserPairedToDevice(keyboard).HasValue)
                count++;
            foreach (Gamepad pad in Gamepad.all)
                if (pad.added && pad.enabled && !InputUser.FindUserPairedToDevice(pad).HasValue)
                    count++;
            return Mathf.Min(4, count);
        }

        public void Initialize(LobbySnapshot lockedRoster, string localPeer, OnlineLobbyProtocol lobby,
            OnlineRaceMessageRouting messageRouting)
        {
            if (frame != null) throw new InvalidOperationException("The local input sender already owns a round.");
            if (lockedRoster == null || lockedRoster.Racers == null || lockedRoster.Round == 0 ||
                string.IsNullOrWhiteSpace(localPeer) || lobby == null || messageRouting == null)
                throw new ArgumentException("A locked roster, local peer, protocol, and race router are required.");

            var owned = new List<LobbyRacer>(4);
            foreach (LobbyRacer racer in lockedRoster.Racers)
                if (racer != null && !racer.Bot && string.Equals(racer.Peer, localPeer, StringComparison.Ordinal))
                    owned.Add(racer);
            owned.Sort((a, b) => a.Slot.CompareTo(b.Slot));
            if (owned.Count == 0 || owned.Count > 4)
                throw new InvalidOperationException("The peer must own one to four human racers.");

            Keyboard keyboard = Keyboard.current;
            bool keyboardAvailable = keyboard != null && keyboard.added && keyboard.enabled &&
                !InputUser.FindUserPairedToDevice(keyboard).HasValue;
            var pads = new List<Gamepad>(4);
            foreach (Gamepad pad in Gamepad.all)
                if (pad.added && pad.enabled && !InputUser.FindUserPairedToDevice(pad).HasValue)
                    pads.Add(pad);
            int requiredPads = owned.Count - (keyboardAvailable ? 1 : 0);
            if (pads.Count < requiredPads)
                throw new InvalidOperationException("Not enough unpaired local controllers for this peer's racers.");

            try
            {
                for (int i = 0; i < owned.Count; i++)
                {
                    Keyboard assignedKeyboard = i == 0 && keyboardAvailable ? keyboard : null;
                    Gamepad assignedPad = assignedKeyboard == null ? pads[i - (keyboardAvailable ? 1 : 0)] :
                        owned.Count == 1 && pads.Count > 0 ? pads[0] : null;
                    InputDevice primary = assignedKeyboard != null ? (InputDevice)assignedKeyboard : assignedPad;
                    InputUser user = InputUser.PerformPairingWithDevice(primary);
                    var binding = new Binding { RacerId = owned[i].Id, User = user,
                        Keyboard = assignedKeyboard, Gamepad = assignedPad };
                    bindings.Add(binding);
                    if (assignedKeyboard != null && assignedPad != null)
                        InputUser.PerformPairingWithDevice(assignedPad, user);
                }
                protocol = lobby;
                routing = messageRouting;
                frame = new PeerInputFrame { Epoch = lockedRoster.Epoch, Round = lockedRoster.Round,
                    Racers = new RacerInputCommand[bindings.Count] };
                sequence = 0;
                nextSendAt = 0;
            }
            catch
            {
                Shutdown();
                throw;
            }
        }

        private void Update()
        {
            if (frame == null || protocol == null || routing == null) return;
            if (protocol.Snapshot == null || protocol.Snapshot.Epoch != frame.Epoch ||
                protocol.Snapshot.Round != frame.Round || protocol.Snapshot.Phase != OnlinePhase.Racing)
            {
                nextSendAt = 0;
                foreach (Binding binding in bindings) binding.PendingEdges = InputEdges.None;
                return;
            }

            foreach (Binding binding in bindings)
            {
                if (controlsSuspended) continue;
                if (binding.Keyboard != null && binding.Keyboard.added && binding.Keyboard.spaceKey.wasPressedThisFrame)
                    binding.PendingEdges |= InputEdges.Fire;
                if (binding.Keyboard != null && binding.Keyboard.added && binding.Keyboard.rKey.wasPressedThisFrame)
                    binding.PendingEdges |= InputEdges.Recover;
                if (binding.Gamepad != null && binding.Gamepad.added && binding.Gamepad.buttonWest.wasPressedThisFrame)
                    binding.PendingEdges |= InputEdges.Fire;
                if (binding.Gamepad != null && binding.Gamepad.added && binding.Gamepad.buttonNorth.wasPressedThisFrame)
                    binding.PendingEdges |= InputEdges.Recover;
            }
            double now = Time.realtimeSinceStartupAsDouble;
            if (now < nextSendAt) return;
            nextSendAt = now + SendInterval;
            frame.Sequence = ++sequence;
            for (int i = 0; i < bindings.Count; i++)
            {
                Binding binding = bindings[i];
                float throttle = 0, steer = 0, brake = 0;
                if (!controlsSuspended) ReadAxes(binding, out throttle, out steer, out brake);
                frame.Racers[i] = new RacerInputCommand
                {
                    RacerId = binding.RacerId, Sequence = sequence,
                    Throttle = QuantizeSigned(throttle), Steer = QuantizeSigned(steer),
                    Brake = QuantizeUnsigned(brake), Edges = binding.PendingEdges
                };
            }
            InputError result = routing.SubmitLocalInput(frame);
            if (result == InputError.None)
                foreach (Binding binding in bindings) binding.PendingEdges = InputEdges.None;
        }

        private static void ReadAxes(Binding binding, out float throttle, out float steer, out float brake)
        {
            throttle = steer = brake = 0;
            if (binding.Keyboard != null && binding.Keyboard.added && binding.Keyboard.enabled)
            {
                Keyboard keys = binding.Keyboard;
                throttle = (keys.wKey.isPressed || keys.upArrowKey.isPressed ? 1f : 0f) -
                    (keys.sKey.isPressed || keys.downArrowKey.isPressed ? 1f : 0f);
                steer = (keys.dKey.isPressed || keys.rightArrowKey.isPressed ? 1f : 0f) -
                    (keys.aKey.isPressed || keys.leftArrowKey.isPressed ? 1f : 0f);
                brake = keys.leftShiftKey.isPressed || keys.rightShiftKey.isPressed ? 1f : 0f;
            }
            if (binding.Gamepad != null && binding.Gamepad.added && binding.Gamepad.enabled &&
                (binding.Keyboard == null || (throttle == 0 && steer == 0 && brake == 0)))
            {
                Gamepad pad = binding.Gamepad;
                throttle = pad.rightTrigger.ReadValue() - pad.leftTrigger.ReadValue();
                steer = pad.leftStick.x.ReadValue();
                brake = pad.buttonSouth.isPressed ? 1f : 0f;
            }
        }

        internal static short QuantizeSigned(float value) => (short)Mathf.RoundToInt(Mathf.Clamp(value, -1f, 1f) * 1000f);
        internal static short QuantizeUnsigned(float value) => (short)Mathf.RoundToInt(Mathf.Clamp01(value) * 1000f);

        public void Shutdown()
        {
            foreach (Binding binding in bindings)
                if (binding.User.valid) binding.User.UnpairDevicesAndRemoveUser();
            bindings.Clear();
            frame = null;
            protocol = null;
            routing = null;
            sequence = 0;
            nextSendAt = 0;
            controlsSuspended = false;
        }

        private void OnDisable() => Shutdown();
        private void OnDestroy() => Shutdown();
    }
}
