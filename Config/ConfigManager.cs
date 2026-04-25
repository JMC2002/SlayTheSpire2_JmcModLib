using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using JmcModLib.Config.Entry;
using JmcModLib.Config.Storage;
using JmcModLib.Config.UI;
using JmcModLib.Reflection;
using AttributeRouting = JmcModLib.Core.AttributeRouter.AttributeRouter;

namespace JmcModLib.Config;

/// <summary>
/// Central registration and persistence layer for config entries.
/// </summary>
public static class ConfigManager
{
    private static readonly ConcurrentDictionary<Assembly, ConcurrentDictionary<string, ConfigEntry>> Entries = new();
    private static readonly ConcurrentDictionary<Assembly, IConfigStorage> Storages = new();
    private static readonly IConfigStorage DefaultStorage = new JsonConfigStorage();

    private static int initialized;

    public static bool FlushOnSet { get; set; } = true;

    public static event Action<Assembly>? AssemblyRegistered;

    public static event Action<Assembly>? AssemblyUnregistered;

    public static event Action<ConfigEntry>? EntryRegistered;

    public static event Action<ConfigEntry, object?>? ValueChanged;

    public static bool IsInitialized => Volatile.Read(ref initialized) == 1;

    public static void Init()
    {
        if (Interlocked.Exchange(ref initialized, 1) == 1)
        {
            return;
        }

        AttributeRouting.Init();
        AttributeRouting.RegisterHandler<ConfigAttribute>(new ConfigAttributeHandler());
        AttributeRouting.AssemblyScanned += OnAssemblyScanned;
        ModRegistry.OnUnregistered += OnModUnregistered;
        AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
        ModLogger.Debug("ConfigManager initialized.");
    }

    public static void Dispose()
    {
        if (Interlocked.Exchange(ref initialized, 0) == 0)
        {
            return;
        }

        AttributeRouting.AssemblyScanned -= OnAssemblyScanned;
        ModRegistry.OnUnregistered -= OnModUnregistered;
        AppDomain.CurrentDomain.ProcessExit -= OnProcessExit;

        foreach (Assembly assembly in Entries.Keys.ToArray())
        {
            Unregister(assembly);
        }
    }

    public static void SetStorage(IConfigStorage storage, Assembly? assembly = null)
    {
        ArgumentNullException.ThrowIfNull(storage);
        EnsureInitialized();
        Storages[ResolveAssembly(assembly)] = storage;
    }

    public static IConfigStorage GetStorage(Assembly? assembly = null)
    {
        Assembly resolvedAssembly = ResolveAssembly(assembly);
        return GetStorageInternal(resolvedAssembly);
    }

    public static string CreateStorageKey(Type declaringType, string memberName)
    {
        return ConfigEntry.CreateStorageKey(declaringType, memberName);
    }

    public static string CreateKey(string storageKey, string group = ConfigAttribute.DefaultGroup)
    {
        return ConfigEntry.CreateKey(storageKey, group);
    }

    public static void Flush(Assembly? assembly = null)
    {
        Assembly resolvedAssembly = ResolveAssembly(assembly);
        GetStorageInternal(resolvedAssembly).Flush(resolvedAssembly);
    }

    public static IReadOnlyCollection<ConfigEntry> GetEntries(Assembly? assembly = null)
    {
        Assembly resolvedAssembly = ResolveAssembly(assembly);
        return Entries.TryGetValue(resolvedAssembly, out ConcurrentDictionary<string, ConfigEntry>? lookup)
            ? lookup.Values.OrderBy(static entry => entry.Attribute.Order).ThenBy(static entry => entry.DisplayName).ToArray()
            : [];
    }

    public static IEnumerable<ConfigEntry> GetEntries(string group, Assembly? assembly = null)
    {
        return GetEntries(assembly).Where(entry => string.Equals(entry.Group, group, StringComparison.Ordinal));
    }

