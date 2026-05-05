namespace JmcModLib.Input;

/// <summary>
/// JML 内部输入后端管理器，负责把 Steam Input 等轮询型输入来源统一调度到热键系统。
/// </summary>
internal static class JmcInputManager
{
    private static readonly List<IJmcInputBackend> Backends =
    [
        new GodotActionInputBackend(),
        new SteamInputBackend()
    ];

    private static int initialized;

    /// <summary>
    /// 已注册的内部输入后端。
    /// </summary>
    internal static IReadOnlyList<IJmcInputBackend> RegisteredBackends => Backends;

    /// <summary>
    /// 初始化所有输入后端。
    /// </summary>
    internal static void Initialize()
    {
        if (Interlocked.Exchange(ref initialized, 1) == 1)
        {
            return;
        }

        foreach (IJmcInputBackend backend in Backends)
        {
            backend.Initialize();
        }
    }

    /// <summary>
    /// 每帧轮询所有输入后端。
    /// </summary>
    internal static void Process()
    {
        if (Volatile.Read(ref initialized) != 1)
        {
            Initialize();
        }

        foreach (IJmcInputBackend backend in Backends)
        {
            backend.Process();
        }
    }

    /// <summary>
    /// 关闭所有输入后端。
    /// </summary>
    internal static void Shutdown()
    {
        if (Interlocked.Exchange(ref initialized, 0) == 0)
        {
            return;
        }

        foreach (IJmcInputBackend backend in Backends)
        {
            backend.Shutdown();
        }
    }
}
