using Core;
using UnityEngine;
using UnityEngine.InputSystem.Users;

namespace Vehicle
{
    /// <summary>Read-only roster entry. VehicleManager owns its lifetime and pairing.</summary>
    public sealed class LocalPlayer
    {
        internal LocalPlayer(int index, InputUser user, VehicleController vehicle, InputHandler input,
            VehicleRecovery recovery, ChaseCamera chaseCamera, AudioListener listener, RacerState racer,
            VehiclePresentationResponseOwner presentationResponse)
        {
            PlayerIndex = index;
            User = user;
            Vehicle = vehicle;
            FamilyVisual = vehicle != null ? vehicle.GetComponent<VehicleFamilyVisual>() : null;
            Input = input;
            Recovery = recovery;
            ChaseCamera = chaseCamera;
            Listener = listener;
            Racer = racer;
            PresentationResponse = presentationResponse;
            WasConnected = true;
        }

        internal InputUser User { get; }
        internal AudioListener Listener { get; }
        internal bool WasConnected { get; set; }
        public int PlayerIndex { get; }
        public VehicleController Vehicle { get; }
        public VehicleFamilyVisual FamilyVisual { get; }
        public InputHandler Input { get; }
        public VehicleRecovery Recovery { get; }
        public ChaseCamera ChaseCamera { get; }
        public Camera Camera => ChaseCamera.Camera;
        public RacerState Racer { get; }
        public VehiclePresentationResponseOwner PresentationResponse { get; }
        public bool IsConnected => Input != null && Input.IsConnected;
    }
}
