using MegaCrit.Sts2.Core.Logging;
using System.Collections.Concurrent;
using System.Reflection;

namespace JmcModLib.Utils;

/// <summary>
/// 控制 JML 日志前缀的附加内容。
/// </summary>
[Flags]
public enum LogPrefixFlags
{
    /// <summary>
    /// 不添加额外前缀。
    /// </summary>
    None = 0,

    /// <summary>
    /// 在日志正文前添加当前本地时间。
    /// </summary>
    Timestamp = 1 << 0,

    /// <summary>
    /// 默认前缀格式。
    /// </summary>
    Default = Timestamp,
}

/// <summary>
/// 指定程序集的 JML 日志配置。
/// </summary>
public sealed class AssemblyLogConfiguration
{
    /// <summary>
    /// 传给 STS2 原生日志系统的日志类型；显示等级由游戏原生 <c>log</c> 命令控制。
    /// </summary>
    public LogType LogType { get; set; } = LogType.Generic;

    /// <summary>
    /// 日志正文前附加的 JML 前缀格式。
    /// </summary>
    public LogPrefixFlags PrefixFlags { get; set; } = LogPrefixFlags.Default;

    /// <summary>
    /// 调用 <see cref="ModLogger.Fatal(Exception, string?, Assembly?)"/> 时是否重新抛出异常。
    /// </summary>
    public bool ThrowOnFatal { get; set; } = true;

    /// <summary>
    /// 记录异常时是否输出完整异常详情；关闭时仅输出异常消息。
    /// </summary>
    public bool IncludeExceptionDetails { get; set; } = true;
}

/// <summary>
/// 指定程序集当前日志配置的只读快照。
/// </summary>
/// <param name="LogType">传给 STS2 原生日志系统的日志类型。</param>
/// <param name="PrefixFlags">日志正文前附加的 JML 前缀格式。</param>
/// <param name="ThrowOnFatal">调用 Fatal 时是否重新抛出异常。</param>
/// <param name="IncludeExceptionDetails">记录异常时是否输出完整异常详情。</param>
/// <param name="Context">传给 STS2 原生日志器的上下文名称。</param>
public readonly record struct LoggerSnapshot(
    LogType LogType,
    LogPrefixFlags PrefixFlags,
    bool ThrowOnFatal,
    bool IncludeExceptionDetails,
    string Context);

/// <summary>
/// JML 对 STS2 原生日志器的轻量封装，按程序集隔离日志上下文、类型和格式。
/// </summary>
public static partial class ModLogger
{
    private readonly record struct LoggerHandle(LogType Type, Logger Logger);

    private static readonly ConcurrentDictionary<Assembly, LoggerHandle> Loggers = new();
    private static readonly ConcurrentDictionary<Assembly, AssemblyLogConfiguration> Configurations = new();

    /// <summary>
    /// 新程序集日志配置默认使用的 STS2 日志类型。
    /// </summary>
    public static LogType DefaultLogType { get; set; } = LogType.Generic;

    /// <summary>
    /// 新程序集日志配置默认使用的前缀格式。
    /// </summary>
    public static LogPrefixFlags DefaultPrefixFlags { get; set; } = LogPrefixFlags.Default;

    /// <summary>
    /// 新程序集日志配置默认的 Fatal 重新抛出行为。
    /// </summary>
    public static bool DefaultThrowOnFatal { get; set; } = true;

    /// <summary>
    /// 新程序集日志配置默认的异常详情输出行为。
    /// </summary>
    public static bool DefaultIncludeExceptionDetails { get; set; } = true;

    /// <summary>
    /// 为指定程序集注册或更新日志配置，并创建按程序集隔离的原生日志器。
    /// </summary>
    /// <param name="assembly">目标程序集；留空时自动解析调用方程序集。</param>
    /// <param name="prefixFlags">日志前缀格式。</param>
    /// <param name="throwOnFatal">调用 <see cref="Fatal(Exception, string?, Assembly?)"/> 时是否重新抛出异常。</param>
    /// <param name="logType">传给 STS2 原生日志系统的日志类型。</param>
    /// <param name="includeExceptionDetails">输出异常时是否包含完整异常详情。</param>
    /// <remarks>
    /// 子 MOD 入口通常不需要手动调用本方法，<see cref="ModRegistry.Register(string, string?, string?, Assembly?)"/>
    /// 和相关重载会自动注册默认日志配置。需要覆盖日志类型或格式时再显式调用。
    /// 日志显示等级由游戏原生 <c>log</c> 命令控制。
    /// </remarks>
    /// <example>
    /// <code><![CDATA[
    /// ModLogger.RegisterAssembly(logType: LogType.Generic);
    /// ]]></code>
    /// </example>
    public static void RegisterAssembly(
        Assembly? assembly = null,
        LogPrefixFlags prefixFlags = LogPrefixFlags.Default,
        bool throwOnFatal = true,
        LogType logType = LogType.Generic,
        bool includeExceptionDetails = true)
    {
        assembly = ResolveAssembly(assembly);
        AssemblyLogConfiguration configuration = Configurations.GetOrAdd(assembly, _ => CreateDefaultConfiguration());
        configuration.PrefixFlags = prefixFlags;
        configuration.ThrowOnFatal = throwOnFatal;
        configuration.LogType = logType;
        configuration.IncludeExceptionDetails = includeExceptionDetails;

        _ = GetLogger(assembly);
    }

