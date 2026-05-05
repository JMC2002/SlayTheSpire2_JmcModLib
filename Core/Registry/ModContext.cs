// 文件用途：保存单个已注册子 MOD 的程序集、标识、显示名、版本与注册状态。
using System.Reflection;

namespace JmcModLib.Core;

public sealed class ModContext
{
    internal ModContext(Assembly assembly, string modId, string displayName, string version)
    {
        Assembly = assembly;
        ModId = modId;
        DisplayName = displayName;
        Version = version;
    }

    public Assembly Assembly { get; }

    public string ModId { get; internal set; }

    public string DisplayName { get; internal set; }

    public string Version { get; internal set; }

    public bool LoggerConfigured { get; internal set; }

    public bool IsCompleted { get; internal set; }

    public string LoggerContext => string.IsNullOrWhiteSpace(Version) ? DisplayName : $"{DisplayName} v{Version}";

    public string Tag => string.IsNullOrWhiteSpace(Version) ? $"[{DisplayName}]" : $"[{DisplayName} v{Version}]";
}
