using Godot;
using JmcModLib.Config.Entry;

namespace JmcModLib.Config.UI;

internal sealed partial class ModSettingsPanel
{
    private Control BuildKeybindEditor(
        ConfigEntry entry,
        UIKeybindAttribute keybindAttribute,
        List<Control> focusableControls)
    {
        JmcKeyBinding binding = JmcKeyBindingValue.FromValue(entry.GetValue());
        string label = ConfigLocalization.GetDisplayName(entry);
        bool supportsEnableToggle = SupportsKeybindEnableToggle(entry.ValueType);

        if (nativeTemplates?.KeybindTemplate != null)
        {
            JmcSettingsTickbox? enableTickbox = null;
            JmcKeybindButton keybind = JmcKeybindButton.Create(
                nativeTemplates.KeybindTemplate,
                label,
                binding,
                keybindAttribute,
                button =>
                {
                    if (listeningKeybind != button)
                    {
                        listeningKeybind?.CancelListening();
                    }

                    listeningKeybind = button;
                },
                newBinding =>
                {
                    if (suppressControlEvents)
                    {
                        return;
                    }

                    TrySetEntryValue(entry, JmcKeyBindingValue.ToEntryValue(newBinding, entry.ValueType));
                });

            bindings[CreateBindingKey(entry)] = rawValue =>
            {
                RunSuppressed(() =>
                {
                    JmcKeyBinding updatedBinding = JmcKeyBindingValue.FromValue(rawValue);
                    keybind.SetValue(updatedBinding);
                    enableTickbox?.SetValue(updatedBinding.Enabled);
                });
            };

            keybind.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            keybind.SizeFlagsVertical = SizeFlags.ShrinkCenter;
            focusableControls.Add(keybind);

            if (!supportsEnableToggle || nativeTemplates.TickboxTemplate == null)
            {
                return keybind;
            }

            enableTickbox = JmcSettingsTickbox.Create(
                nativeTemplates.TickboxTemplate,
                binding.Enabled,
                enabled =>
                {
                    if (suppressControlEvents)
                    {
                        return;
                    }

                    JmcKeyBinding updatedBinding = JmcKeyBindingValue.FromValue(entry.GetValue()).WithEnabled(enabled);
                    keybind.SetValue(updatedBinding);
                    TrySetEntryValue(entry, updatedBinding);
                });
            enableTickbox.CustomMinimumSize = new Vector2(KeybindEnableToggleWidth, ActionButtonHeight);
            enableTickbox.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
            enableTickbox.SizeFlagsVertical = SizeFlags.ShrinkCenter;
            focusableControls.Add(enableTickbox);

            keybind.CustomMinimumSize = new Vector2(KeybindButtonWithToggleWidth, keybind.CustomMinimumSize.Y);

            var row = new HBoxContainer
            {
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                SizeFlagsVertical = SizeFlags.ShrinkCenter
            };
            row.AddThemeConstantOverride("separation", 12);
            row.AddChild(keybind);
            row.AddChild(enableTickbox);
            return row;
        }

        var fallbackButton = new Button
        {
            Text = $"{label}: {FormatKeybind(binding, keybindAttribute)}",
            FocusMode = FocusModeEnum.All,
            CustomMinimumSize = new Vector2(420f, ActionButtonHeight)
        };
        fallbackButton.Pressed += () => ModLogger.Warn(
            $"Keybind config {entry.Key} is using fallback display because the native input settings template was not found.",
            entry.Assembly);

        focusableControls.Add(fallbackButton);
        return fallbackButton;
    }

    private static bool SupportsKeybindEnableToggle(Type valueType)
    {
        Type actualType = Nullable.GetUnderlyingType(valueType) ?? valueType;
        return actualType == typeof(JmcKeyBinding);
    }
}
