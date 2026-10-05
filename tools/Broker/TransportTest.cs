#if BROKER_TEST
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using DiscordRPC.IO;
using Newtonsoft.Json.Linq;
using Varkaria.DiscordPresence;

internal static class TransportTest
{
    internal static int Run()
    {
        var pipe = new FakePipe();
        using (var connection = new ManagedRpcConnection("1283700247440134174", pipe))
        {
            var timeout = Stopwatch.StartNew();
            while (!connection.Ready && timeout.ElapsedMilliseconds < 5000) { connection.Pump(); Thread.Sleep(10); }
            if (!connection.Ready) throw new Exception("SDK handshake did not reach Ready");
            connection.Send(PresenceSnapshot.Create("Project", "Avatar", "2022.3", false, DateTime.UtcNow, true, true));
            while (pipe.Sends == 0 && timeout.ElapsedMilliseconds < 5000) { connection.Pump(); Thread.Sleep(10); }
            if (pipe.Sends != 1) throw new Exception("SDK did not send activity");
        }
        if (!pipe.Cleared || !pipe.Disposed) throw new Exception("Shutdown failed to send null activity and dispose the transport");
        Console.WriteLine("SDK handshake, SET_ACTIVITY, null activity and transport disposal passed");
        return 0;
    }
    private sealed class FakePipe : INamedPipeClient
    {
        private readonly ConcurrentQueue<PipeFrame> _incoming = new ConcurrentQueue<PipeFrame>();
        private volatile bool _connected;
        public volatile bool Cleared, Disposed;
        public int Sends;
        public DiscordRPC.Logging.ILogger Logger { get; set; }
        public bool IsConnected => _connected;
        public int ConnectedPipe => -1;
        public bool Connect(int pipe) { _connected = true; return true; }
        public bool ReadFrame(out PipeFrame frame) => _incoming.TryDequeue(out frame);
        public bool WriteFrame(PipeFrame frame)
        {
            if (frame.Opcode == Opcode.Handshake)
                _incoming.Enqueue(new PipeFrame { Opcode = Opcode.Frame, Message = "{\"cmd\":\"DISPATCH\",\"evt\":\"READY\",\"data\":{\"v\":1,\"config\":{\"cdn_host\":\"cdn.discordapp.com\",\"api_endpoint\":\"//discord.com/api\",\"environment\":\"production\"},\"user\":{\"id\":\"123456789012345678\",\"username\":\"fixture\",\"discriminator\":\"0001\",\"avatar\":null}}}" });
            if (frame.Opcode == Opcode.Frame)
            {
                var payload = JObject.Parse(frame.Message);
                if ((string)payload["cmd"] == "SET_ACTIVITY")
                {
                    if (payload["args"]["activity"].Type == JTokenType.Null) Cleared = true;
                    else Interlocked.Increment(ref Sends);
                }
            }
            if (frame.Opcode == Opcode.Close) _incoming.Enqueue(new PipeFrame { Opcode = Opcode.Close, Message = "{\"code\":1000,\"message\":\"Closed\"}" });
            return true;
        }
        public void Close() { _connected = false; }
        public void Dispose() { Close(); Disposed = true; }
    }
}
#endif
