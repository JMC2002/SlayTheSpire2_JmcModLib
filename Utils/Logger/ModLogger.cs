using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;
using JmcModLib.Core;
using MegaCrit.Sts2.Core.Logging;

namespace JmcModLib.Utils;

[Flags]
public enum LogPrefixFlags
{
    None = 0,
    Timestamp = 1 << 0,
    Default = Timestamp,
}

public sealed class AssemblyLogConfiguration
{
    public LogLevel MinimumLevel { get; set; } = LogLevel.Info;

    public LogType LogType { get; set; } = LogType.Generic;

    public LogPrefixFlags PrefixFlags { get; set; } = LogPrefixFlags.Default;

    public bool ThrowOnFatal { get; set; } = true;

    public bool IncludeExceptionDetails { get; set; } = true;
}

public readonly record struct LoggerSnapshot(
    LogLevel MinimumLevel,
    LogType LogType,
    LogPrefixFlags PrefixFlags,
    bool ThrowOnFatal,
    bool IncludeExceptionDetails,
    string Context);

/// <summary>
/// Thin wrapper over STS2's logger with per-assembly configuration.
/// </summary>
public static partial class ModLogger
{
    private readonly record struct LoggerHandle(LogType Type, Logger Logger);

    private static readonly object NativeLoggerLock = typeof(Logger)
        .GetField("_lockObj", BindingFlags.NonPublic | BindingFlags.Static)?
        .GetValue(null) as object ?? new object();

    private static readonly ConcurrentDictionary<Assembly, LoggerHandle> Loggers = new();
    private static readonly ConcurrentDictionary<Assembly, AssemblyLogConfiguration> Configurations = new();

    public static LogLevel DefaultLogLevel { get; set; } = LogLevel.Info;

    public static LogType DefaultLogType { get; set; } = LogType.Generic;

    public static LogPrefixFlags DefaultPrefixFlags { get; set; } = LogPrefixFlags.Default;

    public static bool DefaultThrowOnFatal { get; set; } = true;

    public static bool DefaultIncludeExceptionDetails { get; set; } = true;

    public static void RegisterAssembly(
        Assembly? assembly = null,
        LogLevel minimumLevel = LogLevel.Info,
        LogPrefixFlags prefixFlags = LogPrefixFlags.Default,
        bool throwOnFatal = true,
        LogType logType = LogType.Generic,
        bool includeExceptionDetails = true,
        LogConfigUIFlags uiFlags = LogConfigUIFlags.None)
    {
        assembly = ResolveAssembly(assembly);
        AssemblyLogConfiguration configuration = Configurations.GetOrAdd(assembly, _ => CreateDefaultConfiguration());
        configuration.MinimumLevel = minimumLevel;
        configuration.PrefixFlags = prefixFlags;
        configuration.ThrowOnFatal = throwOnFatal;
        configuration.LogType = logType;
        configuration.IncludeExceptionDetails = includeExceptionDetails;

        BuildLoggerUI.BuildUI(assembly, uiFlags);
        _ = GetLogger(assembly);
    }

    public static void UnregisterAssembly(Assembly? assembly = null)
    {
        assembly = ResolveAssembly(assembly);
        Configurations.TryRemove(assembly, out _);
        Loggers.TryRemove(assembly, out _);
    }

    public static LogLevel GetLogLevel(Assembly? assembly = null)
    {
        return GetOrCreateConfiguration(ResolveAssembly(assembly)).MinimumLevel;
    }

    public static void SetLogLevel(LogLevel level, Assembly? assembly = null)
    {
        GetOrCreateConfiguration(ResolveAssembly(assembly)).MinimumLevel = level;
    }

    public static LogType GetLogType(Assembly? assembly = null)
    {
        return GetOrCreateConfiguration(ResolveAssembly(assembly)).LogType;
    }

    public static void SetLogType(LogType logType, Assembly? assembly = null)
    {
        assembly = ResolveAssembly(assembly);
        GetOrCreateConfiguration(assembly).LogType = logType;
        Loggers.TryRemove(assembly, out _);
    }

    public static LogPrefixFlags GetPrefixFlags(Assembly? assembly = null)
    {
        return GetOrCreateConfiguration(ResolveAssembly(assembly)).PrefixFlags;
    }

    public static void SetPrefixFlags(LogPrefixFlags flags, Assembly? assembly = null)
    {
        GetOrCreateConfiguration(ResolveAssembly(assembly)).PrefixFlags = flags;
    }

    public static bool HasPrefixFlag(LogPrefixFlags flag, Assembly? assembly = null)
    {
        return (GetPrefixFlags(assembly) & flag) != 0;
    }

    public static void TogglePrefixFlag(LogPrefixFlags flag, Assembly? assembly = null)
    {
        assembly = ResolveAssembly(assembly);
        AssemblyLogConfiguration configuration = GetOrCreateConfiguration(assembly);
        configuration.PrefixFlags ^= flag;
    }

    public static LoggerSnapshot GetSnapshot(Assembly? assembly = null)
    {
        assembly = ResolveAssembly(assembly);
        AssemblyLogConfiguration configuration = GetOrCreateConfiguration(assembly);
        return new LoggerSnapshot(
            configuration.MinimumLevel,
            configuration.LogType,
            configuration.PrefixFlags,
            configuration.ThrowOnFatal,
            configuration.IncludeExceptionDetails,
            ResolveContext(assembly));
    }

    public static void Load(string message, Assembly? assembly = null)
    {
        Log(LogLevel.Load, message, null, ResolveAssembly(assembly));
    }

