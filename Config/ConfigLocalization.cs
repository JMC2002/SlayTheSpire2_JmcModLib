using System.Reflection;
using JmcModLib.Config.Entry;
using JmcModLib.Utils;

namespace JmcModLib.Config;

internal static class ConfigLocalization
{
    public const string KeyPrefix = "EXTENSION.JMCMODLIB.CONFIG";

    private const string NameSuffix = "NAME";
    private const string DescriptionSuffix = "DESCRIPTION";
    private const string ButtonSuffix = "BUTTON";
    private const string OptionPrefix = "OPTION";
    private const string GroupPrefix = "GROUP";

    public static string GetDisplayName(ConfigEntry entry)
    {
        return ResolveEntryText(entry, entry.Attribute.DisplayNameKey, NameSuffix, entry.DisplayName);
    }

    public static string GetDisplayName(ConfigEntry entry, string language)
    {
        return ResolveEntryTextForLanguage(entry, entry.Attribute.DisplayNameKey, NameSuffix, entry.DisplayName, language);
    }

    public static string GetDescription(ConfigEntry entry)
    {
        return ResolveEntryText(entry, entry.Attribute.DescriptionKey, DescriptionSuffix, entry.Attribute.Description);
    }

    public static string GetButtonText(ButtonEntry entry)
    {
        return ResolveEntryText(entry, entry.ButtonTextKey, ButtonSuffix, entry.ButtonText);
    }

    public static string GetOptionText(ConfigEntry entry, string option)
    {
        string optionSuffix = $"{OptionPrefix}.{NormalizeKeySegment(option)}";
        return ResolveEntryText(entry, explicitKey: null, optionSuffix, option);
    }

    public static string GetGroupName(Assembly assembly, string group, IReadOnlyCollection<ConfigEntry> entries)
    {
        string? explicitKey = entries
            .Select(static entry => entry.Attribute.GroupKey)
            .FirstOrDefault(static key => !string.IsNullOrWhiteSpace(key));
        string? table = entries
            .Select(static entry => entry.Attribute.LocTable)
            .FirstOrDefault(static value => !string.IsNullOrWhiteSpace(value));
        string conventionalKey = BuildGroupKey(assembly, group);

        return L10n.ResolveAny(
            [explicitKey, conventionalKey],
            group,
            table,
            assembly);
    }

    public static string BuildEntryKey(ConfigEntry entry, string suffix)
    {
        return $"{KeyPrefix}.{NormalizeKeySegment(ModRegistry.GetModId(entry.Assembly))}.{NormalizeKeySegment(entry.StorageKey)}.{suffix}";
    }

    public static string BuildGroupKey(Assembly assembly, string group)
    {
        return $"{KeyPrefix}.{NormalizeKeySegment(ModRegistry.GetModId(assembly))}.{GroupPrefix}.{NormalizeKeySegment(group)}";
    }

    private static string ResolveEntryText(ConfigEntry entry, string? explicitKey, string suffix, string? fallback)
    {
        string conventionalKey = BuildEntryKey(entry, suffix);
        return L10n.ResolveAny(
            [explicitKey, conventionalKey],
            fallback,
            entry.Attribute.LocTable,
            entry.Assembly);
    }

    private static string ResolveEntryTextForLanguage(
        ConfigEntry entry,
        string? explicitKey,
        string suffix,
        string? fallback,
        string language)
    {
        string conventionalKey = BuildEntryKey(entry, suffix);
        return L10n.ResolveAnyForLanguage(
            [explicitKey, conventionalKey],
            language,
            fallback,
            entry.Attribute.LocTable,
            entry.Assembly);
    }

    private static string NormalizeKeySegment(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "_";
        }

        string trimmed = value.Trim().Replace('/', '.').Replace('\\', '.');
        return string.Join("_", trimmed.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }
}
