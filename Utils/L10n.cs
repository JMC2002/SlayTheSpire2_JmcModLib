using System.Reflection;
using Godot;
using MegaCrit.Sts2.Core.Localization;

namespace JmcModLib.Utils;

/// <summary>
/// STS2 localization helpers.
///
/// Unlike Duckov, STS2 loads mod tables automatically from:
/// res://{mod_pck_name}/localization/{language}/{file}.csv
/// </summary>
public static class L10n
{
    public const string FallbackLanguage = "eng";

    public static string CurrentLanguage => LocManager.Instance?.Language ?? FallbackLanguage;

    public static string GetModLocalizationRoot(Assembly? assembly = null)
    {
        assembly ??= Assembly.GetCallingAssembly();
        return $"res://{ModRuntime.GetPckName(assembly)}/localization";
    }

    public static string GetModLocalizationDirectory(string? language = null, Assembly? assembly = null)
    {
        assembly ??= Assembly.GetCallingAssembly();
        return $"{GetModLocalizationRoot(assembly)}/{NormalizeLanguage(language)}";
    }

    public static string GetModTablePath(string fileName, string? language = null, Assembly? assembly = null)
    {
        assembly ??= Assembly.GetCallingAssembly();
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        string normalizedFileName = fileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase)
            ? fileName
            : $"{fileName}.csv";

        return $"{GetModLocalizationDirectory(language, assembly)}/{normalizedFileName}";
    }

    public static bool HasModTable(string fileName, string? language = null, Assembly? assembly = null)
    {
        return ResourceLoader.Exists(GetModTablePath(fileName, language, assembly));
    }

    public static IEnumerable<string> EnumerateExistingModTablePaths(string fileName, Assembly? assembly = null)
    {
        assembly ??= Assembly.GetCallingAssembly();

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
        if (LocManager.Instance == null || !LocString.Exists(table, key))
        {
            return null;
        }

        LocString locString = new(table, key);
        configure?.Invoke(locString);
        return locString;
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
}
