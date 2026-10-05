using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using Varkaria.DiscordPresence;

public sealed class PresenceTests
{
    private static readonly DateTime Start = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static PresenceSnapshot Snapshot(string scene = "Avatar", bool playing = false) => PresenceSnapshot.Create("Project", scene, "2022.3.22f1", playing, Start, true, true);
    private sealed class Connection : IPresenceConnection
    {
        public bool Ready { get; set; } = true;
        public int Revision { get; set; }
        public string Error { get; set; }
        public int Pumps, Sends, Disposals;
        public bool ThrowPump, ThrowSend;
        public PresenceSnapshot Last;
        public void Pump() { Pumps++; if (ThrowPump) throw new InvalidOperationException("Disconnected"); }
        public void Send(PresenceSnapshot snapshot) { if (ThrowSend) throw new InvalidOperationException("Send failed"); Sends++; Last = snapshot; }
        public void Dispose() { Disposals++; }
    }
    [Test] public void UnchangedFramesPumpWithoutSendingOrReconnecting()
    {
        var connection = new Connection(); var creations = 0;
        using (var controller = new PresenceController(() => { creations++; return connection; }, null))
        {
            for (var i = 0; i < 500; i++) controller.Tick(i, Snapshot());
            Assert.That(creations, Is.EqualTo(1)); Assert.That(connection.Sends, Is.EqualTo(1)); Assert.That(connection.Pumps, Is.EqualTo(500));
        }
        Assert.That(connection.Disposals, Is.EqualTo(1));
    }
    [Test] public void LatestSceneChangeIsCoalescedWithinRateLimit()
    {
        var connection = new Connection();
        using (var controller = new PresenceController(() => connection, null))
        {
            controller.Tick(0, Snapshot()); controller.Tick(1, Snapshot("B")); controller.Tick(2, Snapshot("C"));
            controller.RequestUpdate(); controller.Tick(3, Snapshot("D")); Assert.That(connection.Sends, Is.EqualTo(1));
            controller.Tick(15, Snapshot("D")); Assert.That(connection.Sends, Is.EqualTo(2)); Assert.That(connection.Last.State, Is.EqualTo("D scene"));
        }
    }
    [Test] public void StartsWhenDiscordAppearsWithoutRecreatingClient()
    {
        var connection = new Connection { Ready = false }; var creations = 0;
        using (var controller = new PresenceController(() => { creations++; return connection; }, null))
        {
            controller.Tick(0, Snapshot()); controller.Tick(120, Snapshot()); Assert.That(connection.Sends, Is.Zero);
            connection.Ready = true; connection.Revision++; controller.Tick(121, Snapshot());
            Assert.That(connection.Sends, Is.EqualTo(1)); Assert.That(creations, Is.EqualTo(1));
            connection.Ready = false; controller.Tick(150, Snapshot()); connection.Ready = true; connection.Revision++;
            controller.Tick(151, Snapshot()); Assert.That(connection.Sends, Is.EqualTo(2));
        }
    }
    [Test] public void FailedInitializationBacksOffWithoutLogSpam()
    {
        var attempts = 0; var warnings = new List<string>();
        using (var controller = new PresenceController(() => { attempts++; throw new InvalidOperationException("Unavailable"); }, warnings.Add))
        {
            for (var t = 0; t < 5; t++) controller.Tick(t, Snapshot()); Assert.That(attempts, Is.EqualTo(1));
            controller.Tick(5, Snapshot()); controller.Tick(14, Snapshot()); Assert.That(attempts, Is.EqualTo(2));
            controller.Tick(15, Snapshot()); Assert.That(attempts, Is.EqualTo(3)); Assert.That(warnings.Count, Is.EqualTo(1));
        }
    }
    [TestCase(true)] [TestCase(false)] public void TransportFailureDisposesAndRetriesWithoutCachingFailure(bool pumpFailure)
    {
        var first = new Connection { ThrowPump = pumpFailure, ThrowSend = !pumpFailure }; var second = new Connection(); var attempts = 0;
        using (var controller = new PresenceController(() => attempts++ == 0 ? first : second, null))
        {
            controller.Tick(0, Snapshot()); Assert.That(first.Disposals, Is.EqualTo(1));
            controller.Tick(4, Snapshot()); Assert.That(attempts, Is.EqualTo(1));
            controller.Tick(5, Snapshot()); Assert.That(second.Sends, Is.EqualTo(1));
        }
    }
    [Test] public void DiscordErrorRetriesUnchangedPresenceAfterRateLimit()
    {
        var connection = new Connection();
        using (var controller = new PresenceController(() => connection, null))
        {
            controller.Tick(0, Snapshot()); connection.Error = "Rate limited"; connection.Revision++;
            controller.Tick(1, Snapshot()); Assert.That(connection.Sends, Is.EqualTo(1));
            controller.Tick(15, Snapshot()); Assert.That(connection.Sends, Is.EqualTo(2));
        }
    }
    [Test] public void DisposalIsIdempotentAndStopsFutureWork()
    {
        var connection = new Connection(); var controller = new PresenceController(() => connection, null);
        controller.Tick(0, Snapshot()); controller.Dispose(); controller.Dispose(); controller.Tick(20, Snapshot());
        Assert.That(connection.Disposals, Is.EqualTo(1)); Assert.That(connection.Pumps, Is.EqualTo(1));
    }
    [Test] public void PrivacyHidesProjectAndSceneFromPayload()
    {
        var snapshot = PresenceSnapshot.Create("Private project", "Private scene", "2022.3", false, Start, false, false);
        var json = Newtonsoft.Json.JsonConvert.SerializeObject(snapshot.ToPresence());
        StringAssert.DoesNotContain("Private", json); Assert.That(snapshot.Details, Is.EqualTo("Working in Unity")); Assert.That(snapshot.State, Is.EqualTo("Edit mode"));
    }
    [Test] public void PlayModeChangesIconAndRetainsSessionTimestamp()
    {
        var edit = Snapshot().ToPresence(); var play = Snapshot(playing: true).ToPresence();
        Assert.That(edit.Assets.SmallImageKey, Is.EqualTo("edit-mode")); Assert.That(play.Assets.SmallImageKey, Is.EqualTo("play-mode"));
        Assert.That(play.Timestamps.Start, Is.EqualTo(edit.Timestamps.Start));
    }
    [TestCase("")] [TestCase(null)] public void UnsavedSceneHasUsableLabel(string name) => Assert.That(Snapshot(name).State, Is.EqualTo("Unsaved scene"));
    [Test] public void LongUnicodeNamesStayWithinDiscordByteLimit()
    {
        var name = string.Concat(System.Linq.Enumerable.Repeat("ทดสอบ🎉", 50));
        var snapshot = PresenceSnapshot.Create(name, name, name, false, Start, true, true);
        foreach (var value in new[] { snapshot.Details, snapshot.State, snapshot.UnityVersion })
        {
            Assert.That(Encoding.UTF8.GetByteCount(value), Is.LessThanOrEqualTo(128));
            Assert.That(value.EndsWith("\ud83c"), Is.False);
        }
        Assert.DoesNotThrow(() => snapshot.ToPresence());
    }
    [Test] public void BatchModeDoesNotCreateARealDiscordConnection()
    {
        if (!UnityEngine.Application.isBatchMode) Assert.Ignore("Only applicable to the batch runner.");
        VRMDiscordRPC.UpdateActivity(true); Assert.That(VRMDiscordRPC.Status, Is.EqualTo("Disabled in batch mode"));
    }
}
