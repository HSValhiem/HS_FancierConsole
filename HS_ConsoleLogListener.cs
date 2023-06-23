using System;
using System.Text.RegularExpressions;
using BepInEx;
using BepInEx.Logging;

namespace HS_FancierConsole;


// Custom ConsoleLogListener to Implement Colored Console Text and Regex Formatting
public class HS_ConsoleLogListener : ILogListener
{
    public void LogEvent(object sender, LogEventArgs eventArgs)
    {
        // Handle Unity Logs
        if (!HS_FancierConsole.ConfigLogUnity.Value && sender is UnityLogSource) return;

        // Handle Log Level
        if ((eventArgs.Level & HS_FancierConsole.ConfigConsoleDisplayedLevel.Value) == 0) return;

        // Get raw console line to work with
        var line = eventArgs.ToStringLine();

        // Make Stake Trace Pretty
        if (line.Contains("Stack trace:") && HS_FancierConsole.ConfigEnablePrettyStackTrace.Value)
        {
            LogStackTrace(line);
            return;
        }

        // set Date Time Format and Default Colors
        var dateTimeFormat = HS_FancierConsole.ConfigDateTimeFormat.Value;
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

            // Check if Mod is Enabled
            if (HS_FancierConsole.ModEnabled.Value)
            {
                // Write the colored line
                line.ForeColor(foregroundColor).BackColor(backgroundColor).Print();
            }
            else
            {
                // Write Default BepInEx Non Colored Line
                ConsoleManager.SetConsoleColor(eventArgs.Level.GetConsoleColor());
                ConsoleManager.ConsoleStream?.Write(eventArgs.ToStringLine());
                ConsoleManager.SetConsoleColor(ConsoleColor.Gray);
            }
        }
    }

    public void LogStackTrace(string line)
    {
        // Load Config Settings
        var stackTraceBannerFGColor = HS_FancierConsole.DefaultColorsExceptionPre[0];
        var stackTraceBannerBGColor = HS_FancierConsole.DefaultColorsExceptionPre[1];
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
        if(exceptionMessage != String.Empty)
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
                prefix += "\u2191\u2192";

            var sLine = stackLine.Replace("\r", "");
            if (string.IsNullOrEmpty(sLine))
                continue;


            (prefix + stackLine + "\n\r").Underline().ForeColor(stackTraceFGColor).BackColor(stackTraceBGColor).Print();
            count++;
        }
    }

    public void Dispose() { }
}