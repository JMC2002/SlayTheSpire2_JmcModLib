using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace JmcModLib.Core;

/// <summary>
/// Lightweight assembly-to-mod context registry for STS2 mods.
/// </summary>
public static class ModRegistry
{
    private static readonly ConcurrentDictionary<Assembly, ModContext> Contexts = new();

    public static event Action<ModContext>? OnRegistered;

    public static event Action<ModContext>? OnUnregistered;

    public static RegistryBuilder Register(
        string modId,
        string? displayName = null,
        string? version = null,
        Assembly? assembly = null)
    {
        assembly = ResolveAssembly(assembly);
        _ = Contexts.AddOrUpdate(
            assembly,
            asm => CreateContext(asm, modId, displayName, version),
            (asm, existing) => UpdateContext(existing, asm, modId, displayName, version));

        return new RegistryBuilder(assembly);
    }

    public static RegistryBuilder Register<T>(string modId, string? displayName = null, string? version = null)
    {
        return Register(modId, displayName, version, typeof(T).Assembly);
    }

    public static bool IsRegistered(Assembly? assembly = null)
    {
        return Contexts.ContainsKey(ResolveAssembly(assembly));
    }

    public static bool TryGetContext(out ModContext? context, Assembly? assembly = null)
    {
        return Contexts.TryGetValue(ResolveAssembly(assembly), out context);
    }

    public static ModContext? GetContext(Assembly? assembly = null)
    {
        Contexts.TryGetValue(ResolveAssembly(assembly), out ModContext? context);
        return context;
    }

    public static string GetModId(Assembly? assembly = null)
    {
        ModContext? context = GetContext(assembly);
        return context?.ModId ?? VersionInfo.GetName(ResolveAssembly(assembly));
    }

    public static string GetDisplayName(Assembly? assembly = null)
    {
        ModContext? context = GetContext(assembly);
        return context?.DisplayName ?? VersionInfo.GetName(ResolveAssembly(assembly));
    }

    public static string GetVersion(Assembly? assembly = null)
    {
        ModContext? context = GetContext(assembly);
        return context?.Version ?? VersionInfo.GetVersion(ResolveAssembly(assembly));
    }

    public static string GetTag(Assembly? assembly = null)
    {
        string displayName = GetDisplayName(assembly);
        string version = GetVersion(assembly);
        return string.IsNullOrWhiteSpace(version) ? $"[{displayName}]" : $"[{displayName} v{version}]";
    }

    public static bool Unregister(Assembly? assembly = null)
    {
        assembly = ResolveAssembly(assembly);
        if (!Contexts.TryRemove(assembly, out ModContext? context))
        {
            return false;
        }

        ModLogger.UnregisterAssembly(assembly);
        context.LoggerConfigured = false;
        context.IsCompleted = false;
        OnUnregistered?.Invoke(context);
        return true;
    }

    internal static string GetLoggerContext(Assembly? assembly = null)
    {
        ModContext? context = GetContext(assembly);
        if (context != null)
        {
            return context.LoggerContext;
        }

        string displayName = VersionInfo.GetName(ResolveAssembly(assembly));
        string version = VersionInfo.GetVersion(ResolveAssembly(assembly));
        return string.IsNullOrWhiteSpace(version) ? displayName : $"{displayName} v{version}";
    }

    internal static void MarkLoggerConfigured(Assembly assembly)
    {
        if (Contexts.TryGetValue(assembly, out ModContext? context))
        {
            context.LoggerConfigured = true;
        }
    }

    internal static void UpdateDisplayName(Assembly assembly, string displayName)
    {
        if (Contexts.TryGetValue(assembly, out ModContext? context) && !string.IsNullOrWhiteSpace(displayName))
        {
            context.DisplayName = displayName.Trim();
        }
    }

    internal static void UpdateVersion(Assembly assembly, string version)
    {
        if (Contexts.TryGetValue(assembly, out ModContext? context) && !string.IsNullOrWhiteSpace(version))
        {
            context.Version = version.Trim();
        }
    }

    internal static void Complete(Assembly assembly)
    {
        if (!Contexts.TryGetValue(assembly, out ModContext? context))
        {
            throw new InvalidOperationException($"{assembly.GetName().Name} has not been registered.");
        }

        if (context.IsCompleted)
        {
            return;
        }

        context.IsCompleted = true;
        OnRegistered?.Invoke(context);
    }

    private static ModContext CreateContext(Assembly assembly, string modId, string? displayName, string? version)
    {
        string resolvedModId = ResolveModId(assembly, modId);
        var mod = ModRuntime.FindModById(resolvedModId);

        string resolvedDisplayName = string.IsNullOrWhiteSpace(displayName)
            ? mod?.manifest?.name ?? assembly.GetName().Name ?? resolvedModId
            : displayName.Trim();

        string resolvedVersion = string.IsNullOrWhiteSpace(version)
            ? mod?.manifest?.version
                ?? assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                ?? assembly.GetName().Version?.ToString()
                ?? "0.0.0"
            : version.Trim();

        return new ModContext(assembly, resolvedModId, resolvedDisplayName, resolvedVersion);
    }

    private static ModContext UpdateContext(
        ModContext existing,
        Assembly assembly,
        string modId,
        string? displayName,
        string? version)
    {
        string resolvedModId = ResolveModId(assembly, modId);
        var mod = ModRuntime.FindModById(resolvedModId);

        existing.ModId = resolvedModId;
        existing.DisplayName = string.IsNullOrWhiteSpace(displayName)
            ? existing.DisplayName
            : displayName.Trim();
        existing.Version = string.IsNullOrWhiteSpace(version)
            ? existing.Version
            : version.Trim();

        if (string.IsNullOrWhiteSpace(existing.DisplayName))
        {
            existing.DisplayName = mod?.manifest?.name ?? assembly.GetName().Name ?? resolvedModId;
        }

        if (string.IsNullOrWhiteSpace(existing.Version))
        {
            existing.Version = mod?.manifest?.version
                ?? assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                ?? assembly.GetName().Version?.ToString()
                ?? "0.0.0";
        }

        return existing;
    }

    private static string ResolveModId(Assembly assembly, string modId)
    {
        if (!string.IsNullOrWhiteSpace(modId))
        {
            return modId.Trim();
        }

        return assembly.GetName().Name ?? VersionInfo.GetName(assembly);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static Assembly ResolveAssembly(Assembly? assembly)
    {
        return assembly ?? Assembly.GetCallingAssembly();
    }
}
