using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.ControllerInput;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens.ModdingScreen;
using MegaCrit.Sts2.Core.Nodes.Screens.Settings;
using Godot;
using JmcModLib.Reflection;
using JmcModLib.Utils;

namespace JmcModLib.Config.UI;

internal sealed class SettingsUiTemplates
{
    public NSettingsTickbox? TickboxTemplate { get; private init; }

    public NSettingsSlider? SliderTemplate { get; private init; }

    public NSettingsDropdown? DropdownTemplate { get; private init; }

    public NDropdownItem? DropdownItemTemplate { get; private init; }

    public NSettingsButton? ButtonTemplate { get; private init; }

    public NSettingsButton? ResetButtonTemplate { get; private init; }

    public Control? CompactButtonTemplate { get; private init; }

    public Control? KeybindTemplate { get; private init; }

    public MegaRichTextLabel? RichLabelTemplate { get; private init; }

    public MegaRichTextLabel? DescriptionLabelTemplate { get; private init; }

    public bool HasStyledControls =>
        TickboxTemplate != null
        && SliderTemplate != null
        && DropdownTemplate != null
        && DropdownItemTemplate != null
        && ButtonTemplate != null;

    public Control? GetButtonTemplate(UIButtonColor color)
    {
        return color switch
        {
            UIButtonColor.Reset => ResetButtonTemplate ?? ButtonTemplate ?? CompactButtonTemplate,
            _ => ButtonTemplate ?? CompactButtonTemplate
        };
    }

