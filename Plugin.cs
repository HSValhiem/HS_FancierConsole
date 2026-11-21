using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using Mono.Cecil;
using UnityEngine;
using static BepInEx.ConsoleUtil.Kon;
using Debug = UnityEngine.Debug;
using Logger = UnityEngine.Logger;

namespace HS_FancierConsole
{

    public static class HS_FancierConsole
    {
        #region Required Patcher Code
        public static IEnumerable<string> TargetDLLs { get; } = Array.Empty<string>();

        public static void Patch(AssemblyDefinition assembly)
        {
        }
        #endregion

        public const string ModGUID = "hs.fancierconsole";
        public const string ModName = "HS_FancierConsole";
        public const string ModVersion = "0.1.4";


        public static object? OriginalLogger;
        public static object? CustomLogger;

        public static ConsoleLogListener? OriginalListener;
        public static HS_ConsoleLogListener? Listener;

        public static bool IsHeadless = Environment.GetCommandLineArgs().Contains("-batchmode");


        public static CONSOLE_FONT_INFO_EX OldFontInfo;

        public unsafe struct CONSOLE_FONT_INFO_EX
        {
            internal uint cbSize;
            internal uint nFont;
            internal COORD dwFontSize;
            internal int FontFamily;
            internal int FontWeight;
            internal fixed char FaceName[32];
        }

        #region PInvoke
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

        #endregion

        #region Config Boilerplate

        public static BepInPlugin FancierConsole = new(ModGUID, ModName, ModVersion);
        private static readonly ConfigFile Config = new(Path.Combine(Paths.ConfigPath, ModGUID) + ".cfg", true, FancierConsole);

        public static ConfigEntry<bool> ModEnabled = null!;
        public static ConfigEntry<bool> ConfigBannerEnabled = null!;
        public static ConfigEntry<bool> ConfigBannerRainbow = null!;
        public static ConfigEntry<LogLevel> ConfigConsoleDisplayedLevel = null!;
        public static ConfigEntry<bool> ConfigLogUnity = null!;
        public static ConfigEntry<string> ConfigDateTimeFormat = null!;
        public static ConfigEntry<string> ConfigDefaultColors = null!;
        public static ConfigEntry<string> ConfigColorMappings = null!;
        public static ConfigEntry<int> ConfigFontWeight = null!;
        public static ConfigEntry<string> ConfigFontName = null!;
        public static ConfigEntry<bool> ConfigEnablePrettyStackTrace = null!;
        public static ConfigEntry<bool> ConfigChangeFont = null!;

        public static ConfigEntry<bool> ChangeTitle = null!;

        public static ConfigEntry<string> ConfigDefaultColorsStackTraceBanner = null!;
        public static ConfigEntry<string> ConfigDefaultColorsStackTrace = null!;
        public static ConfigEntry<string> ConfigDefaultColorsException = null!;
        public static ConfigEntry<string> ConfigDefaultColorsExceptionPre = null!;

        public static ConfigEntry<string> ConfigDefaultColorsBanner = null!;

        public static ConfigEntry<string> ConfigDefaultColorsLoading = null!;

        public static string[] DefaultColors = { string.Empty };
        public static string[] DefaultColorsBanner = { string.Empty };
        public static string[] DefaultColorsLoading = { string.Empty };
        public static List<string> ColorMappingsStrings = new();
        public static string[] DefaultColorsStackTrace = { string.Empty };
        public static string[] DefaultColorsStackTraceBanner = { string.Empty };
        public static string[] DefaultColorsException = { string.Empty };
        public static string[] DefaultColorsExceptionPre = { string.Empty };

        #endregion

