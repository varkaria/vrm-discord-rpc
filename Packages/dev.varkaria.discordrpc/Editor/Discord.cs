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

    // Cache variables
    private static string _cachedSceneName;
    private static bool _cachedPlayMode;
    private static string _cachedProductName;

    [InitializeOnLoadMethod]
    private static void Initialize()
    {
        if (!_isInitialized)
        {
            _isInitialized = true;
            InitializeAsync();
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }
    }

    private static async void InitializeAsync()
    {
        await Task.Delay(InitializationDelay);
        if (IsDiscordRunning())
        {
            InitializeDiscord();
        }
        else
        {
            Debug.Log("Discord is not running. Rich Presence will not be initialized.");
        }
    }

    private static void InitializeDiscord()
    {
        try 
        {
            DisposeDiscord(); // Dispose of any existing Discord instance
            _discord = new Discord.Discord(long.Parse(ApplicationId), (long)CreateFlags.Default);
            SetStartTimestamp();
            RegisterCallbacks();
            UpdateActivity(true); // Force initial update
            Debug.Log("Discord Rich Presence initialized successfully.");
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to initialize Discord: {e}");
        }
    }

    private static void SetStartTimestamp()
    {
        _startTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }

    private static void RegisterCallbacks()
    {
        EditorApplication.update += Update;
        EditorSceneManager.activeSceneChangedInEditMode += OnActiveSceneChanged;
    }

    private static void Update()
    {
        if (_discord == null) return;

        try
        {
            _discord.RunCallbacks();

            if (Time.realtimeSinceStartup - _lastUpdateTime >= UpdateInterval)
            {
                UpdateActivity();
                _lastUpdateTime = Time.realtimeSinceStartup;
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error in Update: {e}");
            DisposeDiscord(); // Dispose of the Discord instance if an error occurs
            InitializeDiscord(); // Attempt to reinitialize
        }
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        switch (state)
        {
            case PlayModeStateChange.EnteredEditMode:
                EditorApplication.delayCall += () => {
                    InitializeDiscord();
                    UpdateActivity(true);
                };
                break;
            case PlayModeStateChange.ExitingEditMode:
                DisposeDiscord();
                break;
            case PlayModeStateChange.EnteredPlayMode:
                EditorApplication.delayCall += () => {
                    InitializeDiscord();
                    UpdateActivity(true);
                };
                break;
            case PlayModeStateChange.ExitingPlayMode:
                DisposeDiscord();
                break;
        }
    }

    private static void OnActiveSceneChanged(UnityEngine.SceneManagement.Scene prevScene, UnityEngine.SceneManagement.Scene newScene)
    {
        UpdateActivity(true); // Force update on scene change
    }

    public static void UpdateActivity(bool forceUpdate = false)
    {
        if (_discord == null)
        {
            Debug.LogWarning("Discord is null. Attempting to reinitialize.");
            InitializeDiscord();
            return;
        }

        _currentSceneName = EditorSceneManager.GetActiveScene().name;
        _isPlayMode = EditorApplication.isPlaying;

        // Check if anything has changed
        if (!forceUpdate &&
            _currentSceneName == _cachedSceneName &&
            _isPlayMode == _cachedPlayMode &&
            Application.productName == _cachedProductName)
        {
            return; // No changes, skip update
        }

        // Update cache
        _cachedSceneName = _currentSceneName;
        _cachedPlayMode = _isPlayMode;
        _cachedProductName = Application.productName;

        var activity = CreateActivity();
        _discord.GetActivityManager().UpdateActivity(activity, OnActivityUpdated);
        Debug.Log($"Updating activity. Scene: {_currentSceneName}, Play Mode: {_isPlayMode}");
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
                SmallImage = _isPlayMode ? "play-mode" : "edit-mode",
                SmallText = _isPlayMode ? "Play mode" : "Edit mode",
            },
        };
    }

    private static void OnActivityUpdated(Result result)
    {
        if (result == Result.Ok)
        {
            Debug.Log("Discord activity updated successfully.");
        }
        else
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

    private static void DisposeDiscord()
    {
        if (_discord != null)
        {
            try
            {
                _discord.Dispose();
            }
            catch (Exception e)
            {
                Debug.LogError($"Error disposing Discord: {e}");
            }
            finally
            {
                _discord = null;
            }
        }
    }
}
#endif 