    public static SettingsUiTemplates Resolve(Node context)
    {
        NSettingsScreen? screen = FindAncestor<NSettingsScreen>(context);
        if (screen == null)
        {
            return new SettingsUiTemplates();
        }

        NSettingsTickbox? tickboxTemplate = FindDescendant<NSettingsTickbox>(
            GetPanelRow(screen, "%GeneralSettings", "ShowRunTimer"));
        NSettingsSlider? sliderTemplate = FindDescendant<NSettingsSlider>(
            GetPanelRow(screen, "%SoundSettings", "MasterVolume"));
        NSettingsDropdown? displayDropdown =
            FindDescendant<NDisplayDropdown>(screen)
            ?? FindDescendantByName<NSettingsDropdown>(screen, "DisplayDropdown")
            ?? FindDescendant<NSettingsDropdown>(GetPanelRow(screen, "%GraphicsSettings", "DisplaySelection"));
        NSettingsDropdown? resolutionDropdown =
            FindDescendant<NResolutionDropdown>(screen)
            ?? FindDescendantByName<NSettingsDropdown>(screen, "ResolutionDropdown")
            ?? FindDescendant<NSettingsDropdown>(GetPanelRow(screen, "%GraphicsSettings", "WindowedResolution"));
        NSettingsDropdown? aspectRatioDropdown =
            FindDescendant<NAspectRatioDropdown>(screen)
            ?? FindDescendantByName<NSettingsDropdown>(screen, "AspectRatioDropdown")
            ?? FindDescendant<NSettingsDropdown>(GetPanelRow(screen, "%GraphicsSettings", "AspectRatio"));
        NSettingsDropdown? languageDropdown =
            FindDescendant<NLanguageDropdown>(screen)
            ?? FindDescendantByName<NSettingsDropdown>(screen, "LanguageDropdown")
            ?? FindDescendant<NSettingsDropdown>(GetPanelRow(screen, "%GeneralSettings", "LanguageLine"));
        NSettingsDropdown? dropdownTemplate = displayDropdown ?? resolutionDropdown ?? aspectRatioDropdown ?? languageDropdown;
        NDropdownItem? dropdownItemTemplate = ResolveDropdownItemTemplate(
            displayDropdown,
            resolutionDropdown,
            languageDropdown);
        NSettingsButton? buttonTemplate =
            screen.GetNodeOrNull<NSettingsButton>("%FeedbackButton")
            ?? FindDescendantByName<NSettingsButton>(screen, "FeedbackButton");
        NSettingsButton? resetButtonTemplate =
            FindDescendant<NResetGameplayButton>(GetPanelRow(screen, "%GeneralSettings", "ResetGameplay"))
            ?? FindDescendant<NResetGraphicsButton>(GetPanelRow(screen, "%GraphicsSettings", "ResetGraphics"))
            ?? FindDescendant<NResetTutorialsButton>(GetPanelRow(screen, "%GeneralSettings", "ResetTutorials"))
            ?? FindDescendantByName<NSettingsButton>(screen, "ResetGameplay")
            ?? FindDescendantByName<NSettingsButton>(screen, "ResetGraphics")
            ?? FindDescendantByName<NSettingsButton>(screen, "ResetTutorials");
        NSettingsPanel? inputSettingsPanel = screen.GetNodeOrNull<NSettingsPanel>("%InputSettings");
        Control? compactButtonTemplate =
            inputSettingsPanel?.GetNodeOrNull<Control>("%ResetToDefaultButton")
            ?? FindDescendantByName<Control>(inputSettingsPanel, "ResetToDefaultButton")
            ?? FindDescendantByName<Control>(screen, "ResetToDefaultButton");
        Control? keybindTemplate =
            FindDescendant<NInputSettingsEntry>(inputSettingsPanel?.Content)
            ?? InstantiateInputSettingsEntryTemplate();
        MegaRichTextLabel? richLabelTemplate = GetPanelRow(screen, "%GeneralSettings", "ShowRunTimer")
            ?.GetNodeOrNull<MegaRichTextLabel>("Label");
        MegaRichTextLabel? descriptionLabelTemplate = ResolveModDescriptionTemplate();

        if (compactButtonTemplate == null)
        {
            ModLogger.Warn("Could not find ResetToDefaultButton; compact mod setting buttons will use FeedbackButton as a fallback.");
        }

        if (buttonTemplate == null)
        {
            ModLogger.Warn("Could not find FeedbackButton; mod setting action buttons will fall back to plain Godot buttons.");
        }

        if (resetButtonTemplate == null)
        {
            ModLogger.Warn("Could not find a native reset settings button; reset-style mod buttons will fall back to FeedbackButton.");
        }

        if (dropdownTemplate == null || dropdownItemTemplate == null)
        {
            ModLogger.Warn(
                $"Could not find native settings dropdown templates; dropdown configs will use the fallback OptionButton. Dropdown={dropdownTemplate != null}, Item={dropdownItemTemplate != null}.");
        }
        else
        {
            ModLogger.Info(
                $"Resolved native settings dropdown template: Dropdown={dropdownTemplate.GetType().Name}, Item={dropdownItemTemplate.GetType().Name}.");
        }

        if (keybindTemplate == null)
        {
            ModLogger.Warn("Could not find native input settings entry template; keybind configs will use a fallback button.");
        }

        return new SettingsUiTemplates
        {
            TickboxTemplate = tickboxTemplate,
            SliderTemplate = sliderTemplate,
            DropdownTemplate = dropdownTemplate,
            DropdownItemTemplate = dropdownItemTemplate,
            ButtonTemplate = buttonTemplate,
            ResetButtonTemplate = resetButtonTemplate,
            CompactButtonTemplate = compactButtonTemplate,
            KeybindTemplate = keybindTemplate,
            RichLabelTemplate = richLabelTemplate,
            DescriptionLabelTemplate = descriptionLabelTemplate
        };
    }

    private static MegaRichTextLabel? ResolveModDescriptionTemplate()
    {
        NModdingScreen? moddingScreen = NModdingScreen.Create();
        try
        {
            NModInfoContainer? modInfoContainer = FindDescendant<NModInfoContainer>(moddingScreen);
            MegaRichTextLabel? description = modInfoContainer?.GetNodeOrNull<MegaRichTextLabel>("ModDescription");
            return description == null
                ? null
                : (MegaRichTextLabel)description.Duplicate();
        }
        finally
        {
            moddingScreen?.Free();
        }
    }

