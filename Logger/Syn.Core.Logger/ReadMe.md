# Syn.Core.Logger

![Syn.Core.Logger – Quick Reference](logger-quick-reference.png)


| Method / Property                  | Purpose                                  | Example |
|-------------------------------------|------------------------------------------|---------|
| `ConsoleLog.Info(msg, bg?, prefix?)`    | Info log (cyan)                          | `ConsoleLog.Info("Start");` |
| `ConsoleLog.Warning(msg, bg?, prefix?)` | Warning log (yellow)                     | `ConsoleLog.Warning("Check");` |
| `ConsoleLog.Error(msg, bg?, prefix?)`   | Error log (red)                           | `ConsoleLog.Error("Fail");` |
| `ConsoleLog.Success(msg, bg?, prefix?)` | Success log (green)                       | `ConsoleLog.Success("Done");` |
| `ConsoleLog.Debug(msg, bg?, prefix?)`   | Debug log (gray)                          | `ConsoleLog.Debug("Trace");` |
| `ConsoleLog.Custom(msg, level, fg, bg?, prefix?)` | Custom colors log                | `ConsoleLog.Custom("Custom", LogLevel.Info, ConsoleColor.Magenta);` |
| `"text".LogInfo(bg?, prefix?)`      | String ext. – Info log                    | `"Msg".LogInfo();` |
| `"text".LogWarning(bg?, prefix?)`   | String ext. – Warning log                 | `"Msg".LogWarning();` |
| `"text".LogError(bg?, prefix?)`     | String ext. – Error log                   | `"Msg".LogError();` |
| `"text".LogSuccess(bg?, prefix?)`   | String ext. – Success log                 | `"Msg".LogSuccess();` |
| `"text".LogDebug(bg?, prefix?)`     | String ext. – Debug log                   | `"Msg".LogDebug();` |
| `"text".Log(level, bg?, prefix?)`   | String ext. – Explicit level              | `"Msg".Log(LogLevel.Warning);` |
| `"text".LogCustom(level, fg, bg?, prefix?)` | String ext. – Custom colors         | `"Msg".LogCustom(LogLevel.Error, ConsoleColor.White, ConsoleColor.DarkRed);` |
| `ConsoleLog.LogFilePath`            | File path for log output                  | `ConsoleLog.LogFilePath = "app.log";` |
| `ConsoleLog.EnableTimestamps`       | Show timestamps (bool)                    | `ConsoleLog.EnableTimestamps = true;` |
| `ConsoleLog.UseUtcTime`             | Use UTC timestamps (bool)                 | `ConsoleLog.UseUtcTime = false;` |
| `ConsoleLog.TimestampFormat`        | Timestamp format string                   | `ConsoleLog.TimestampFormat = "yyyy-MM-dd HH:mm:ss";` |
| `ConsoleLog.GlobalPrefix`           | Static prefix for all logs                | `ConsoleLog.GlobalPrefix = "Tenant2";` |
| `ConsoleLog.DynamicPrefixProvider`  | Func<string> for runtime prefix           | `ConsoleLog.DynamicPrefixProvider = () => "Module:Core";` |






| Method / Extension                              | Parameters                                                                                          | Description                                                                                           | Example Usage |
|-------------------------------------------------|-----------------------------------------------------------------------------------------------------|-------------------------------------------------------------------------------------------------------|---------------|
| `ConsoleLog.Info`                               | `string message`, `ConsoleColor? background = null`, `string customPrefix = null`                   | Logs an informational message in cyan.                                                                | `ConsoleLog.Info("Starting migration...");` |
| `ConsoleLog.Warning`                            | `string message`, `ConsoleColor? background = null`, `string customPrefix = null`                   | Logs a warning message in yellow.                                                                     | `ConsoleLog.Warning("PK column type changed");` |
| `ConsoleLog.Error`                              | `string message`, `ConsoleColor? background = null`, `string customPrefix = null`                   | Logs an error message in red.                                                                         | `ConsoleLog.Error("Migration failed!", ConsoleColor.Black);` |
| `ConsoleLog.Success`                            | `string message`, `ConsoleColor? background = null`, `string customPrefix = null`                   | Logs a success message in green.                                                                      | `ConsoleLog.Success("Migration completed successfully");` |
| `ConsoleLog.Debug`                              | `string message`, `ConsoleColor? background = null`, `string customPrefix = null`                   | Logs a debug message in gray.                                                                         | `ConsoleLog.Debug("Checking constraints...");` |
| `ConsoleLog.Custom`                             | `string message`, `LogLevel level`, `ConsoleColor foreground`, `ConsoleColor? background = null`, `string customPrefix = null` | Logs a message with fully custom foreground/background colors.                                        | `ConsoleLog.Custom("Custom log", LogLevel.Info, ConsoleColor.Magenta, ConsoleColor.White);` |
| `string.LogInfo()`                              | `ConsoleColor? background = null`, `string customPrefix = null`                                     | Extension: logs the string as Info.                                                                   | `"Message".LogInfo();` |
| `string.LogWarning()`                           | `ConsoleColor? background = null`, `string customPrefix = null`                                     | Extension: logs the string as Warning.                                                                | `"Message".LogWarning();` |
| `string.LogError()`                             | `ConsoleColor? background = null`, `string customPrefix = null`                                     | Extension: logs the string as Error.                                                                  | `"Message".LogError(ConsoleColor.Black);` |
| `string.LogSuccess()`                           | `ConsoleColor? background = null`, `string customPrefix = null`                                     | Extension: logs the string as Success.                                                                | `"Message".LogSuccess();` |
| `string.LogDebug()`                             | `ConsoleColor? background = null`, `string customPrefix = null`                                     | Extension: logs the string as Debug.                                                                  | `"Message".LogDebug();` |
| `string.Log()`                                  | `LogLevel level`, `ConsoleColor? background = null`, `string customPrefix = null`                   | Extension: logs the string with explicit level.                                                       | `"Message".Log(LogLevel.Warning);` |
| `string.LogCustom()`                            | `LogLevel level`, `ConsoleColor foreground`, `ConsoleColor? background = null`, `string customPrefix = null` | Extension: logs the string with fully custom colors.                                                  | `"Message".LogCustom(LogLevel.Error, ConsoleColor.White, ConsoleColor.DarkRed);` |
| **Global Settings**                             |                                                                                                     |                                                                                                       |               |
| `ConsoleLog.LogFilePath`                        | `string`                                                                                            | If set, all logs are appended to this file (thread-safe).                                             | `ConsoleLog.LogFilePath = "app.log";` |
| `ConsoleLog.EnableTimestamps`                   | `bool`                                                                                              | Enables/disables timestamps in log output.                                                            | `ConsoleLog.EnableTimestamps = true;` |
| `ConsoleLog.UseUtcTime`                         | `bool`                                                                                              | Uses UTC time for timestamps if true; local time if false.                                            | `ConsoleLog.UseUtcTime = false;` |
| `ConsoleLog.TimestampFormat`                    | `string`                                                                                            | Custom timestamp format (default `"HH:mm:ss"`).                                                        | `ConsoleLog.TimestampFormat = "yyyy-MM-dd HH:mm:ss";` |
| `ConsoleLog.GlobalPrefix`                       | `string`                                                                                            | Static prefix added to all log messages.                                                              | `ConsoleLog.GlobalPrefix = "Tenant2";` |
| `ConsoleLog.DynamicPrefixProvider`              | `Func<string>`                                                                                      | Function called before each log to generate a dynamic prefix.                                         | `ConsoleLog.DynamicPrefixProvider = () => $"Module:{GetModuleName()}";` |