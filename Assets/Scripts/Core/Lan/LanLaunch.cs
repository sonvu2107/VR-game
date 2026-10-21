using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum LanMode { Solo, Host, Client }

public static class LanLaunch
{
    public static LanMode Mode { get; private set; }
    public static string Address { get; private set; } = "127.0.0.1";
    public static int Port { get; private set; } = 7777;
    public const int CampaignLevelCount = 10;
    public static int StartingLevel { get; private set; } = 1;
    public static PlayableCharacterId SelectedCharacter { get; private set; } = PlayableCharacterId.AshenKnight;
    public static string LastError { get; set; }
    public static bool MenuSelectionMade { get; private set; }
    public static string DisplayName { get; private set; } = LanPlayerNames.DefaultName;

    public static void SetDisplayName(string value)
    {
        DisplayName = LanPlayerNames.Normalize(value);
        PlayerPrefs.SetString("LanDisplayName", DisplayName);
        PlayerPrefs.Save();
    }

    public static void Configure(LanMode mode, string address = "127.0.0.1", int port = 7777, int startingLevel = 1)
    {
        LanTransport.ValidatePort(port);
        if (startingLevel < 1 || startingLevel > CampaignLevelCount)
            throw new ArgumentOutOfRangeException(nameof(startingLevel), "Chọn level từ 1 đến 10.");
        if (mode == LanMode.Client && (!System.Net.IPAddress.TryParse(address, out var ip) ||
            ip.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork))
            throw new ArgumentException("Nhập địa chỉ IPv4 của máy Host.");
        Mode = mode; Address = address; Port = port; LastError = null;
        MenuSelectionMade = true;
        StartingLevel = mode == LanMode.Client ? 1 : startingLevel; // Client nhận level có thẩm quyền từ Host.
    }

    public static void SelectCharacter(PlayableCharacterId character)
    {
        if (!Enum.IsDefined(typeof(PlayableCharacterId), character))
            throw new ArgumentOutOfRangeException(nameof(character));
        SelectedCharacter = character;
    }

    public static void SelectNextCharacter()
    {
        int count = Enum.GetValues(typeof(PlayableCharacterId)).Length;
        SelectedCharacter = (PlayableCharacterId)(((int)SelectedCharacter + 1) % count);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        Mode = LanMode.Solo; Address = "127.0.0.1"; Port = 7777; LastError = null;
        StartingLevel = 1;
        MenuSelectionMade = false;
        SelectedCharacter = PlayableCharacterId.AshenKnight;
        LanSessionBridge.Active = null;
        try { DisplayName = LanPlayerNames.Normalize(PlayerPrefs.GetString("LanDisplayName", LanPlayerNames.DefaultName)); }
        catch (ArgumentException) { DisplayName = LanPlayerNames.DefaultName; }
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "-lanHost") Mode = LanMode.Host;
            if (args[i] == "-lanName" && i + 1 < args.Length) DisplayName = LanPlayerNames.Normalize(args[++i]);
            if (args[i] == "-lanJoin" && i + 1 < args.Length) { Mode = LanMode.Client; Address = args[++i]; }
            if (args[i] == "-lanPort" && i + 1 < args.Length && int.TryParse(args[++i], out int port)) Port = port;
            if (args[i] == "-character" && i + 1 < args.Length &&
                Enum.TryParse(args[++i], true, out PlayableCharacterId character)) SelectedCharacter = character;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoLaunch()
    {
        // Editor Play uses whichever scene is open. Enter the lobby for every
        // interactive run until a menu choice has been made.
        if (Application.isEditor && !Application.isBatchMode && !MenuSelectionMade &&
            Mode == LanMode.Solo && SceneManager.GetActiveScene().name != "StartMenu")
        {
            SceneManager.LoadScene("StartMenu");
            return;
        }
        if (Mode != LanMode.Solo && SceneManager.GetActiveScene().name == "StartMenu")
            SceneManager.LoadScene("Dungeon");
    }
}
