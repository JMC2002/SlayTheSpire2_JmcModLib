using MegaCrit.Sts2.Core.Logging;

namespace JmcModLib.Utils;

internal static class BuildLogLevelSettings
{
    private static readonly LogLevel[] SupportedLevels =
    [
        LogLevel.VeryDebug,
        LogLevel.Load,
        LogLevel.Debug,
        LogLevel.Info,
        LogLevel.Warn,
        LogLevel.Error,
    ];

    internal static IReadOnlyList<LogLevel> GetSupportedLevels()
    {
        return SupportedLevels;
    }
}
