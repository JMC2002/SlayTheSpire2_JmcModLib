using System.Reflection;
using System.Text.Json;
using Godot;
using MegaCrit.Sts2.Core.Localization;

namespace JmcModLib.Utils;

/// <summary>
/// STS2 localization helpers.
///
/// Unlike Duckov, STS2 discovers mod localization tables from the PCK automatically.
/// This helper only builds resource paths and LocString wrappers; no startup registration is required.
/// </summary>
public static class L10n
{
    public const string FallbackLanguage = "eng";
    public const string DefaultTable = "settings_ui";
    private static readonly Dictionary<(Assembly Assembly, string Language, string Table), IReadOnlyDictionary<string, string>> TableCache = [];

    public static IReadOnlyList<string> SupportedLanguages => LocManager.Languages;

    public static string CurrentLanguage => LocManager.Instance?.Language ?? FallbackLanguage;

    public static string GetModLocalizationRoot(Assembly? assembly = null)
    {
        assembly = ResolveAssembly(assembly);
        return $"res://{ModRuntime.GetPckName(assembly)}/localization";
    }

    public static string GetModLocalizationDirectory(string? language = null, Assembly? assembly = null)
    {
        assembly = ResolveAssembly(assembly);
        return $"{GetModLocalizationRoot(assembly)}/{NormalizeLanguage(language)}";
    }

    public static string GetModTablePath(string fileName, string? language = null, Assembly? assembly = null)
    {
        assembly = ResolveAssembly(assembly);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        string normalizedFileName = fileName.Contains('.')
            ? fileName
            : $"{fileName}.json";

        return $"{GetModLocalizationDirectory(language, assembly)}/{normalizedFileName}";
    }

    public static bool HasModTable(string fileName, string? language = null, Assembly? assembly = null)
    {
        return ResourceLoader.Exists(GetModTablePath(fileName, language, assembly));
    }

    public static IEnumerable<string> EnumerateExistingModTablePaths(string fileName, Assembly? assembly = null)
    {
        assembly = ResolveAssembly(assembly);

        string primaryPath = GetModTablePath(fileName, CurrentLanguage, assembly);
        if (ResourceLoader.Exists(primaryPath))
        {
            yield return primaryPath;
        }

        if (!string.Equals(CurrentLanguage, FallbackLanguage, StringComparison.OrdinalIgnoreCase))
        {
            string fallbackPath = GetModTablePath(fileName, FallbackLanguage, assembly);
            if (ResourceLoader.Exists(fallbackPath))
            {
                yield return fallbackPath;
            }
        }
    }

    public static LocString Create(string table, string key, Action<LocString>? configure = null)
    {
        LocString locString = new(table, key);
        configure?.Invoke(locString);
        return locString;
    }

    public static LocString? CreateIfExists(string table, string key, Action<LocString>? configure = null)
    {
        if (!Exists(table, key))
        {
            return null;
        }

        LocString locString = new(table, key);
        configure?.Invoke(locString);
        return locString;
    }

