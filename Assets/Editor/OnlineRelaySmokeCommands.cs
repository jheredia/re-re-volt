using Core;
using ReReVolt.Online.Unity;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor-only aid for exercising the live Relay Results/rematch UI with two
/// peers on one machine. It uses the host's normal ordered checkpoint API, but
/// deliberately does not validate driving or physical trigger crossings.
/// </summary>
internal static class OnlineRelaySmokeCommands
{
    private const string CompleteRaceMenu = "Tools/ReReVolt/Online Smoke/Complete Host Race";

    [MenuItem(CompleteRaceMenu, true)]
    private static bool CanCompleteHostRace()
    {
        if (!EditorApplication.isPlaying) return false;
        var world = Object.FindAnyObjectByType<OnlineRaceHostWorld>();
        return world != null && world.Race != null && world.Race.RaceRunning;
    }

    [MenuItem(CompleteRaceMenu)]
    private static void CompleteHostRace()
    {
        var world = Object.FindAnyObjectByType<OnlineRaceHostWorld>();
        RaceManager race = world != null ? world.Race : null;
        if (race == null || !race.RaceRunning)
        {
            Debug.LogWarning("[OnlineSmoke] Start a host-owned online race before completing it.");
            return;
        }

        int racers = race.Racers.Count;
        int crossings = 0;
        for (int lap = 0; lap < race.TotalLaps; lap++)
            for (int checkpoint = 0; checkpoint < race.CheckpointCount; checkpoint++)
                for (int index = 0; index < racers; index++)
                {
                    RacerState racer = race.Racers[index];
                    if (!race.ReportCheckpoint(racer.RacerId, checkpoint))
                    {
                        Debug.LogError("[OnlineSmoke] Checkpoint authority rejected " + racer.RacerId +
                            " at lap " + (lap + 1) + ", checkpoint " + checkpoint + ".");
                        return;
                    }
                    crossings++;
                }

        Debug.Log("[OnlineSmoke] Host reported " + crossings + " ordered crossings for " +
            racers + " racers; resulting phase: " + race.CurrentState + ".");
    }
}