    public static void Trace(string message, Assembly? assembly = null)
    {
        Log(LogLevel.VeryDebug, message, null, ResolveAssembly(assembly));
    }

    public static void Debug(string message, Assembly? assembly = null)
    {
        Log(LogLevel.Debug, message, null, ResolveAssembly(assembly));
    }

    public static void Info(string message, Assembly? assembly = null)
    {
        Log(LogLevel.Info, message, null, ResolveAssembly(assembly));
    }

    public static void Warn(string message, Assembly? assembly = null)
    {
        Log(LogLevel.Warn, message, null, ResolveAssembly(assembly));
    }

    public static void Warn(string message, Exception exception, Assembly? assembly = null)
    {
        Log(LogLevel.Warn, message, exception, ResolveAssembly(assembly));
    }

    public static void Error(string message, Assembly? assembly = null)
    {
        Log(LogLevel.Error, message, null, ResolveAssembly(assembly));
    }

    public static void Error(string message, Exception exception, Assembly? assembly = null)
    {
        Log(LogLevel.Error, message, exception, ResolveAssembly(assembly));
    }

    public static void Fatal(Exception exception, string? message = null, Assembly? assembly = null)
    {
        assembly = ResolveAssembly(assembly);
        Log(LogLevel.Error, message ?? "Fatal error.", exception, assembly);
        if (GetOrCreateConfiguration(assembly).ThrowOnFatal)
        {
            throw exception;
        }
    }

    private static void Log(LogLevel level, string? message, Exception? exception, Assembly assembly)
    {
        AssemblyLogConfiguration configuration = GetOrCreateConfiguration(assembly);
        if (!ShouldLog(level, configuration))
        {
            return;
        }

        Logger logger = GetLogger(assembly);
        string text = Format(message, assembly, exception, configuration);

        lock (NativeLoggerLock)
        {
            LogType logType = configuration.LogType;
            bool hadOverride = Logger.logLevelTypeMap.TryGetValue(logType, out LogLevel previousLevel);
            LogLevel currentThreshold = hadOverride ? previousLevel : Logger.GlobalLogLevel;
            bool shouldTemporarilyLowerThreshold = level < currentThreshold;

            if (shouldTemporarilyLowerThreshold)
            {
                Logger.SetLogLevelForType(logType, level);
            }

            try
            {
                logger.LogMessage(level, text, skipFrames: 2);
            }
            finally
            {
                if (shouldTemporarilyLowerThreshold)
                {
                    Logger.SetLogLevelForType(logType, hadOverride ? previousLevel : null);
                }
            }
        }
    }

    private static bool ShouldLog(LogLevel level, AssemblyLogConfiguration configuration)
    {
        return level >= configuration.MinimumLevel;
    }

    private static Logger GetLogger(Assembly assembly)
    {
        AssemblyLogConfiguration configuration = GetOrCreateConfiguration(assembly);
        string context = ResolveContext(assembly);

        LoggerHandle handle = Loggers.AddOrUpdate(
            assembly,
            _ => CreateLoggerHandle(context, configuration.LogType),
            (_, existing) => existing.Type == configuration.LogType
                ? UpdateLoggerHandle(existing, context)
                : CreateLoggerHandle(context, configuration.LogType));

        return handle.Logger;
    }

    private static LoggerHandle CreateLoggerHandle(string context, LogType logType)
    {
        return new LoggerHandle(logType, new Logger(context, logType));
    }

    private static LoggerHandle UpdateLoggerHandle(LoggerHandle existing, string context)
    {
        existing.Logger.Context = context;
        return existing;
    }

    private static AssemblyLogConfiguration GetOrCreateConfiguration(Assembly assembly)
    {
        return Configurations.GetOrAdd(assembly, _ => CreateDefaultConfiguration());
    }

    private static AssemblyLogConfiguration CreateDefaultConfiguration()
    {
        return new AssemblyLogConfiguration
        {
            MinimumLevel = DefaultLogLevel,
            LogType = DefaultLogType,
            PrefixFlags = DefaultPrefixFlags,
            ThrowOnFatal = DefaultThrowOnFatal,
            IncludeExceptionDetails = DefaultIncludeExceptionDetails,
        };
    }

    private static string ResolveContext(Assembly assembly)
    {
        if (ModRegistry.TryGetContext(out ModContext? context, assembly) && context != null)
        {
            return context.LoggerContext;
        }

        string name = VersionInfo.GetName(assembly);
        string version = VersionInfo.GetVersion(assembly);
        return string.IsNullOrWhiteSpace(version) ? name : $"{name} v{version}";
    }

    private static string Format(string? message, Assembly assembly, Exception? exception, AssemblyLogConfiguration configuration)
    {
        var builder = new System.Text.StringBuilder();

        if ((configuration.PrefixFlags & LogPrefixFlags.Timestamp) != 0)
        {
            builder.Append('[');
            builder.Append(DateTime.Now.ToString("HH:mm:ss"));
            builder.Append("] ");
        }

        if (!string.IsNullOrWhiteSpace(message))
        {
            builder.Append(message);
        }

        if (exception != null)
        {
            if (builder.Length > 0)
            {
                builder.AppendLine();
            }

            builder.Append(configuration.IncludeExceptionDetails ? exception.ToString() : exception.Message);
        }

        if (builder.Length == 0)
        {
            builder.Append(ModRegistry.GetTag(assembly));
        }

        return builder.ToString();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static Assembly ResolveAssembly(Assembly? assembly)
    {
        return assembly ?? Assembly.GetCallingAssembly();
    }
}
