#if UNITY_EDITOR
using System;
using UnityEngine;
using UnityEditor.SceneManagement;
using UnityEditor;
using System.Threading.Tasks;
using Discord;
using Debug = UnityEngine.Debug;
using System.Diagnostics;

public static class VRMDiscordRPC
{
    private const string ApplicationId = "1283700247440134174";
    private const int InitializationDelay = 1000; // milliseconds
    private const float UpdateInterval = 5f; // seconds
    
    private static Discord.Discord _discord;
    private static long _startTimestamp;
    private static bool _isPlayMode;
    private static bool _isInitialized = false;
    private static string _currentSceneName;
    private static float _lastUpdateTime;

    [InitializeOnLoadMethod]
    private static void Initialize()
    {
        if (!_isInitialized)
        {
            _isInitialized = true;
            InitializeAsync();
        }
    }

    private static async void InitializeAsync()
    {
        await Task.Delay(InitializationDelay);
        if (IsDiscordRunning())
        {
            InitializeDiscord();
        }
    }

    private static void InitializeDiscord()
    {
        try 
        {
            _discord = new Discord.Discord(long.Parse(ApplicationId), (long)CreateFlags.Default);
            SetStartTimestamp();
            RegisterCallbacks();
            UpdateActivity();
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to initialize Discord: {e}");
        }
    }

    private static void SetStartTimestamp()
    {
        TimeSpan elapsedTime = TimeSpan.FromMilliseconds(EditorAnalyticsSessionInfo.elapsedTime);
        _startTimestamp = DateTimeOffset.Now.Subtract(elapsedTime).ToUnixTimeSeconds();
    }

    private static void RegisterCallbacks()
    {
        EditorApplication.update += Update;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        EditorSceneManager.sceneOpened += OnSceneOpened;
    }

    private static void Update()
    {
        _discord?.RunCallbacks();

        if (Time.realtimeSinceStartup - _lastUpdateTime >= UpdateInterval)
        {
            UpdateActivity();
            _lastUpdateTime = Time.realtimeSinceStartup;
        }
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        _isPlayMode = EditorApplication.isPlaying;
        UpdateActivity();
    }

    private static void OnSceneOpened(UnityEngine.SceneManagement.Scene scene, OpenSceneMode mode)
    {
        UpdateActivity();
    }

    public static void UpdateActivity()
    {
        if (_discord == null)
        {
            InitializeDiscord();
            return;
        }

        _currentSceneName = EditorSceneManager.GetActiveScene().name;
        var activity = CreateActivity();
        _discord.GetActivityManager().UpdateActivity(activity, OnActivityUpdated);
    }

    private static Activity CreateActivity()
    {
        return new Activity
        {
            State = $"{_currentSceneName} scene",
            Details = Application.productName,
            Timestamps = { Start = _startTimestamp },
            Assets = {
                LargeImage = "logo",
                LargeText = $"Unity {Application.unityVersion}",
                SmallImage = _isPlayMode ? "play-mode-v2" : "edit-mode-v2",
                SmallText = _isPlayMode ? "Play mode" : "Edit mode",
            },
        };
    }

    private static void OnActivityUpdated(Result result)
    {
        if (result != Result.Ok)
        {
            Debug.LogError($"Failed to update Discord activity: {result}");
        }
    }

    private static bool IsDiscordRunning()
    {
        string[] discordProcessNames = { "Discord", "DiscordPTB", "DiscordCanary" };
        foreach (var processName in discordProcessNames)
        {
            if (Process.GetProcessesByName(processName).Length > 0)
            {
                return true;
            }
        }
        return false;
    }
}
#endif