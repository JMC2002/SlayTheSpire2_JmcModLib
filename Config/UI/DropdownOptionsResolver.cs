using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using JmcModLib.Config.Entry;
using JmcModLib.Utils;

namespace JmcModLib.Config.UI;

internal static class DropdownOptionsResolver
{
    private static readonly string[] ProviderNameFormats =
    [
        "{0}Options",
        "Get{0}Options",
        "Build{0}Options"
    ];

    public static IReadOnlyList<string> Resolve(
        ConfigEntry entry,
        UIDropdownAttribute? dropdownAttribute,
        Type valueType)
    {
        Type actualType = Nullable.GetUnderlyingType(valueType) ?? valueType;
        IReadOnlyList<string> options = actualType.IsEnum
            ? [.. Enum.GetNames(actualType).Where(option => dropdownAttribute?.Exclude.Contains(option, StringComparer.OrdinalIgnoreCase) != true)]
            : dropdownAttribute?.Options.Count > 0
                ? dropdownAttribute.Options
                : TryResolveConventionOptions(entry);

        if (options.Count == 0)
        {
            options = [entry.GetValue()?.ToString() ?? string.Empty];
        }

        string[] filteredOptions = [.. options
            .Where(option => !string.IsNullOrWhiteSpace(option))
            .Distinct(StringComparer.Ordinal)];

        return filteredOptions.Length > 0
            ? filteredOptions
            : [entry.GetValue()?.ToString() ?? string.Empty];
    }

    private static IReadOnlyList<string> TryResolveConventionOptions(ConfigEntry entry)
    {
        if (!TryResolveDeclaringTypeAndMember(entry, out Type? declaringType, out string? memberName))
        {
            return [];
        }

        foreach (string nameFormat in ProviderNameFormats)
        {
            string providerName = string.Format(nameFormat, memberName);
            object? rawOptions = InvokeProvider(declaringType, providerName);
            IReadOnlyList<string> options = NormalizeOptions(rawOptions);
            if (options.Count > 0)
            {
                return options;
            }
        }

        return [];
    }

    private static bool TryResolveDeclaringTypeAndMember(
        ConfigEntry entry,
        [NotNullWhen(true)]
        out Type? declaringType,
        [NotNullWhen(true)]
        out string? memberName)
    {
        declaringType = null;
        memberName = null;

        foreach (Type type in entry.Assembly.GetTypes().OrderByDescending(type => type.FullName?.Length ?? 0))
        {
            string? fullName = type.FullName;
            if (string.IsNullOrWhiteSpace(fullName)
                || !entry.StorageKey.StartsWith(fullName + ".", StringComparison.Ordinal))
            {
                continue;
            }

            string candidateMemberName = entry.StorageKey[(fullName.Length + 1)..];
            if (string.IsNullOrWhiteSpace(candidateMemberName) || candidateMemberName.Contains('.'))
            {
                continue;
            }

            declaringType = type;
            memberName = candidateMemberName;
            return true;
        }

        return false;
    }

    private static object? InvokeProvider(Type declaringType, string providerName)
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

        try
        {
            MethodInfo? method = declaringType.GetMethod(providerName, flags, Type.EmptyTypes);
            if (method != null)
            {
                return method.Invoke(null, null);
            }

            PropertyInfo? property = declaringType.GetProperty(providerName, flags);
            return property?.GetValue(null);
        }
        catch (Exception ex)
        {
            ModLogger.Warn($"动态下拉选项 provider {declaringType.FullName}.{providerName} 执行失败：{ex.Message}");
            return null;
        }
    }

    private static IReadOnlyList<string> NormalizeOptions(object? rawOptions)
    {
        if (rawOptions == null)
        {
            return [];
        }

        if (rawOptions is string singleOption)
        {
            return [singleOption];
        }

        if (rawOptions is IEnumerable<string> stringOptions)
        {
            return [.. stringOptions];
        }

        if (rawOptions is IEnumerable options)
        {
            return [.. options.Cast<object?>().Select(option => option?.ToString() ?? string.Empty)];
        }

        return [rawOptions.ToString() ?? string.Empty];
    }
}
