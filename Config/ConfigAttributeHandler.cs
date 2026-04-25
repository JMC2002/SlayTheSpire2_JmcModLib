using System.Reflection;
using JmcModLib.Config.Entry;
using JmcModLib.Config.UI;
using JmcModLib.Core.AttributeRouter;
using JmcModLib.Reflection;

namespace JmcModLib.Config;

internal sealed class ConfigAttributeHandler : IAttributeHandler
{
    public Action<Assembly, IReadOnlyList<ReflectionAccessorBase>>? Unregister => null;

    public void Handle(Assembly assembly, ReflectionAccessorBase accessor, Attribute attribute)
    {
        if (accessor is not MemberAccessor member || attribute is not ConfigAttribute configAttribute)
        {
            return;
        }

        try
        {
            ConfigEntry entry = ConfigManager.BuildConfigEntry(assembly, member, configAttribute);
            ConfigManager.RegisterEntry(entry);
            ModLogger.Trace($"Registered config entry {entry.Key}", assembly);
        }
        catch (Exception ex)
        {
            ModLogger.Error(
                $"Failed to build config entry for {member.DeclaringType.FullName}.{member.Name}",
                ex,
                assembly);
        }
    }
}

internal sealed class UIButtonAttributeHandler : IAttributeHandler
{
    public Action<Assembly, IReadOnlyList<ReflectionAccessorBase>>? Unregister => null;

    public void Handle(Assembly assembly, ReflectionAccessorBase accessor, Attribute attribute)
    {
        if (accessor is not MethodAccessor method || attribute is not UIButtonAttribute buttonAttribute)
        {
            return;
        }

        try
        {
            ButtonEntry entry = ConfigManager.BuildButtonEntry(assembly, method, buttonAttribute);
            ConfigManager.RegisterEntry(entry);
            ModLogger.Trace($"Registered button entry {entry.Key}", assembly);
        }
        catch (Exception ex)
        {
            ModLogger.Error(
                $"Failed to build button entry for {method.DeclaringType.FullName}.{method.Name}",
                ex,
                assembly);
        }
    }
}
