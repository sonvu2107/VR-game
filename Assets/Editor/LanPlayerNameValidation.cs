using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using TMPro;
using UnityEngine;

public static class LanPlayerNameValidation
{
    public static void Run()
    {
        int checks = 0;
        void Check(bool ok, string reason) { if (!ok) throw new Exception(reason); checks++; }
        void Reject(string value)
        {
            try { LanPlayerNames.Normalize(value); }
            catch (ArgumentException) { checks++; return; }
            throw new Exception("Invalid name accepted: " + value);
        }
        Check(LanPlayerNames.Normalize("  Minh Đức  ") == "Minh Đức", "Vietnamese trimming");
        Check(LanPlayerNames.Normalize("Nguye\u0302\u0303n") == "Nguyễn", "Unicode normalization");
        Reject(""); Reject("   "); Reject("<b>Host</b>"); Reject("Tên\nkhác"); Reject(new string('W',21));
        Check(LanPlayerNames.ForGuest("Minh Đức", "Minh Đức") == "Minh Đức 2", "duplicate names distinguished");
        Check(LanPlayerNames.ForGuest(new string('W',20),new string('W',20)).Length == 20, "duplicate suffix stays bounded");
        var listener = new TcpListener(IPAddress.Loopback,0);
        listener.Start(); int port = ((IPEndPoint)listener.LocalEndpoint).Port; listener.Stop();
        using (var host = new LanTransport())
        using (var client = new LanTransport())
        {
            host.Host(port); client.Join("127.0.0.1",port);
            Wait(() => host.Connected && client.Connected);
            client.Send(JsonUtility.ToJson(new LanMessage { type = "hello", playerName = "Ngọc Sơn" }));
            string packet = null;
            Wait(() => host.TryReceive(out packet));
            var hello = JsonUtility.FromJson<LanMessage>(packet);
            Check(hello.protocol == LanMessage.CurrentProtocol && hello.playerName == "Ngọc Sơn", "UTF8 handshake over TCP");
            var state = new LanMessage { type = "state", players = new[] {
                new LanPlayerState { displayName = "Minh Đức", nameAnchor = new Vector3(2,3,0), health = 5 },
                new LanPlayerState { displayName = LanPlayerNames.ForGuest(hello.playerName,"Minh Đức"), nameAnchor = new Vector3(4,5,0), health = 5 }
            }};
            host.Send(JsonUtility.ToJson(state));
            Wait(() => client.TryReceive(out packet));
            var received = JsonUtility.FromJson<LanMessage>(packet);
            Check(received.players[0].displayName == "Minh Đức" && received.players[1].displayName == "Ngọc Sơn", "both authoritative names reach client");
            Check(received.players[1].nameAnchor == state.players[1].nameAnchor, "head anchor round trip");
            var owner = new GameObject("Temporary Nameplate Test");
            try
            {
                var plate = LanPlayerNameplate.Create(owner.transform,1);
                plate.Present(received.players[1].displayName,received.players[1].nameAnchor,true,false);
                var label = plate.GetComponentInChildren<TMP_Text>();
                Check(label.text == "Ngọc Sơn" && !label.richText && !label.raycastTarget, "name shown as plain noninteractive text");
                Check(plate.transform.position == new Vector3(4,5,0), "label follows head anchor");
                plate.Present(new string('W',20),Vector3.one,true,false); label.ForceMeshUpdate();
                Check(label.textBounds.size.x <= label.rectTransform.rect.width + 1, "longest name fits label");
                plate.Present("Ngọc Sơn",Vector3.zero,false,false);
                Check(!plate.gameObject.activeSelf,"hidden for absent/dead player");
                plate.Present("Người mới",new Vector3(20,20,0),true,true);
                Check(plate.transform.position == new Vector3(20,20,0),"rejoin/level change snaps to new anchor");
            }
            finally { UnityEngine.Object.DestroyImmediate(owner); }
        }
        Directory.CreateDirectory("Captures/LanNames");
        File.WriteAllText("Captures/LanNames/validation.txt", "PASS: " + checks +
            " checks; real TCP loopback UTF8 hello/snapshot, name rules, duplicate names and nameplate positioning.\nNot a two-machine gameplay test.");
    }

    private static void Wait(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(6);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline) throw new TimeoutException("LAN name loopback timeout");
            Thread.Sleep(10);
        }
    }
}
