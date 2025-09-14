namespace Syn.Core.Logger;

/// <summary>
/// Supported log levels for console output.
/// </summary>
public enum LogLevel
{
    Info,
    Warning,
    Error,
    Success,
    Debug
}

/// <summary>
/// Static console logger with:
/// - Colored output per log level
/// - Optional background color
/// - Optional global static prefix
/// - Optional global dynamic prefix (Func<string>)
/// - Optional per-call custom prefix
/// - Optional file logging (thread-safe)
/// - Timestamp support (local or UTC)
/// - Custom foreground/background colors per call
/// </summary>
public static class ConsoleLog
{
    private static readonly object _fileLock = new();

    /// <summary>
    /// Optional log file path. If set, all logs will also be appended to this file (thread-safe).
    /// </summary>
    public static string LogFilePath { get; set; }

    /// <summary>
    /// Enables timestamps prefix for each line. Default: true.
    /// </summary>
    public static bool EnableTimestamps { get; set; } = true;

    /// <summary>
    /// When true, timestamps use UTC time; otherwise local time. Default: false (local).
    /// </summary>
    public static bool UseUtcTime { get; set; } = false;

    /// <summary>
    /// Timestamp format used when EnableTimestamps is true. Default: "HH:mm:ss".
    /// </summary>
    public static string TimestampFormat { get; set; } = "HH:mm:ss";

    /// <summary>
    /// Optional global static prefix (e.g., module name, tenant). Appears before every log level.
    /// </summary>
    public static string GlobalPrefix { get; set; }

    /// <summary>
    /// Optional global dynamic prefix generator. If set, called before each log to get the prefix.
    /// </summary>
    public static Func<string> DynamicPrefixProvider { get; set; }

    /// <summary>Logs an informational message (cyan by default).</summary>
    public static void Info(string message, ConsoleColor? background = null, string customPrefix = null) =>
        Write(message, LogLevel.Info, ConsoleColor.Cyan, background, customPrefix);

    /// <summary>Logs a warning message (yellow by default).</summary>
    public static void Warning(string message, ConsoleColor? background = null, string customPrefix = null) =>
        Write(message, LogLevel.Warning, ConsoleColor.Yellow, background, customPrefix);

    /// <summary>Logs an error message (red by default).</summary>
    public static void Error(string message, ConsoleColor? background = null, string customPrefix = null) =>
        Write(message, LogLevel.Error, ConsoleColor.Red, background, customPrefix);

    /// <summary>Logs a success message (green by default).</summary>
    public static void Success(string message, ConsoleColor? background = null, string customPrefix = null) =>
        Write(message, LogLevel.Success, ConsoleColor.Green, background, customPrefix);

    /// <summary>Logs a debug message (gray by default).</summary>
    public static void Debug(string message, ConsoleColor? background = null, string customPrefix = null) =>
        Write(message, LogLevel.Debug, ConsoleColor.Gray, background, customPrefix);

    /// <summary>
    /// Logs a message with fully custom foreground and background colors, bypassing defaults.
    /// </summary>
    public static void Custom(string message, LogLevel level, ConsoleColor foreground, ConsoleColor? background = null, string customPrefix = null) =>
        Write(message, level, foreground, background, customPrefix);

    public static void Log(string message, LogLevel level, ConsoleColor? background = null, string customPrefix = null)
    {
        switch (level)
        {
            case LogLevel.Info:
                Info(message, background, customPrefix);
                break;
            case LogLevel.Warning:
                Warning(message, background, customPrefix);
                break;
            case LogLevel.Error:
                Error(message, background, customPrefix);
                break;
            case LogLevel.Success:
                Success(message, background, customPrefix);
                break;
            case LogLevel.Debug:
                Debug(message, background, customPrefix);
                break;
            default:
                Info(message, background, customPrefix);
                break;
        }
    }

    /// <summary>
    /// Core writer: prints multi-line text with consistent prefix and colors, then optionally writes to file.
    /// </summary>
    private static void Write(string message, LogLevel level, ConsoleColor fg, ConsoleColor? bg = null, string customPrefix = null)
    {
        var lines = (message ?? string.Empty).Replace("\r\n", "\n").Split('\n');
        var now = UseUtcTime ? DateTime.UtcNow : DateTime.Now;

        // Build prefix: [time] [GlobalPrefix] [DynamicPrefix] [CustomPrefix] [LEVEL]
        string prefix = "";
        if (EnableTimestamps)
            prefix += $"[{now.ToString(TimestampFormat)}] ";

        if (!string.IsNullOrWhiteSpace(GlobalPrefix))
            prefix += $"[{GlobalPrefix}] ";

        var dynamicPrefix = DynamicPrefixProvider?.Invoke();
        if (!string.IsNullOrWhiteSpace(dynamicPrefix))
            prefix += $"[{dynamicPrefix}] ";

        if (!string.IsNullOrWhiteSpace(customPrefix))
            prefix += $"[{customPrefix}] ";

        prefix += $"[{level.ToString().ToUpper()}] ";

        var originalFg = Console.ForegroundColor;
        var originalBg = Console.BackgroundColor;

        Console.ForegroundColor = fg;
        if (bg.HasValue) Console.BackgroundColor = bg.Value;

        foreach (var line in lines)
            Console.WriteLine(prefix + line);

        Console.ForegroundColor = originalFg;
        Console.BackgroundColor = originalBg;

        if (!string.IsNullOrWhiteSpace(LogFilePath))
        {
            var toWrite = string.Join(Environment.NewLine, lines.Select(l => prefix + l)) + Environment.NewLine;
            try
            {
                lock (_fileLock)
                {
                    File.AppendAllText(LogFilePath, toWrite);
                }
            }
            catch
            {
                // Ignore file write errors
            }
        }
    }
}

