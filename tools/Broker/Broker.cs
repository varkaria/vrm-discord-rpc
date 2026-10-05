using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Varkaria.DiscordPresence;

internal static class Broker
{
    private static int Main(string[] args)
    {
#if BROKER_TEST
        if (args.Length == 1 && args[0] == "--transport-test") return TransportTest.Run();
        if (args.Length == 2 && args[0] == "--identity")
        {
            using (var process = Process.GetProcessById(int.Parse(args[1]))) Console.WriteLine(process.StartTime.ToUniversalTime().Ticks);
            return 0;
        }
#endif
        if (args.Length != 3 || !int.TryParse(args[1], out var parent) || !long.TryParse(args[2], out var parentStarted)) return 2;
        var directory = Path.GetFullPath(args[0]);
        if (!Directory.Exists(directory)) return 2;
        // Every launch must win this lock BEFORE creating a Discord client.
        using (var ownership = BrokerFiles.TryLock(directory))
        {
            if (ownership == null) return 0;
            PresenceController controller = null;
            string application = null;
            DateTime? sessionStart = null;
            var timer = Stopwatch.StartNew();
            var self = Process.GetCurrentProcess();
            var status = new BrokerStatus { Pid = self.Id, Started = self.StartTime.ToUniversalTime().Ticks };
            var nextStatus = 0d;
            try
            {
                while (BrokerFiles.IsAlive(parent, parentStarted))
                {
                    var request = BrokerFiles.Read<BrokerRequest>(Path.Combine(directory, "request.json"));
                    if (request != null && request.ParentPid == parent && request.ParentStarted == parentStarted)
                    {
                        if (request.Protocol != BrokerRequest.CurrentProtocol || !request.Enabled) break;
                        if (!string.IsNullOrEmpty(request.OwnershipFile) && !File.Exists(request.OwnershipFile)) break;
                        if (request.Snapshot != null && ulong.TryParse(request.ApplicationId, out var appId) && appId > 0)
                        {
                            if (application != request.ApplicationId)
                            {
                                controller?.Dispose();
                                application = request.ApplicationId;
                                controller = new PresenceController(() => CreateConnection(application, directory), message => status.Message = message);
                            }
                            sessionStart = sessionStart ?? request.Snapshot.Started;
                            request.Snapshot.Started = sessionStart.Value;
                            controller.Tick(timer.Elapsed.TotalSeconds, request.Snapshot);
                            status.Message = controller.Status;
                        }
                    }
                    if (timer.Elapsed.TotalSeconds >= nextStatus)
                    {
                        BrokerFiles.Write(Path.Combine(directory, "status.json"), status);
                        nextStatus = timer.Elapsed.TotalSeconds + 1;
                    }
                    Thread.Sleep(100);
                }
                return 0;
            }
            catch (Exception exception)
            {
                status.Message = "Helper stopped: " + exception.Message;
                try { BrokerFiles.Write(Path.Combine(directory, "status.json"), status); } catch (IOException) { }
                return 1;
            }
            finally { controller?.Dispose(); self.Dispose(); }
        }
    }
    private static IPresenceConnection CreateConnection(string application, string directory)
    {
#if BROKER_TEST
        return new RecordingConnection(directory);
#else
        return new ManagedRpcConnection(application);
#endif
    }
#if BROKER_TEST
    // Compiled only into the test fixture; the distributed helper has no test switch.
    private sealed class RecordingConnection : IPresenceConnection
    {
        private readonly string _log;
        public RecordingConnection(string directory) { _log = Path.Combine(directory, "trace.jsonl"); Record("connect", null); }
        public bool Ready => true;
        public int Revision => 0;
        public string Error => null;
        public void Pump() { }
        public void Send(PresenceSnapshot snapshot) => Record("send", snapshot);
        public void Dispose() => Record("disconnect", null);
        private void Record(string action, PresenceSnapshot snapshot) => File.AppendAllText(_log,
            Newtonsoft.Json.JsonConvert.SerializeObject(new { action, pid = Process.GetCurrentProcess().Id, snapshot }) + "\n");
    }
#endif
}
