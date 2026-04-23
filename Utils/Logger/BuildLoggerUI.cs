using System.Reflection;

namespace JmcModLib.Core;

/// <summary>
/// Reserved hook point for future logger UI integration.
/// </summary>
[Flags]
public enum LogConfigUIFlags
{
    None = 0,
    LogLevel = 1 << 0,
    PrefixFlags = 1 << 1,
    TestButtons = 1 << 2,
    Default = LogLevel | PrefixFlags,
    All = LogLevel | PrefixFlags | TestButtons,
}

internal static class BuildLoggerUI
{
    internal static void BuildUI(Assembly assembly, LogConfigUIFlags flags)
    {
        _ = assembly;
        _ = flags;
    }
}
