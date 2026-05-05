using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using JmcModLib.Config;
using JmcModLib.Config.Entry;
using JmcModLib.Config.UI;

namespace JmcModLib.Input;

/// <summary>
/// JML 内部逻辑输入动作注册表，用于把配置热键映射到 Steam Input 可识别的稳定 Action。
/// </summary>
internal static class JmcInputActionRegistry
{
    private const int MaxSteamActionIdLength = 120;

    private static readonly object SyncRoot = new();
    private static readonly Dictionary<EntryIdentity, ConfigEntry> ControllerEntries = [];
    private static readonly Dictionary<EntryIdentity, HashSet<HotkeyIdentity>> EntryHotkeys = [];
    private static readonly Dictionary<SourceIdentity, HashSet<HotkeyIdentity>> SourceHotkeys = [];

    public static event Action? ActionsChanged;

    public static void RegisterConfigEntry(ConfigEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        if (entry.UIAttribute is not UIKeybindAttribute { AllowController: true })
        {
            return;
        }

        Type actualType = Nullable.GetUnderlyingType(entry.ValueType) ?? entry.ValueType;
        if (actualType != typeof(JmcKeyBinding))
        {
            return;
        }

        EntryIdentity identity = EntryIdentity.FromEntry(entry);
        lock (SyncRoot)
        {
            ControllerEntries[identity] = entry;
        }

        ActionsChanged?.Invoke();
    }

    public static void BindHotkeyToEntryKey(Assembly assembly, string hotkeyKey, string entryKey)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        ArgumentException.ThrowIfNullOrWhiteSpace(hotkeyKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(entryKey);

        EntryIdentity entryIdentity = new(assembly, entryKey.Trim());
        HotkeyIdentity hotkeyIdentity = new(assembly, hotkeyKey.Trim());
        lock (SyncRoot)
        {
            AddBinding(EntryHotkeys, entryIdentity, hotkeyIdentity);
        }

        ActionsChanged?.Invoke();
    }

    public static void BindHotkeyToSourceMember(
        Assembly assembly,
        string hotkeyKey,
        Type declaringType,
        string memberName)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        ArgumentException.ThrowIfNullOrWhiteSpace(hotkeyKey);
        ArgumentNullException.ThrowIfNull(declaringType);
        ArgumentException.ThrowIfNullOrWhiteSpace(memberName);

        SourceIdentity sourceIdentity = new(
            assembly,
            declaringType.FullName ?? declaringType.Name,
            memberName.Trim());
        HotkeyIdentity hotkeyIdentity = new(assembly, hotkeyKey.Trim());

        lock (SyncRoot)
        {
            AddBinding(SourceHotkeys, sourceIdentity, hotkeyIdentity);
        }

