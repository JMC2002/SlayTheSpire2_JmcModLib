using MegaCrit.Sts2.Core.Logging;
using System.Reflection;

namespace JmcModLib.Config;

/// <summary>
/// Marks a static field or property as a configuration entry.
/// </summary>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false)]
public sealed class ConfigAttribute(
    string displayName,
    string? onChanged = null,
    string group = ConfigAttribute.DefaultGroup) : Attribute
{
    public const string DefaultGroup = "DefaultGroup";

    public string DisplayName { get; } = displayName;

    public string? OnChanged { get; } = onChanged;

    public string Group { get; } = group;

    public string? Key { get; set; }

    public string? Description { get; set; }

    public string? LocTable { get; set; }

    public string? DisplayNameKey { get; set; }

    public string? DescriptionKey { get; set; }

    public string? GroupKey { get; set; }

    public int Order { get; set; }

    public bool RestartRequired { get; set; }

    public static bool IsValidMethod(
        MethodInfo method,
        Type valueType,
        out LogLevel? level,
        out string? errorMessage)
    {
        ArgumentNullException.ThrowIfNull(method);
        ArgumentNullException.ThrowIfNull(valueType);

        level = null;
        errorMessage = null;

        if (!method.IsStatic)
        {
            level = LogLevel.Error;
            errorMessage = "Config callback must be static.";
            return false;
        }

        ParameterInfo[] parameters = method.GetParameters();
        if (parameters.Length != 1)
        {
            level = LogLevel.Error;
            errorMessage = $"Config callback must have exactly one parameter, but {parameters.Length} were found.";
            return false;
        }

        if (parameters[0].ParameterType != valueType)
        {
            level = LogLevel.Error;
            errorMessage =
                $"Config callback parameter type must be {valueType.FullName}, but was {parameters[0].ParameterType.FullName}.";
            return false;
        }

        if (method.ReturnType != typeof(void))
        {
            level = LogLevel.Warn;
            errorMessage = $"Config callback {method.Name} should return void. The return value will be ignored.";
        }

        return true;
    }
}