    private static Control? InstantiateInputSettingsEntryTemplate()
    {
        try
        {
            return ResourceLoader
                .Load<PackedScene>(
                    "res://scenes/screens/settings_screen/input_settings_entry.tscn",
                    null,
                    ResourceLoader.CacheMode.Reuse)
                ?.Instantiate<Control>(PackedScene.GenEditState.Disabled);
        }
        catch (Exception ex)
        {
            ModLogger.Warn("Failed to instantiate native input settings entry template.", ex);
            return null;
        }
    }

    private static Node? GetPanelRow(NSettingsScreen screen, string panelPath, string rowName)
    {
        NSettingsPanel? panel = screen.GetNodeOrNull<NSettingsPanel>(panelPath);
        return panel?.Content?.GetNodeOrNull<Node>(rowName);
    }

    private static NDropdownItem? ResolveDropdownItemTemplate(params NSettingsDropdown?[] dropdowns)
    {
        foreach (NSettingsDropdown? dropdown in dropdowns)
        {
            NDropdownItem? item = FindDescendant<NDropdownItem>(dropdown);
            if (item != null)
            {
                return item;
            }
        }

        foreach (NSettingsDropdown? dropdown in dropdowns)
        {
            NDropdownItem? item = InstantiateDropdownItemTemplate(dropdown);
            if (item != null)
            {
                return item;
            }
        }

        return null;
    }

    private static NDropdownItem? InstantiateDropdownItemTemplate(NSettingsDropdown? dropdown)
    {
        if (dropdown == null)
        {
            return null;
        }

        MemberAccessor? field = GetMemberInHierarchy(dropdown.GetType(), "_dropdownItemScene");
        if (field?.GetValue(dropdown) is not PackedScene scene)
        {
            return null;
        }

        try
        {
            return scene.Instantiate<NDropdownItem>(PackedScene.GenEditState.Disabled);
        }
        catch (Exception ex)
        {
            ModLogger.Warn($"Failed to instantiate dropdown item template from {dropdown.GetType().FullName}.", ex);
            return null;
        }
    }

    private static MemberAccessor? GetMemberInHierarchy(Type type, string name)
    {
        for (Type? current = type; current != null; current = current.BaseType)
        {
            try
            {
                return MemberAccessor.Get(current, name);
            }
            catch (MissingMemberException)
            {
            }
        }

        return null;
    }

    private static T? FindAncestor<T>(Node? node) where T : Node
    {
        Node? current = node;
        while (current != null)
        {
            if (current is T found)
            {
                return found;
            }

            current = current.GetParent();
        }

        return null;
    }

    private static T? FindDescendant<T>(Node? root) where T : Node
    {
        if (root == null)
        {
            return null;
        }

        if (root is T match)
        {
            return match;
        }

        foreach (Node child in root.GetChildren())
        {
            T? nested = FindDescendant<T>(child);
            if (nested != null)
            {
                return nested;
            }
        }

        return null;
    }

    private static T? FindDescendantByName<T>(Node? root, string name) where T : Node
    {
        if (root == null)
        {
            return null;
        }

        if (root.Name == name && root is T match)
        {
            return match;
        }

        foreach (Node child in root.GetChildren())
        {
            T? nested = FindDescendantByName<T>(child, name);
            if (nested != null)
            {
                return nested;
            }
        }

        return null;
    }
}

internal static class NativeTemplateCloner
{
    public static T? FindDescendantByName<T>(Node? root, string name) where T : Node
    {
        if (root == null)
        {
            return null;
        }

        if (root.Name == name && root is T match)
        {
            return match;
        }

        foreach (Node child in root.GetChildren())
        {
            T? nested = FindDescendantByName<T>(child, name);
            if (nested != null)
            {
                return nested;
            }
        }

        return null;
    }

