using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using ServerSync;

namespace HS_FancierConsole;

[BepInPlugin(ModGUID, ModName, ModVersion)]
public class HS_FancierConsole : BaseUnityPlugin
{
	public const string ModGUID = "hs.fancierconsole";
	public const string ModName = "HS_FancierConsole";
    public const string ModVersion = "0.1.1";

	public static ConfigSync configSync = new(ModGUID) { DisplayName = ModName, CurrentVersion = ModVersion, MinimumRequiredVersion = "0.1.0", ModRequired = false };

	private static ConfigEntry<Toggle> ServerConfigLocked = null!;
    public static ConfigEntry<bool> ModEnabled = null!;
    public static ConfigEntry<LogLevel> ConfigConsoleDisplayedLevel = null!;
    public static ConfigEntry<bool> ConfigLogUnity = null!;
    public static ConfigEntry<string> ConfigDateTimeFormat = null!;
    public static ConfigEntry<string> ConfigDefaultColors = null!;
    public static ConfigEntry<string> ConfigColorMappings = null!;
    public static ConfigEntry<int> ConfigFontWeight = null!;
    public static ConfigEntry<string> ConfigFontName = null!;
    public static ConfigEntry<bool> ConfigEnablePrettyStackTrace = null!;
    public static ConfigEntry<bool> ConfigChangeFont = null!;

    public static ConfigEntry<string> ConfigDefaultColorsStackTraceBanner = null!;
    public static ConfigEntry<string> ConfigDefaultColorsStackTrace = null!;
    public static ConfigEntry<string> ConfigDefaultColorsException = null!;
    public static ConfigEntry<string> ConfigDefaultColorsExceptionPre = null!;

    public static string[] DefaultColors = { string.Empty };
    public static List<string> ColorMappingsStrings = new();
    public static string[] DefaultColorsStackTrace = { string.Empty };
    public static string[] DefaultColorsStackTraceBanner = { string.Empty };
    public static string[] DefaultColorsException = { string.Empty };
    public static string[] DefaultColorsExceptionPre = { string.Empty };

    #region Config Boilerplate

    private ConfigEntry<T> config<T>(string group, string name, T value, ConfigDescription description,
		bool synchronizedSetting = true)
	{
		ConfigEntry<T> configEntry = Config.Bind(group, name, value, description);
		SyncedConfigEntry<T> syncedConfigEntry = configSync.AddConfigEntry(configEntry);
		syncedConfigEntry.SynchronizedConfig = synchronizedSetting;
		return configEntry;
	}

	private ConfigEntry<T> config<T>(string group, string name, T value, string description,
		bool synchronizedSetting = true)
	{
		return config(group, name, value, new ConfigDescription(description), synchronizedSetting);
	}

	private enum Toggle
	{
		On = 1,
		Off = 0
	}

    #endregion


    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern bool GetCurrentConsoleFontEx(
        IntPtr consoleOutput,
        bool maximumWindow,
        ref CONSOLE_FONT_INFO_EX lpConsoleCurrentFontEx);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool SetCurrentConsoleFontEx(
        IntPtr consoleOutput,
        bool maximumWindow,
        CONSOLE_FONT_INFO_EX consoleCurrentFontEx);

    private const int STD_OUTPUT_HANDLE = -11;
    private const int TMPF_TRUETYPE = 4;
    private const int LF_FACESIZE = 32;
    private static IntPtr INVALID_HANDLE_VALUE = new IntPtr(-1);

    [StructLayout(LayoutKind.Sequential)]
    internal struct COORD
    {
        internal short X;
        internal short Y;

        internal COORD(short x, short y)
        {
            X = x;
            Y = y;
        }
    }

    internal unsafe struct CONSOLE_FONT_INFO_EX
    {
        internal uint cbSize;
        internal uint nFont;
        internal COORD dwFontSize;
        internal int FontFamily;
        internal int FontWeight;
        internal fixed char FaceName[LF_FACESIZE];
    }


