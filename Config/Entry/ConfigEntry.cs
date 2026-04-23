using System.Globalization;
using System.Reflection;
using System.Text.Json;
using JmcModLib.Config.Storage;
using JmcModLib.Config.UI;

namespace JmcModLib.Config.Entry;

/// <summary>
/// Base class for a single registered config entry.
/// </summary>
public abstract class ConfigEntry
{
    protected ConfigEntry(
        Assembly assembly,
        string storageKey,
        string group,
        string displayName,
        ConfigAttribute attribute,
        UIConfigAttribute? uiAttribute)
    {
        Assembly = assembly;
        StorageKey = storageKey;
        Group = group;
        DisplayName = displayName;
        Attribute = attribute;
        UIAttribute = uiAttribute;
    }

    public Assembly Assembly { get; }

    public string StorageKey { get; }

    public string Group { get; }

    public string DisplayName { get; }

    public string Key => CreateKey(StorageKey, Group);

    public ConfigAttribute Attribute { get; }

    public UIConfigAttribute? UIAttribute { get; }

    public abstract Type ValueType { get; }

    public abstract object? DefaultValue { get; }

    public abstract object? GetValue();

    public abstract void SetValue(object? value);

    public abstract bool Reset();

    public event Action<ConfigEntry, object?>? ValueChanged;

    public static string CreateStorageKey(Type declaringType, string memberName)
    {
        ArgumentNullException.ThrowIfNull(declaringType);
        ArgumentException.ThrowIfNullOrWhiteSpace(memberName);
        return $"{declaringType.FullName}.{memberName}";
    }

    public static string CreateKey(string storageKey, string group = ConfigAttribute.DefaultGroup)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storageKey);
        return $"{group}.{storageKey}";
    }

    internal abstract void SyncFromStorage(IConfigStorage storage);

    internal abstract void SyncFromSource(IConfigStorage storage);

    protected void RaiseValueChanged(object? value)
    {
        ValueChanged?.Invoke(this, value);
    }
}

public sealed class ConfigEntry<TValue> : ConfigEntry
{
    private readonly Func<TValue> getter;
    private readonly Action<TValue> setter;
    private readonly Action<TValue>? onChanged;
    private TValue currentValue;
    private bool isGetting;
    private bool isSetting;

    public ConfigEntry(
        Assembly assembly,
        string storageKey,
        string group,
        string displayName,
        TValue defaultValue,
        Func<TValue> getter,
        Action<TValue> setter,
        Action<TValue>? onChanged,
        ConfigAttribute attribute,
        UIConfigAttribute? uiAttribute)
        : base(assembly, storageKey, group, displayName, attribute, uiAttribute)
    {
        this.getter = getter ?? throw new ArgumentNullException(nameof(getter));
        this.setter = setter ?? throw new ArgumentNullException(nameof(setter));
        this.onChanged = onChanged;
        currentValue = defaultValue;
        DefaultValueTyped = defaultValue;
    }

    public TValue DefaultValueTyped { get; }

    public override Type ValueType => typeof(TValue);

    public override object? DefaultValue => DefaultValueTyped;

    public TValue GetTypedValue()
    {
        if (isGetting)
        {
            throw new InvalidOperationException($"Config {Key} triggered a recursive getter call.");
        }

        isGetting = true;
        try
        {
            return getter();
        }
        finally
        {
            isGetting = false;
        }
    }

    public void SetTypedValue(TValue value)
    {
        ApplyValue(value, persist: true, notify: true);
    }

    public override object? GetValue()
    {
        return GetTypedValue();
    }

    public override void SetValue(object? value)
    {
        SetTypedValue(ConfigValueConverter.Convert<TValue>(value));
    }

    public override bool Reset()
    {
        TValue liveValue = GetTypedValue();
        currentValue = liveValue;

        if (EqualityComparer<TValue>.Default.Equals(liveValue, DefaultValueTyped))
        {
            return false;
        }

        SetTypedValue(DefaultValueTyped);
        return true;
    }

    internal override void SyncFromStorage(IConfigStorage storage)
    {
        if (storage.TryLoad(StorageKey, Group, typeof(TValue), out object? loaded, Assembly))
        {
            TValue value = ConfigValueConverter.Convert<TValue>(loaded);
            TValue liveValue = GetTypedValue();
            currentValue = liveValue;

            if (EqualityComparer<TValue>.Default.Equals(liveValue, value))
            {
                currentValue = value;
                return;
            }

            ApplyValue(value, persist: false, notify: true);
            return;
        }

        storage.Save(StorageKey, Group, currentValue, Assembly);
        if (ConfigManager.FlushOnSet)
        {
            storage.Flush(Assembly);
        }
    }

    internal override void SyncFromSource(IConfigStorage storage)
    {
        TValue liveValue = GetTypedValue();
        if (EqualityComparer<TValue>.Default.Equals(currentValue, liveValue))
        {
            return;
        }

        currentValue = liveValue;
        storage.Save(StorageKey, Group, liveValue, Assembly);
    }

    private void ApplyValue(TValue value, bool persist, bool notify)
    {
        if (isSetting)
        {
            throw new InvalidOperationException($"Config {Key} triggered a recursive setter call.");
        }

        TValue liveValue = GetTypedValue();
        currentValue = liveValue;

        if (EqualityComparer<TValue>.Default.Equals(liveValue, value))
        {
            if (persist)
            {
                ConfigManager.Persist(this, value);
            }

            return;
        }

        TValue previousValue = liveValue;
        isSetting = true;
        try
        {
            setter(value);
            currentValue = value;

            if (persist)
            {
                ConfigManager.Persist(this, value);
            }

            onChanged?.Invoke(value);
            if (notify)
            {
                RaiseValueChanged(value);
            }
        }
        catch
        {
            currentValue = previousValue;
            throw;
        }
        finally
        {
            isSetting = false;
        }
    }
}

internal static class ConfigValueConverter
{
    public static TValue Convert<TValue>(object? value)
    {
        object? converted = Convert(value, typeof(TValue));
        return converted is null ? default! : (TValue)converted;
    }

    public static object? Convert(object? value, Type targetType)
    {
        ArgumentNullException.ThrowIfNull(targetType);

        if (value == null)
        {
            Type? nullableUnderlying = Nullable.GetUnderlyingType(targetType);
            if (!targetType.IsValueType || nullableUnderlying != null)
            {
                return null;
            }

            return Activator.CreateInstance(targetType);
        }

        if (targetType.IsInstanceOfType(value))
        {
            return value;
        }

        Type? nullableType = Nullable.GetUnderlyingType(targetType);
        if (nullableType != null)
        {
            return Convert(value, nullableType);
        }

        if (value is System.Text.Json.JsonElement jsonElement)
        {
            return jsonElement.ValueKind == System.Text.Json.JsonValueKind.Null
                ? null
                : JsonSerializer.Deserialize(jsonElement.GetRawText(), targetType);
        }

        if (targetType.IsEnum)
        {
            if (value is string enumName)
            {
                return Enum.Parse(targetType, enumName, ignoreCase: true);
            }

            object enumValue = System.Convert.ChangeType(value, Enum.GetUnderlyingType(targetType), CultureInfo.InvariantCulture);
            return Enum.ToObject(targetType, enumValue);
        }

        if (targetType == typeof(Guid))
        {
            return value is Guid guid ? guid : Guid.Parse(value.ToString()!);
        }

        if (targetType == typeof(string))
        {
            return value.ToString();
        }

        return System.Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture);
    }
}