    public static void ApplyControlTemplate(Control source, Control target)
    {
        target.FocusMode = source.FocusMode;
        target.CustomMinimumSize = source.CustomMinimumSize;
        target.SizeFlagsHorizontal = source.SizeFlagsHorizontal;
        target.SizeFlagsVertical = source.SizeFlagsVertical;
        target.GrowHorizontal = source.GrowHorizontal;
        target.GrowVertical = source.GrowVertical;
        target.MouseFilter = source.MouseFilter;
        target.PivotOffset = source.PivotOffset;
        target.Scale = source.Scale;
        target.Rotation = source.Rotation;
        target.Modulate = source.Modulate;
        target.SelfModulate = source.SelfModulate;
        target.Theme = source.Theme;
        target.ThemeTypeVariation = source.ThemeTypeVariation;

        foreach (Node child in source.GetChildren())
        {
            target.AddChild(child.Duplicate());
        }
    }
}

internal sealed class JmcSettingsButton : NSettingsButton
{
    private string text = string.Empty;
    private Action? onPressed;
    private UIButtonColor color;
    private bool hideImage;

    public static JmcSettingsButton Create(
        Control template,
        string text,
        Action onPressed,
        UIButtonColor color = UIButtonColor.Default,
        bool hideImage = false)
    {
        JmcSettingsButton button = new()
        {
            Name = "JmcSettingsButton",
            text = text,
            onPressed = onPressed,
            color = color,
            hideImage = hideImage
        };
        NativeTemplateCloner.ApplyControlTemplate(template, button);
        return button;
    }

    public override void _Ready()
    {
        ConnectSignals();
        GetNodeOrNull<MegaLabel>("Label")?.SetTextAutoSize(text);
        GetNodeOrNull<MegaRichTextLabel>("Label")?.SetTextAutoSize(text);

        Control? image = GetNodeOrNull<Control>("Image");
        image?.Visible = !hideImage;
        ApplyColor(image);

        Connect(NClickableControl.SignalName.Released, Callable.From<NButton>(_ => onPressed?.Invoke()));
    }

    private void ApplyColor(Control? image)
    {
        if (!JmcButtonColor.TryGetTint(color, out Color tint))
        {
            return;
        }

        Control? target = image ?? NativeTemplateCloner.FindDescendantByName<Control>(this, "Image");
        if (target == null)
        {
            Modulate = tint;
            return;
        }

        target.Modulate = tint;
        target.SelfModulate = tint;
    }
}

internal sealed class JmcKeybindButton : NButton
{
    private const float RowWidth = 1040f;
    private const float RowHeight = 58f;
    private const float InputLabelWidth = 600f;
    private const float KeyLabelWidth = 210f;
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

        if (inputEvent is not InputEventKey { Echo: false } keyEvent)
        {
            return false;
        }

        Key keycode = keyEvent.Keycode != Key.None ? keyEvent.Keycode : keyEvent.PhysicalKeycode;
        if (keycode == Key.None)
        {
            return false;
        }

