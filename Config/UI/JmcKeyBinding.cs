using Godot;

namespace JmcModLib.Config.UI;

[Flags]
public enum JmcKeyModifiers
{
    None = 0,
    Ctrl = 1,
    Shift = 2,
    Alt = 4,
    Meta = 8
}

/// <summary>
/// Stores a mod-owned key binding without injecting it into the game's built-in input command table.
/// </summary>
public readonly record struct JmcKeyBinding(
    Key Keyboard = Key.None,
    string Controller = "",
    JmcKeyModifiers Modifiers = JmcKeyModifiers.None)
{
    public bool HasKeyboard => Keyboard != Key.None;

    public bool HasModifiers => Modifiers != JmcKeyModifiers.None;

    public bool HasController => !string.IsNullOrWhiteSpace(Controller);

    public JmcKeyBinding(Key keyboard, JmcKeyModifiers modifiers)
        : this(keyboard, string.Empty, modifiers)
    {
    }

    public JmcKeyBinding WithKeyboard(Key keyboard)
    {
        return WithKeyboard(keyboard, JmcKeyModifiers.None);
    }

    public JmcKeyBinding WithKeyboard(Key keyboard, JmcKeyModifiers modifiers)
    {
        return this with
        {
            Keyboard = keyboard,
            Modifiers = keyboard == Key.None ? JmcKeyModifiers.None : modifiers
        };
    }

    public JmcKeyBinding WithController(string? controller)
    {
        return this with { Controller = controller?.Trim() ?? string.Empty };
    }

    public bool IsPressed(InputEvent inputEvent, bool allowEcho = false, bool exactModifiers = true)
    {
        ArgumentNullException.ThrowIfNull(inputEvent);

        if (HasKeyboard
            && inputEvent is InputEventKey { Pressed: true } keyEvent
            && (allowEcho || !keyEvent.Echo)
            && ResolveKeycode(keyEvent) == Keyboard
            && AreModifiersMatched(keyEvent, exactModifiers))
        {
            return true;
        }

        return HasController && inputEvent.IsActionPressed(new StringName(Controller));
    }

    public bool IsReleased(InputEvent inputEvent)
    {
        ArgumentNullException.ThrowIfNull(inputEvent);

        if (HasKeyboard
            && inputEvent is InputEventKey { Pressed: false } keyEvent
            && ResolveKeycode(keyEvent) == Keyboard)
        {
            return true;
        }

        return HasController && inputEvent.IsActionReleased(new StringName(Controller));
    }

    public static implicit operator JmcKeyBinding(Key keyboard)
    {
        return new JmcKeyBinding(keyboard);
    }

    public static bool IsPressed(Key keyboard, InputEvent inputEvent, bool allowEcho = false)
    {
        return new JmcKeyBinding(keyboard).IsPressed(inputEvent, allowEcho);
    }

    public static bool IsReleased(Key keyboard, InputEvent inputEvent)
    {
        return new JmcKeyBinding(keyboard).IsReleased(inputEvent);
    }

    public string ToKeyboardText()
    {
        if (!HasKeyboard)
        {
            return string.Empty;
        }

        string key = FormatKey(Keyboard);
        return HasModifiers
            ? $"{FormatModifiers(Modifiers)} + {key}"
            : key;
    }

    public override string ToString()
    {
        string keyboard = ToKeyboardText();
        return (keyboard, HasController ? Controller : string.Empty) switch
        {
            ({ Length: > 0 }, { Length: > 0 } controller) => $"{keyboard} / {controller}",
            ({ Length: > 0 }, _) => keyboard,
            (_, { Length: > 0 } controller) => controller,
            _ => string.Empty
        };
    }

    public static JmcKeyModifiers ReadModifiers(InputEventKey keyEvent)
    {
        ArgumentNullException.ThrowIfNull(keyEvent);

        JmcKeyModifiers modifiers = JmcKeyModifiers.None;
        if (keyEvent.CtrlPressed)
        {
            modifiers |= JmcKeyModifiers.Ctrl;
        }

        if (keyEvent.ShiftPressed)
        {
            modifiers |= JmcKeyModifiers.Shift;
        }

        if (keyEvent.AltPressed)
        {
            modifiers |= JmcKeyModifiers.Alt;
        }

        if (keyEvent.MetaPressed)
        {
            modifiers |= JmcKeyModifiers.Meta;
        }

        return modifiers;
    }

    public static bool IsModifierKey(Key key)
    {
        return key is Key.Ctrl
            or Key.Shift
            or Key.Alt
            or Key.Meta;
    }

    public static Key ReadKey(InputEventKey keyEvent)
    {
        ArgumentNullException.ThrowIfNull(keyEvent);
        return ResolveKeycode(keyEvent);
    }

    private static Key ResolveKeycode(InputEventKey keyEvent)
    {
        if (keyEvent.Keycode != Key.None)
        {
            return keyEvent.Keycode;
        }

        if (keyEvent.PhysicalKeycode != Key.None)
        {
            return keyEvent.PhysicalKeycode;
        }

        return keyEvent.KeyLabel;
    }

    private bool AreModifiersMatched(InputEventKey keyEvent, bool exactModifiers)
    {
        JmcKeyModifiers pressedModifiers = ReadModifiers(keyEvent);
        return exactModifiers
            ? pressedModifiers == Modifiers
            : (pressedModifiers & Modifiers) == Modifiers;
    }

    private static string FormatModifiers(JmcKeyModifiers modifiers)
    {
        List<string> parts = [];
        if (modifiers.HasFlag(JmcKeyModifiers.Ctrl))
        {
            parts.Add("Ctrl");
        }

        if (modifiers.HasFlag(JmcKeyModifiers.Shift))
        {
            parts.Add("Shift");
        }

        if (modifiers.HasFlag(JmcKeyModifiers.Alt))
        {
            parts.Add("Alt");
        }

        if (modifiers.HasFlag(JmcKeyModifiers.Meta))
        {
            parts.Add("Meta");
        }

        return string.Join(" + ", parts);
    }

    private static string FormatKey(Key key)
    {
        return key.ToString();
    }
}

internal static class JmcKeyBindingValue
{
    public static JmcKeyBinding FromValue(object? value)
    {
        return value switch
        {
            JmcKeyBinding binding => binding,
            Key keyboard => new JmcKeyBinding(keyboard),
            null => default,
            _ => throw new ArgumentException($"Cannot convert {value.GetType().FullName} to {nameof(JmcKeyBinding)}.")
        };
    }

    public static object ToEntryValue(JmcKeyBinding binding, Type targetType)
    {
        Type actualType = Nullable.GetUnderlyingType(targetType) ?? targetType;
        return IsGodotKeyType(actualType)
            ? binding.Keyboard
            : binding;
    }

    private static bool IsGodotKeyType(Type type)
    {
        return type == typeof(Key)
            || string.Equals(type.FullName, typeof(Key).FullName, StringComparison.Ordinal);
    }
}
