using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using Newtonsoft.Json;

namespace Varkaria.DiscordPresence
{
    internal sealed class BrokerRequest
    {
        public const int CurrentProtocol = 1;
        public int Protocol = CurrentProtocol;
        public int ParentPid;
        public long ParentStarted;
        public bool Enabled;
        public string ApplicationId;
        public string OwnershipFile;
        public PresenceSnapshot Snapshot;
    }
    internal sealed class BrokerStatus
    {
        public int Protocol = BrokerRequest.CurrentProtocol;
        public int Pid;
        public long Started;
        public string Message;
    }
    internal static class BrokerFiles
    {
        public static void Write(string path, object value)
        {
            var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temporary, JsonConvert.SerializeObject(value), new UTF8Encoding(false));
                if (File.Exists(path)) File.Replace(temporary, path, null);
                else File.Move(temporary, path);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
        public static T Read<T>(string path) where T : class
        {
            try
            {
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                using (var reader = new StreamReader(stream)) return JsonConvert.DeserializeObject<T>(reader.ReadToEnd());
            }
            catch (IOException) { return null; }
            catch (JsonException) { return null; }
        }
        public static bool IsAlive(int pid, long started)
        {
            try
            {
                using (var process = Process.GetProcessById(pid))
                    return !process.HasExited && process.StartTime.ToUniversalTime().Ticks == started;
            }
            catch (ArgumentException) { return false; }
            catch (InvalidOperationException) { return false; }
            catch (System.ComponentModel.Win32Exception) { return false; }
        }
        public static FileStream TryLock(string directory)
        {
            try { return new FileStream(Path.Combine(directory, "owner.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
            catch (IOException) { return null; }
        }
    }
}
