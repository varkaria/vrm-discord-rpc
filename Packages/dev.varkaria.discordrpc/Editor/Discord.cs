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
    private const string ApplicationId = "846826015904497714";
    private const int InitializationDelay = 1000; // milliseconds
    
    private static Discord.Discord _discord;
    private static long _startTimestamp;
    private static bool _isPlayMode;
    private static bool _isInitialized = false;

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
    }

    private static void Update()
    {
        _discord?.RunCallbacks();
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (EditorApplication.isPlaying != _isPlayMode)
        {
            _isPlayMode = EditorApplication.isPlaying;
            UpdateActivity();
        }
    }

    public static void UpdateActivity()
    {
        if (_discord == null)
        {
            InitializeDiscord();
            return;
        }

        var activity = CreateActivity();
        _discord.GetActivityManager().UpdateActivity(activity, OnActivityUpdated);
    }

    private static Activity CreateActivity()
    {
        return new Activity
        {
            State = $"{EditorSceneManager.GetActiveScene().name} scene",
            Details = Application.productName,
            Timestamps = { Start = _startTimestamp },
            Assets = {
                LargeImage = "unity-icon",
                LargeText = $"Unity {Application.unityVersion}",
                SmallImage = _isPlayMode ? "play-mode" : "edit-mode",
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