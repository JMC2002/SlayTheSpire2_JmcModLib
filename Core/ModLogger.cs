using JmcModLib.Core;

namespace AllCardIs.Core;

/// <summary>
/// 封装日志打印，自动附带时间戳 [HH:mm:ss]
/// </summary>
public static class ModLogger
{
    private static MegaCrit.Sts2.Core.Logging.Logger _logger =
        new(VersionInfo.Name, MegaCrit.Sts2.Core.Logging.LogType.Generic);

    private static string Timestamp => DateTime.Now.ToString("HH:mm:ss");

    private static string msg(string message)
    {
        return $"[{Timestamp}][{VersionInfo.Tag}] {message}";
    }

    public static void Info(string message)
    {
        _logger.Info(msg(message));
    }

    public static void Warn(string message)
    {
        _logger.Warn(msg(message));
    }

    public static void Error(string message)
    {
        _logger.Error(msg(message));
    }
}