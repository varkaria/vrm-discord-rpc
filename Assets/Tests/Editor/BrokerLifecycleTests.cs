using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.TestTools;
using Varkaria.DiscordPresence;

public sealed class BrokerLifecycleTests
{
    private const string Key = "VRMDiscordRPC.TestDirectory";
    private static string DirectoryPath => SessionState.GetString(Key, "");
    private static string Executable => Path.GetFullPath("Logs/BrokerTest/VrmPresence.exe");
    private static PresenceSnapshot Snapshot(bool playing) => PresenceSnapshot.Create("Test", "Test scene", "2022.3", playing,
        new DateTime(2026, 10, 5, 0, 0, 0, DateTimeKind.Utc), true, true);

    [UnityTest]
    public IEnumerator PlayModeWithoutDomainReloadKeepsConnection()
    {
        SessionState.SetBool(Key + ".RestoreOptions", true);
        SessionState.SetBool(Key + ".OptionsEnabled", EditorSettings.enterPlayModeOptionsEnabled);
        SessionState.SetInt(Key + ".Options", (int)EditorSettings.enterPlayModeOptions);
        EditorSettings.enterPlayModeOptionsEnabled = true;
        EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
        SessionState.SetString(Key, Path.GetFullPath("Library/RpcLifecycleTest-" + Guid.NewGuid().ToString("N")));
        new BrokerClient(DirectoryPath, Executable).Tick(0, Snapshot(false), PresenceSettings.DefaultApplicationId);
        var deadline = EditorApplication.timeSinceStartup + 10;
        while (!File.Exists(Path.Combine(DirectoryPath, "status.json")) && EditorApplication.timeSinceStartup < deadline) yield return null;
        var status = BrokerFiles.Read<BrokerStatus>(Path.Combine(DirectoryPath, "status.json"));
        Assert.That(status, Is.Not.Null);
        SessionState.SetInt(Key + ".Pid", status.Pid);
        yield return new EnterPlayMode(false);
        CheckConnection(true);
        yield return new ExitPlayMode();
        CheckConnection(false);
        Assert.That(File.ReadAllLines(Path.Combine(DirectoryPath, "trace.jsonl")).Count(row => row.Contains("\"action\":\"connect\"")), Is.EqualTo(1));
    }

    [UnityTest]
    public IEnumerator PlayModeAndScriptReloadKeepHelperPidAndSingleConnection()
    {
        Assert.That(File.Exists(Executable), Is.True, "Build the recording helper before running integration tests.");
        SessionState.SetString(Key, Path.GetFullPath("Library/RpcLifecycleTest-" + Guid.NewGuid().ToString("N")));
        new BrokerClient(DirectoryPath, Executable).Tick(0, Snapshot(false), PresenceSettings.DefaultApplicationId);
        var deadline = EditorApplication.timeSinceStartup + 10;
        while (!File.Exists(Path.Combine(DirectoryPath, "status.json")) && EditorApplication.timeSinceStartup < deadline) yield return null;
        var status = BrokerFiles.Read<BrokerStatus>(Path.Combine(DirectoryPath, "status.json"));
        Assert.That(status, Is.Not.Null);
        SessionState.SetInt(Key + ".Pid", status.Pid);
        yield return new EnterPlayMode();
        CheckConnection(true);
        yield return new ExitPlayMode();
        CheckConnection(false);
        EditorUtility.RequestScriptReload();
        yield return new WaitForDomainReload();
        CheckConnection(false);
        var trace = File.ReadAllLines(Path.Combine(DirectoryPath, "trace.jsonl"));
        Assert.That(trace.Count(row => row.Contains("\"action\":\"connect\"")), Is.EqualTo(1));
        Assert.That(trace.Any(row => row.Contains("disconnect")), Is.False);
    }
    private static void CheckConnection(bool playing)
    {
        var client = new BrokerClient(DirectoryPath, Executable);
        client.Tick(EditorApplication.timeSinceStartup, Snapshot(playing), PresenceSettings.DefaultApplicationId);
        Assert.That(BrokerFiles.Read<BrokerStatus>(Path.Combine(DirectoryPath, "status.json")).Pid, Is.EqualTo(SessionState.GetInt(Key + ".Pid", 0)));
    }
    [TearDown]
    public void Cleanup()
    {
        if (!string.IsNullOrEmpty(DirectoryPath)) new BrokerClient(DirectoryPath, Executable).Stop();
        SessionState.EraseString(Key);
        if (SessionState.GetBool(Key + ".RestoreOptions", false))
        {
            EditorSettings.enterPlayModeOptionsEnabled = SessionState.GetBool(Key + ".OptionsEnabled", false);
            EditorSettings.enterPlayModeOptions = (EnterPlayModeOptions)SessionState.GetInt(Key + ".Options", 0);
            SessionState.EraseBool(Key + ".RestoreOptions");
        }
    }
}
