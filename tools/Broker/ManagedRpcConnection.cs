using System;
using DiscordRPC;

namespace Varkaria.DiscordPresence
{
    internal sealed class ManagedRpcConnection : IPresenceConnection
    {
        private readonly DiscordRpcClient _client;
        private readonly ClosingPipe _pipe;
        public bool Ready { get; private set; }
        public int Revision { get; private set; }
        public string Error { get; private set; }
        public ManagedRpcConnection(string applicationId, DiscordRPC.IO.INamedPipeClient transport = null)
        {
            _pipe = new ClosingPipe(transport ?? new DiscordRPC.IO.ManagedNamedPipeClient());
            _client = new DiscordRpcClient(applicationId, autoEvents: false, client: _pipe) { ShutdownOnly = true, SkipIdenticalPresence = false };
            _client.OnReady += (_, __) => { Ready = true; Error = null; Revision++; };
            _client.OnClose += (_, __) => { Ready = false; Revision++; };
            _client.OnConnectionFailed += (_, __) => { Ready = false; };
            _client.OnError += (_, error) => { Error = error.Message; Revision++; };
            try { if (!_client.Initialize()) throw new InvalidOperationException("Could not start the Discord connection."); }
            catch { _client.Dispose(); throw; }
        }
        public void Pump() => _client.Invoke();
        public void Send(PresenceSnapshot snapshot) => _client.SetPresence(snapshot.ToPresence());
        public void Dispose()
        {
            Ready = false; _client.Dispose();
            // The SDK's Dispose queues a graceful shutdown; keep the helper alive until
            // its pipe worker finishes sending the null activity and closes the socket.
            if (!_pipe.Finished.WaitOne(3000))
            {
                _pipe.Close();
                // Do not start another connection while the old worker is still running.
                Environment.Exit(3);
            }
        }
    }
    internal sealed class ClosingPipe : DiscordRPC.IO.INamedPipeClient
    {
        private readonly DiscordRPC.IO.INamedPipeClient _inner;
        public ClosingPipe(DiscordRPC.IO.INamedPipeClient inner) { _inner = inner; }
        internal readonly System.Threading.ManualResetEvent Finished = new System.Threading.ManualResetEvent(false);
        public DiscordRPC.Logging.ILogger Logger { get => _inner.Logger; set => _inner.Logger = value; }
        public bool IsConnected => _inner.IsConnected;
        public int ConnectedPipe => -1;
        public bool Connect(int pipe) => _inner.Connect(pipe);
        public bool ReadFrame(out DiscordRPC.IO.PipeFrame frame) => _inner.ReadFrame(out frame);
        public bool WriteFrame(DiscordRPC.IO.PipeFrame frame) => _inner.WriteFrame(frame);
        public void Close() => _inner.Close();
        public void Dispose() { try { _inner.Dispose(); } finally { Finished.Set(); } }
    }
}
