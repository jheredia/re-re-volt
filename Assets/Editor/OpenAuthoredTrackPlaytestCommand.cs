using System;
using System.Linq;
using Core;
using Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Starts the current saved course through the real local front end.</summary>
[InitializeOnLoad]
public static class OpenAuthoredTrackPlaytestCommand
{
    private const string MenuPath = "Tools/ReReVolt/Tracks/Play Registered Open Track Locally";
    private const string FrontEndPath = "Assets/Scenes/FrontEnd.unity";
    private const string PendingKey = "ReReVolt.TrackPlaytest.Pending";
    private const string PreviousStartSceneKey = "ReReVolt.TrackPlaytest.StartScene";
    private const string PreviousPreferencesKey = "ReReVolt.TrackPlaytest.Preferences";
    private const string HadPreferencesKey = "ReReVolt.TrackPlaytest.HadPreferences";

    static OpenAuthoredTrackPlaytestCommand()
    {
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        EditorApplication.delayCall += RestoreAfterInterruptedPlay;
    }

    [MenuItem(MenuPath)]
    private static void PlayOpenTrack()
    {
        try
        {
            StartPlaytest();
        }
        catch (Exception exception)
        {
            Restore();
            Debug.LogError("[TrackAuthoring] Cannot start local playtest: " + exception);
            EditorUtility.DisplayDialog("Local track playtest unavailable", exception.Message, "OK");
        }
    }

    private static void StartPlaytest()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Exit Play mode before starting a track playtest.");
        Scene scene = SceneManager.GetActiveScene();
        if (SceneManager.sceneCount != 1 || !scene.IsValid() || !scene.isLoaded ||
            string.IsNullOrEmpty(scene.path) || scene.isDirty)
            throw new InvalidOperationException("Open the saved track scene alone and save all changes first.");
        GameObject[] roots = scene.GetRootGameObjects();
        if (roots.Length != 1 || !roots[0].TryGetComponent(out TrackAuthoringRoot authoring))
            throw new InvalidOperationException("The open scene needs one TrackAuthoringRoot as its only root.");
        if (!TrackAuthoringEditorTools.TryValidateScene(scene, out string report))
            throw new InvalidOperationException("Fix the authored track before playing:\n" + report);
        if (!TrackCatalog.TryGet(authoring.StableId, out TrackDefinition definition) ||
            !definition.IsSceneAuthored || !definition.IsAvailable ||
            !string.Equals(definition.ScenePath, scene.path, StringComparison.Ordinal))
            throw new InvalidOperationException("Register this saved track for play first. Baked built-in tracks must be activated.");
        int checkpointCount = authoring.GetComponentsInChildren<RaceCheckpoint>(true).Length;
        if (!string.Equals(definition.ContentRevision, authoring.ContentRevision, StringComparison.Ordinal) ||
            definition.LapCount != authoring.Laps || definition.OrderedCheckpointCount != checkpointCount)
            throw new InvalidOperationException("Track metadata changed. Sync its registration and content revision first.");
        if (!EditorBuildSettings.scenes.Any(entry => entry.enabled &&
                string.Equals(entry.path, scene.path, StringComparison.Ordinal)))
            throw new InvalidOperationException("Enable the authored track scene in Build Settings first.");
        SceneAsset frontEnd = AssetDatabase.LoadAssetAtPath<SceneAsset>(FrontEndPath);
        if (frontEnd == null || !EditorBuildSettings.scenes.Any(entry => entry.enabled &&
                string.Equals(entry.path, FrontEndPath, StringComparison.Ordinal)))
            throw new InvalidOperationException("The FrontEnd scene must exist and be enabled in Build Settings.");

        SceneAsset previousStartScene = EditorSceneManager.playModeStartScene;
        bool hadPreferences = PlayerPrefs.HasKey(DemoFlowPreferences.StorageKey);
        string previousPreferences = hadPreferences
            ? PlayerPrefs.GetString(DemoFlowPreferences.StorageKey) : string.Empty;
        SessionState.SetString(PreviousStartSceneKey,
            previousStartScene == null ? string.Empty : AssetDatabase.GetAssetPath(previousStartScene));
        SessionState.SetBool(HadPreferencesKey, hadPreferences);
        SessionState.SetString(PreviousPreferencesKey, previousPreferences);
        SessionState.SetBool(PendingKey, true);

        var preferences = new DemoFlowPreferences(new PlayerPrefsLocalPreferenceStore());
        preferences.Load();
        if (!preferences.SetTrackId(authoring.StableId))
            throw new InvalidOperationException("The selected track could not be saved for this playtest.");
        EditorSceneManager.playModeStartScene = frontEnd;
        Debug.Log("[TrackAuthoring] Playing saved track '" + authoring.StableId +
                  "' through FrontEnd. Stop Play mode to restore the previous selection.");
        EditorApplication.isPlaying = true;
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredEditMode) Restore();
    }

    private static void RestoreAfterInterruptedPlay()
    {
        if (!EditorApplication.isPlayingOrWillChangePlaymode) Restore();
    }

    private static void Restore()
    {
        if (!SessionState.GetBool(PendingKey, false)) return;
        string previousScenePath = SessionState.GetString(PreviousStartSceneKey, string.Empty);
        EditorSceneManager.playModeStartScene = string.IsNullOrEmpty(previousScenePath)
            ? null : AssetDatabase.LoadAssetAtPath<SceneAsset>(previousScenePath);
        if (SessionState.GetBool(HadPreferencesKey, false))
            PlayerPrefs.SetString(DemoFlowPreferences.StorageKey,
                SessionState.GetString(PreviousPreferencesKey, string.Empty));
        else
            PlayerPrefs.DeleteKey(DemoFlowPreferences.StorageKey);
        PlayerPrefs.Save();
        SessionState.SetString(PreviousStartSceneKey, string.Empty);
        SessionState.SetString(PreviousPreferencesKey, string.Empty);
        SessionState.SetBool(HadPreferencesKey, false);
        SessionState.SetBool(PendingKey, false);
    }
}