        ActionsChanged?.Invoke();
    }

    public static void UnregisterAssembly(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        lock (SyncRoot)
        {
            RemoveEntryKeys(ControllerEntries, assembly);
            RemoveEntryKeys(EntryHotkeys, assembly);
            RemoveSourceKeys(SourceHotkeys, assembly);
            RemoveHotkeyValues(EntryHotkeys, assembly);
            RemoveHotkeyValues(SourceHotkeys, assembly);
        }

        ActionsChanged?.Invoke();
    }

    public static IReadOnlyList<JmcInputActionDescriptor> GetActions()
    {
        lock (SyncRoot)
        {
            return ControllerEntries.Values
                .Select(CreateDescriptor)
                .Where(static descriptor => descriptor.Bindings.Count > 0)
                .OrderBy(static descriptor => descriptor.ModId, StringComparer.Ordinal)
                .ThenBy(static descriptor => descriptor.Entry.StorageKey, StringComparer.Ordinal)
                .ToArray();
        }
    }

    public static bool IsHotkeyBoundToAction(Assembly assembly, string hotkeyKey, string actionId)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        ArgumentException.ThrowIfNullOrWhiteSpace(hotkeyKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(actionId);

        HotkeyIdentity target = new(assembly, hotkeyKey.Trim());
        lock (SyncRoot)
        {
            foreach (JmcInputActionDescriptor descriptor in ControllerEntries.Values.Select(CreateDescriptor))
            {
                if (!string.Equals(descriptor.ActionId, actionId, StringComparison.Ordinal)
                    || descriptor.Bindings.Count == 0)
                {
                    continue;
                }

                if (descriptor.Bindings.Any(binding => binding.Assembly == target.Assembly
                        && string.Equals(binding.HotkeyKey, target.Key, StringComparison.Ordinal)))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static JmcInputActionDescriptor CreateDescriptor(ConfigEntry entry)
    {
        EntryIdentity entryIdentity = EntryIdentity.FromEntry(entry);
        var bindings = new HashSet<HotkeyIdentity>();

        if (EntryHotkeys.TryGetValue(entryIdentity, out HashSet<HotkeyIdentity>? entryHotkeys))
        {
            bindings.UnionWith(entryHotkeys);
        }

        if (entry.SourceDeclaringType != null && !string.IsNullOrWhiteSpace(entry.SourceMemberName))
        {
            SourceIdentity sourceIdentity = new(
                entry.Assembly,
                entry.SourceDeclaringType.FullName ?? entry.SourceDeclaringType.Name,
                entry.SourceMemberName);
            if (SourceHotkeys.TryGetValue(sourceIdentity, out HashSet<HotkeyIdentity>? sourceHotkeys))
            {
                bindings.UnionWith(sourceHotkeys);
            }
        }

        string modId = ModRegistry.GetModId(entry.Assembly);
        string actionId = CreateActionId(modId, entry.StorageKey);
        return new JmcInputActionDescriptor(
            actionId,
            entry.Assembly,
            modId,
            entry,
            bindings.Select(static binding => new JmcInputHotkeyBinding(binding.Assembly, binding.Key)).ToArray());
    }

    private static string CreateActionId(string modId, string storageKey)
    {
        string raw = $"JML_{modId}_{storageKey}";
        string sanitized = SanitizeActionId(raw);
        string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)))[..10];
        string candidate = $"{sanitized}_{hash}";
        if (candidate.Length <= MaxSteamActionIdLength)
        {
            return candidate;
        }

        int prefixLength = MaxSteamActionIdLength - hash.Length - 1;
        return $"{sanitized[..prefixLength]}_{hash}";
    }

    private static string SanitizeActionId(string value)
    {
        StringBuilder builder = new(value.Length);
        bool previousUnderscore = false;
        foreach (char ch in value)
        {
            char next = char.IsAsciiLetterOrDigit(ch) ? ch : '_';
            if (next == '_' && previousUnderscore)
            {
                continue;
            }

            builder.Append(next);
            previousUnderscore = next == '_';
        }

        return builder.ToString().Trim('_');
    }

    private static void AddBinding<TKey>(
        Dictionary<TKey, HashSet<HotkeyIdentity>> lookup,
        TKey key,
        HotkeyIdentity hotkey)
        where TKey : notnull
    {
        if (!lookup.TryGetValue(key, out HashSet<HotkeyIdentity>? hotkeys))
        {
            hotkeys = [];
            lookup[key] = hotkeys;
        }

        _ = hotkeys.Add(hotkey);
    }

    private static void RemoveEntryKeys<TValue>(Dictionary<EntryIdentity, TValue> lookup, Assembly assembly)
    {
        foreach (EntryIdentity identity in lookup.Keys.Where(key => key.Assembly == assembly).ToArray())
        {
            _ = lookup.Remove(identity);
        }
    }

    private static void RemoveSourceKeys(Dictionary<SourceIdentity, HashSet<HotkeyIdentity>> lookup, Assembly assembly)
    {
        foreach (SourceIdentity identity in lookup.Keys.Where(key => key.Assembly == assembly).ToArray())
        {
            _ = lookup.Remove(identity);
        }
    }

    private static void RemoveHotkeyValues<TKey>(Dictionary<TKey, HashSet<HotkeyIdentity>> lookup, Assembly assembly)
        where TKey : notnull
    {
        foreach (TKey key in lookup.Keys.ToArray())
        {
            lookup[key].RemoveWhere(hotkey => hotkey.Assembly == assembly);
            if (lookup[key].Count == 0)
            {
                _ = lookup.Remove(key);
            }
        }
    }

    private readonly record struct EntryIdentity(Assembly Assembly, string EntryKey)
    {
        public static EntryIdentity FromEntry(ConfigEntry entry)
        {
            return new EntryIdentity(entry.Assembly, entry.Key);
        }
    }

    private readonly record struct SourceIdentity(Assembly Assembly, string DeclaringType, string MemberName);

    private readonly record struct HotkeyIdentity(Assembly Assembly, string Key);
}

internal sealed record JmcInputActionDescriptor(
    string ActionId,
    Assembly Assembly,
    string ModId,
    ConfigEntry Entry,
    IReadOnlyList<JmcInputHotkeyBinding> Bindings)
{
    public string LocalizationKey => $"Action_{ActionId}";

    public string GetDisplayName(string language)
    {
        return ConfigLocalization.GetDisplayName(Entry, language);
    }
}

internal readonly record struct JmcInputHotkeyBinding(Assembly Assembly, string HotkeyKey);
