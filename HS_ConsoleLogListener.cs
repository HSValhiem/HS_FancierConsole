using System;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Preloader;
using UnityEngine;

namespace HS_FancierConsole;

// Custom ConsoleLogListener to Implement Colored Console Text and Regex Formatting
public class HS_ConsoleLogListener : ILogListener
{
    public static string lastLine = string.Empty;
    public static int init;

    private static CancellationTokenSource cancellationTokenSource;

    public void LogEvent(object sender, LogEventArgs eventArgs)
    {
        // Handle Unity Logs
        if (!HS_FancierConsole.ConfigLogUnity.Value && sender is UnityLogSource) return;

        // Handle Log Level
        if ((eventArgs.Level & HS_FancierConsole.ConfigConsoleDisplayedLevel.Value) == 0) return;

        // Get Log Line
        var line = eventArgs.ToStringLine();

        // Always Remove Duplicates
        if (line == lastLine) return;
            lastLine = line;


        // Make Stake Trace Pretty
        if (line.Contains("Stack trace:") && HS_FancierConsole.ConfigEnablePrettyStackTrace.Value)
        {
            LogStackTrace(line);
            return;
        }

        // Set Default Colors from Settings
        var foregroundColor = HS_FancierConsole.DefaultColors[0];
        var backgroundColor = HS_FancierConsole.DefaultColors[1];

        // Setup Colors from Color Map Settings
        foreach (var mapping in HS_FancierConsole.ColorMappingsStrings)
        {
            var mappedLine = mapping.Split(new[] { ',' });
            if (Regex.IsMatch(line, mappedLine[0]))
            {
                foregroundColor = mappedLine[1];
                backgroundColor = mappedLine[2];
                break;
            }
        }

        // Check for First Line after Patchers are Loaded so we can Clear the Console and the Replay the Preloader Logs with Formatting and Color
        if (line.Contains("Preloader finished") && init == 0)
        {
            // Step Initialization
            init = 1;

            Console.Clear();

            // Replay Preloader Log
            if (HS_FancierConsole.IsHeadless)
            {
                PreloaderConsoleListener.LogEvents.ToList().ForEach(logEventArgs => new HS_ConsoleLogListener().LogEvent(new ManualLogSource("Preloader"), logEventArgs));
                DisplayBanner(HS_FancierConsole.DefaultColorsBanner[0], HS_FancierConsole.DefaultColorsBanner[1]);
            }
        }

        // Check for Duplicate Preloader finished Line which also signifies the Preloader is finished, so now is a good time to display our banner.
        if (line.Contains("Preloader finished") && init == 1 && !HS_FancierConsole.IsHeadless)
        {
            // Set Init to Fully Initialized
            init = 3;

            // Ensure that the Cursor is in the correct position
            Console.SetCursorPosition(0, Console.CursorTop);

            // Clear the Preloader finished Line
            line = "";
            int bannerID = 0;
            if (HS_FancierConsole.ConfigBannerEnabled.Value)
            {
                if (HS_FancierConsole.ConfigBannerRainbow.Value)
                {
                    // Setup Break out Token
                    cancellationTokenSource = new CancellationTokenSource();
                    var cancellationToken = cancellationTokenSource.Token;

                    // Create Rainbow Intro while waiting for Intro Movies to Finish
                    Thread thread = new Thread(() =>
                        Enumerable.Range(0, int.MaxValue)
                            .Select(_ =>
                            {
                                if (cancellationToken.IsCancellationRequested)
                                    return false;

                                DisplayBanner(bannerID);
                                "Waiting for Intro Movies to Finish\n".PadLeft((Console.WindowWidth + 34) / 2).ForeColor(bannerID / 2).Print();
                                Console.SetCursorPosition(0, Console.CursorTop - 4);

                                bannerID++;
                                if (bannerID > 229) // Only go to 229 because the rest is greyscale
                                    bannerID = 0;

                                Thread.Sleep(300);
                                return true;
                            })
                            .TakeWhile(condition => condition)
                            .ToList()
                    );
                    thread.Start();
                }
                else
                {
                    // Display Normal Banner
                    DisplayBanner(HS_FancierConsole.DefaultColorsBanner[0], HS_FancierConsole.DefaultColorsBanner[1]);
                    "Waiting for Intro Movies to Finish\n".PadLeft((Console.WindowWidth + 34) / 2).ForeColor(HS_FancierConsole.DefaultColorsLoading[0]).BackColor(HS_FancierConsole.DefaultColorsLoading[1]).Print();
                }
            }
        }

        // Check for BepInEx Line to detect when Intro is Finished
        Version version = typeof(Paths).Assembly.GetName().Version;
        if (line.Contains($"BepInEx {version}"))
        {
            // Stop the Rainbow Banner Thread if Enabled
            if (HS_FancierConsole.ConfigBannerRainbow.Value && !HS_FancierConsole.IsHeadless)
            {
                // Request cancellation of the loop thread
                cancellationTokenSource?.Cancel();

                // Wait for the loop thread to finish
                cancellationTokenSource?.Token.WaitHandle.WaitOne();

                // Clean up the CancellationTokenSource
                cancellationTokenSource?.Dispose();
                cancellationTokenSource = null;
            }

            string.Empty.Print();

            // Clear the Console here since it did not get cleared in the Loop if Rainbow Banner is Disabled
            if (!HS_FancierConsole.ConfigBannerRainbow.Value && HS_FancierConsole.ConfigBannerEnabled.Value)
                Console.Clear();

            // Display Intro Banner
            if (HS_FancierConsole.ConfigBannerEnabled.Value && !HS_FancierConsole.IsHeadless)
                DisplayBanner(HS_FancierConsole.DefaultColorsBanner[0], HS_FancierConsole.DefaultColorsBanner[1]);
        }

        // Set Console Title
        if (line.Contains("Chainloader started") && HS_FancierConsole.ChangeTitle.Value)
            SetTitle();

        // Fill lines with whitespace to change background
        new string(' ', Console.WindowWidth - Console.CursorLeft).ForeColor(foregroundColor).BackColor(backgroundColor).Print();
        Console.SetCursorPosition(0, Console.CursorTop);

        // Format the Line
        line = Filter(line);

        // Write the colored line
        line.ForeColor(foregroundColor).BackColor(backgroundColor).Print();
        
    }

