using System;
using System.Globalization;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Varkaria.DiscordPresence;

// Preserve the entry point used by existing editor integrations.
[InitializeOnLoad]
public static class VRMDiscordRPC
{
    private const string TimestampKey = "VRMDiscordRPC.SessionStart";
    private static BrokerClient _client;
    private static DateTime _sessionStart;
    private static double _nextUpdate;
    private static string _error;
    public static string Status => Application.isBatchMode ? "Disabled in batch mode" : _error ?? _client?.Status ?? "Disabled";
    static VRMDiscordRPC()
    {
        if (Application.isBatchMode) return;
        if (!DateTime.TryParse(SessionState.GetString(TimestampKey, ""), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out _sessionStart))
        {
            _sessionStart = DateTime.UtcNow;
            SessionState.SetString(TimestampKey, _sessionStart.ToString("O", CultureInfo.InvariantCulture));
        }
        EditorApplication.update += Tick;
        EditorApplication.playModeStateChanged += PlayModeChanged;
        EditorSceneManager.activeSceneChangedInEditMode += SceneChanged;
        SceneManager.activeSceneChanged += SceneChanged;
        AssemblyReloadEvents.beforeAssemblyReload += Detach;
        EditorApplication.quitting += Shutdown;
    }
    internal static void ApplySettings() { _nextUpdate = 0; Tick(); }
    private static void Tick()
    {
        if (Application.isBatchMode || EditorApplication.timeSinceStartup < _nextUpdate) return;
        _nextUpdate = EditorApplication.timeSinceStartup + 1;
        try
        {
            if (!PresenceSettings.Enabled || !PresenceSettings.ValidApplicationId) { Shutdown(); return; }
            if (_client == null) _client = new BrokerClient();
            var snapshot = PresenceSnapshot.Create(Application.productName, SceneManager.GetActiveScene().name, Application.unityVersion,
                EditorApplication.isPlayingOrWillChangePlaymode, _sessionStart, PresenceSettings.ShowProject, PresenceSettings.ShowScene);
            _client.Tick(EditorApplication.timeSinceStartup, snapshot, PresenceSettings.ApplicationId);
            _error = null;
        }
        catch (Exception exception)
        {
            if (_error != exception.Message) Debug.LogWarning("VRM Discord RPC: " + exception.Message);
            _error = exception.Message; _nextUpdate += 9;
        }
    }
    public static void UpdateActivity(bool forceUpdate = false) => _nextUpdate = 0;
    private static void SceneChanged(Scene previous, Scene current) => UpdateActivity();
    private static void PlayModeChanged(PlayModeStateChange state) => UpdateActivity();
    private static void Shutdown()
    {
        try { if (_client != null) _client.Stop(); else BrokerClient.StopExisting(); }
        catch (System.IO.IOException) { /* Parent monitoring still stops the helper on exit. */ }
        _client = null;
    }
    private static void Detach()
    {
        EditorApplication.update -= Tick;
        EditorApplication.playModeStateChanged -= PlayModeChanged;
        EditorSceneManager.activeSceneChangedInEditMode -= SceneChanged;
        SceneManager.activeSceneChanged -= SceneChanged;
        AssemblyReloadEvents.beforeAssemblyReload -= Detach;
        EditorApplication.quitting -= Shutdown;
        // Deliberately leave the helper and its Discord connection running.
    }
}
