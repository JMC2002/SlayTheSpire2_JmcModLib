using System.Globalization;
using System.Reflection;
using Godot;
using JmcModLib.Config.Serialization;
using JmcModLib.Config.Storage;
using JmcModLib.Config.UI;

namespace JmcModLib.Config.Entry;

/// <summary>
/// Base class for a single registered config entry.
/// </summary>
public abstract class ConfigEntry(
    Assembly assembly,
    string storageKey,
    string group,
    string displayName,
    ConfigAttribute attribute,
    UIConfigAttribute? uiAttribute)
{
    public Assembly Assembly { get; } = assembly;

    public string StorageKey { get; } = storageKey;

    public string Group { get; } = group;

    public string DisplayName { get; } = displayName;

    public string Key => CreateKey(StorageKey, Group);

    public ConfigAttribute Attribute { get; } = attribute;

    public UIConfigAttribute? UIAttribute { get; } = uiAttribute;

    public Type? SourceDeclaringType { get; internal set; }

    public string? SourceMemberName { get; internal set; }

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

public sealed class ConfigEntry<TValue>(
    Assembly assembly,
    string storageKey,
    string group,
    string displayName,
    TValue defaultValue,
    Func<TValue> getter,
    Action<TValue> setter,
    Action<TValue>? onChanged,
    ConfigAttribute attribute,
    UIConfigAttribute? uiAttribute) : ConfigEntry(assembly, storageKey, group, displayName, attribute, uiAttribute)
{
    private readonly Func<TValue> getter = getter ?? throw new ArgumentNullException(nameof(getter));
    private readonly Action<TValue> setter = setter ?? throw new ArgumentNullException(nameof(setter));
    private readonly Action<TValue>? onChanged = onChanged;
    private TValue currentValue = defaultValue;
    private bool isGetting;
    private bool isSetting;

    public TValue DefaultValueTyped { get; } = defaultValue;

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

        if (value is JmcKeyBinding keyBinding)
        {
            if (targetType == typeof(Key))
            {
                return keyBinding.Keyboard;
            }

            if (targetType == typeof(string))
            {
                return keyBinding.ToString();
            }
        }

        if (targetType == typeof(JmcKeyBinding) && value is Key keyboard)
        {
            return new JmcKeyBinding(keyboard);
        }

        if (targetType == typeof(Color))
        {
            return JmcColorValue.Convert(value);
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
