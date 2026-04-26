using Godot;

namespace JmcModLib.Config.UI;

internal sealed class JmcKeybindInputRelay : Node
{
    private const string RelayName = "JmcModLibKeybindInputRelay";

    public static void Ensure(Node context)
    {
        SceneTree? tree = context.GetTree();
        Node? root = tree?.Root;
        if (root == null || root.GetNodeOrNull<Node>(RelayName) != null)
        {
            return;
        }

        root.AddChild(new JmcKeybindInputRelay
        {
            Name = RelayName,
            ProcessMode = ProcessModeEnum.Always
        });
    }

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        SetProcessInput(true);
        SetProcessUnhandledInput(true);
        SetProcessUnhandledKeyInput(true);
    }

    public override void _Input(InputEvent inputEvent)
    {
        HandleInput(inputEvent);
    }

    public override void _UnhandledInput(InputEvent inputEvent)
    {
        HandleInput(inputEvent);
    }

    public override void _UnhandledKeyInput(InputEvent inputEvent)
    {
        HandleInput(inputEvent);
    }

    private void HandleInput(InputEvent inputEvent)
    {
        if (JmcKeybindButton.TryHandleActive(inputEvent))
        {
            GetViewport()?.SetInputAsHandled();
        }
    }
}