        public static void Finish()
        {
            #region Default Config Settings

            ModEnabled = Config.Bind("1 - General", "Mod Enabled", true, "");
            ModEnabled.SettingChanged += (_, _) => ToggleMod();
            ToggleMod();

            ChangeTitle = Config.Bind("1 - General", "Change Title", true, "Disable Fancier Log from Changing the Title");

            ConfigConsoleDisplayedLevel = Config.Bind("1 - General", "LogLevels",
                LogLevel.Fatal | LogLevel.Error | LogLevel.Message | LogLevel.Info | LogLevel.Warning,
                "Which log levels to show in the console output.");
            ConfigLogUnity = Config.Bind("1 - General", "Log Unity", true, "Enable Unity Log Messages in Console");
            ConfigDateTimeFormat = Config.Bind("1 - General", "Date Time Format", "hh:mm:ss tt",
                "Set the Format for the Date Time Prefix");

            ConfigChangeFont = Config.Bind("2 - Font", "Enable Font", true, "Enable Changing the Font");
            ConfigChangeFont.SettingChanged += (_, _) => SetFont();
            ConfigFontWeight = Config.Bind("2 - Font", "Font Weight", 16, "Set Size of the Font");
            ConfigFontName = Config.Bind("2 - Font", "Font Name", "Consolas", "Name of the Font to Use (Must be TrueType)");
            SetFont();

            ConfigBannerEnabled = Config.Bind("3 - Fancier Log Intro Banner", "Banner Enabled", true, "Enable the Fancier Log Intro Banner");
            ConfigBannerRainbow = Config.Bind("3 - Fancier Log Intro Banner", "Fancier Log Banner Rainbow Effect", true, "Enable the Fancier Log Intro Banner Rainbow Effect");

            ConfigDefaultColorsBanner = Config.Bind("3 - Fancier Log Intro Banner", "Banner Color", "#0000CC,#000000", "Set Colors for the Fancier Console Intro Banner");
            ConfigDefaultColorsBanner.SettingChanged += (_, _) => DefaultColorsBanner = ConfigDefaultColorsBanner.Value.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            DefaultColorsBanner = ConfigDefaultColorsBanner.Value.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

            ConfigDefaultColorsLoading = Config.Bind("3 - Fancier Log Intro Banner", "Loading Text Color", "#0000CC,#000000", "Set Colors for the Fancier Console Intro Loading Text");
            ConfigDefaultColorsLoading.SettingChanged += (_, _) => DefaultColorsLoading = ConfigDefaultColorsLoading.Value.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            DefaultColorsLoading = ConfigDefaultColorsLoading.Value.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

            ConfigDefaultColors = Config.Bind("4 - General Color Mapping", "Default Colors", "#FFFFFF,#000000",
                "Set Default Colors of the Console");
            ConfigDefaultColors.SettingChanged += (_, _) =>
                DefaultColors = ConfigDefaultColors.Value.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            DefaultColors = ConfigDefaultColors.Value.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

            ConfigColorMappings = Config.Bind("4 - General Color Mapping", "Color Map",
                "Error|Failed,#FF0000,#000000;" +
                "AzuAntiCheat,#000000,#FF0000;" +
                "Warning,#FFFF00,#000000;" +
                "Steam game server initialized,#0000FF,#000000;" +
                "Message,#00FFFF,#000000;" +
                "Unity Log,#FF00FF,#000000;" +
                "BepInEx,#00FF00,#000000;", "Change Colors based on Regex Match (Name,Foreground, Background");
            ConfigColorMappings.SettingChanged += (_, _) =>
                ColorMappingsStrings = ConfigColorMappings.Value
                    .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                    .ToList();
            ColorMappingsStrings = ConfigColorMappings.Value.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                .ToList();


            ConfigEnablePrettyStackTrace = Config.Bind("5 - Stack Trace Color Mapping", "Pretty Stack Trace", true,
    "Enable Pretty Stack Trace");

            ConfigDefaultColorsStackTraceBanner = Config.Bind("5 - Stack Trace Color Mapping", "Stack Trace Banner Colors",
                "#000000,#FFA500", "Set Colors for the Stack Trace Banner");
            ConfigDefaultColorsStackTraceBanner.SettingChanged += (_, _) =>
                DefaultColorsStackTraceBanner =
                    ConfigDefaultColorsStackTraceBanner.Value.Split(new[] { ';' },
                        StringSplitOptions.RemoveEmptyEntries);
            DefaultColorsStackTraceBanner =
                ConfigDefaultColorsStackTraceBanner.Value.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

            ConfigDefaultColorsStackTrace = Config.Bind("5 - Stack Trace Color Mapping", "Stack Trace Colors",
                "#FF0000,#000000",
                "Set Colors for the Stack Trace");
            ConfigDefaultColorsStackTrace.SettingChanged += (_, _) =>
                DefaultColorsStackTrace =
                    ConfigDefaultColorsStackTrace.Value.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            DefaultColorsStackTrace =
                ConfigDefaultColorsStackTrace.Value.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

            ConfigDefaultColorsExceptionPre = Config.Bind("5 - Stack Trace Color Mapping", "Exception Prefix Color",
                "#880000,#000000", "Set Colors for Exception Prefix");
            ConfigDefaultColorsExceptionPre.SettingChanged += (_, _) =>
                DefaultColorsExceptionPre =
                    ConfigDefaultColorsExceptionPre.Value.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            DefaultColorsExceptionPre =
                ConfigDefaultColorsExceptionPre.Value.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

            ConfigDefaultColorsException = Config.Bind("5 - Stack Trace Color Mapping", "Exception Message Color",
                "#FF0000,#000000", "Set Colors Exception Message");
            ConfigDefaultColorsException.SettingChanged += (_, _) =>
                DefaultColorsException =
                    ConfigDefaultColorsException.Value.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            DefaultColorsException =
                ConfigDefaultColorsException.Value.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

            #endregion

            // Store Original Font
            IntPtr hwnd = GetStdHandle(-11);
            if (hwnd != new IntPtr(-1))
            {
                GetCurrentConsoleFontEx(hwnd, false, ref OldFontInfo);
                OldFontInfo.cbSize = (uint)Marshal.SizeOf(OldFontInfo);
            }
        }

