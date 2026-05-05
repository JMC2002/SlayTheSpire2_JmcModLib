using Godot;

namespace JmcModLib.Config.UI;

internal sealed partial class ModSettingsPanel
{
    public override void _Ready()
    {
        SetProcessInput(true);
        SetProcessUnhandledKeyInput(true);
        JmcKeybindInputRelay.Ensure(this);
        nativeTemplates = SettingsUiTemplates.Resolve(this);
        BuildLayout();
        Connect(CanvasItem.SignalName.VisibilityChanged, Callable.From(OnVisibilityChange));
        ConnectViewportSizeChanged();
        ConfigManager.ValueChanged += OnConfigValueChanged;
        ConfigManager.EntryRegistered += OnEntryRegistered;
        ConfigManager.AssemblyRegistered += OnAssemblyChanged;
        ConfigManager.AssemblyUnregistered += OnAssemblyChanged;
        L10n.SubscribeToLocaleChange(OnLocaleChanged);
        RefreshPanelSize();
        RebuildContent();
    }

    public override void _ExitTree()
    {
        ConfigManager.ValueChanged -= OnConfigValueChanged;
        ConfigManager.EntryRegistered -= OnEntryRegistered;
        ConfigManager.AssemblyRegistered -= OnAssemblyChanged;
        ConfigManager.AssemblyUnregistered -= OnAssemblyChanged;
        L10n.UnsubscribeToLocaleChange(OnLocaleChanged);
        DisconnectViewportSizeChanged();
        bindings.Clear();
        listeningKeybind = null;
        centerRoot = null;
        root = null;
        listRoot = null;
        titleActions = null;
        titleLabel = null;
        descriptionLabel = null;
        nativeTemplates = null;
        base._ExitTree();
    }

    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (listeningKeybind?.TryHandleKey(@event) == true)
        {
            listeningKeybind = null;
            GetViewport()?.SetInputAsHandled();
            return;
        }

        base._UnhandledKeyInput(@event);
    }

    public override void _Input(InputEvent @event)
    {
        if (listeningKeybind?.TryHandleKey(@event) == true)
        {
            listeningKeybind = null;
            GetViewport()?.SetInputAsHandled();
            return;
        }

        if (listeningKeybind?.TryHandleController(@event) == true)
        {
            listeningKeybind = null;
            GetViewport()?.SetInputAsHandled();
            return;
        }

        base._Input(@event);
    }

    protected override void OnVisibilityChange()
    {
        if (!Visible)
        {
            listeningKeybind?.CancelListening();
            listeningKeybind = null;
            return;
        }

        RebuildContent();
        RefreshPanelSize();

        Tween tween = CreateTween().SetParallel();
        tween.TweenProperty(this, "modulate", Colors.White, 0.35).From(Colors.Transparent)
            .SetEase(Tween.EaseType.Out)
            .SetTrans(Tween.TransitionType.Cubic);
    }

    private void ConnectViewportSizeChanged()
    {
        Viewport? viewport = GetViewport();
        if (viewport == null)
        {
            return;
        }

        Callable callable = Callable.From(RefreshPanelSize);
        connectedViewport = viewport;
        viewportSizeChangedCallable = callable;
        if (!viewport.IsConnected(Viewport.SignalName.SizeChanged, callable))
        {
            viewport.Connect(Viewport.SignalName.SizeChanged, callable);
        }
    }

    private void DisconnectViewportSizeChanged()
    {
        try
        {
            if (connectedViewport != null
                && GodotObject.IsInstanceValid(connectedViewport)
                && viewportSizeChangedCallable is { } callable
                && connectedViewport.IsConnected(Viewport.SignalName.SizeChanged, callable))
            {
                connectedViewport.Disconnect(Viewport.SignalName.SizeChanged, callable);
            }
        }
        catch
        {
            // 设置界面可能在场景切换期间销毁，失效信号断开失败可以忽略。
        }
        finally
        {
            connectedViewport = null;
            viewportSizeChangedCallable = null;
        }
    }
}
