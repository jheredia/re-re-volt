using System;
using System.IO;
using System.Text;
using Core;
using UnityEngine;

namespace Vehicle
{
    public interface IHandlingBakeOffReportSink
    {
        string Export(HandlingBakeOffReport report);
    }

    public sealed class FileHandlingBakeOffReportSink : IHandlingBakeOffReportSink
    {
        public string Export(HandlingBakeOffReport report)
        {
            string csv = HandlingBakeOffCsv.Format(report);
#if UNITY_WEBGL && !UNITY_EDITOR
            Debug.Log($"[HandlingBakeOff] WebGL report {report.RunId}; copy the CSV below.\n{csv}");
            return "WebGL Debug.Log";
#else
            string directory = Path.Combine(Application.persistentDataPath, "HandlingBakeOff");
            Directory.CreateDirectory(directory);
            string stem = "handling-bakeoff-" + SanitizeFileName(report.RunId);
            string path = Path.Combine(directory, stem + ".csv");
            for (int suffix = 2; File.Exists(path); suffix++)
                path = Path.Combine(directory, stem + "-" + suffix + ".csv");
            File.WriteAllText(path, csv, new UTF8Encoding(false));
            return path;
#endif
        }

        private static string SanitizeFileName(string value)
        {
            char[] invalid = Path.GetInvalidFileNameChars();
            StringBuilder safe = new StringBuilder(value.Length);
            for (int i = 0; i < value.Length; i++)
            {
                char character = value[i];
                bool allowed = true;
                for (int invalidIndex = 0; invalidIndex < invalid.Length; invalidIndex++)
                    if (character == invalid[invalidIndex])
                    {
                        allowed = false;
                        break;
                    }
                safe.Append(allowed ? character : '_');
            }
            return safe.Length > 0 ? safe.ToString() : "run";
        }
    }

    [DisallowMultipleComponent]
    public sealed class HandlingCollisionProbe : MonoBehaviour
    {
        public int CollisionCount { get; private set; }
        private bool tracking;

        internal void BeginRun()
        {
            CollisionCount = 0;
            tracking = true;
        }

        internal void EndRun() => tracking = false;

        private void OnCollisionEnter(Collision collision)
        {
            if (tracking) CollisionCount++;
        }
    }

    /// <summary>Samples one neutral handling record per active local racer and exports once at race completion.</summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(1000)]
    public sealed class HandlingBakeOffCoordinator : MonoBehaviour
    {
        public const string TrackId = "WORKBENCH_CIRCUIT";

        private sealed class PlayerRunSlot
        {
            public readonly HandlingBakeOffAccumulator Accumulator = new HandlingBakeOffAccumulator();
            public LocalPlayer Player;
            public HandlingCollisionProbe CollisionProbe;

            public bool IsBound => Player != null;

            public void Bind(LocalPlayer player)
            {
                Player = player;
                CollisionProbe = player.Vehicle.GetComponent<HandlingCollisionProbe>() ??
                                 player.Vehicle.gameObject.AddComponent<HandlingCollisionProbe>();
                Accumulator.Reset();
                CollisionProbe.BeginRun();
            }

            public void Clear()
            {
                if (CollisionProbe != null) CollisionProbe.EndRun();
                Player = null;
                CollisionProbe = null;
                Accumulator.Reset();
            }
        }

        private readonly PlayerRunSlot[] slots =
        {
            new PlayerRunSlot(), new PlayerRunSlot(), new PlayerRunSlot(), new PlayerRunSlot()
        };

        private RaceManager race;
        private VehicleManager vehicles;
        private IHandlingBakeOffReportSink reportSink;
        private bool configured;
        private bool subscribed;
        private bool runActive;
        private int runSequence;
        private string runId;
        private string createdUtc;

        public bool IsRunActive => runActive;
        public int ExportedRunCount { get; private set; }
        public string LastExportDestination { get; private set; }

        public void Configure(RaceManager raceManager, VehicleManager vehicleManager,
            IHandlingBakeOffReportSink sink = null)
        {
            if (raceManager == null) throw new ArgumentNullException(nameof(raceManager));
            if (vehicleManager == null) throw new ArgumentNullException(nameof(vehicleManager));
            if (configured) Unsubscribe();
            CancelRun();
            race = raceManager;
            vehicles = vehicleManager;
            reportSink = sink ?? new FileHandlingBakeOffReportSink();
            Subscribe();
            configured = true;
        }

