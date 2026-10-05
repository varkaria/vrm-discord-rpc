using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;

namespace Varkaria.DiscordPresence
{
    // Disposable editor-side sender. The helper's lifetime belongs to the OS editor
    // process, never to this object or the current scripting domain.
    internal sealed class BrokerClient
    {
        private readonly int _parent;
        private readonly long _parentStarted;
        internal readonly string DirectoryPath;
        private readonly string _helperDirectory, _mono;
        private PresenceSnapshot _last;
        private string _lastApplication;
        private double _nextLaunch;
        public string Status { get; private set; } = "Starting helper";
        internal static string PackageRoot => UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(BrokerClient).Assembly)?.resolvedPath
            ?? Path.GetFullPath("Packages/dev.varkaria.discordrpc");
        public BrokerClient(string testDirectory = null, string testExecutable = null)
        {
            using (var parent = Process.GetCurrentProcess()) { _parent = parent.Id; _parentStarted = parent.StartTime.ToUniversalTime().Ticks; }
            DirectoryPath = testDirectory ?? Path.GetFullPath(Path.Combine("Library", "VRMDiscordRPC", "Sessions", _parent + "-" + _parentStarted));
            Directory.CreateDirectory(DirectoryPath);
            var executable = testExecutable ?? Path.Combine(PackageRoot, "Editor", "Broker~", "VrmPresence.exe");
            var rpc = Path.Combine(PackageRoot, "Editor", "ThirdParty", "DiscordRPC.dll");
            var json = typeof(Newtonsoft.Json.JsonConvert).Assembly.Location;
            string digest;
            using (var sha = SHA256.Create())
            {
                var contents = string.Join("", Array.ConvertAll(new[] { executable, rpc, json }, path => BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path)))));
                digest = BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(contents))).Replace("-", "");
            }
            _helperDirectory = Path.GetFullPath(Path.Combine("Library", "VRMDiscordRPC", "Bin", digest));
            Directory.CreateDirectory(_helperDirectory);
            CopyIfMissing(executable, "VrmPresence.exe");
            CopyIfMissing(rpc, "DiscordRPC.dll");
            CopyIfMissing(json, "Newtonsoft.Json.dll");
            _mono = Path.Combine(EditorApplication.applicationContentsPath, "MonoBleedingEdge", "bin",
                Application.platform == RuntimePlatform.WindowsEditor ? "mono.exe" : "mono");
            if (!File.Exists(_mono)) throw new FileNotFoundException("This Unity installation has no bundled Mono runtime.", _mono);
        }
        private void CopyIfMissing(string source, string name)
        {
            var destination = Path.Combine(_helperDirectory, name);
            if (File.Exists(destination)) return;
            var temporary = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try { File.Copy(source, temporary); File.Move(temporary, destination); }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
        internal BrokerRequest Request(bool enabled, PresenceSnapshot snapshot, string application) => new BrokerRequest
        {
            ParentPid = _parent, ParentStarted = _parentStarted, Enabled = enabled, Snapshot = snapshot, ApplicationId = application,
            OwnershipFile = Path.Combine(PackageRoot, "Editor", "Discord.cs")
        };
        public void Tick(double now, PresenceSnapshot snapshot, string application)
        {
            if (!snapshot.Equals(_last) || application != _lastApplication)
            {
                BrokerFiles.Write(Path.Combine(DirectoryPath, "request.json"), Request(true, snapshot, application));
                _last = snapshot; _lastApplication = application;
            }
            var status = BrokerFiles.Read<BrokerStatus>(Path.Combine(DirectoryPath, "status.json"));
            if (status != null && BrokerFiles.IsAlive(status.Pid, status.Started))
            {
                Status = status.Message;
                if (status.Protocol != BrokerRequest.CurrentProtocol) Status = "Restart Unity to update the presence helper";
                return;
            }
            if (now < _nextLaunch) return;
            _nextLaunch = now + 10;
            // A helper may own the lock before it has written its first status.
            using (var available = BrokerFiles.TryLock(DirectoryPath)) { if (available == null) return; }
            var start = new ProcessStartInfo(_mono,
                Quote(Path.Combine(_helperDirectory, "VrmPresence.exe")) + " " + Quote(DirectoryPath) + " " + _parent + " " + _parentStarted)
            { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden, WorkingDirectory = _helperDirectory };
            // Unity sets these for its embedded Mono; they must not contaminate a standalone child runtime.
            start.EnvironmentVariables.Remove("MONO_PATH");
            start.EnvironmentVariables.Remove("MONO_CONFIG");
            using (Process.Start(start)) { }
            Status = "Starting helper / waiting for Discord";
        }
        internal static string Quote(string value) => "\"" + System.Text.RegularExpressions.Regex.Replace(value, "(\\\\*)\"", "$1$1\\\"").TrimEnd('\\')
            + new string('\\', (value.Length - value.TrimEnd('\\').Length) * 2) + "\"";
        public void Stop()
        {
            BrokerFiles.Write(Path.Combine(DirectoryPath, "request.json"), Request(false, null, null));
            _last = null; Status = "Disabled";
        }
        public static void StopExisting()
        {
            using (var parent = Process.GetCurrentProcess())
            {
                var started = parent.StartTime.ToUniversalTime().Ticks;
                var directory = Path.GetFullPath(Path.Combine("Library", "VRMDiscordRPC", "Sessions", parent.Id + "-" + started));
                if (Directory.Exists(directory)) BrokerFiles.Write(Path.Combine(directory, "request.json"),
                    new BrokerRequest { ParentPid = parent.Id, ParentStarted = started, Enabled = false });
            }
        }
    }
}
