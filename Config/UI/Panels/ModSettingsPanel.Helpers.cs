using System.Globalization;
using JmcModLib.Config;
using JmcModLib.Config.Entry;
using JmcModLib.Config.Serialization;
using Godot;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Nodes.Screens.Settings;

namespace JmcModLib.Config.UI;

internal sealed partial class ModSettingsPanel
{
    private static void AttachEntryHoverTip(Control owner, ConfigEntry entry)
    {
        string description = BuildEntryHoverDescription(entry);
        if (!string.IsNullOrWhiteSpace(description))
        {
            JmcSettingsHoverTips.Attach(owner, ConfigLocalization.GetDisplayName(entry), description);
        }
    }

    private static string BuildEntryHoverDescription(ConfigEntry entry)
    {
        string description = ConfigLocalization.GetDescription(entry);
        if (!entry.Attribute.RestartRequired)
        {
            return description;
        }

        string restartText = $"[color=#e0b24f]{ModSettingsText.RestartRequired()}[/color]";
        return string.IsNullOrWhiteSpace(description)
            ? restartText
            : $"{description}\n{restartText}";
    }

    private static bool IsGodotObjectValid(GodotObject? value)
    {
        return value != null && GodotObject.IsInstanceValid(value);
    }

    private static string CreateBindingKey(ConfigEntry entry)
    {
        return $"{entry.Assembly.FullName}::{entry.Key}";
    }

    private static string GetSectionKey(Mod mod)
    {
        return mod.manifest?.id
            ?? mod.manifest?.name
            ?? mod.assembly?.FullName
            ?? "unknown";
    }

    private static bool IsSectionCollapsed(Mod mod)
    {
        return ModSettingsUiState.IsSectionCollapsed(GetSectionKey(mod));
    }

    private static void SetAllSectionsCollapsed(IEnumerable<Mod> mods, bool collapsed)
    {
        ModSettingsUiState.SetSectionsCollapsed(mods.Select(GetSectionKey), collapsed);
    }
    private static bool IsNumericType(Type type)
    {
        return type == typeof(byte)
            || type == typeof(sbyte)
            || type == typeof(short)
            || type == typeof(ushort)
            || type == typeof(int)
            || type == typeof(uint)
            || type == typeof(long)
            || type == typeof(ulong)
            || type == typeof(float)
            || type == typeof(double)
            || type == typeof(decimal);
    }

    private static double GetMinNumericValue(Type type)
    {
        if (type == typeof(byte)) return byte.MinValue;
        if (type == typeof(sbyte)) return sbyte.MinValue;
        if (type == typeof(short)) return short.MinValue;
        if (type == typeof(ushort)) return ushort.MinValue;
        if (type == typeof(int)) return int.MinValue;
        if (type == typeof(uint)) return uint.MinValue;
        if (type == typeof(long)) return long.MinValue;
        if (type == typeof(ulong)) return 0;
        return -1000000;
    }

    private static double GetMaxNumericValue(Type type)
    {
        if (type == typeof(byte)) return byte.MaxValue;
        if (type == typeof(sbyte)) return sbyte.MaxValue;
        if (type == typeof(short)) return short.MaxValue;
        if (type == typeof(ushort)) return ushort.MaxValue;
        if (type == typeof(int)) return int.MaxValue;
        if (type == typeof(uint)) return uint.MaxValue;
        if (type == typeof(long)) return long.MaxValue;
        if (type == typeof(ulong)) return 1000000;
        return 1000000;
    }

    private static string FormatNumericValue(double value, Type valueType)
    {
        if (valueType == typeof(float) || valueType == typeof(double) || valueType == typeof(decimal))
        {
            return value.ToString("0.##", CultureInfo.InvariantCulture);
        }

        return Math.Round(value).ToString(CultureInfo.InvariantCulture);
    }

    private static string FormatKeybind(JmcKeyBinding binding, UIKeybindAttribute attribute)
    {
        string keyboard = attribute.AllowKeyboard
            ? binding.HasKeyboard ? binding.ToKeyboardText() : ModSettingsText.KeybindUnbound()
            : string.Empty;
        string controller = attribute.AllowController && binding.HasController ? binding.Controller : string.Empty;

        return (keyboard, controller) switch
        {
            ({ Length: > 0 }, { Length: > 0 }) => $"{keyboard} / {controller}",
            ({ Length: > 0 }, _) => keyboard,
            (_, { Length: > 0 }) => controller,
            _ => ModSettingsText.KeybindUnbound()
        };
    }

    private static bool ToBoolean(object? value)
    {
        return value switch
        {
            bool b => b,
            null => false,
            _ => System.Convert.ToBoolean(value, CultureInfo.InvariantCulture)
        };
    }
}
