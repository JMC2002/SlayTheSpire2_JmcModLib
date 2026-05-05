namespace JmcModLib.Input;

/// <summary>
/// JML 内部输入后端的最小抽象，用于把 Godot Action、Steam Input 等来源统一接入热键系统。
/// </summary>
internal interface IJmcInputBackend
{
    /// <summary>
    /// 输入后端的内部名称，仅用于日志和诊断。
    /// </summary>
    string Name { get; }

    /// <summary>
    /// 初始化输入后端。
    /// </summary>
    void Initialize();

    /// <summary>
    /// 每帧轮询输入后端，并把逻辑动作变化分发给热键系统。
    /// </summary>
    void Process();

    /// <summary>
    /// 释放输入后端持有的运行时资源。
    /// </summary>
    void Shutdown();
}
