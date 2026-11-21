using BepInEx.Logging;
using System;
using UnityEngine;


namespace HS_FancierConsole;

public class HS_UnityLogSource : ILogSource
{
    public string SourceName { get; } = "Unity Log";

    public event EventHandler<LogEventArgs> LogEvent;

    public void Dispose()
    {

    }
}

internal sealed class HS_DebugLogHandler : ILogHandler
{
    internal static void Internal_Log(LogType type, LogOption options, string msg, UnityEngine.Object obj)
    {
        LogLevel level;
        switch (type)
        {
            case LogType.Error:
            case LogType.Assert:
            case LogType.Exception:
                level = LogLevel.Error;
                break;
            case LogType.Warning:
                level = LogLevel.Warning;
                break;
            default:
                level = LogLevel.Info;
                break;
        }
        HS_FancierConsole.Listener?.LogEvent("Unity Log", new LogEventArgs(msg, level, new HS_UnityLogSource()));
    }

    internal static void Internal_LogException(Exception ex, UnityEngine.Object obj)
    {
        var msg = "\nStack trace:\n" + ex.StackTrace;
        HS_FancierConsole.Listener?.LogEvent("Unity Log", new LogEventArgs(msg, LogLevel.Error, new HS_UnityLogSource()));
    }

    public void LogFormat(LogType logType, UnityEngine.Object context, string format, params object[] args)
    {
        Internal_Log(logType, LogOption.None, string.Format(format, args), context);
    }

    public void LogFormat(LogType logType, LogOption logOptions, UnityEngine.Object context, string format, params object[] args)
    {
        Internal_Log(logType, logOptions, string.Format(format, args), context);
    }

    public void LogException(Exception exception, UnityEngine.Object context)
    {
        bool flag = exception == null;
        if (flag)
        {
            throw new ArgumentNullException("exception");
        }
        Internal_LogException(exception, context);
    }
}