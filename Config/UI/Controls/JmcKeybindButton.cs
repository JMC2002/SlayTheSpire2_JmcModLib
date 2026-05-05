using Godot;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.ControllerInput;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;

namespace JmcModLib.Config.UI;

internal sealed class JmcKeybindButton : NButton
{
    private const float RowWidth = 1040f;
    private const float RowHeight = 58f;
    private const float InputLabelWidth = 600f;
    private const float KeyLabelWidth = 280f;
    private const float ControllerIconWidth = 84f;
    private const int FontSize = 24;

    private string labelText = string.Empty;
    private JmcKeyBinding value;
    private UIKeybindAttribute attribute = new();
    private Action<JmcKeybindButton>? onListeningStarted;
    private Action<JmcKeyBinding>? onChanged;
    private bool isListening;
    private Control? bg;
    private MegaRichTextLabel? inputLabel;
    private MegaRichTextLabel? keyBindingLabel;
    private TextureRect? controllerBindingIcon;

    private static JmcKeybindButton? ActiveListeningButton { get; set; }

    internal static bool HasActiveListener => ActiveListeningButton is { isListening: true };

    internal static bool HasRecentCapture => lastCaptureTicks != 0
        && Time.GetTicksMsec() - lastCaptureTicks < 150;

    private static ulong lastCaptureTicks;

    public static JmcKeybindButton Create(
        Control template,
        string labelText,
        JmcKeyBinding value,
        UIKeybindAttribute attribute,
        Action<JmcKeybindButton> onListeningStarted,
        Action<JmcKeyBinding> onChanged)
    {
        JmcKeybindButton button = new()
        {
            Name = "JmcKeybindButton",
            labelText = labelText,
            value = value,
            attribute = attribute,
            onListeningStarted = onListeningStarted,
            onChanged = onChanged
        };
        NativeTemplateCloner.ApplyControlTemplate(template, button);
        button.FocusMode = FocusModeEnum.All;
        button.MouseFilter = MouseFilterEnum.Stop;
        button.SetProcessInput(true);
        button.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        button.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        button.CustomMinimumSize = new Vector2(RowWidth, Math.Max(RowHeight, template.CustomMinimumSize.Y));
        return button;
    }

    public override void _Ready()
    {
        SetProcessInput(true);
        ConnectSignals();
        bg = GetNodeOrNull<Control>("%Bg")
            ?? NativeTemplateCloner.FindDescendantByName<Control>(this, "Bg");
        inputLabel = GetNodeOrNull<MegaRichTextLabel>("%InputLabel")
            ?? NativeTemplateCloner.FindDescendantByName<MegaRichTextLabel>(this, "InputLabel");
        keyBindingLabel = GetNodeOrNull<MegaRichTextLabel>("%KeyBindingInputLabel")
            ?? NativeTemplateCloner.FindDescendantByName<MegaRichTextLabel>(this, "KeyBindingInputLabel");
        controllerBindingIcon = GetNodeOrNull<TextureRect>("%ControllerBindingIcon")
            ?? NativeTemplateCloner.FindDescendantByName<TextureRect>(this, "ControllerBindingIcon");

        if (inputLabel == null || keyBindingLabel == null || controllerBindingIcon == null)
        {
            ModLogger.Warn(
                $"JmcKeybindButton template is missing visual nodes. InputLabel={inputLabel != null}, KeyLabel={keyBindingLabel != null}, ControllerIcon={controllerBindingIcon != null}.");
        }

        NormalizeLayout();
        TryConnectControllerSignals();
        Connect(CanvasItem.SignalName.VisibilityChanged, Callable.From(UpdateDisplay));
        UpdateDisplay();
    }

    public override void _Input(InputEvent inputEvent)
    {
        if (TryHandleKey(inputEvent) || TryHandleController(inputEvent))
        {
            GetViewport()?.SetInputAsHandled();
            return;
        }

        base._Input(inputEvent);
    }

    public override void _ExitTree()
    {
        if (ActiveListeningButton == this)
        {
            ActiveListeningButton = null;
        }

        TryDisconnectControllerSignals();
        TryDisconnectVisibilitySignal();
        base._ExitTree();
    }

    public static bool TryHandleActive(InputEvent inputEvent)
    {
        JmcKeybindButton? button = ActiveListeningButton;
        return button is { isListening: true }
            && (button.TryHandleKey(inputEvent) || button.TryHandleController(inputEvent));
    }

    public void CancelListening()
    {
        if (ActiveListeningButton == this)
        {
            ActiveListeningButton = null;
        }

        isListening = false;
        UpdateDisplay();
    }

    public void SetValue(JmcKeyBinding binding)
    {
        value = binding;
        UpdateDisplay();
    }

    public bool TryHandleKey(InputEvent inputEvent)
    {
        if (!isListening || !attribute.AllowKeyboard)
        {
            return false;
        }

        if (inputEvent is not InputEventKey { Pressed: true, Echo: false } keyEvent)
        {
            return false;
        }

        Key keycode = JmcKeyBinding.ReadKey(keyEvent);
        if (keycode == Key.None || JmcKeyBinding.IsModifierKey(keycode))
        {
            return false;
        }

        JmcKeyModifiers modifiers = JmcKeyBinding.ReadModifiers(keyEvent);
        ApplyValue(value.WithKeyboard(keycode, modifiers));
        return true;
    }