        private void Subscribe()
        {
            if (subscribed) return;
            race.RaceStateChanged += OnRaceStateChanged;
            race.RaceCompleted += OnRaceCompleted;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (race != null && subscribed)
            {
                race.RaceStateChanged -= OnRaceStateChanged;
                race.RaceCompleted -= OnRaceCompleted;
            }
            subscribed = false;
        }

        private void OnRaceStateChanged(RaceManager.RaceState state)
        {
            if (state == RaceManager.RaceState.Racing) BeginRun();
            else if (state == RaceManager.RaceState.Idle && runActive) CancelRun();
        }

        private void BeginRun()
        {
            CancelRun();
            DateTime now = DateTime.UtcNow;
            runSequence++;
            createdUtc = now.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", System.Globalization.CultureInfo.InvariantCulture);
            runId = now.ToString("yyyyMMdd'T'HHmmssfff'Z'", System.Globalization.CultureInfo.InvariantCulture) +
                    "-r" + runSequence.ToString("000", System.Globalization.CultureInfo.InvariantCulture);

            for (int i = 0; i < vehicles.Players.Count; i++)
            {
                LocalPlayer player = vehicles.Players[i];
                if (player.PlayerIndex >= 0 && player.PlayerIndex < slots.Length) slots[player.PlayerIndex].Bind(player);
            }
            runActive = true;
        }

        private void FixedUpdate()
        {
            if (!runActive || race == null || !race.RaceRunning || Time.timeScale <= 0f) return;
            double deltaSeconds = Time.fixedDeltaTime;
            for (int i = 0; i < slots.Length; i++)
            {
                PlayerRunSlot slot = slots[i];
                if (!slot.IsBound) continue;
                if (slot.Player.Racer.IsFinished)
                {
                    slot.CollisionProbe.EndRun();
                    continue;
                }
                if (!slot.Player.IsConnected) continue;
                VehicleController vehicle = slot.Player.Vehicle;
                slot.Accumulator.Step(deltaSeconds, vehicle.ThrottleInput, vehicle.SteerInput,
                    vehicle.CurrentHandlingSample, vehicle.GroundedWheelCount > 0);
            }
        }

        private void OnRaceCompleted(System.Collections.Generic.IReadOnlyList<RacerState> standings)
        {
            if (!runActive) return;
            // Close first so a sink failure or repeated event cannot duplicate this run.
            runActive = false;
            HandlingBakeOffRow[] rows = new HandlingBakeOffRow[CountBoundSlots()];
            int rowIndex = 0;
            for (int i = 0; i < slots.Length; i++)
            {
                PlayerRunSlot slot = slots[i];
                if (!slot.IsBound) continue;
                slot.CollisionProbe.EndRun();
                LocalPlayer player = slot.Player;
                RacerState racer = player.Racer;
                rows[rowIndex++] = new HandlingBakeOffRow(
                    runId,
                    createdUtc,
                    TrackId,
                    player.PlayerIndex + 1,
                    racer.RacerId,
                    player.Input.IsKeyboardPlayer ? "KEYBOARD" : "GAMEPAD",
                    HandlingModelCatalog.GetPublicLabel(player.Vehicle.SelectedHandlingModel),
                    slot.Accumulator.Capture(),
                    slot.CollisionProbe.CollisionCount,
                    racer.FinishPosition,
                    racer.CompletedLaps,
                    race.TotalLaps,
                    racer.FinishTime >= 0f ? racer.FinishTime : null);
            }

            HandlingBakeOffReport report = new HandlingBakeOffReport(runId, createdUtc, TrackId, rows);
            try
            {
                LastExportDestination = reportSink.Export(report);
                ExportedRunCount++;
                Debug.Log($"[HandlingBakeOff] Exported {rows.Length} player row(s) to {LastExportDestination}.", this);
            }
            catch (Exception exception)
            {
                LastExportDestination = null;
                Debug.LogError($"[HandlingBakeOff] Could not export run {runId}: {exception.Message}", this);
            }
        }

        private int CountBoundSlots()
        {
            int count = 0;
            for (int i = 0; i < slots.Length; i++)
                if (slots[i].IsBound) count++;
            return count;
        }

        private void CancelRun()
        {
            runActive = false;
            for (int i = 0; i < slots.Length; i++) slots[i].Clear();
        }

        private void OnEnable()
        {
            if (configured) Subscribe();
        }

        private void OnDisable()
        {
            if (configured) Unsubscribe();
            CancelRun();
        }
    }
}