    public static bool Exists(string table, string key)
    {
        if (LocManager.Instance == null
            || string.IsNullOrWhiteSpace(table)
            || string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        try
        {
            return LocString.Exists(table.Trim(), key.Trim());
        }
        catch
        {
            return false;
        }
    }

    public static bool TryGetFormattedText(
        string table,
        string key,
        out string? text,
        Action<LocString>? configure = null,
        Assembly? assembly = null)
    {
        text = null;

        if (!TryNormalizeLocReference(table, key, out string normalizedTable, out string normalizedKey)
            || !Exists(normalizedTable, normalizedKey))
        {
            return false;
        }

        try
        {
            text = GetFormattedText(normalizedTable, normalizedKey, configure);
            return true;
        }
        catch (Exception ex)
        {
            ModLogger.Warn(
                $"Failed to format localization entry {normalizedTable}/{normalizedKey}.",
                ex,
                assembly);
            return false;
        }
    }

    public static string Resolve(
        string? key,
        string? fallback = null,
        string? table = null,
        Assembly? assembly = null,
        Action<LocString>? configure = null)
    {
        if (TryResolve(key, out string? text, table, assembly, configure))
        {
            return text;
        }

        return fallback ?? string.Empty;
    }

    public static string ResolveAny(
        IEnumerable<string?> keys,
        string? fallback = null,
        string? table = null,
        Assembly? assembly = null,
        Action<LocString>? configure = null)
    {
        foreach (string? key in keys)
        {
            if (TryResolve(key, out string? text, table, assembly, configure))
            {
                return text;
            }
        }

        return fallback ?? string.Empty;
    }

    internal static string ResolveAnyForLanguage(
        IEnumerable<string?> keys,
        string language,
        string? fallback = null,
        string? table = null,
        Assembly? assembly = null)
    {
        foreach (string? key in keys)
        {
            if (TryResolveForLanguage(key, language, out string? text, table, assembly))
            {
                return text;
            }
        }

        return fallback ?? string.Empty;
    }

    public static string ResolvePath(
        string? path,
        string? fallback = null,
        Assembly? assembly = null,
        Action<LocString>? configure = null)
    {
        return Resolve(path, fallback, DefaultTable, assembly, configure);
    }

    public static bool TryResolve(
        string? key,
        out string text,
        string? table = null,
        Assembly? assembly = null,
        Action<LocString>? configure = null)
    {
        text = string.Empty;
        if (string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        string resolvedTable = string.IsNullOrWhiteSpace(table) ? DefaultTable : table.Trim();
        string resolvedKey = key.Trim();
        if (!TryNormalizeLocReference(resolvedTable, resolvedKey, out resolvedTable, out resolvedKey))
        {
            return false;
        }

        if (!TryGetFormattedText(resolvedTable, resolvedKey, out string? resolvedText, configure, assembly))
        {
            return false;
        }

        text = resolvedText ?? string.Empty;
        return true;
    }

    internal static bool TryResolveForLanguage(
        string? key,
        string language,
        out string text,
        string? table = null,
        Assembly? assembly = null)
    {
        text = string.Empty;
        if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(language))
        {
            return false;
        }

        assembly = ResolveAssembly(assembly);
        string resolvedTable = string.IsNullOrWhiteSpace(table) ? DefaultTable : table.Trim();
        string resolvedKey = key.Trim();
        if (!TryNormalizeLocReference(resolvedTable, resolvedKey, out resolvedTable, out resolvedKey))
        {
            return false;
        }

        if (TryGetRawTextForLanguage(resolvedTable, resolvedKey, language, assembly, out text))
        {
            return true;
        }

        if (!string.Equals(language, FallbackLanguage, StringComparison.OrdinalIgnoreCase)
            && TryGetRawTextForLanguage(resolvedTable, resolvedKey, FallbackLanguage, assembly, out text))
        {
            return true;
        }

        return false;
    }

    public static string GetFormattedText(string table, string key, Action<LocString>? configure = null)
    {
        return Create(table, key, configure).GetFormattedText();
    }

    public static string GetRawText(string table, string key)
    {
        return new LocString(table, key).GetRawText();
    }

    public static void SubscribeToLocaleChange(LocManager.LocaleChangeCallback callback)
    {
        if (LocManager.Instance != null)
        {
            LocString.SubscribeToLocaleChange(callback);
        }
    }

    public static void UnsubscribeToLocaleChange(LocManager.LocaleChangeCallback callback)
    {
        if (LocManager.Instance != null)
        {
            LocString.UnsubscribeToLocaleChange(callback);
        }
    }

    private static string NormalizeLanguage(string? language)
    {
        return string.IsNullOrWhiteSpace(language)
            ? CurrentLanguage
            : language.Trim().ToLowerInvariant();
    }

    private static bool TryNormalizeLocReference(
        string table,
        string key,
        out string normalizedTable,
        out string normalizedKey)
    {
        normalizedTable = table.Trim();
        normalizedKey = key.Trim();

        int separatorIndex = normalizedKey.IndexOf('/');
        if (separatorIndex > 0 && separatorIndex < normalizedKey.Length - 1)
        {
            normalizedTable = normalizedKey[..separatorIndex].Trim();
            normalizedKey = normalizedKey[(separatorIndex + 1)..].Trim();
        }

        return !string.IsNullOrWhiteSpace(normalizedTable)
            && !string.IsNullOrWhiteSpace(normalizedKey);
    }

    private static Assembly ResolveAssembly(Assembly? assembly)
    {
        return AssemblyResolver.Resolve(assembly, typeof(L10n));
    }

    private static bool TryGetRawTextForLanguage(
        string table,
        string key,
        string language,
        Assembly assembly,
        out string text)
    {
        IReadOnlyDictionary<string, string> lookup = LoadTable(table, language, assembly);
        return lookup.TryGetValue(key, out text!);
    }

    private static IReadOnlyDictionary<string, string> LoadTable(string table, string language, Assembly assembly)
    {
        string normalizedLanguage = NormalizeLanguage(language);
        string normalizedTable = table.Trim();
        (Assembly, string, string) cacheKey = (assembly, normalizedLanguage, normalizedTable);
        lock (TableCache)
        {
            if (TableCache.TryGetValue(cacheKey, out IReadOnlyDictionary<string, string>? cached))
            {
                return cached;
            }
        }

        IReadOnlyDictionary<string, string> loaded = LoadTableUncached(normalizedTable, normalizedLanguage, assembly);
        lock (TableCache)
        {
            TableCache[cacheKey] = loaded;
        }

        return loaded;
    }

    private static IReadOnlyDictionary<string, string> LoadTableUncached(
        string table,
        string language,
        Assembly assembly)
    {
        string path = GetModTablePath(table, language, assembly);
        try
        {
            using Godot.FileAccess? file = Godot.FileAccess.Open(path, Godot.FileAccess.ModeFlags.Read);
            if (file == null)
            {
                return new Dictionary<string, string>(StringComparer.Ordinal);
            }

            using JsonDocument document = JsonDocument.Parse(file.GetAsText());
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return new Dictionary<string, string>(StringComparer.Ordinal);
            }

            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (JsonProperty property in document.RootElement.EnumerateObject())
            {
                result[property.Name] = property.Value.ValueKind == JsonValueKind.String
                    ? property.Value.GetString() ?? string.Empty
                    : property.Value.ToString();
            }

            return result;
        }
        catch (Exception ex)
        {
            ModLogger.Warn($"读取本地化表失败：{path}", ex, assembly);
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }
    }
}
