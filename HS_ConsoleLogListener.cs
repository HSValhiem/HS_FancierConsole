using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Preloader;

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

        var line = eventArgs.ToStringLine();


        // Always Remove Duplicates
        if (line == lastLine) return;
        lastLine = line;

        // Don't Handle Unity Logs in Client because of out Custom Debug Log Override, but if Headless it doesn't care so we Allow it.
        if (eventArgs.Source.SourceName != "Unity Log" || HS_FancierConsole.IsHeadless)
        {
            // Make Stake Trace Pretty
            if (line.Contains("Stack trace:") && HS_FancierConsole.ConfigEnablePrettyStackTrace.Value)
            {
                LogStackTrace(line);
                return;
            }

            // Set Default Colors
            var foregroundColor = HS_FancierConsole.DefaultColors[0];
            var backgroundColor = HS_FancierConsole.DefaultColors[1];

            // Setup Colors from Color Map
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

            // Check for First Like after Patchers are Loaded.
            if (line.Contains("Preloader finished") && init == 0)
            {
                Console.Clear();
                if (HS_FancierConsole.IsHeadless)
                    PreloaderConsoleListener.LogEvents.ToList().ForEach(logEventArgs => new HS_ConsoleLogListener().LogEvent(new ManualLogSource("Preloader"), logEventArgs));
                init = 1;
            }

            if (line.Contains("Preloader finished") && init == 1)
            { 
                int bannerID = 0;
                Console.SetCursorPosition(0, Console.CursorTop);
                line = "";
                init = 3;
                cancellationTokenSource = new CancellationTokenSource();
                var cancellationToken = cancellationTokenSource.Token;
                // Create a new thread and start the loop
                Thread thread = new Thread(() =>
                    Enumerable.Range(0, int.MaxValue)
                        .Select(_ =>
                        {
                            if (cancellationToken.IsCancellationRequested)
                                return false;

                            int consoleWidth = Console.WindowWidth;
                            int textLength = 64;
                            int leftPadding = (consoleWidth - textLength) / 2;


                            // Display Banner
                            var text1 = "╔══════════════════════════════════════════════════════════╗\n".PadLeft(leftPadding + textLength);
                            var text2 = $"║                     Fancier Log v{HS_FancierConsole.ModVersion}                   ║\n".PadLeft(leftPadding + textLength);
                            var text3 = "╚══════════════════════════════════════════════════════════╝\n".PadLeft(leftPadding + textLength);

                            text1.ForeColor(bannerID).Print();
                            text2.ForeColor(bannerID).Print();
                            text3.ForeColor(bannerID).Print();
                            "Waiting for Intro Movies to Finish\n".PadLeft(leftPadding + 50).ForeColor(bannerID / 2).Print();
                            Console.SetCursorPosition(0, Console.CursorTop - 4);


                            bannerID++;

                            if (bannerID > 255)
                                bannerID = 0;
                            // Sleep for a short duration to avoid high CPU usage
                            Thread.Sleep(300); // Sleep for 1 second

                            return true;
                        })
                        .TakeWhile(condition => condition)
                        .ToList()
                );
                thread.Start();
            }


            Version version = typeof(Paths).Assembly.GetName().Version;
            if (line.Contains($"BepInEx {version}"))
            { ;
                init = 2;
                // Request cancellation of the loop thread
                cancellationTokenSource?.Cancel();

                // Wait for the loop thread to finish
                cancellationTokenSource?.Token.WaitHandle.WaitOne();

                // Clean up the CancellationTokenSource
                cancellationTokenSource?.Dispose();
                cancellationTokenSource = null;
                StartFancierLog();

            }

            // Fill lines with whitespace to change background
            new string(' ', Console.WindowWidth - Console.CursorLeft).ForeColor(foregroundColor).BackColor(backgroundColor).Print();
            Console.SetCursorPosition(0, Console.CursorTop);

            // Format the Line
            line = Filter(line);

            // Write the colored line
            line.ForeColor(foregroundColor).BackColor(backgroundColor).Print();

        }
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

    public static void StartFancierLog()
    {
        Console.Clear();
        int consoleWidth = Console.WindowWidth;
        int textLength = 64;
        int leftPadding = (consoleWidth - textLength) / 2;

        var foregroundColor = HS_FancierConsole.DefaultColors[0];
        var backgroundColor = HS_FancierConsole.DefaultColors[1];

        System.Random random = new System.Random();
        int bannerID = random.Next(1, 229);
        // Display Banner
        new string(' ', Console.WindowWidth - Console.CursorLeft).ForeColor(bannerID).BackColor(backgroundColor).Print();
        Console.SetCursorPosition(0, Console.CursorTop);
        "╔══════════════════════════════════════════════════════════╗\n".PadLeft(leftPadding + textLength).ForeColor(bannerID).BackColor(backgroundColor).Print();
        new string(' ', Console.WindowWidth - Console.CursorLeft).ForeColor(bannerID).BackColor(backgroundColor).Print();
        Console.SetCursorPosition(0, Console.CursorTop);
        $"║                     Fancier Log v{HS_FancierConsole.ModVersion}                   ║\n".PadLeft(leftPadding + textLength).ForeColor(bannerID).BackColor(backgroundColor).Print();
        new string(' ', Console.WindowWidth - Console.CursorLeft).ForeColor(bannerID).BackColor(backgroundColor).Print();
        Console.SetCursorPosition(0, Console.CursorTop);
        "╚══════════════════════════════════════════════════════════╝\n".PadLeft(leftPadding + textLength).ForeColor(bannerID).BackColor(backgroundColor).Print();


        string.Empty.Print();

        // Replay Preloader Log
        if (HS_FancierConsole.IsHeadless)
            PreloaderConsoleListener.LogEvents.ToList().ForEach(logEventArgs => new HS_ConsoleLogListener().LogEvent(new ManualLogSource("Preloader"), logEventArgs));
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