using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

public static class LanTransportProbe
{
    public static void Run()
    {
        int passed = 0;
        ExpectThrows(() => LanTransport.ValidatePort(1)); passed++;
        using (var invalid = new LanTransport()) ExpectThrows(() => invalid.Join("not-an-ip", 7777)); passed++;
        var portFinder = new TcpListener(IPAddress.Loopback, 0);
        portFinder.Start(); int port = ((IPEndPoint)portFinder.LocalEndpoint).Port; portFinder.Stop();
        using (var host = new LanTransport())
        using (var client = new LanTransport())
        {
            host.Host(port); client.Join("127.0.0.1", port);
            Wait(() => host.Connected && client.Connected);
            client.Send("LAN · người chơi 2");
            string received = null;
            Wait(() => host.TryReceive(out received));
            Check(received == "LAN · người chơi 2", "UTF8 round trip"); passed++;
            host.Send("authoritative-world");
            Wait(() => client.TryReceive(out received));
            Check(received == "authoritative-world", "Host to client"); passed++;
            client.DisconnectPeer();
            Wait(() => !host.Connected); passed++;
            using (var rejoin = new LanTransport())
            {
                rejoin.Join("127.0.0.1", port); Wait(() => host.Connected && rejoin.Connected);
                rejoin.Send("rejoin"); Wait(() => host.TryReceive(out received));
                Check(received == "rejoin", "Reconnect cleared old queues"); passed++;
                rejoin.DisconnectPeer(); Wait(() => !host.Connected);
            }
            using (var malformed = new TcpClient())
            {
                malformed.Connect(IPAddress.Loopback, port); Wait(() => host.Connected);
                using (var writer = new BinaryWriter(malformed.GetStream(), Encoding.UTF8, true))
                { writer.Write(LanTransport.MaxMessageBytes + 1); writer.Flush(); }
                Wait(() => !host.Connected); passed++;
            }
        }
        var input = new LanInputFrame { movement = Vector2.right, attackPressed = true };
        input.MergeEdges(new LanInputFrame { movement = Vector2.up, q = true });
        Check(input.attackPressed && input.q && input.movement == Vector2.up && input.IsValid, "Input edge buffering");
        input.ClearEdges(); Check(!input.attackPressed && !input.q, "Input consumed once"); passed++;
        Check(!new LanInputFrame { movement = new Vector2(float.NaN, 0) }.IsValid, "Reject NaN input"); passed++;
        Debug.Log("LAN_TRANSPORT_PROBE_PASS tests=" + passed);
    }
    private static void Wait(Func<bool> condition)
    {
        DateTime deadline = DateTime.UtcNow.AddSeconds(6);
        while (!condition()) { if (DateTime.UtcNow > deadline) throw new TimeoutException("LAN transport probe timeout."); Thread.Sleep(10); }
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private static void ExpectThrows(Action action)
    {
        try { action(); } catch (ArgumentException) { return; }
        throw new InvalidOperationException("Expected invalid LAN configuration to be rejected.");
    }
}
