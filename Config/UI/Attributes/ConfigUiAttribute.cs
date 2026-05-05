using Godot;
using MegaCrit.Sts2.Core.Logging;
using System.Reflection;

namespace JmcModLib.Config.UI;

/// <summary>
/// Adds a button row to the in-game mod settings UI.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class UIButtonAttribute(
    string description,
    string buttonText = "按钮",
    string group = ConfigAttribute.DefaultGroup) : Attribute
{
    public string Description { get; } = description;

    public string ButtonText { get; } = buttonText;

    public string Group { get; } = group;

    public string? Key { get; set; }

    public string? LocTable { get; set; }

    public string? DisplayNameKey { get; set; }

    public string? DescriptionKey { get; set; }

    public string? ButtonTextKey { get; set; }

    public string? GroupKey { get; set; }

    public UIButtonColor Color { get; set; } = UIButtonColor.Default;

    public int Order { get; set; }

    public string? HelpText { get; set; }

    public static bool IsValidMethod(MethodInfo method, out LogLevel? level, out string? errorMessage)
    {
        ArgumentNullException.ThrowIfNull(method);

        level = null;
        errorMessage = null;

        if (!method.IsStatic)
        {
            level = LogLevel.Error;
            errorMessage = "UIButton method must be static.";
            return false;
        }

        ParameterInfo[] parameters = method.GetParameters();
        if (parameters.Length != 0)
        {
            level = LogLevel.Error;
            errorMessage = $"UIButton method must have no parameters, but {parameters.Length} were found.";
            return false;
        }

        if (method.ReturnType != typeof(void))
        {
            level = LogLevel.Warn;
            errorMessage = $"UIButton method {method.Name} should return void. The return value will be ignored.";
        }

        return true;
    }
}

/// <summary>
/// Base metadata attribute for later in-game config UI bridging.
/// </summary>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false)]
public abstract class UIConfigAttribute : Attribute
{
    public virtual bool IsValid(Type valueType, object? defaultValue, out string? errorMessage)
    {
        errorMessage = null;
        return true;
    }
}

public abstract class UIConfigAttribute<TValue> : UIConfigAttribute
{
    public override bool IsValid(Type valueType, object? defaultValue, out string? errorMessage)
    {
        if (valueType != typeof(TValue))
        {
            errorMessage =
                $"{GetType().Name} only supports {typeof(TValue).FullName}, but the config value is {valueType.FullName}.";
            return false;
        }

        errorMessage = null;
        return true;
    }
}

public sealed class UIToggleAttribute : UIConfigAttribute<bool>
{
}

/// <summary>
/// 将 <see cref="Key"/> 或 <see cref="JmcKeyBinding"/> 配置项渲染为按键绑定控件。
/// </summary>
/// <param name="allowController">是否允许手柄绑定；为 <see langword="true"/> 时字段类型必须是 <see cref="JmcKeyBinding"/>，并会参与 JML Steam Input action 生成。</param>
/// <param name="allowKeyboard">是否允许键盘绑定。</param>
public sealed class UIKeybindAttribute(bool allowController = false, bool allowKeyboard = true) : UIConfigAttribute
{
    /// <summary>
    /// 是否允许键盘绑定。
    /// </summary>
    public bool AllowKeyboard { get; } = allowKeyboard;

    /// <summary>
    /// 是否允许手柄绑定；Steam Input 可用时，实际手柄按键建议在 Steam 输入中绑定。
    /// </summary>
    public bool AllowController { get; } = allowController;

    public override bool IsValid(Type valueType, object? defaultValue, out string? errorMessage)
    {
        Type actualType = Nullable.GetUnderlyingType(valueType) ?? valueType;

        if (!AllowKeyboard && !AllowController)
        {
            errorMessage = $"{GetType().Name} must allow keyboard, controller, or both.";
            return false;
        }

        if (actualType == typeof(Key))
        {
            if (AllowController)
            {
                errorMessage = $"{GetType().Name} with controller support requires {typeof(JmcKeyBinding).FullName}.";
                return false;
            }

            errorMessage = null;
            return true;
        }

        if (actualType == typeof(JmcKeyBinding))
        {
            errorMessage = null;
            return true;
        }

        errorMessage =
            $"{GetType().Name} only supports {typeof(Key).FullName} or {typeof(JmcKeyBinding).FullName}, but received {valueType.FullName}.";
        return false;
    }
}

public sealed class UIInputAttribute(int characterLimit = 0, bool multiline = false) : UIConfigAttribute<string>
{
    public int CharacterLimit { get; } = characterLimit;

