namespace Syn.Core.Logger;

/// <summary>
/// String extensions to log messages directly from string literals or variables.
/// </summary>
public static class StringLogExtensions
{
    public static void LogInfo(this string message, ConsoleColor? background = null, string customPrefix = null) =>
        ConsoleLog.Info(message, background, customPrefix);

    public static void LogWarning(this string message, ConsoleColor? background = null, string customPrefix = null) =>
        ConsoleLog.Warning(message, background, customPrefix);

    public static void LogError(this string message, ConsoleColor? background = null, string customPrefix = null) =>
        ConsoleLog.Error(message, background, customPrefix);

    public static void LogSuccess(this string message, ConsoleColor? background = null, string customPrefix = null) =>
        ConsoleLog.Success(message, background, customPrefix);

    public static void LogDebug(this string message, ConsoleColor? background = null, string customPrefix = null) =>
        ConsoleLog.Debug(message, background, customPrefix);

    /// <summary>
    /// Logs with explicit level and optional background color.
    /// </summary>
    public static void Log(this string message, LogLevel level, ConsoleColor? background = null, string customPrefix = null)
    {
        switch (level)
        {
            case LogLevel.Info:
                ConsoleLog.Info(message, background, customPrefix);
                break;
            case LogLevel.Warning:
                ConsoleLog.Warning(message, background, customPrefix);
                break;
            case LogLevel.Error:
                ConsoleLog.Error(message, background, customPrefix);
                break;
            case LogLevel.Success:
                ConsoleLog.Success(message, background, customPrefix);
                break;
            case LogLevel.Debug:
                ConsoleLog.Debug(message, background, customPrefix);
                break;
            default:
                ConsoleLog.Info(message, background, customPrefix);
                break;
        }
    }

    /// <summary>
    /// Logs with fully custom foreground and background colors.
    /// </summary>
    public static void LogCustom(this string message, LogLevel level, ConsoleColor foreground, ConsoleColor? background = null, string customPrefix = null) =>
        ConsoleLog.Custom(message, level, foreground, background, customPrefix);
}
