using System;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using DiscordRPC;

[assembly: InternalsVisibleTo("VRMDiscordRPC.Editor.Tests")]

namespace Varkaria.DiscordPresence
{
    internal sealed class PresenceSnapshot : IEquatable<PresenceSnapshot>
    {
        public string Details { get; set; }
        public string State { get; set; }
        public string UnityVersion { get; set; }
        public bool Playing { get; set; }
        public DateTime Started { get; set; }
        public static PresenceSnapshot Create(string project, string scene, string unity, bool playing, DateTime started, bool showProject, bool showScene) => new PresenceSnapshot
        {
            Details = Limit(showProject && !string.IsNullOrWhiteSpace(project) ? project : "Working in Unity"),
            State = Limit(showScene ? (string.IsNullOrWhiteSpace(scene) ? "Unsaved scene" : scene + " scene") : playing ? "Play mode" : "Edit mode"),
            UnityVersion = Limit("Unity " + unity), Playing = playing, Started = started
        };
        // Preserve Unicode text elements within Discord's UTF-8 byte limit.
        internal static string Limit(string text)
        {
            var result = new StringBuilder();
            var elements = StringInfo.GetTextElementEnumerator(text ?? "");
            var length = 0;
            while (elements.MoveNext())
            {
                var element = elements.GetTextElement();
                length += Encoding.UTF8.GetByteCount(element);
                if (length > 128) break;
                result.Append(element);
            }
            return result.ToString();
        }
        public RichPresence ToPresence() => new RichPresence
        {
            Details = Details, State = State, Timestamps = new Timestamps(Started),
            Assets = new Assets { LargeImageKey = "logo", LargeImageText = UnityVersion, SmallImageKey = Playing ? "play-mode" : "edit-mode", SmallImageText = Playing ? "Play mode" : "Edit mode" }
        };
        public bool Equals(PresenceSnapshot other) => other != null && Details == other.Details && State == other.State && UnityVersion == other.UnityVersion && Playing == other.Playing && Started == other.Started;
        public override bool Equals(object other) => Equals(other as PresenceSnapshot);
        public override int GetHashCode() => Details.GetHashCode() ^ State.GetHashCode() ^ Playing.GetHashCode() ^ Started.GetHashCode();
    }
    internal interface IPresenceConnection : IDisposable
    {
        bool Ready { get; }
        int Revision { get; }
        string Error { get; }
        void Pump();
        void Send(PresenceSnapshot snapshot);
    }
    // Runs in the helper process. It never calls Unity APIs.
    internal sealed class PresenceController : IDisposable
    {
        private readonly Func<IPresenceConnection> _factory;
        private readonly Action<string> _warn;
        private IPresenceConnection _connection;
        private PresenceSnapshot _sent;
        private double _nextConnection, _nextSend;
        private int _revision = -1, _failures;
        private bool _dirty = true, _disposed;
        private string _lastWarning;
        public string Status { get; private set; } = "Waiting for Discord";
        internal const double SendInterval = 15;
        public PresenceController(Func<IPresenceConnection> factory, Action<string> warn) { _factory = factory; _warn = warn; }
        public void RequestUpdate() => _dirty = true;
        public void Tick(double now, PresenceSnapshot snapshot)
        {
            if (_disposed || snapshot == null) return;
            try
            {
                if (_connection == null)
                {
                    if (now < _nextConnection) return;
                    _connection = _factory(); _revision = -1; _dirty = true;
                }
                _connection.Pump();
                if (_revision != _connection.Revision) { _revision = _connection.Revision; _dirty = true; }
                if (!_connection.Ready) { Status = "Waiting for Discord"; return; }
                Status = string.IsNullOrEmpty(_connection.Error) ? "Connected" : "Discord: " + _connection.Error;
                if ((!_dirty && snapshot.Equals(_sent)) || now < _nextSend) return;
                _connection.Send(snapshot);
                _sent = snapshot; _dirty = false; _nextSend = now + SendInterval;
                _failures = 0; _lastWarning = null;
            }
            catch (Exception e)
            {
                Disconnect();
                _nextConnection = now + Math.Min(60, 5 * Math.Pow(2, Math.Min(_failures++, 4)));
                Status = "Retrying connection: " + e.Message;
                if (_lastWarning != e.Message) { _lastWarning = e.Message; _warn?.Invoke(e.Message); }
            }
        }
        private void Disconnect()
        {
            try { _connection?.Dispose(); }
            catch (Exception e) { _warn?.Invoke("Could not close the connection: " + e.Message); }
            finally { _connection = null; _sent = null; }
        }
        public void Dispose() { if (_disposed) return; _disposed = true; Disconnect(); Status = "Disabled"; }
    }
}
