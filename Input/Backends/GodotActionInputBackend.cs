namespace JmcModLib.Input;

/// <summary>
/// Godot Action 输入后端。当前 Godot 输入仍由热键中继直接处理 InputEvent，这里只保留统一调度占位。
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
    public void Process()
    {
    }

    /// <inheritdoc />
    public void Shutdown()
    {
    }
}
