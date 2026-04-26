using Godot;
using JmcModLib.Config;
using MegaCrit.Sts2.Core.Logging;
using System.Reflection;

namespace JmcModLib.Config.UI;

/// <summary>
/// Binds a static no-argument method to an existing config member that stores a Key or JmcKeyBinding.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class JmcHotkeyAttribute(string bindingMember) : Attribute
{
    public string BindingMember { get; } = bindingMember;

    public string? Key { get; set; }

    public bool ConsumeInput { get; set; } = true;

    public bool ExactModifiers { get; set; } = true;

    public bool AllowEcho { get; set; }

    public ulong DebounceMs { get; set; } = 150;
}

/// <summary>
/// Creates a configurable hotkey row and binds it to a static no-argument method.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class UIHotkeyAttribute(
    string displayName,
    string group = ConfigAttribute.DefaultGroup) : Attribute
{
    public string DisplayName { get; } = displayName;

    public string Group { get; } = group;

    public string? Key { get; set; }

    public string? Description { get; set; }

    public string? LocTable { get; set; }

    public string? DisplayNameKey { get; set; }

    public string? DescriptionKey { get; set; }

    public string? GroupKey { get; set; }

    public int Order { get; set; }

    public bool RestartRequired { get; set; }

    public Key DefaultKeyboard { get; set; } = Godot.Key.None;

    public JmcKeyModifiers DefaultModifiers { get; set; }

    public string DefaultController { get; set; } = string.Empty;

    public bool AllowKeyboard { get; set; } = true;

    public bool AllowController { get; set; }

    public bool ConsumeInput { get; set; } = true;

    public bool ExactModifiers { get; set; } = true;

    public bool AllowEcho { get; set; }

    public ulong DebounceMs { get; set; } = 150;
}

internal static class HotkeyMethodValidator
{
    public static bool IsValidMethod(MethodInfo method, out LogLevel? level, out string? errorMessage)
    {
        ArgumentNullException.ThrowIfNull(method);

        level = null;
        errorMessage = null;

        if (!method.IsStatic)
        {
            level = LogLevel.Error;
            errorMessage = "Hotkey method must be static.";
            return false;
        }

        ParameterInfo[] parameters = method.GetParameters();
        if (parameters.Length != 0)
        {
            level = LogLevel.Error;
            errorMessage = $"Hotkey method must have no parameters, but {parameters.Length} were found.";
            return false;
        }

        if (method.ReturnType != typeof(void))
        {
            level = LogLevel.Warn;
            errorMessage = $"Hotkey method {method.Name} should return void. The return value will be ignored.";
        }

        return true;
    }
}
