using System.Reflection;
using JmcModLib.Config.Entry;
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
