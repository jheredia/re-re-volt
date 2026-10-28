using System;
using System.Collections.Generic;
using AI;
using Core;
using Presentation;
using ReReVolt.Online.Contracts;
using UnityEngine;
using Vehicle;

namespace ReReVolt.Online.Unity
{
    /// <summary>
    /// Host-only world owner. It must be composed into an isolated gameplay scene/root,
    /// never alongside an active offline circuit. No local players, devices or cameras are created here.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class OnlineRaceHostWorld : MonoBehaviour
    {
        private sealed class Racer
        {
            public uint Id;
            public VehicleController Vehicle;
            public Rigidbody Body;
            public VehicleRecovery Recovery;
            public AIDriverController Bot;
            public RacerState Progress;
            public ulong AcknowledgedInput;
            public uint RecoveryRevision;
            public Action RecoveryHandler;
        }
        private readonly List<Racer> racers = new List<Racer>(4);
        private OnlineLobbyAuthority authority;
        private OnlineInputAuthority input;
        private LobbySnapshot roster;
        private ITrackRuntime track;
        private GameObject content;
        private ulong tick, snapshotSequence;
        private int countdown = 3;
        private bool initialized;
        public RaceManager Race { get; private set; }
        public bool IsInitialized => initialized;
        public int RacerCount => racers.Count;
        public event Action<uint, InputEdges> ActionRequested;
        public event Action<string> Aborted;

        /// <summary>The host must first validate Start and lock the roster in Countdown.</summary>
        public void Initialize(OnlineLobbyAuthority lobby, VisualStyleController style = null)
        {
            if (initialized || content != null) throw new InvalidOperationException("This world already owns a round.");
            if (lobby == null || lobby.Phase != OnlinePhase.Countdown || lobby.Round == 0)
                throw new ArgumentException("A host-authorized countdown is required.", nameof(lobby));
            authority = lobby;
            roster = lobby.Snapshot();
            try
            {
                if (!TrackCatalog.TryGet(roster.TrackId, out TrackDefinition definition) || !definition.IsAvailable)
                    throw new InvalidOperationException("The selected track is unavailable.");
                content = new GameObject("Online Host Round " + roster.Round);
                content.transform.SetParent(transform, false);
                Race = content.AddComponent<RaceManager>();
                Race.ControlsGlobalGameState = false;
                if (!Race.ConfigureRace(definition.LapCount, definition.OrderedCheckpointCount))
                    throw new InvalidOperationException("Online race rules rejected track configuration.");
                var tracks = new TrackSession(TrackCatalog.Entries, roster.TrackId);
                track = tracks.BuildSelected(Race, content.transform, style);
                if (track.Grid == null || track.Grid.Length < roster.Racers.Length || track.Checkpoints == null ||
                    track.Checkpoints.Length != definition.OrderedCheckpointCount)
                    throw new InvalidOperationException("Online track runtime is incomplete.");
                var checkpoints = new Transform[track.Checkpoints.Length];
                for (int i = 0; i < checkpoints.Length; i++) checkpoints[i] = track.Checkpoints[i].transform;
                for (int i = 0; i < roster.Racers.Length; i++) CreateRacer(roster.Racers[i], i, checkpoints);
                input = new OnlineInputAuthority(authority);
                Race.RaceStateChanged += OnRaceStateChanged;
                Race.CountdownChanged += OnCountdownChanged;
                initialized = true;
                if (!Race.StartRace()) throw new InvalidOperationException("Online countdown could not start.");
            }
            catch { Shutdown(); throw; }
        }

        private void CreateRacer(LobbyRacer identity, int gridIndex, Transform[] checkpoints)
        {
            Transform grid = track.Grid[gridIndex];
            Pose pose = new Pose(grid.position, grid.rotation);
            GameObject car = PrototypeVehicleFactory.Create(pose);
            car.name = "Online Racer " + identity.Id;
            car.transform.SetParent(content.transform, true);
            var racer = new Racer { Id = identity.Id, Vehicle = car.GetComponent<VehicleController>(), Body = car.GetComponent<Rigidbody>() };
            racers.Add(racer); // Own the object before any later setup can fail.
            racer.Vehicle.SetInputEnabled(false);
            PrototypeVehicleFactory.CreatePlayerIdentityMarker(car, gridIndex);
            racer.Recovery = car.AddComponent<VehicleRecovery>();
            racer.Recovery.Initialize(pose, Race);
            racer.Recovery.ConfigureGroundMask(track.DrivableMask);
            racer.RecoveryHandler = () => racer.RecoveryRevision++;
            racer.Recovery.Recovered += racer.RecoveryHandler;
            racer.Progress = Race.RegisterRacer("online-" + identity.Id, car,
                identity.Bot ? "AI " + identity.Id : "RACER " + identity.Id, !identity.Bot, identity.Bot ? -1 : gridIndex);
            if (racer.Progress == null) throw new InvalidOperationException("Online racer registration failed.");
            if (identity.Bot)
            {
                racer.Bot = car.AddComponent<AIDriverController>();
                float lane = AIDrivingPolicy.CalculateSpawnLaneOffset(pose.position, checkpoints[0].position, checkpoints[0].right);
                racer.Bot.Initialize(Race, racer.Progress, checkpoints, racer.Recovery, track.DrivableMask, lane);
            }
        }

        public InputError SubmitInput(string transportPeer, PeerInputFrame frame)
        {
            if (!initialized) return InputError.IllegalPhase;
            return input.Accept(transportPeer, frame, Time.realtimeSinceStartupAsDouble);
        }

        private void FixedUpdate()
        {
            if (!initialized) return;
            if (authority.Round != roster.Round || authority.Phase == OnlinePhase.Lobby)
            {
                Shutdown();
                Aborted?.Invoke("The host roster changed or a peer left. Return to the retained lobby.");
                return;
            }
            tick++;
            bool canDrive = Race.RaceRunning && Time.timeScale > 0;
            foreach (var racer in racers)
            {
                if (racer.Bot != null)
                {
                    racer.Bot.SetDrivingEnabled(canDrive);
                    if (canDrive) racer.Bot.Tick(Time.fixedDeltaTime);
                    continue;
                }
                bool active = canDrive && !racer.Progress.IsFinished;
                racer.Vehicle.SetInputEnabled(active);
                if (!active) continue;
                var command = input.Consume(racer.Id, Time.realtimeSinceStartupAsDouble);
                racer.AcknowledgedInput = command.Sequence;
                racer.Vehicle.ThrottleInput = command.Throttle / 1000f;
                racer.Vehicle.BrakeInput = command.Brake / 1000f;
                racer.Vehicle.SteerInput = command.Steer / 1000f;
                if ((command.Edges & InputEdges.Recover) != 0) racer.Recovery.TryRecover();
                InputEdges forwarded = command.Edges & (InputEdges.Fire | InputEdges.Pause);
                if (forwarded != InputEdges.None) ActionRequested?.Invoke(racer.Id, forwarded);
            }
        }

        /// <summary>Call after physics at the network snapshot cadence; no send or allocation occurs every physics tick.</summary>
        public DrivingWorldSnapshot CaptureSnapshot()
        {
            if (!initialized || Race.CurrentState == RaceManager.RaceState.Idle)
                throw new InvalidOperationException("No online round is active.");
            var states = new DrivingRacerState[racers.Count];
            for (int i = 0; i < racers.Count; i++)
            {
                var racer = racers[i];
                Vector3 p = racer.Body.position, v = racer.Body.linearVelocity, a = racer.Body.angularVelocity;
                Quaternion q = racer.Body.rotation;
                states[i] = new DrivingRacerState
                {
                    RacerId = racer.Id, Position = Vector(p), Velocity = Vector(v), AngularVelocity = Vector(a),
                    Rotation = new WorldRotation { X = q.x, Y = q.y, Z = q.z, W = q.w },
                    CompletedLaps = racer.Progress.CompletedLaps, NextCheckpoint = racer.Progress.NextCheckpointIndex,
                    PositionInRace = racer.Progress.CurrentPosition, FinishPosition = racer.Progress.FinishPosition,
                    FinishTime = racer.Progress.FinishTime, AcknowledgedInput = racer.AcknowledgedInput,
                    RecoveryRevision = racer.RecoveryRevision
                };
            }
            return new DrivingWorldSnapshot
            {
                Epoch = roster.Epoch, Round = roster.Round, Sequence = ++snapshotSequence, Tick = tick,
                TrackId = roster.TrackId, Elapsed = Race.ElapsedTime, Racers = states,
                Phase = Race.CurrentState == RaceManager.RaceState.Countdown ? DrivingPhase.Countdown :
                    Race.CurrentState == RaceManager.RaceState.Finished ? DrivingPhase.Results : DrivingPhase.Racing,
                Countdown = Race.CurrentState == RaceManager.RaceState.Countdown ? countdown : 0
            };
        }

        public bool TryGetVehicle(uint racerId, out VehicleController vehicle)
        {
            foreach (var racer in racers) if (racer.Id == racerId) { vehicle = racer.Vehicle; return true; }
            vehicle = null;
            return false;
        }

        private void OnCountdownChanged(int value) => countdown = Mathf.Clamp(value, 0, 3);
        private void OnRaceStateChanged(RaceManager.RaceState state)
        {
            if (state == RaceManager.RaceState.Racing) authority.BeginRace();
            else if (state == RaceManager.RaceState.Finished) authority.CompleteRace();
            else if (state == RaceManager.RaceState.Idle) authority.AbortRound();
        }
        private static WorldVector Vector(Vector3 value) => new WorldVector(value.x, value.y, value.z);

        public void Shutdown()
        {
            initialized = false;
            if (Race != null)
            {
                Race.RaceStateChanged -= OnRaceStateChanged;
                Race.CountdownChanged -= OnCountdownChanged;
                Race.ResetRace(); // This RaceManager explicitly does not control the offline global state.
            }
            foreach (var racer in racers)
            {
                if (racer.Recovery != null) racer.Recovery.Recovered -= racer.RecoveryHandler;
                if (racer.Vehicle != null) racer.Vehicle.SetInputEnabled(false);
            }
            racers.Clear();
            authority?.AbortRound();
            GameObject trackRoot = track?.Root;
            if (trackRoot != null) trackRoot.SetActive(false);
            track?.Dispose();
            bool rootStillOwned = track != null && track.Root != null;
            track = null;
            if (rootStillOwned && trackRoot != null) Release(trackRoot);
            if (content != null) { content.SetActive(false); Release(content); }
            content = null; Race = null; input = null;
            tick = snapshotSequence = 0; countdown = 3;
        }
        private static void Release(GameObject value)
        {
            if (Application.isPlaying) Destroy(value); else DestroyImmediate(value);
        }
        private void OnDisable() => Shutdown();
        private void OnDestroy() => Shutdown();
    }
}
