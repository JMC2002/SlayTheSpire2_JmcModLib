using JmcModLib.Config.Entry;

namespace JmcModLib.Config.UI;

internal sealed partial class ModSettingsPanel
{
    private void TrySetEntryValue(ConfigEntry entry, object? rawValue)
    {
        try
        {
            object? converted = ConfigValueConverter.Convert(rawValue, entry.ValueType);
            entry.SetValue(converted);
        }
        catch (Exception ex)
        {
            ModLogger.Error($"Failed to update config entry {entry.Key}", ex, entry.Assembly);
            try
            {
                _ = TryUpdateBinding(entry, entry.GetValue(), logUnexpectedFailure: false);
            }
            catch (Exception rollbackEx)
            {
                ModLogger.Warn($"Failed to restore config entry UI {entry.Key} after a rejected value.", rollbackEx, entry.Assembly);
            }
        }
    }

    private void OnConfigValueChanged(ConfigEntry entry, object? value)
    {
        _ = TryUpdateBinding(entry, value, logUnexpectedFailure: true);
    }

    private bool TryUpdateBinding(ConfigEntry entry, object? value, bool logUnexpectedFailure)
    {
        string bindingKey = CreateBindingKey(entry);
        if (!bindings.TryGetValue(bindingKey, out Action<object?>? updateBinding))
        {
            return true;
        }

        try
        {
            updateBinding(value);
            return true;
        }
        catch (ObjectDisposedException)
        {
            suppressControlEvents = false;
            bindings.Remove(bindingKey);
            return false;
        }
        catch (Exception ex)
        {
            suppressControlEvents = false;
            bindings.Remove(bindingKey);
            if (logUnexpectedFailure)
            {
                ModLogger.Warn($"Failed to refresh config entry UI {entry.Key}. The config value was already updated.", ex, entry.Assembly);
            }

            return false;
        }
    }

    private void RunSuppressed(Action action)
    {
        bool previous = suppressControlEvents;
        suppressControlEvents = true;
        try
        {
            action();
        }
        finally
        {
            suppressControlEvents = previous;
        }
    }
}