        ApplyValue(value.WithKeyboard(keycode));
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
        if (bg != null)
        {
            bg.Visible = true;
        }
    }

    protected override void OnUnfocus()
    {
        base.OnUnfocus();
        if (bg != null)
        {
            bg.Visible = isListening;
        }
    }

    private void ApplyValue(JmcKeyBinding binding)
    {
        value = binding;
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
        if (inputLabel != null)
        {
            inputLabel.SetTextAutoSize(labelText);
        }

        if (keyBindingLabel != null)
        {
            keyBindingLabel.SetTextAutoSize(GetKeyboardText());
        }

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

        if (bg != null)
        {
            bg.Visible = isListening || HasFocus();
        }
    }

    private string GetKeyboardText()
    {
        if (isListening)
        {
            return ModSettingsText.KeybindListening();
        }

        if (!attribute.AllowKeyboard)
        {
            return string.Empty;
        }

        return value.HasKeyboard ? value.Keyboard.ToString() : ModSettingsText.KeybindUnbound();
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

internal static class JmcButtonColor
{
    public static bool TryGetTint(UIButtonColor color, out Color tint)
    {
        tint = color switch
        {
            UIButtonColor.Green => new Color("65A83A"),
            UIButtonColor.Red => new Color("B94A3F"),
            UIButtonColor.Gold => new Color("D69A35"),
            UIButtonColor.Blue => new Color("3C6F8F"),
            _ => Colors.White
        };

        return color is UIButtonColor.Green
            or UIButtonColor.Red
            or UIButtonColor.Gold
            or UIButtonColor.Blue;
    }
}

internal sealed class JmcSettingsTickbox : NButton
{
    private bool initialValue;
    private Action<bool>? onChanged;
    private bool isTicked;
    private Control? tickedImage;
    private Control? notTickedImage;
    private NSelectionReticle? selectionReticle;

    public static JmcSettingsTickbox Create(NSettingsTickbox template, bool initialValue, Action<bool> onChanged)
    {
        JmcSettingsTickbox tickbox = new()
        {
            Name = "JmcSettingsTickbox",
            initialValue = initialValue,
            onChanged = onChanged
        };
        NativeTemplateCloner.ApplyControlTemplate(template, tickbox);
        tickbox.FocusMode = FocusModeEnum.All;
        tickbox.MouseFilter = MouseFilterEnum.Stop;
        return tickbox;
    }

    public override void _Ready()
    {
        ConnectSignals();
        tickedImage = NativeTemplateCloner.FindDescendantByName<Control>(this, "Ticked");
        notTickedImage = NativeTemplateCloner.FindDescendantByName<Control>(this, "NotTicked");
        selectionReticle = GetNodeOrNull<NSelectionReticle>("SelectionReticle")
            ?? NativeTemplateCloner.FindDescendantByName<NSelectionReticle>(this, "SelectionReticle");
        if (tickedImage == null || notTickedImage == null)
        {
            ModLogger.Warn($"JmcSettingsTickbox template is missing visual nodes. Ticked={tickedImage != null}, NotTicked={notTickedImage != null}.");
        }

        SetValue(initialValue);
    }

    public void SetValue(bool value)
    {
        ApplyValue(value, notify: false);
    }

    protected override void OnRelease()
    {
        base.OnRelease();
        ApplyValue(!isTicked, notify: true);
    }

    protected override void OnFocus()
    {
        base.OnFocus();
        if (NControllerManager.Instance?.IsUsingController == true)
        {
            selectionReticle?.OnSelect();
        }
    }

    protected override void OnUnfocus()
    {
        base.OnUnfocus();
        selectionReticle?.OnDeselect();
    }

    private void ApplyValue(bool value, bool notify)
    {
        isTicked = value;

        tickedImage?.Visible = value;

        notTickedImage?.Visible = !value;

        if (notify)
        {
            onChanged?.Invoke(value);
        }
    }
}

internal sealed class JmcSettingsSlider : NSettingsSlider
{
    private double minValue;
    private double maxValue;
    private double stepValue;
    private double initialValue;
    private Func<double, string>? formatter;
    private Action<double>? onChanged;
    private bool suppressChanged;
    private MegaLabel? valueLabel;

    public static JmcSettingsSlider Create(
        NSettingsSlider template,
        double minValue,
        double maxValue,
        double stepValue,
        double initialValue,
        Func<double, string> formatter,
        Action<double> onChanged)
    {
        JmcSettingsSlider slider = new()
        {
            Name = "JmcSettingsSlider",
            minValue = minValue,
            maxValue = maxValue,
            stepValue = stepValue,
            initialValue = initialValue,
            formatter = formatter,
            onChanged = onChanged
        };
        NativeTemplateCloner.ApplyControlTemplate(template, slider);
        return slider;
    }

    public override void _Ready()
    {
        ConnectSignals();
        valueLabel = GetNodeOrNull<MegaLabel>("SliderValue");
        _slider.MinValue = minValue;
        _slider.MaxValue = maxValue;
        _slider.Step = stepValue;
        _slider.Connect(Godot.Range.SignalName.ValueChanged, Callable.From<double>(HandleValueChanged));
        SetValue(initialValue);
    }

    public void SetValue(double value)
    {
        suppressChanged = true;
        _slider.SetValueWithoutAnimation(value);
        UpdateValueLabel(value);
        suppressChanged = false;
    }

    private void HandleValueChanged(double value)
    {
        UpdateValueLabel(value);
        if (!suppressChanged)
        {
            onChanged?.Invoke(value);
        }
    }

    private void UpdateValueLabel(double value)
    {
        valueLabel?.SetTextAutoSize(formatter?.Invoke(value) ?? value.ToString("0.##"));
    }
}

internal readonly record struct JmcDropdownOption(string Text, string Value);

internal sealed class JmcDropdownItem : NDropdownItem
{
    private string text = string.Empty;
    private string value = string.Empty;

    public string Value => value;

    public static JmcDropdownItem Create(NDropdownItem template, string text, string value)
    {
        JmcDropdownItem item = new()
        {
            Name = "JmcDropdownItem",
            text = text,
            value = value
        };
        NativeTemplateCloner.ApplyControlTemplate(template, item);
        item.FocusMode = FocusModeEnum.All;
        item.MouseFilter = MouseFilterEnum.Stop;
        return item;
    }

    public override void _Ready()
    {
        base._Ready();
        Text = text;
    }
}

internal sealed class JmcSettingsDropdown : NButton
{
    private const int PopupZIndex = 4000;

    private IReadOnlyList<JmcDropdownOption> options = [];
    private string selectedValue = string.Empty;
    private NDropdownItem? itemTemplate;
    private Action<string>? onChanged;
    private bool suppressChanged;
    private bool isOpen;
    private MegaLabel? currentLabel;
    private Control? currentHighlight;
    private Control? dropdownContainer;
    private Control? dropdownItems;
    private NButton? dismisser;
    private NSelectionReticle? selectionReticle;
    private Vector2 dropdownLocalPosition;
    private bool dropdownOriginalTopLevel;
    private int dropdownOriginalZIndex;
    private bool dropdownOriginalZAsRelative;
    private Vector2 dismisserLocalPosition;
    private Vector2 dismisserOriginalSize;
    private bool dismisserOriginalTopLevel;
    private int dismisserOriginalZIndex;
    private bool dismisserOriginalZAsRelative;

    public static JmcSettingsDropdown Create(
        NSettingsDropdown template,
        NDropdownItem itemTemplate,
        IReadOnlyList<string> options,
        string selectedValue,
        Action<string> onChanged)
    {
        return Create(
            template,
            itemTemplate,
            [.. options.Select(static option => new JmcDropdownOption(option, option))],
            selectedValue,
            onChanged);
    }

    public static JmcSettingsDropdown Create(
        NSettingsDropdown template,
        NDropdownItem itemTemplate,
        IReadOnlyList<JmcDropdownOption> options,
        string selectedValue,
        Action<string> onChanged)
    {
        JmcSettingsDropdown dropdown = new()
        {
            Name = "JmcSettingsDropdown",
            options = options,
            selectedValue = selectedValue,
            itemTemplate = itemTemplate,
            onChanged = onChanged
        };
        NativeTemplateCloner.ApplyControlTemplate(template, dropdown);
        dropdown.FocusMode = FocusModeEnum.All;
        dropdown.MouseFilter = MouseFilterEnum.Stop;
        return dropdown;
    }

    public override void _Ready()
    {
        ConnectSignals();
        currentLabel = GetNodeOrNull<MegaLabel>("Label")
            ?? NativeTemplateCloner.FindDescendantByName<MegaLabel>(this, "Label");
        currentHighlight = GetNodeOrNull<Control>("Highlight")
            ?? NativeTemplateCloner.FindDescendantByName<Control>(this, "Highlight");
        dropdownContainer = GetNodeOrNull<Control>("DropdownContainer")
            ?? NativeTemplateCloner.FindDescendantByName<Control>(this, "DropdownContainer");
        dropdownItems = dropdownContainer?.GetNodeOrNull<Control>("VBoxContainer")
            ?? NativeTemplateCloner.FindDescendantByName<Control>(dropdownContainer, "VBoxContainer");
        dismisser = GetNodeOrNull<NButton>("Dismisser")
            ?? NativeTemplateCloner.FindDescendantByName<NButton>(this, "Dismisser");
        selectionReticle = GetNodeOrNull<NSelectionReticle>("SelectionReticle")
            ?? NativeTemplateCloner.FindDescendantByName<NSelectionReticle>(this, "SelectionReticle");

        if (dismisser != null)
        {
            dismisserOriginalTopLevel = dismisser.TopLevel;
            dismisserOriginalZIndex = dismisser.ZIndex;
            dismisserOriginalZAsRelative = dismisser.ZAsRelative;
            dismisserLocalPosition = dismisser.Position;
            dismisserOriginalSize = dismisser.Size;
            dismisser.Connect(NClickableControl.SignalName.Released, Callable.From<NButton>(_ => CloseDropdown()));
            dismisser.Visible = false;
        }

        if (dropdownContainer != null)
        {
            dropdownOriginalTopLevel = dropdownContainer.TopLevel;
            dropdownOriginalZIndex = dropdownContainer.ZIndex;
            dropdownOriginalZAsRelative = dropdownContainer.ZAsRelative;
            dropdownLocalPosition = dropdownContainer.Position;
            dropdownContainer.Visible = false;
        }

        if (currentLabel == null || dropdownContainer == null || dropdownItems == null)
        {
            ModLogger.Warn(
                $"JmcSettingsDropdown template is missing required nodes. Label={currentLabel != null}, Container={dropdownContainer != null}, Items={dropdownItems != null}.");
        }

        PopulateItems();
        SetValue(selectedValue);
    }

    public override void _Process(double delta)
    {
        if (isOpen)
        {
            PositionPopupLayer();
        }
    }

    public void SetValue(string value)
    {
        suppressChanged = true;
        selectedValue = value;
        currentLabel?.SetTextAutoSize(GetDisplayText(value));
        suppressChanged = false;
    }

    private void PopulateItems()
    {
        if (itemTemplate == null || dropdownItems == null)
        {
            return;
        }

        foreach (Node child in dropdownItems.GetChildren())
        {
            dropdownItems.RemoveChild(child);
            child.QueueFree();
        }

        foreach (JmcDropdownOption option in options)
        {
            JmcDropdownItem item = JmcDropdownItem.Create(itemTemplate, option.Text, option.Value);
            dropdownItems.AddChild(item);
            item.Connect(NDropdownItem.SignalName.Selected, Callable.From<NDropdownItem>(OnDropdownItemSelected));
        }

        dropdownItems.GetParent()?.CallDeferred("RefreshLayout");
    }

    protected override void OnRelease()
    {
        base.OnRelease();
        if (isOpen)
        {
            CloseDropdown();
        }
        else
        {
            OpenDropdown();
        }
    }

    protected override void OnFocus()
    {
        base.OnFocus();
        currentHighlight?.Modulate = new Color("3C5B6B");

        if (NControllerManager.Instance?.IsUsingController == true)
        {
            selectionReticle?.OnSelect();
        }
    }

    protected override void OnUnfocus()
    {
        base.OnUnfocus();
        currentHighlight?.Modulate = new Color("2C434F");

        selectionReticle?.OnDeselect();
    }

    private void OpenDropdown()
    {
        if (dropdownContainer == null)
        {
            return;
        }

        dropdownContainer.Visible = true;
        PreparePopupLayer();
        dismisser?.Visible = true;

        isOpen = true;
        GetParent()?.MoveChild(this, GetParent().GetChildCount() - 1);

        if (dropdownItems == null)
        {
            return;
        }

        List<NDropdownItem> items = [.. dropdownItems.GetChildren().OfType<NDropdownItem>()];
        for (int i = 0; i < items.Count; i++)
        {
            items[i].UnhoverSelection();
            items[i].FocusNeighborLeft = items[i].GetPath();
            items[i].FocusNeighborRight = items[i].GetPath();
            items[i].FocusNeighborTop = i > 0 ? items[i - 1].GetPath() : items[i].GetPath();
            items[i].FocusNeighborBottom = i < items.Count - 1 ? items[i + 1].GetPath() : items[i].GetPath();
            items[i].FocusMode = FocusModeEnum.All;
        }

        items.FirstOrDefault()?.TryGrabFocus();
    }

    private void CloseDropdown()
    {
        dismisser?.Visible = false;

        dropdownContainer?.Visible = false;

        RestorePopupLayer();
        isOpen = false;
        this.TryGrabFocus();
    }

    private void PreparePopupLayer()
    {
        if (dropdownContainer != null)
        {
            dropdownContainer.TopLevel = true;
            dropdownContainer.ZAsRelative = false;
            dropdownContainer.ZIndex = PopupZIndex + 10;
            dropdownContainer.MouseFilter = MouseFilterEnum.Stop;
            dropdownContainer.CallDeferred("RefreshLayout");
        }

        if (dismisser != null)
        {
            dismisser.TopLevel = true;
            dismisser.ZAsRelative = false;
            dismisser.ZIndex = PopupZIndex;
            dismisser.MouseFilter = MouseFilterEnum.Stop;

            Viewport? viewport = GetViewport();
            if (viewport != null)
            {
                Rect2 visibleRect = viewport.GetVisibleRect();
                dismisser.GlobalPosition = visibleRect.Position;
                dismisser.Size = visibleRect.Size;
            }
        }

        PositionPopupLayer();
    }

    private void RestorePopupLayer()
    {
        if (dropdownContainer != null)
        {
            dropdownContainer.TopLevel = dropdownOriginalTopLevel;
            dropdownContainer.ZIndex = dropdownOriginalZIndex;
            dropdownContainer.ZAsRelative = dropdownOriginalZAsRelative;
            dropdownContainer.Position = dropdownLocalPosition;
        }

        if (dismisser != null)
        {
            dismisser.TopLevel = dismisserOriginalTopLevel;
            dismisser.ZIndex = dismisserOriginalZIndex;
            dismisser.ZAsRelative = dismisserOriginalZAsRelative;
            dismisser.Position = dismisserLocalPosition;
            dismisser.Size = dismisserOriginalSize;
        }
    }

    private void PositionPopupLayer()
    {
        dropdownContainer?.GlobalPosition = GlobalPosition + dropdownLocalPosition;

        if (dismisser != null)
        {
            Viewport? viewport = GetViewport();
            if (viewport != null)
            {
                Rect2 visibleRect = viewport.GetVisibleRect();
                dismisser.GlobalPosition = visibleRect.Position;
                dismisser.Size = visibleRect.Size;
            }
        }
    }

    private void OnDropdownItemSelected(NDropdownItem dropdownItem)
    {
        string value = dropdownItem is JmcDropdownItem item ? item.Value : dropdownItem.Text;
        SetValue(value);
        CloseDropdown();
        if (!suppressChanged)
        {
            onChanged?.Invoke(value);
        }
    }

    private string GetDisplayText(string value)
    {
        return options.FirstOrDefault(option =>
            string.Equals(option.Value, value, StringComparison.OrdinalIgnoreCase)) is { Text: { Length: > 0 } text }
            ? text
            : value;
    }
}
