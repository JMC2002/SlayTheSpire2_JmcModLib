namespace JmcModLib.Input;

/// <summary>
/// JML 内部输入后端管理器。当前仅作为结构占位，后续 Steam Input 接入会在这里统一调度输入来源。
/// </summary>
internal static class JmcInputManager
{
    private static readonly List<IJmcInputBackend> Backends = [new GodotActionInputBackend()];

    /// <summary>
    /// 已注册的内部输入后端。
    /// </summary>
    internal static IReadOnlyList<IJmcInputBackend> RegisteredBackends => Backends;

    /// <summary>
    /// 初始化所有输入后端。
    /// </summary>
    internal static void Initialize()
    {
        foreach (IJmcInputBackend backend in Backends)
        {
            backend.Initialize();
        }
    }

    /// <summary>
    /// 关闭所有输入后端。
    /// </summary>
    internal static void Shutdown()
    {
        foreach (IJmcInputBackend backend in Backends)
        {
            backend.Shutdown();
        }
    }
}
