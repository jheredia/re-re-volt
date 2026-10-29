using System;
using Presentation;
using ReReVolt.Online.Contracts;
using UnityEngine;

namespace ReReVolt.Online.Unity
{
    /// <summary>
    /// Owns the short boundary between an accepted online lobby and the isolated
    /// race presentation/world. It does not create a second lobby, alter offline
    /// composition, or decide race authority: the host world and protocol retain
    /// those responsibilities.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class OnlineRoundCoordinator : MonoBehaviour
    {
        private OnlineLobbyProtocol protocol;
        private Stage3Bootstrap bootstrap;
        private OnlineRaceHostWorld hostWorld;
        private OnlineHostLocalPresentation hostPresentation;
        private OnlineRaceClientPresentation clientPresentation;
        private OnlineLocalInputSender localInput;
        private OnlineRaceHud raceHud;
        private OnlineCombatPresentation combatPresentation;
        private OnlineRaceMessageRouting routing;
        private bool initialized;
        private bool stopping;
        private GameObject suspendedOfflineCircuit;
        private bool offlineCircuitWasActive;
        private Canvas suspendedOfflineCanvas;
        private bool offlineCanvasWasEnabled;

        public bool IsRoundActive => hostWorld != null || clientPresentation != null;
        public bool IsHostRound => hostWorld != null;
        public void SetLocalControlsSuspended(bool suspended) => localInput?.SetControlsSuspended(suspended);
        public event Action RoundStarted;
        public event Action<string> RoundStopped;

        public void Initialize(OnlineLobbyProtocol lobbyProtocol, Stage3Bootstrap stageBootstrap)
        {
            if (initialized) throw new InvalidOperationException("This online round coordinator is already initialized.");
            protocol = lobbyProtocol ?? throw new ArgumentNullException(nameof(lobbyProtocol));
            bootstrap = stageBootstrap ?? throw new ArgumentNullException(nameof(stageBootstrap));
            protocol.SnapshotChanged += OnLobbySnapshot;
            if (protocol.IsHost) protocol.SetRoundCompositionHandler(ComposeHostRound);
            initialized = true;
            if (protocol.Snapshot != null) OnLobbySnapshot(protocol.Snapshot);
        }

        private bool ComposeHostRound(LobbySnapshot lockedRoster)
        {
            if (!initialized || !protocol.IsHost || IsRoundActive || lockedRoster == null ||
                lockedRoster.Phase != OnlinePhase.Countdown || protocol.HostAuthority == null)
                return false;

            try
            {
                hostWorld = gameObject.AddComponent<OnlineRaceHostWorld>();
                hostWorld.Aborted += OnHostAborted;
                hostWorld.Initialize(protocol.HostAuthority, bootstrap.VisualStyle);
                routing = new OnlineRaceMessageRouting(protocol.Network, lockedRoster, hostWorld);
                hostPresentation = gameObject.AddComponent<OnlineHostLocalPresentation>();
                hostPresentation.Initialize(lockedRoster, protocol.LocalPeer, hostWorld);
                localInput = gameObject.AddComponent<OnlineLocalInputSender>();
                localInput.Initialize(lockedRoster, protocol.LocalPeer, protocol, routing);
                raceHud = gameObject.AddComponent<OnlineRaceHud>();
                raceHud.Initialize(lockedRoster, protocol.LocalPeer);
                raceHud.Bind(routing);
                combatPresentation = gameObject.AddComponent<OnlineCombatPresentation>();
                combatPresentation.Initialize(hostWorld.PickupPositions, true, bootstrap.VisualStyle);
                combatPresentation.Bind(routing);
                SuspendOfflinePresentation();
                hostWorld.Race.RaceStateChanged += OnHostRaceStateChanged;
                RoundStarted?.Invoke();
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                CleanupRound(publishHostAbort: false, "Online host round could not start: " + exception.Message);
                return false;
            }
        }

        private void OnLobbySnapshot(LobbySnapshot snapshot)
        {
            if (!initialized || snapshot == null) return;
            if (snapshot.Phase == OnlinePhase.Lobby)
            {
                if (IsRoundActive) CleanupRound(publishHostAbort: false, "Online round returned to the lobby.");
                return;
            }

            // The host composes synchronously from the accepted Start command. Clients
            // wait for the host's locked roster and own presentation only.
            if (protocol.IsHost || IsRoundActive) return;
            try
            {
                routing = new OnlineRaceMessageRouting(protocol.Network, snapshot);
                clientPresentation = gameObject.AddComponent<OnlineRaceClientPresentation>();
                clientPresentation.Initialize(snapshot, protocol.LocalPeer, bootstrap.VisualStyle);
                clientPresentation.BindAcceptedSnapshots(routing);
                localInput = gameObject.AddComponent<OnlineLocalInputSender>();
                localInput.Initialize(snapshot, protocol.LocalPeer, protocol, routing);
                raceHud = gameObject.AddComponent<OnlineRaceHud>();
                raceHud.Initialize(snapshot, protocol.LocalPeer);
                raceHud.Bind(routing);
                combatPresentation = gameObject.AddComponent<OnlineCombatPresentation>();
                combatPresentation.Initialize(clientPresentation.PickupPositions, false, bootstrap.VisualStyle);
                combatPresentation.Bind(routing);
                SuspendOfflinePresentation();
                RoundStarted?.Invoke();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                CleanupRound(publishHostAbort: false, "Online client round could not start: " + exception.Message);
            }
        }

        private void FixedUpdate()
        {
            // OnlineRaceHostWorld has an earlier execution order, making this an
            // explicit post-physics snapshot cadence rather than a per-frame send.
            if (hostWorld != null && routing != null)
                routing.PublishHostSnapshot(Time.realtimeSinceStartupAsDouble);
        }

        private void Update()
        {
            if (initialized && protocol != null && protocol.TerminalMessage != null && IsRoundActive)
                CleanupRound(publishHostAbort: protocol.IsHost, protocol.TerminalMessage);
        }

        private void OnHostRaceStateChanged(Core.RaceManager.RaceState state)
        {
            // The input sender gates controls on the published lobby phase. The host
            // world enters Racing after countdown, so that transition must reach all
            // peers before their first race input can be submitted.
            if (state == Core.RaceManager.RaceState.Racing ||
                state == Core.RaceManager.RaceState.Finished)
                protocol?.PublishAuthoritySnapshot();
        }

        private void OnHostAborted(string reason)
        {
            if (!stopping) CleanupRound(publishHostAbort: true, reason);
        }

        /// <summary>Called by the lobby entry on leave, connection failure and destruction.</summary>
        public void AbortRound(string reason = "Online round ended.") => CleanupRound(publishHostAbort: protocol != null && protocol.IsHost, reason);

        private void CleanupRound(bool publishHostAbort, string reason)
        {
            if (stopping) return;
            stopping = true;
            bool hadRound = IsRoundActive;
            if (hostWorld != null)
            {
                if (hostWorld.Race != null) hostWorld.Race.RaceStateChanged -= OnHostRaceStateChanged;
                hostWorld.Aborted -= OnHostAborted;
            }
            if (localInput != null)
            {
                localInput.Shutdown();
                Destroy(localInput);
                localInput = null;
            }
            if (raceHud != null)
            {
                raceHud.Shutdown();
                Destroy(raceHud);
                raceHud = null;
            }
            if (combatPresentation != null)
            {
                combatPresentation.Shutdown();
                Destroy(combatPresentation);
                combatPresentation = null;
            }
            routing?.Dispose();
            routing = null;
            if (hostPresentation != null)
            {
                hostPresentation.Shutdown();
                Destroy(hostPresentation);
                hostPresentation = null;
            }
            if (clientPresentation != null)
            {
                clientPresentation.Shutdown();
                Destroy(clientPresentation);
                clientPresentation = null;
            }
            if (hostWorld != null)
            {
                hostWorld.Shutdown();
                Destroy(hostWorld);
                hostWorld = null;
            }
            RestoreOfflinePresentation();
            if (publishHostAbort) protocol?.PublishAuthoritySnapshot();
            stopping = false;
            if (hadRound) RoundStopped?.Invoke(reason);
        }

        private void OnDisable() => DisposeCoordinator();
        private void OnDestroy() => DisposeCoordinator();

        private void SuspendOfflinePresentation()
        {
            if (bootstrap == null) return;
            suspendedOfflineCircuit = bootstrap.CircuitRoot;
            if (suspendedOfflineCircuit != null)
            {
                offlineCircuitWasActive = suspendedOfflineCircuit.activeSelf;
                suspendedOfflineCircuit.SetActive(false);
            }
            suspendedOfflineCanvas = bootstrap.Presentation != null ? bootstrap.Presentation.PresentationCanvas : null;
            if (suspendedOfflineCanvas != null)
            {
                offlineCanvasWasEnabled = suspendedOfflineCanvas.enabled;
                suspendedOfflineCanvas.enabled = false;
            }
        }

        private void RestoreOfflinePresentation()
        {
            if (suspendedOfflineCircuit != null) suspendedOfflineCircuit.SetActive(offlineCircuitWasActive);
            if (suspendedOfflineCanvas != null) suspendedOfflineCanvas.enabled = offlineCanvasWasEnabled;
            suspendedOfflineCircuit = null;
            suspendedOfflineCanvas = null;
        }

        private void DisposeCoordinator()
        {
            if (!initialized) return;
            CleanupRound(publishHostAbort: false, "Online round coordinator was disabled.");
            if (protocol != null)
            {
                protocol.SnapshotChanged -= OnLobbySnapshot;
                if (protocol.IsHost) protocol.SetRoundCompositionHandler(null);
            }
            RoundStarted = null;
            RoundStopped = null;
            protocol = null;
            bootstrap = null;
            initialized = false;
        }
    }
}
