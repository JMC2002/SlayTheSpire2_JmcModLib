using Godot;

namespace JmcModLib.Config.UI;

/// <summary>
/// Stores a mod-owned key binding without injecting it into the game's built-in input command table.
/// </summary>
public readonly record struct JmcKeyBinding(Key Keyboard = Key.None, string Controller = "")
{
    public bool HasKeyboard => Keyboard != Key.None;

    public bool HasController => !string.IsNullOrWhiteSpace(Controller);

    public JmcKeyBinding WithKeyboard(Key keyboard)
    {
        return this with { Keyboard = keyboard };
    }

    public JmcKeyBinding WithController(string? controller)
    {
        return this with { Controller = controller?.Trim() ?? string.Empty };
    }

    public bool IsPressed(InputEvent inputEvent, bool allowEcho = false)
    {
        ArgumentNullException.ThrowIfNull(inputEvent);

        if (HasKeyboard
            && inputEvent is InputEventKey { Pressed: true } keyEvent
            && (allowEcho || !keyEvent.Echo)
            && ResolveKeycode(keyEvent) == Keyboard)
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

    private static Key ResolveKeycode(InputEventKey keyEvent)
    {
        return keyEvent.Keycode != Key.None ? keyEvent.Keycode : keyEvent.PhysicalKeycode;
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