    /// <summary>
    /// 清理指定程序集的日志配置和已创建的原生日志器。
    /// </summary>
    /// <param name="assembly">目标程序集；留空时自动解析调用方程序集。</param>
    public static void UnregisterAssembly(Assembly? assembly = null)
    {
        assembly = ResolveAssembly(assembly);
        Configurations.TryRemove(assembly, out _);
        Loggers.TryRemove(assembly, out _);
    }

    /// <summary>
    /// 获取指定程序集当前使用的 STS2 日志类型。
    /// </summary>
    /// <param name="assembly">目标程序集；留空时自动解析调用方程序集。</param>
    /// <returns>当前日志类型。</returns>
    public static LogType GetLogType(Assembly? assembly = null)
    {
        return GetOrCreateConfiguration(ResolveAssembly(assembly)).LogType;
    }

    /// <summary>
    /// 设置指定程序集使用的 STS2 日志类型。
    /// </summary>
    /// <param name="logType">新的日志类型。</param>
    /// <param name="assembly">目标程序集；留空时自动解析调用方程序集。</param>
    public static void SetLogType(LogType logType, Assembly? assembly = null)
    {
        assembly = ResolveAssembly(assembly);
        GetOrCreateConfiguration(assembly).LogType = logType;
        Loggers.TryRemove(assembly, out _);
    }

    /// <summary>
    /// 获取指定程序集当前使用的日志前缀格式。
    /// </summary>
    /// <param name="assembly">目标程序集；留空时自动解析调用方程序集。</param>
    /// <returns>当前前缀格式。</returns>
    public static LogPrefixFlags GetPrefixFlags(Assembly? assembly = null)
    {
        return GetOrCreateConfiguration(ResolveAssembly(assembly)).PrefixFlags;
    }

    /// <summary>
    /// 设置指定程序集使用的日志前缀格式。
    /// </summary>
    /// <param name="flags">新的前缀格式。</param>
    /// <param name="assembly">目标程序集；留空时自动解析调用方程序集。</param>
    public static void SetPrefixFlags(LogPrefixFlags flags, Assembly? assembly = null)
    {
        GetOrCreateConfiguration(ResolveAssembly(assembly)).PrefixFlags = flags;
    }

    /// <summary>
    /// 检查指定程序集的日志前缀是否包含某个标记。
    /// </summary>
    /// <param name="flag">需要检查的前缀标记。</param>
    /// <param name="assembly">目标程序集；留空时自动解析调用方程序集。</param>
    /// <returns>包含该标记时返回 <c>true</c>。</returns>
    public static bool HasPrefixFlag(LogPrefixFlags flag, Assembly? assembly = null)
    {
        return (GetPrefixFlags(assembly) & flag) != 0;
    }

    /// <summary>
    /// 切换指定程序集的某个日志前缀标记。
    /// </summary>
    /// <param name="flag">需要切换的前缀标记。</param>
    /// <param name="assembly">目标程序集；留空时自动解析调用方程序集。</param>
    public static void TogglePrefixFlag(LogPrefixFlags flag, Assembly? assembly = null)
    {
        assembly = ResolveAssembly(assembly);
        AssemblyLogConfiguration configuration = GetOrCreateConfiguration(assembly);
        configuration.PrefixFlags ^= flag;
    }

    /// <summary>
    /// 获取指定程序集当前日志配置的快照。
    /// </summary>
    /// <param name="assembly">目标程序集；留空时自动解析调用方程序集。</param>
    /// <returns>当前配置快照。</returns>
    public static LoggerSnapshot GetSnapshot(Assembly? assembly = null)
    {
        assembly = ResolveAssembly(assembly);
        AssemblyLogConfiguration configuration = GetOrCreateConfiguration(assembly);
        return new LoggerSnapshot(
            configuration.LogType,
            configuration.PrefixFlags,
            configuration.ThrowOnFatal,
            configuration.IncludeExceptionDetails,
            ResolveContext(assembly));
    }