        public static unsafe void SetFont()
        {
            if (ModEnabled.Value && ConfigChangeFont.Value)
            {
                // Set Font
                string fontName = ConfigFontName.Value;
                IntPtr hnd = GetStdHandle(-11);
                if (hnd != new IntPtr(-1))
                {
                    CONSOLE_FONT_INFO_EX newInfo = new CONSOLE_FONT_INFO_EX();

                    newInfo.cbSize = (uint)Marshal.SizeOf(newInfo);
                    newInfo.FontFamily = 4;
                    IntPtr ptr = new IntPtr(newInfo.FaceName);

                    // Set Font Name
                    Marshal.Copy(fontName.ToCharArray(), 0, ptr, fontName.Length);

                    // Set Font Size
                    newInfo.dwFontSize = new COORD { X = (short)ConfigFontWeight.Value, Y = (short)ConfigFontWeight.Value };
                    newInfo.FontWeight = ConfigFontWeight.Value;

                    SetCurrentConsoleFontEx(hnd, false, newInfo);
                }
            }
            else
            {
                // Reset Font to Default
                IntPtr hnd = GetStdHandle(-11);
                if (hnd != new IntPtr(-1)) SetCurrentConsoleFontEx(hnd, false, OldFontInfo);
            }
        }

        public static void ToggleMod()
        {
            if (ModEnabled.Value)
            {
                // Store Existing Console Listener
                OriginalListener ??= BepInEx.Logging.Logger.Listeners.OfType<ConsoleLogListener>().FirstOrDefault();

                // Remove Existing Console Listener
                if (OriginalListener != null) BepInEx.Logging.Logger.Listeners.Remove(OriginalListener);

                // Add new Custom Config Listener
                BepInEx.Logging.Logger.Listeners.Add(Listener ??= new HS_ConsoleLogListener());

                // Override Unity Debug Output ( This is necessary because Headless Servers output directly to console regardless of BepInEx Configuration )
                FieldInfo[] debugStdOut = typeof(Debug).GetFields(BindingFlags.Static | BindingFlags.NonPublic);
                if (debugStdOut.Length > 0)
                {
                    // Create our Custom Unity Output Handler.
                    CustomLogger ??= new Logger(new HS_DebugLogHandler());

                    // Store Original Logger
                    OriginalLogger ??= (ILogger)debugStdOut[0].GetValue(null);

                    // Use reflection to Override unity debug output
                    debugStdOut[0].SetValue(null, CustomLogger);
                    debugStdOut[1].SetValue(null, CustomLogger);

                }
            }
            else
            {
                // Remove Existing Custom Console Listener
                if (Listener != null) BepInEx.Logging.Logger.Listeners.Remove(Listener);

                // Add Back Default Config Listener
                if (OriginalListener != null) BepInEx.Logging.Logger.Listeners.Add(OriginalListener);

                // Revert Debug Output Override
                if (OriginalLogger != null)
                {
                    var debugStdOut = typeof(Debug).GetFields(BindingFlags.Static | BindingFlags.NonPublic);
                    if (debugStdOut.Length > 0)
                    {
                        debugStdOut[0].SetValue(null, (ILogger)OriginalLogger);
                        debugStdOut[1].SetValue(null, (ILogger)OriginalLogger);
                    }
                }

                // Set Console Title to Default
                Version version = typeof(Paths).Assembly.GetName().Version;
                ConsoleManager.SetConsoleTitle($"BepInEx {version} - {Paths.ProcessName}");

                // Revert Font to Default
                SetFont();

                // Revert Colors to Default
                ResetConsoleColor();
            }
        }
    }
}

