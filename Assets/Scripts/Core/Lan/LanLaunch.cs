using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum LanMode { Solo, Host, Client }

public static class LanLaunch
{
    public static LanMode Mode { get; private set; }
    public static string Address { get; private set; } = "127.0.0.1";
    public static int Port { get; private set; } = 7777;
    public static string LastError { get; set; }

    public static void Configure(LanMode mode, string address = "127.0.0.1", int port = 7777)
    {
        LanTransport.ValidatePort(port);
        if (mode == LanMode.Client && (!System.Net.IPAddress.TryParse(address, out var ip) ||
            ip.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork))
            throw new ArgumentException("Nhập địa chỉ IPv4 của máy Host.");
        Mode = mode; Address = address; Port = port; LastError = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        Mode = LanMode.Solo; Address = "127.0.0.1"; Port = 7777; LastError = null;
        LanSessionBridge.Active = null;
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "-lanHost") Mode = LanMode.Host;
            if (args[i] == "-lanJoin" && i + 1 < args.Length) { Mode = LanMode.Client; Address = args[++i]; }
            if (args[i] == "-lanPort" && i + 1 < args.Length && int.TryParse(args[++i], out int port)) Port = port;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoLaunch()
    {
        if (Mode != LanMode.Solo && SceneManager.GetActiveScene().name == "StartMenu")
            SceneManager.LoadScene("Dungeon");
    }
}
