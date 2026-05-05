using Godot;

namespace JmcModLib.Config.UI;

/// <summary>
/// 键盘热键使用的修饰键组合。
/// </summary>
[Flags]
public enum JmcKeyModifiers
{
    /// <summary>
    /// 不使用修饰键。
    /// </summary>
    None = 0,

    /// <summary>
    /// Ctrl 修饰键。
    /// </summary>
    Ctrl = 1,

    /// <summary>
    /// Shift 修饰键。
    /// </summary>
    Shift = 2,

    /// <summary>
    /// Alt 修饰键。
    /// </summary>
    Alt = 4,

    /// <summary>
    /// Meta/Command 修饰键。
    /// </summary>
    Meta = 8
}

/// <summary>
/// 表示由 MOD 自己持有的热键绑定，不会直接写入游戏原生输入命令表。
/// </summary>
public readonly record struct JmcKeyBinding
{
    private readonly bool disabled;

    /// <summary>
    /// 键盘按键；为 <see cref="Key.None"/> 时表示不绑定键盘。
    /// </summary>
    public Key Keyboard { get; init; }

    /// <summary>
    /// 手柄输入 Action 名称；为空时表示不绑定手柄。
    /// </summary>
    public string Controller { get; init; }

    /// <summary>
    /// 键盘组合修饰键。
    /// </summary>
    public JmcKeyModifiers Modifiers { get; init; }

    /// <summary>
    /// 当前热键是否启用。
    /// </summary>
    public bool Enabled
    {
        get => !disabled;
        init => disabled = !value;
    }

    /// <summary>
    /// 创建一个未绑定的热键。
    /// </summary>
    public JmcKeyBinding()
        : this(Key.None)
    {
    }

    /// <summary>
    /// 使用单个键盘按键创建热键。
    /// </summary>
    /// <param name="keyboard">键盘按键。</param>
    public JmcKeyBinding(Key keyboard)
        : this(keyboard, string.Empty, JmcKeyModifiers.None, true)
    {
    }

    /// <summary>
    /// 创建可同时保存键盘和手柄 Action 的热键。
    /// </summary>
    /// <param name="keyboard">键盘按键。</param>
    /// <param name="controller">手柄输入 Action 名称。</param>
    /// <param name="modifiers">键盘组合修饰键。</param>
    /// <param name="enabled">热键是否启用。</param>
    public JmcKeyBinding(
        Key keyboard = Key.None,
        string controller = "",
        JmcKeyModifiers modifiers = JmcKeyModifiers.None,
        bool enabled = true)
    {
        Keyboard = keyboard;
        Controller = controller;
        Modifiers = modifiers;
        disabled = !enabled;
    }

    /// <summary>
    /// 创建同时包含键盘按键、手柄 Action 和修饰键的启用热键。
    /// </summary>
    /// <param name="keyboard">键盘按键。</param>
    /// <param name="controller">手柄输入 Action 名称。</param>
    /// <param name="modifiers">键盘组合修饰键。</param>
    public JmcKeyBinding(Key keyboard, string controller, JmcKeyModifiers modifiers)
        : this(keyboard, controller, modifiers, true)
    {
    }

    /// <summary>
    /// 使用键盘按键和修饰键创建热键。
    /// </summary>
    /// <param name="keyboard">键盘按键。</param>
    /// <param name="modifiers">键盘组合修饰键。</param>
    /// <param name="enabled">热键是否启用。</param>
    public JmcKeyBinding(Key keyboard, JmcKeyModifiers modifiers, bool enabled = true)
        : this(keyboard, string.Empty, modifiers, enabled)
    {
    }

    /// <summary>
    /// 是否绑定了键盘按键。
    /// </summary>
    public bool HasKeyboard => Keyboard != Key.None;

    /// <summary>
    /// 是否配置了键盘修饰键。
    /// </summary>
    public bool HasModifiers => Modifiers != JmcKeyModifiers.None;

    /// <summary>
    /// 是否绑定了手柄 Action。
    /// </summary>
    public bool HasController => !string.IsNullOrWhiteSpace(Controller);

    /// <summary>
    /// 返回替换键盘按键后的新热键，并清空修饰键。
    /// </summary>
    /// <param name="keyboard">新的键盘按键。</param>
    /// <returns>替换后的热键值。</returns>
    public JmcKeyBinding WithKeyboard(Key keyboard)
    {
        return WithKeyboard(keyboard, JmcKeyModifiers.None);
    }

    /// <summary>
    /// 返回替换键盘按键和修饰键后的新热键。
    /// </summary>
    /// <param name="keyboard">新的键盘按键。</param>
    /// <param name="modifiers">新的修饰键组合。</param>
    /// <returns>替换后的热键值。</returns>
    public JmcKeyBinding WithKeyboard(Key keyboard, JmcKeyModifiers modifiers)
    {
        return this with
        {
            Keyboard = keyboard,
            Modifiers = keyboard == Key.None ? JmcKeyModifiers.None : modifiers
        };
    }

    /// <summary>
    /// 返回替换手柄 Action 后的新热键。
    /// </summary>
    /// <param name="controller">新的手柄输入 Action 名称。</param>
    /// <returns>替换后的热键值。</returns>
    public JmcKeyBinding WithController(string? controller)
    {
        return this with { Controller = controller?.Trim() ?? string.Empty };
    }

    /// <summary>
    /// 返回修改启用状态后的新热键。
    /// </summary>
    /// <param name="enabled">是否启用热键。</param>
    /// <returns>替换后的热键值。</returns>
    public JmcKeyBinding WithEnabled(bool enabled)
    {
        return this with { Enabled = enabled };
    }

    /// <summary>
    /// 判断输入事件是否按下了此热键。
    /// </summary>
    /// <param name="inputEvent">Godot 输入事件。</param>
    /// <param name="allowEcho">是否允许键盘 Echo 输入触发。</param>
    /// <param name="exactModifiers">是否要求修饰键完全一致。</param>
    /// <returns>如果本次输入触发了热键，返回 <see langword="true"/>。</returns>
    public bool IsPressed(InputEvent inputEvent, bool allowEcho = false, bool exactModifiers = true)
    {
        ArgumentNullException.ThrowIfNull(inputEvent);

        if (!Enabled)
        {
            return false;
        }

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

    /// <summary>
    /// 判断输入事件是否释放了此热键。
    /// </summary>
    /// <param name="inputEvent">Godot 输入事件。</param>
    /// <returns>如果本次输入释放了热键，返回 <see langword="true"/>。</returns>
    public bool IsReleased(InputEvent inputEvent)
    {
        ArgumentNullException.ThrowIfNull(inputEvent);

        if (!Enabled)
        {
            return false;
        }

        if (HasKeyboard
            && inputEvent is InputEventKey { Pressed: false } keyEvent
            && ResolveKeycode(keyEvent) == Keyboard)
        {
            return true;
        }

        return HasController && inputEvent.IsActionReleased(new StringName(Controller));
    }

    /// <summary>
    /// 判断此热键当前是否处于按下状态。
    /// </summary>
    /// <param name="exactModifiers">是否要求修饰键完全一致。</param>
    /// <returns>如果热键当前处于按下状态，返回 <see langword="true"/>。</returns>
    public bool IsDown(bool exactModifiers = true)
    {
        if (!Enabled)
        {
            return false;
        }

        if (HasKeyboard
            && Godot.Input.IsKeyPressed(Keyboard)
            && AreCurrentModifiersMatched(exactModifiers))
        {
            return true;
        }

        return HasController && Godot.Input.IsActionPressed(new StringName(Controller));
    }

    /// <summary>
    /// 将 <see cref="Key"/> 隐式转换为仅包含键盘按键的 <see cref="JmcKeyBinding"/>。
    /// </summary>
    /// <param name="keyboard">键盘按键。</param>
    public static implicit operator JmcKeyBinding(Key keyboard)
    {
        return new JmcKeyBinding(keyboard);
    }

    /// <summary>
    /// 判断输入事件是否按下了指定键盘按键。
    /// </summary>
    /// <param name="keyboard">键盘按键。</param>
    /// <param name="inputEvent">Godot 输入事件。</param>
    /// <param name="allowEcho">是否允许键盘 Echo 输入触发。</param>
    /// <returns>如果本次输入按下了指定按键，返回 <see langword="true"/>。</returns>
    public static bool IsPressed(Key keyboard, InputEvent inputEvent, bool allowEcho = false)
    {
        return new JmcKeyBinding(keyboard).IsPressed(inputEvent, allowEcho);
    }

    /// <summary>
    /// 判断输入事件是否释放了指定键盘按键。
    /// </summary>
    /// <param name="keyboard">键盘按键。</param>
    /// <param name="inputEvent">Godot 输入事件。</param>
    /// <returns>如果本次输入释放了指定按键，返回 <see langword="true"/>。</returns>
    public static bool IsReleased(Key keyboard, InputEvent inputEvent)
    {
        return new JmcKeyBinding(keyboard).IsReleased(inputEvent);
    }

    /// <summary>
    /// 生成键盘热键的可读文本。
    /// </summary>
    /// <returns>键盘热键文本；没有键盘绑定时返回空字符串。</returns>
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

    /// <summary>
    /// 生成包含键盘与手柄绑定的可读文本。
    /// </summary>
    /// <returns>热键文本。</returns>
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

    /// <summary>
    /// 从键盘输入事件中读取当前按下的修饰键。
    /// </summary>
    /// <param name="keyEvent">键盘输入事件。</param>
    /// <returns>修饰键组合。</returns>
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

    /// <summary>
    /// 读取当前实际按下的键盘修饰键。
    /// </summary>
    /// <returns>修饰键组合。</returns>
    public static JmcKeyModifiers ReadCurrentModifiers()
    {
        JmcKeyModifiers modifiers = JmcKeyModifiers.None;
        if (Godot.Input.IsKeyPressed(Key.Ctrl))
        {
            modifiers |= JmcKeyModifiers.Ctrl;
        }

        if (Godot.Input.IsKeyPressed(Key.Shift))
        {
            modifiers |= JmcKeyModifiers.Shift;
        }

        if (Godot.Input.IsKeyPressed(Key.Alt))
        {
            modifiers |= JmcKeyModifiers.Alt;
        }

        if (Godot.Input.IsKeyPressed(Key.Meta))
        {
            modifiers |= JmcKeyModifiers.Meta;
        }

        return modifiers;
    }

    /// <summary>
    /// 判断指定键是否是修饰键。
    /// </summary>
    /// <param name="key">键盘按键。</param>
    /// <returns>如果是修饰键，返回 <see langword="true"/>。</returns>
    public static bool IsModifierKey(Key key)
    {
        return key is Key.Ctrl
            or Key.Shift
            or Key.Alt
            or Key.Meta;
    }

    /// <summary>
    /// 从键盘输入事件中读取实际按键。
    /// </summary>
    /// <param name="keyEvent">键盘输入事件。</param>
    /// <returns>读取到的键盘按键。</returns>
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

    private bool AreCurrentModifiersMatched(bool exactModifiers)
    {
        JmcKeyModifiers pressedModifiers = ReadCurrentModifiers();
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
