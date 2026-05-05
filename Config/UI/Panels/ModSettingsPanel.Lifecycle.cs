using System.Globalization;
using JmcModLib.Config;
using JmcModLib.Config.Entry;
using JmcModLib.Config.Serialization;
using Godot;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Nodes.Screens.Settings;

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
        GetViewport().Connect(Viewport.SignalName.SizeChanged, Callable.From(RefreshPanelSize));
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
}
