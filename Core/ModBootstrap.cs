using System.Reflection;

namespace JmcModLib.Core;

public static class ModBootstrap
{
    public static RegistryBuilder Init<T>(string modId, string? displayName = null, string? version = null)
    {
        return Init(typeof(T).Assembly, modId, displayName, version);
    }

    public static RegistryBuilder Init(Assembly assembly, string modId, string? displayName = null, string? version = null)
    {
        return ModRegistry.Register(modId, displayName, version, assembly);
    }

    public static RegistryBuilder Init<T>()
    {
        Assembly assembly = typeof(T).Assembly;
        string modId = assembly.GetName().Name ?? typeof(T).Name;
        return Init(assembly, modId);
    }
}