    public bool TryHandleController(InputEvent inputEvent)
    {
        if (!isListening || !attribute.AllowController)
        {
            return false;
        }

        foreach (StringName controllerInput in Controller.AllControllerInputs)
        {
            if (!inputEvent.IsActionReleased(controllerInput))
            {
                continue;
            }

            if (NControllerManager.Instance?.ShouldAllowControllerRebinding == true)
            {
                ApplyValue(value.WithController(controllerInput.ToString()));
            }
            else
            {
                CancelListening();
            }

            return true;
        }

        return false;
    }

    protected override void OnRelease()
    {
        base.OnRelease();
        if (ActiveListeningButton != this)
        {
            ActiveListeningButton?.CancelListening();
        }

        ActiveListeningButton = this;
        isListening = true;
        onListeningStarted?.Invoke(this);
        UpdateDisplay();
    }

    protected override void OnFocus()
    {
        base.OnFocus();
        bg?.Visible = true;
    }

    protected override void OnUnfocus()
    {
        base.OnUnfocus();
        bg?.Visible = isListening;
    }

    private void ApplyValue(JmcKeyBinding binding)
    {
        value = binding;
        lastCaptureTicks = Time.GetTicksMsec();
        isListening = false;
        if (ActiveListeningButton == this)
        {
            ActiveListeningButton = null;
        }

        UpdateDisplay();
        onChanged?.Invoke(binding);
    }

    private void UpdateDisplay()
    {
        inputLabel?.SetTextAutoSize(labelText);

        keyBindingLabel?.SetTextAutoSize(GetKeyboardText());

        if (controllerBindingIcon != null)
        {
            bool shouldShowController = attribute.AllowController && value.HasController;
            controllerBindingIcon.Visible = shouldShowController;
            controllerBindingIcon.Texture = shouldShowController
                ? NControllerManager.Instance?.GetHotkeyIcon(value.Controller)
                : null;
            controllerBindingIcon.Modulate = NControllerManager.Instance?.ShouldAllowControllerRebinding == false
                ? new Color(1f, 1f, 1f, 0.15f)
                : Colors.White;
        }

        bg?.Visible = isListening || HasFocus();
    }

    private string GetKeyboardText()
    {
        bool steamInputManaged = attribute.AllowController
            && NControllerManager.Instance?.ShouldAllowControllerRebinding == false;

        if (isListening)
        {
            if (steamInputManaged && !attribute.AllowKeyboard)
            {
                return ModSettingsText.SteamInputManaged();
            }

            return ModSettingsText.KeybindListening();
        }

        if (!attribute.AllowKeyboard)
        {
            return steamInputManaged ? ModSettingsText.SteamInputManaged() : string.Empty;
        }

        string keyboardText = value.HasKeyboard ? value.ToKeyboardText() : ModSettingsText.KeybindUnbound();
        return steamInputManaged
            ? $"{keyboardText} / {ModSettingsText.SteamInputManaged()}"
            : keyboardText;
    }

    private void NormalizeLayout()
    {
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        SizeFlagsVertical = SizeFlags.ShrinkCenter;
        CustomMinimumSize = new Vector2(RowWidth, Math.Max(RowHeight, CustomMinimumSize.Y));

        if (inputLabel != null)
        {
            NormalizeLabel(inputLabel, InputLabelWidth, HorizontalAlignment.Left);
        }

        if (keyBindingLabel != null)
        {
            NormalizeLabel(keyBindingLabel, KeyLabelWidth, HorizontalAlignment.Center);
        }

        if (controllerBindingIcon != null)
        {
            controllerBindingIcon.CustomMinimumSize = new Vector2(ControllerIconWidth, RowHeight);
            controllerBindingIcon.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
            controllerBindingIcon.SizeFlagsVertical = SizeFlags.ShrinkCenter;
            controllerBindingIcon.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
            controllerBindingIcon.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
        }
    }

    private static void NormalizeLabel(MegaRichTextLabel label, float width, HorizontalAlignment alignment)
    {
        label.CustomMinimumSize = new Vector2(width, RowHeight);
        label.SizeFlagsHorizontal = alignment == HorizontalAlignment.Left
            ? SizeFlags.ExpandFill
            : SizeFlags.ShrinkCenter;
        label.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        label.FitContent = false;
        label.ScrollActive = false;
        label.AutoSizeEnabled = false;
        label.MinFontSize = FontSize;
        label.MaxFontSize = FontSize;
        label.AutowrapMode = TextServer.AutowrapMode.Off;
        label.HorizontalAlignment = alignment;
        label.VerticalAlignment = VerticalAlignment.Center;
        label.Call("SetFontSize", FontSize);
    }

    private void TryConnectControllerSignals()
    {
        try
        {
            NControllerManager.Instance?.Connect(NControllerManager.SignalName.ControllerDetected, Callable.From(UpdateDisplay));
            NControllerManager.Instance?.Connect(NControllerManager.SignalName.MouseDetected, Callable.From(UpdateDisplay));
        }
        catch (Exception ex)
        {
            ModLogger.Warn("Failed to connect controller signals for keybind settings.", ex);
        }
    }

    private void TryDisconnectControllerSignals()
    {
        try
        {
            NControllerManager.Instance?.Disconnect(NControllerManager.SignalName.ControllerDetected, Callable.From(UpdateDisplay));
            NControllerManager.Instance?.Disconnect(NControllerManager.SignalName.MouseDetected, Callable.From(UpdateDisplay));
        }
        catch
        {
            // Native settings can be torn down during scene changes; stale signal disconnect failures are harmless.
        }
    }

    private void TryDisconnectVisibilitySignal()
    {
        try
        {
            Disconnect(CanvasItem.SignalName.VisibilityChanged, Callable.From(UpdateDisplay));
        }
        catch
        {
            // Ignore shutdown disconnect races.
        }
    }

}
