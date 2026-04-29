using MegaCrit.Sts2.Core.Localization;

namespace JmcModLib.Config.UI;

internal static class ModSettingsText
{
    private const string KeyPrefix = "EXTENSION.JMCMODLIB.UI";

    public static string TabLabel() => Resolve("MOD_SETTINGS_TAB", "Mod Settings");

    public static string Title() => Resolve("MOD_SETTINGS_TITLE", "Mod Settings");

    public static string Description() => Resolve(
        "MOD_SETTINGS_DESCRIPTION",
        "Adjust registered mod configuration here. Changes are saved immediately.");

    public static string ChangesSavedImmediately() => Resolve(
        "CHANGES_SAVED_IMMEDIATELY",
        "Changes are saved immediately.");

    public static string NoConfigMods() => Resolve(
        "NO_CONFIG_MODS",
        "No registered mods currently have configurable entries.");

    public static string NoManagedAssembly() => Resolve(
        "NO_MANAGED_ASSEMBLY",
        "This mod has no loaded managed assembly, so its config cannot be shown.");

    public static string NoConfigEntries() => Resolve(
        "NO_CONFIG_ENTRIES",
        "This mod currently has no visible config entries.");

    public static string AuthorLabel() => Resolve("AUTHOR_LABEL", "Author");

    public static string VersionLabel() => Resolve("VERSION_LABEL", "Version");

    public static string RestartRequired() => Resolve(
        "RESTART_REQUIRED",
        "Requires restart to fully apply.");

    public static string Expand() => Resolve("EXPAND", "Expand");

    public static string Collapse() => Resolve("COLLAPSE", "Collapse");

    public static string ExpandAll() => Resolve("EXPAND_ALL", "Expand All");

    public static string CollapseAll() => Resolve("COLLAPSE_ALL", "Collapse All");

    public static string ResetMod() => Resolve("RESET_MOD", "Reset This Mod");

    public static string Reset() => Resolve("RESET", "Reset");

    public static string Close() => Resolve("CLOSE", "Close");

    public static string KeybindListening() => Resolve("KEYBIND_LISTENING", "Press a key or button...");

    public static string KeybindUnbound() => Resolve("KEYBIND_UNBOUND", "Unbound");

    public static string ColorSelect() => Resolve("COLOR_SELECT", "Select");

    public static string ConfigTitle(string modName)
    {
        return Resolve(
            "CONFIG_TITLE",
            $"{modName} Config",
            loc => loc.Add("modName", modName));
    }

    public static string UnsupportedType(string typeName)
    {
        return Resolve(
            "UNSUPPORTED_TYPE",
            $"Unsupported type: {typeName}",
            loc => loc.Add("type", typeName));
    }

    private static string Resolve(string key, string fallback, Action<LocString>? configure = null)
    {
        return L10n.Resolve(
            $"{KeyPrefix}.{key}",
            fallback,
            L10n.DefaultTable,
            typeof(ModSettingsText).Assembly,
            configure);
    }
}
