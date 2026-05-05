// 文件用途：保存单个已注册子 MOD 的程序集、标识、显示名、版本与注册状态。
using System.Reflection;

namespace JmcModLib.Core;

/// <summary>
/// 描述一个已注册 MOD 的程序集、标识、显示名、版本和注册状态。
/// </summary>
public sealed class ModContext
{
    internal ModContext(Assembly assembly, string modId, string displayName, string version)
    {
        Assembly = assembly;
        ModId = modId;
        DisplayName = displayName;
        Version = version;
    }

    /// <summary>
    /// 当前 MOD 对应的托管程序集。
    /// </summary>
    public Assembly Assembly { get; }

    /// <summary>
    /// MOD 的稳定标识，通常与 manifest 的 <c>id</c> 一致。
    /// </summary>
    public string ModId { get; internal set; }

    /// <summary>
    /// 用于日志和设置界面显示的名称。
    /// </summary>
    public string DisplayName { get; internal set; }

    /// <summary>
    /// 当前注册上下文记录的 MOD 版本号。
    /// </summary>
    public string Version { get; internal set; }

    /// <summary>
    /// 是否已经调用 <see cref="RegistryBuilder.Done"/> 完成注册。
    /// </summary>
    public bool IsCompleted { get; internal set; }

    /// <summary>
    /// 传给 STS2 原生日志器的上下文名称。
    /// </summary>
    public string LoggerContext => string.IsNullOrWhiteSpace(Version) ? DisplayName : $"{DisplayName} v{Version}";

    /// <summary>
    /// 常用于日志文本的短标签，格式类似 <c>[MyMod v1.0.0]</c>。
    /// </summary>
    public string Tag => string.IsNullOrWhiteSpace(Version) ? $"[{DisplayName}]" : $"[{DisplayName} v{Version}]";
}