    public bool Multiline { get; } = multiline;
}

public enum UIColorPalette
{
    None,
    Basic,
    Game,
    CardRarity,
    Rainbow
}

public sealed class UIColorAttribute(params string[] presets) : UIConfigAttribute<Color>
{
    public string[] Presets { get; } = presets;

    public UIColorPalette Palette { get; set; } = UIColorPalette.Game;

    public bool AllowCustom { get; set; } = true;

    public bool AllowAlpha { get; set; } = true;
}

public interface ISliderConfigAttribute
{
    double Min { get; }

    double Max { get; }

    double Step { get; }
}

public sealed class UISliderAttribute(double min, double max, double step = 1.0) : UIConfigAttribute, ISliderConfigAttribute
{
    public double Min { get; } = min;

    public double Max { get; } = max;

    public double Step { get; } = step;

    public override bool IsValid(Type valueType, object? defaultValue, out string? errorMessage)
    {
        if (!IsNumericType(valueType))
        {
            errorMessage = $"{GetType().Name} only supports numeric config values, but received {valueType.FullName}.";
            return false;
        }

        if (Max < Min)
        {
            errorMessage = $"{GetType().Name} requires Max >= Min.";
            return false;
        }

        if (Step <= 0)
        {
            errorMessage = $"{GetType().Name} requires Step > 0.";
            return false;
        }

        errorMessage = null;
        return true;
    }

    private static bool IsNumericType(Type type)
    {
        Type actualType = Nullable.GetUnderlyingType(type) ?? type;
        return actualType == typeof(byte)
            || actualType == typeof(sbyte)
            || actualType == typeof(short)
            || actualType == typeof(ushort)
            || actualType == typeof(int)
            || actualType == typeof(uint)
            || actualType == typeof(long)
            || actualType == typeof(ulong)
            || actualType == typeof(float)
            || actualType == typeof(double)
            || actualType == typeof(decimal);
    }
}

public sealed class UIIntSliderAttribute(int min, int max, int characterLimit = 5) : UIConfigAttribute<int>, ISliderConfigAttribute
{
    public int CharacterLimit { get; } = characterLimit;

    public double Min { get; } = min;

    public double Max { get; } = max;

    public double Step { get; } = 1.0;

    public override bool IsValid(Type valueType, object? defaultValue, out string? errorMessage)
    {
        if (valueType != typeof(int))
        {
            errorMessage =
                $"{GetType().Name} only supports {typeof(int).FullName}, but the config value is {valueType.FullName}.";
            return false;
        }

        if (Max < Min)
        {
            errorMessage = $"{GetType().Name} requires Max >= Min.";
            return false;
        }

        errorMessage = null;
        return true;
    }
}

public sealed class UIFloatSliderAttribute(
    float min,
    float max,
    int decimalPlaces = 1,
    int characterLimit = 5) : UIConfigAttribute<float>, ISliderConfigAttribute
{
    public int DecimalPlaces { get; } = decimalPlaces;

    public int CharacterLimit { get; } = characterLimit;

    public double Min { get; } = min;

    public double Max { get; } = max;

    public double Step => Math.Pow(10, -Math.Max(0, DecimalPlaces));

    public override bool IsValid(Type valueType, object? defaultValue, out string? errorMessage)
    {
        if (valueType != typeof(float))
        {
            errorMessage =
                $"{GetType().Name} only supports {typeof(float).FullName}, but the config value is {valueType.FullName}.";
            return false;
        }

        if (Max < Min)
        {
            errorMessage = $"{GetType().Name} requires Max >= Min.";
            return false;
        }

        if (DecimalPlaces < 0)
        {
            errorMessage = $"{GetType().Name} requires DecimalPlaces >= 0.";
            return false;
        }

        errorMessage = null;
        return true;
    }
}

public sealed class UIDropdownAttribute(params string[]? exclude) : UIConfigAttribute
{
    public IReadOnlyList<string> Options { get; } = exclude ?? [];

    public IReadOnlyList<string> Exclude { get; } = exclude ?? [];

    public override bool IsValid(Type valueType, object? defaultValue, out string? errorMessage)
    {
        Type actualType = Nullable.GetUnderlyingType(valueType) ?? valueType;
        if (actualType == typeof(string) || actualType.IsEnum)
        {
            errorMessage = null;
            return true;
        }

        errorMessage = $"{GetType().Name} only supports string or enum config values, but received {valueType.FullName}.";
        return false;
    }
}