    public unsafe void Awake()
	{
		// Setup Config
		ServerConfigLocked = config("1 - General", "Lock Configuration", Toggle.On, "If on, the configuration is locked and can be changed by server admins only.");
		configSync.AddLockingConfigEntry(ServerConfigLocked);
		ModEnabled = config("1 - General", "Mod Enabled", true, "");
        ConfigConsoleDisplayedLevel = config("1 - General", "LogLevels", LogLevel.Fatal | LogLevel.Error | LogLevel.Message | LogLevel.Info | LogLevel.Warning, "Which log levels to show in the console output.");
        ConfigLogUnity = config("1 - General", "Log Unity", true, "Enable Unity Log Messages in Console");
        ConfigDateTimeFormat = config("1 - General", "Date Time Format", "hh:mm:ss tt", "Set the Format for the Date Time Prefix");
        ConfigChangeFont = config("2 - Font", "Enable Font", true, "Enable Changing the Font");
        ConfigFontWeight = config("2 - Font", "Font Weight", 16, "Set Size of the Font");
        ConfigFontName = config("2 - Font", "Font Name", "Consolas", "Name of the Font to Use (Must be TrueType)");

        ConfigEnablePrettyStackTrace = config("4 - Stack Trace Color Mapping", "Pretty Stack Trace", true, "Enable Pretty Stack Trace");

        ConfigDefaultColorsStackTraceBanner = config("4 - Stack Trace Color Mapping", "Stack Trace Banner Colors", "#000000,#FFA500", "Set Colors for the Stack Trace Banner");
        ConfigDefaultColorsStackTraceBanner.SettingChanged += (_, _) => DefaultColorsStackTraceBanner = ConfigDefaultColorsStackTraceBanner.Value.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
        DefaultColorsStackTraceBanner = ConfigDefaultColorsStackTraceBanner.Value.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

        ConfigDefaultColorsStackTrace = config("4 - Stack Trace Color Mapping", "Stack Trace Colors", "#FF0000,#000000", "Set Colors for the Stack Trace");
        ConfigDefaultColorsStackTrace.SettingChanged += (_, _) => DefaultColorsStackTrace = ConfigDefaultColorsStackTrace.Value.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
        DefaultColorsStackTrace = ConfigDefaultColorsStackTrace.Value.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

        ConfigDefaultColorsExceptionPre = config("4 - Stack Trace Color Mapping", "Exception Prefix Color", "#880000,#000000", "Set Colors for Exception Prefix");
        ConfigDefaultColorsExceptionPre.SettingChanged += (_, _) => DefaultColorsExceptionPre = ConfigDefaultColorsExceptionPre.Value.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
        DefaultColorsExceptionPre = ConfigDefaultColorsExceptionPre.Value.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

        ConfigDefaultColorsException = config("4 - Stack Trace Color Mapping", "Exception Message Color", "#FF0000,#000000", "Set Colors Exception Message");
        ConfigDefaultColorsException.SettingChanged += (_, _) => DefaultColorsException = ConfigDefaultColorsException.Value.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
        DefaultColorsException = ConfigDefaultColorsException.Value.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

        ConfigDefaultColors = config("3 - General Color Mapping", "Default Colors", "#FFFFFF,#000000", "Set Default Colors of the Console");
        ConfigDefaultColors.SettingChanged += (_, _) => DefaultColors = ConfigDefaultColors.Value.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
        DefaultColors = ConfigDefaultColors.Value.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

        ConfigColorMappings = config("3 - General Color Mapping", "Color Map",
            "Error|Failed,#FF0000,#000000;" +
            "AzuAntiCheat,#000000,#FF0000;" +
            "Warning,#FFFF00,#000000;" +
            "Steam game server initialized,#0000FF,#000000;" +
            "Message,#00FFFF,#000000;" +
            "Unity Log,#FF00FF,#000000;" +
            "BepInEx,#008000,#000000;", new ConfigDescription("Change Colors based on Regex Match (Name,Foreground, Background"));
        ConfigColorMappings.SettingChanged += (_, _) => ColorMappingsStrings = ConfigColorMappings.Value.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries).ToList();
        ColorMappingsStrings = ConfigColorMappings.Value.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries).ToList();

        // Destroy Original Console
        ConsoleManager.Driver.DetachConsole();

        // Remove old Config Listener
        BepInEx.Logging.Logger.Listeners.OfType<ConsoleLogListener>().ToList().ForEach(listener => { BepInEx.Logging.Logger.Listeners.Remove(listener); });

		// Preserve Disk Listener
        DiskLogListener? diskListener = BepInEx.Logging.Logger.Listeners.FirstOrDefault(listener => listener is DiskLogListener) as DiskLogListener;

        // Remove Disk Listener Temporarily as to not Double the Preloader Logs in the LogOutput.log
        BepInEx.Logging.Logger.Listeners.OfType<DiskLogListener>().ToList().ForEach(listener => { BepInEx.Logging.Logger.Listeners.Remove(listener); });

        // Create Custom Driver
        ConsoleManager.Driver = new WindowsConsoleDriver(); // Instantiate
        ConsoleManager.Driver.Initialize(true); // Init

		// Create Console
        ConsoleManager.Driver.CreateConsole(ConsoleManager.ConfigConsoleShiftJis.Value ? 932U : (uint)Encoding.UTF8.CodePage);

        // Add new Listener
        BepInEx.Logging.Logger.Listeners.Add(new HS_ConsoleLogListener());

        // Replay Old Console Logs
        BepInEx.Bootstrap.Chainloader.ReplayPreloaderLogs(BepInEx.Preloader.PreloaderConsoleListener.LogEvents);

		// Add the Disk Listener Back
        BepInEx.Logging.Logger.Listeners.Add(diskListener);

        // Set Title
        ConsoleManager.Driver.SetConsoleTitle($"HS Fancier Console V{ModVersion}");

        // Set Font and Size
        if (ConfigChangeFont.Value)
        {
            string fontName = ConfigFontName.Value;
            IntPtr hnd = BepInEx.ConsoleUtil.Kon.GetStdHandle(STD_OUTPUT_HANDLE);
            if (hnd != INVALID_HANDLE_VALUE)
            {
                CONSOLE_FONT_INFO_EX info = new CONSOLE_FONT_INFO_EX();
                info.cbSize = (uint)Marshal.SizeOf(info);
                // First determine whether there's already a TrueType font.
                if (GetCurrentConsoleFontEx(hnd, false, ref info))
                {
                   // bool tt = (info.FontFamily & TMPF_TRUETYPE) == TMPF_TRUETYPE;
                    //if (tt) { return; }

                    CONSOLE_FONT_INFO_EX newInfo = new CONSOLE_FONT_INFO_EX();
                    newInfo.cbSize = (uint)Marshal.SizeOf(newInfo);
                    newInfo.FontFamily = TMPF_TRUETYPE;
                    IntPtr ptr = new IntPtr(newInfo.FaceName);
                    Marshal.Copy(fontName.ToCharArray(), 0, ptr, fontName.Length);
                    // Get some settings from current font.
                    newInfo.dwFontSize = new COORD((short)ConfigFontWeight.Value, (short)ConfigFontWeight.Value);
                    newInfo.FontWeight = ConfigFontWeight.Value;
                    SetCurrentConsoleFontEx(hnd, false, newInfo);
                }
            }
        }
    }
}

