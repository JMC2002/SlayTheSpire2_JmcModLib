namespace JmcModLib.Input;

/// <summary>
/// 预留的 Godot Action 输入后端，后续会承接当前 JmcKeyBinding 对 Godot InputEvent 的判断逻辑。
/// </summary>
internal sealed class GodotActionInputBackend : IJmcInputBackend
{
    /// <inheritdoc />
    public string Name => "GodotAction";

    /// <inheritdoc />
    public void Initialize()
    {
    }

    /// <inheritdoc />
    public void Shutdown()
    {
    }
}