    public static IEnumerable<string> GetGroups(Assembly? assembly = null)
    {
        return GetEntries(assembly)
            .Select(static entry => entry.Group)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static group => group, StringComparer.Ordinal);
    }

    public static bool TryGetEntry(string key, [NotNullWhen(true)] out ConfigEntry? entry, Assembly? assembly = null)
    {
        Assembly resolvedAssembly = ResolveAssembly(assembly);
        if (Entries.TryGetValue(resolvedAssembly, out ConcurrentDictionary<string, ConfigEntry>? lookup)
            && lookup.TryGetValue(key, out ConfigEntry? resolvedEntry))
        {
            entry = resolvedEntry;
            return true;
        }

        entry = null;
        return false;
    }

    public static object? GetValue(string key, Assembly? assembly = null)
    {
        return TryGetEntry(key, out ConfigEntry? entry, assembly) ? entry.GetValue() : null;
    }

    public static bool SetValue(string key, object? value, Assembly? assembly = null)
    {
        if (!TryGetEntry(key, out ConfigEntry? entry, assembly))
        {
            return false;
        }

        entry.SetValue(value);
        return true;
    }

    public static void ResetAssembly(Assembly? assembly = null)
    {
        Assembly resolvedAssembly = ResolveAssembly(assembly);
        if (!Entries.TryGetValue(resolvedAssembly, out ConcurrentDictionary<string, ConfigEntry>? lookup))
        {
            return;
        }

        foreach (ConfigEntry entry in lookup.Values)
        {
            entry.Reset();
        }

        if (!FlushOnSet)
        {
            Flush(resolvedAssembly);
        }
    }

    public static string RegisterConfig<TValue>(
        string displayName,
        Func<TValue> getter,
        Action<TValue> setter,
        string group = ConfigAttribute.DefaultGroup,
        Action<TValue>? onChanged = null,
        UIConfigAttribute? uiAttribute = null,
        string? storageKey = null,
        Assembly? assembly = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentNullException.ThrowIfNull(getter);
        ArgumentNullException.ThrowIfNull(setter);

        EnsureInitialized();

        Assembly resolvedAssembly = ResolveAssembly(assembly);
        string resolvedGroup = ResolveGroup(group);
        string resolvedStorageKey = string.IsNullOrWhiteSpace(storageKey) ? displayName.Trim() : storageKey.Trim();
        TValue defaultValue = getter();

        ValidateUiAttribute(uiAttribute, typeof(TValue), defaultValue, resolvedStorageKey);

        var descriptor = new ConfigAttribute(displayName, group: resolvedGroup)
        {
            Key = resolvedStorageKey
        };

        var entry = new ConfigEntry<TValue>(
            resolvedAssembly,
            resolvedStorageKey,
            resolvedGroup,
            displayName.Trim(),
            defaultValue,
            getter,
            setter,
            onChanged,
            descriptor,
            uiAttribute);

        RegisterEntry(entry);
        return entry.Key;
    }

    public static void Unregister(Assembly? assembly = null)
    {
        Assembly resolvedAssembly = ResolveAssembly(assembly);
        IConfigStorage storage = GetStorageInternal(resolvedAssembly);

        if (Entries.TryRemove(resolvedAssembly, out ConcurrentDictionary<string, ConfigEntry>? lookup))
        {
            foreach (ConfigEntry entry in lookup.Values)
            {
                entry.ValueChanged -= OnEntryValueChanged;
                entry.SyncFromSource(storage);
            }

            storage.Flush(resolvedAssembly);
        }

        _ = Storages.TryRemove(resolvedAssembly, out _);
        AssemblyUnregistered?.Invoke(resolvedAssembly);
    }

    internal static void RegisterEntry(ConfigEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        EnsureInitialized();

        ConcurrentDictionary<string, ConfigEntry> lookup = Entries.GetOrAdd(
            entry.Assembly,
            static _ => new ConcurrentDictionary<string, ConfigEntry>(StringComparer.Ordinal));

        if (lookup.TryGetValue(entry.Key, out ConfigEntry? existing))
        {
            existing.ValueChanged -= OnEntryValueChanged;
            ModLogger.Warn($"Config entry {entry.Key} was already registered and will be replaced.", entry.Assembly);
        }

        lookup[entry.Key] = entry;
        entry.ValueChanged += OnEntryValueChanged;
        entry.SyncFromStorage(GetStorageInternal(entry.Assembly));
        EntryRegistered?.Invoke(entry);
    }

    internal static ConfigEntry BuildConfigEntry(Assembly assembly, MemberAccessor member, ConfigAttribute attribute)
    {
        ArgumentNullException.ThrowIfNull(member);
        ArgumentNullException.ThrowIfNull(attribute);

        EnsureInitialized();

        try
        {
            MethodInfo factory = typeof(ConfigManager)
                .GetMethod(nameof(CreateAttributeEntry), BindingFlags.Static | BindingFlags.NonPublic)
                ?? throw new MissingMethodException(nameof(CreateAttributeEntry));

            return (ConfigEntry)factory
                .MakeGenericMethod(member.ValueType)
                .Invoke(null, [assembly, member, attribute])!;
        }
        catch (TargetInvocationException ex)
        {
            throw ex.InnerException ?? ex;
        }
    }

    internal static void Persist(ConfigEntry entry, object? value)
    {
        IConfigStorage storage = GetStorageInternal(entry.Assembly);
        storage.Save(entry.StorageKey, entry.Group, value, entry.Assembly);
        if (FlushOnSet)
        {
            storage.Flush(entry.Assembly);
        }
    }

    private static ConfigEntry<TValue> CreateAttributeEntry<TValue>(
        Assembly assembly,
        MemberAccessor member,
        ConfigAttribute attribute)
    {
        ValidateMember(member);

        string displayName = ResolveDisplayName(attribute.DisplayName, member.Name);
        string group = ResolveGroup(attribute.Group);
        string storageKey = ResolveStorageKey(attribute, member);
        UIConfigAttribute? uiAttribute = member.GetAttribute<UIConfigAttribute>();

        Func<TValue> getter = member.TypedGetter is Func<TValue> typedGetter
            ? typedGetter
            : () => (TValue)member.GetValue(null)!;

        Action<TValue> setter = member.TypedSetter is Action<TValue> typedSetter
            ? typedSetter
            : value => member.SetValue(null, value);

        Action<TValue>? onChanged = BuildOnChangedCallback<TValue>(member, attribute, assembly);
        TValue defaultValue = getter();

        ValidateUiAttribute(uiAttribute, typeof(TValue), defaultValue, storageKey);

        return new ConfigEntry<TValue>(
            assembly,
            storageKey,
            group,
            displayName,
            defaultValue,
            getter,
            setter,
            onChanged,
            attribute,
            uiAttribute);
    }

    private static Action<TValue>? BuildOnChangedCallback<TValue>(
        MemberAccessor member,
        ConfigAttribute attribute,
        Assembly assembly)
    {
        if (string.IsNullOrWhiteSpace(attribute.OnChanged))
        {
            return null;
        }

        MethodAccessor callback;
        try
        {
            callback = MethodAccessor.Get(member.DeclaringType, attribute.OnChanged!, [typeof(TValue)]);
        }
        catch (MissingMethodException ex)
        {
            throw new ArgumentException(
                $"Could not find config callback {attribute.OnChanged} for {member.DeclaringType.FullName}.{member.Name}.",
                ex);
        }

        if (!ConfigAttribute.IsValidMethod(callback.MemberInfo, typeof(TValue), out MegaCrit.Sts2.Core.Logging.LogLevel? level, out string? errorMessage))
        {
            throw new ArgumentException(errorMessage ?? "Invalid config callback signature.");
        }

        LogCallbackValidation(level, errorMessage, assembly);

        if (callback.TypedDelegate is Action<TValue> typedCallback)
        {
            return typedCallback;
        }

        return value => callback.InvokeStaticVoid(value);
    }

    private static void LogCallbackValidation(
        MegaCrit.Sts2.Core.Logging.LogLevel? level,
        string? errorMessage,
        Assembly assembly)
    {
        if (level == null || string.IsNullOrWhiteSpace(errorMessage))
        {
            return;
        }

        switch (level.Value)
        {
            case MegaCrit.Sts2.Core.Logging.LogLevel.Warn:
                ModLogger.Warn(errorMessage, assembly);
                break;

            case MegaCrit.Sts2.Core.Logging.LogLevel.Error:
                ModLogger.Error(errorMessage, assembly);
                break;

            case MegaCrit.Sts2.Core.Logging.LogLevel.Info:
                ModLogger.Info(errorMessage, assembly);
                break;

            default:
                ModLogger.Debug(errorMessage, assembly);
                break;
        }
    }

    private static void ValidateMember(MemberAccessor member)
    {
        if (!member.IsStatic)
        {
            throw new ArgumentException(
                $"Attribute-based config only supports static members. {member.DeclaringType.FullName}.{member.Name} is not static.");
        }

        if (!member.CanRead || !member.CanWrite)
        {
            throw new ArgumentException(
                $"Config member {member.DeclaringType.FullName}.{member.Name} must be readable and writable.");
        }
    }

    private static void ValidateUiAttribute(
        UIConfigAttribute? uiAttribute,
        Type valueType,
        object? defaultValue,
        string storageKey)
    {
        if (uiAttribute == null)
        {
            return;
        }

        if (!uiAttribute.IsValid(valueType, defaultValue, out string? errorMessage))
        {
            throw new ArgumentException(
                $"Config entry {storageKey} has invalid UI metadata: {errorMessage}");
        }
    }

    private static string ResolveDisplayName(string? displayName, string fallbackName)
    {
        return string.IsNullOrWhiteSpace(displayName) ? fallbackName : displayName.Trim();
    }

    private static string ResolveGroup(string? group)
    {
        return string.IsNullOrWhiteSpace(group) ? ConfigAttribute.DefaultGroup : group.Trim();
    }

    private static string ResolveStorageKey(ConfigAttribute attribute, MemberAccessor member)
    {
        return string.IsNullOrWhiteSpace(attribute.Key)
            ? ConfigEntry.CreateStorageKey(member.DeclaringType, member.Name)
            : attribute.Key.Trim();
    }

    private static IConfigStorage GetStorageInternal(Assembly assembly)
    {
        return Storages.TryGetValue(assembly, out IConfigStorage? storage) ? storage : DefaultStorage;
    }

    private static void OnAssemblyScanned(Assembly assembly)
    {
        if (Entries.TryGetValue(assembly, out ConcurrentDictionary<string, ConfigEntry>? lookup) && !lookup.IsEmpty)
        {
            AssemblyRegistered?.Invoke(assembly);
        }
    }

    private static void OnModUnregistered(ModContext context)
    {
        Unregister(context.Assembly);
    }

    private static void OnProcessExit(object? sender, EventArgs args)
    {
        foreach (Assembly assembly in Entries.Keys.ToArray())
        {
            try
            {
                Unregister(assembly);
            }
            catch
            {
                // Ignore shutdown flush failures.
            }
        }
    }

    private static void OnEntryValueChanged(ConfigEntry entry, object? value)
    {
        ValueChanged?.Invoke(entry, value);
    }

    private static void EnsureInitialized()
    {
        if (!IsInitialized)
        {
            Init();
        }
    }

    private static Assembly ResolveAssembly(Assembly? assembly)
    {
        return AssemblyResolver.Resolve(assembly, typeof(ConfigManager));
    }
}