    /// <summary>
    /// 输出 STS2 <see cref="LogLevel.Load"/> 等级日志。
    /// </summary>
    /// <param name="message">日志正文。</param>
    /// <param name="assembly">日志归属程序集；留空时自动解析调用方程序集。</param>
    public static void Load(string message, Assembly? assembly = null)
    {
        Log(LogLevel.Load, message, null, ResolveAssembly(assembly));
    }

    /// <summary>
    /// 输出 STS2 <see cref="LogLevel.VeryDebug"/> 等级日志。
    /// </summary>
    /// <param name="message">日志正文。</param>
    /// <param name="assembly">日志归属程序集；留空时自动解析调用方程序集。</param>
    public static void Trace(string message, Assembly? assembly = null)
    {
        Log(LogLevel.VeryDebug, message, null, ResolveAssembly(assembly));
    }

    /// <summary>
    /// 输出 STS2 <see cref="LogLevel.Debug"/> 等级日志。
    /// </summary>
    /// <param name="message">日志正文。</param>
    /// <param name="assembly">日志归属程序集；留空时自动解析调用方程序集。</param>
    public static void Debug(string message, Assembly? assembly = null)
    {
        Log(LogLevel.Debug, message, null, ResolveAssembly(assembly));
    }

    /// <summary>
    /// 输出 STS2 <see cref="LogLevel.Info"/> 等级日志。
    /// </summary>
    /// <param name="message">日志正文。</param>
    /// <param name="assembly">日志归属程序集；留空时自动解析调用方程序集。</param>
    public static void Info(string message, Assembly? assembly = null)
    {
        Log(LogLevel.Info, message, null, ResolveAssembly(assembly));
    }

    /// <summary>
    /// 输出 STS2 <see cref="LogLevel.Warn"/> 等级日志。
    /// </summary>
    /// <param name="message">日志正文。</param>
    /// <param name="assembly">日志归属程序集；留空时自动解析调用方程序集。</param>
    public static void Warn(string message, Assembly? assembly = null)
    {
        Log(LogLevel.Warn, message, null, ResolveAssembly(assembly));
    }

    /// <summary>
    /// 输出带异常信息的 STS2 <see cref="LogLevel.Warn"/> 等级日志。
    /// </summary>
    /// <param name="message">日志正文。</param>
    /// <param name="exception">需要记录的异常。</param>
    /// <param name="assembly">日志归属程序集；留空时自动解析调用方程序集。</param>
    public static void Warn(string message, Exception exception, Assembly? assembly = null)
    {
        Log(LogLevel.Warn, message, exception, ResolveAssembly(assembly));
    }

    /// <summary>
    /// 输出 STS2 <see cref="LogLevel.Error"/> 等级日志。
    /// </summary>
    /// <param name="message">日志正文。</param>
    /// <param name="assembly">日志归属程序集；留空时自动解析调用方程序集。</param>
    public static void Error(string message, Assembly? assembly = null)
    {
        Log(LogLevel.Error, message, null, ResolveAssembly(assembly));
    }

    /// <summary>
    /// 输出带异常信息的 STS2 <see cref="LogLevel.Error"/> 等级日志。
    /// </summary>
    /// <param name="message">日志正文。</param>
    /// <param name="exception">需要记录的异常。</param>
    /// <param name="assembly">日志归属程序集；留空时自动解析调用方程序集。</param>
    public static void Error(string message, Exception exception, Assembly? assembly = null)
    {
        Log(LogLevel.Error, message, exception, ResolveAssembly(assembly));
    }

    /// <summary>
    /// 输出错误日志，并按配置决定是否重新抛出异常。
    /// </summary>
    /// <param name="exception">需要记录的异常。</param>
    /// <param name="message">可选日志正文；为空时使用默认 Fatal 文本。</param>
    /// <param name="assembly">日志归属程序集；留空时自动解析调用方程序集。</param>
    /// <exception cref="Exception">当当前程序集配置启用 ThrowOnFatal 时重新抛出 <paramref name="exception"/>。</exception>
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
        Logger logger = GetLogger(assembly);
        string text = Format(message, assembly, exception, configuration);
        logger.LogMessage(level, text, skipFrames: 2);
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

    private static Assembly ResolveAssembly(Assembly? assembly)
    {
        return AssemblyResolver.Resolve(assembly, typeof(ModLogger));
    }
}