    public static string Filter(string text)
    {
        string line = text;
        string dateTimeFormat = HS_FancierConsole.ConfigDateTimeFormat.Value;

        // Check if mod name or other info exists, to include in the output later.
        string pattern = "(?<=:)[^\\]]+(?=\\])";
        string name = Regex.Match(line, pattern).Value.TrimStart();

        // Remove everything between the first set of brackets.
        line = Regex.Replace(line, "\\[.*?\\] ", "");

        // Create a regular expression that matches timestamps "MM/dd/yyyy HH:mm:ss: "
        string regex = @"^([0-9]{2}/[0-9]{2}/[0-9]{4} [0-9]{2}:[0-9]{2}:[0-9]{2}):\s";

        // Check if a timestamp already exists and remove it.
        if (Regex.IsMatch(line, regex))
        {
            // Remove the timestamp from the line
            line = Regex.Replace(line, regex, "");
        }

        // Remove leading spaces from the line that are sometimes left after removing the timestamp
        line = line.TrimStart();
        if (line.EndsWith("\n\r\n"))
            line = line.TrimEnd('\n');

        // Add timestamps and mod names (or other info) back in, to maintain consistency in the log.
        string timestamp = DateTime.Now.ToString(dateTimeFormat);
        if (!string.IsNullOrEmpty(line))
        {
            // Check if modname is Empty
            if (!string.IsNullOrEmpty(name))
            {
                line = $"[{timestamp}]: ({name}) {line}";
            }
            else
            {
                line = $"[{timestamp}]: {line}";
            }
        }

        return line;
    }

    public static void DisplayBanner(object color, string bgColor = "")
    {

        int consoleWidth = Console.WindowWidth;
        int textLength = 62;
        int leftPadding = (consoleWidth - textLength) / 2;

        // Display Banner
        var text1 =
            "╔══════════════════════════════════════════════════════════╗\n".PadLeft(
                leftPadding + textLength);
        var text2 =
            $"║                    Fancier Log v{HS_FancierConsole.ModVersion}                    ║\n"
                .PadLeft(leftPadding + textLength);
        var text3 =
            "╚══════════════════════════════════════════════════════════╝\n".PadLeft(
                leftPadding + textLength);
        if (color is int)
        {
            text1.ForeColor((int)color).Print();
            text2.ForeColor((int)color).Print();
            text3.ForeColor((int)color).Print();

        }
        else if (color is string)
        {
            text1.ForeColor((string)color).BackColor(bgColor).Print();
            text2.ForeColor((string)color).BackColor(bgColor).Print();
            text3.ForeColor((string)color).BackColor(bgColor).Print();
        }
    }

    public static void SetTitle()
    {
        try
        {
            Version version = typeof(Paths).Assembly.GetName().Version;
            var productNameProp = typeof(Application).GetProperty("productName", BindingFlags.Public | BindingFlags.Static);
            ConsoleManager.SetConsoleTitle($"HS Fancier Log v{HS_FancierConsole.ModVersion} - BepInEx {version} - {productNameProp?.GetValue(null, null) ?? Paths.ProcessName}");
        }
        catch (Exception e)
        {
            Debug.unityLogger.LogError("FancierLog", $"Unable to Set Console Title, error: {e}");
        }
    }

    public static void LogStackTrace(string line)
    {
        // Load Config Settings
        var stackTraceBannerFGColor = HS_FancierConsole.DefaultColorsStackTraceBanner[0];
        var stackTraceBannerBGColor = HS_FancierConsole.DefaultColorsStackTraceBanner[1];
        var stackTraceFGColor = HS_FancierConsole.DefaultColorsStackTrace[0];
        var stackTraceBGColor = HS_FancierConsole.DefaultColorsStackTrace[1];
        var exceptionFGColor = HS_FancierConsole.DefaultColorsException[0];
        var exceptionBGColor = HS_FancierConsole.DefaultColorsException[1];
        var exceptionPreFGColor = HS_FancierConsole.DefaultColorsExceptionPre[0];
        var exceptionPreBGColor = HS_FancierConsole.DefaultColorsExceptionPre[1];

        // Display Banner
        "╔══════════════════════════════════════════════════════════╗\n".ForeColor(stackTraceBannerFGColor).BackColor(stackTraceBannerBGColor).Print();
        "║                       Stack Trace                        ║\n".ForeColor(stackTraceBannerFGColor).BackColor(stackTraceBannerBGColor).Print();
        "╚══════════════════════════════════════════════════════════╝\n".ForeColor(stackTraceBannerFGColor).BackColor(stackTraceBannerBGColor).Print();
        string.Empty.Print();

        // Extract Exception Message
        string exceptionMessage = line.Substring(line.IndexOf("Exception:") + "Exception:".Length).Trim();
        int newlineIndex = exceptionMessage.IndexOf('\n');
        if (newlineIndex != -1) { exceptionMessage = exceptionMessage.Substring(0, newlineIndex); }

        // Display Exception Message
        if (exceptionMessage != String.Empty)
            ("Exception: ".Italic().ForeColor(exceptionPreFGColor).BackColor(exceptionPreBGColor) + exceptionMessage.ForeColor(exceptionFGColor).BackColor(exceptionBGColor)).Print();

        // Display Stack Trace
        string[] lines = line.Split(new[] { "Stack trace:" }, StringSplitOptions.RemoveEmptyEntries);
        string[] stackTraceLines = lines.Length > 1 ? lines[1].Split('\n') : new string[0];
        var count = 0;
        foreach (string stackLine in stackTraceLines)
        {
            string prefix = new('\t', count);

            if (count == 0)
                "\n".Print();
            else
                prefix += "\u2191\u2192"; // Arrows

            var sLine = stackLine.Replace("\r", "");
            if (string.IsNullOrEmpty(sLine))
                continue;

            (prefix + stackLine + "\n\r").Underline().ForeColor(stackTraceFGColor).BackColor(stackTraceBGColor).Print();
            count++;
        }
    }
    
    public void Dispose() { }
}