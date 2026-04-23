using System.Collections;
using System.Reflection;
using MegaCrit.Sts2.Core.Modding;

namespace JmcModLib.Core;

public static class ModRuntime
{
    private const BindingFlags StaticFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
    private const BindingFlags InstanceFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

    public static Mod? TryGetLoadedMod(Assembly? assembly = null)
    {
        assembly ??= Assembly.GetCallingAssembly();
        return GetKnownMods().FirstOrDefault(mod => ReferenceEquals(GetAssembly(mod), assembly));
    }

    public static ModManifest? TryGetManifest(Assembly? assembly = null)
    {
        return GetManifest(TryGetLoadedMod(assembly));
    }

    public static string? GetManifestId(Assembly? assembly = null)
    {
        return GetManifestId(TryGetManifest(assembly));
    }

    public static string GetPckName(Assembly? assembly = null)
    {
        assembly ??= Assembly.GetCallingAssembly();
        return GetPckName(TryGetLoadedMod(assembly))
            ?? assembly.GetName().Name
            ?? VersionInfo.Name;
    }

    public static string GetDisplayName(Assembly? assembly = null)
    {
        assembly ??= Assembly.GetCallingAssembly();
        return GetManifestName(TryGetManifest(assembly))
            ?? assembly.GetName().Name
            ?? VersionInfo.Name;
    }

    public static Version? GetLoadedVersion(Assembly? assembly = null)
    {
        assembly ??= Assembly.GetCallingAssembly();
        string? rawVersion = GetManifestVersion(TryGetManifest(assembly));
        if (Version.TryParse(rawVersion, out Version? parsed))
        {
            return parsed;
        }

        return assembly.GetName().Version;
    }

    public static Mod? FindModById(string modId)
    {
        if (string.IsNullOrWhiteSpace(modId))
        {
            return null;
        }

        return GetKnownMods().FirstOrDefault(mod =>
            string.Equals(GetManifestId(GetManifest(mod)), modId, StringComparison.OrdinalIgnoreCase));
    }

    public static Mod? FindLoadedMod(string modId)
    {
        if (string.IsNullOrWhiteSpace(modId))
        {
            return null;
        }

        return GetKnownMods().FirstOrDefault(mod =>
        {
            Assembly? modAssembly = GetAssembly(mod);
            return string.Equals(GetManifestId(GetManifest(mod)), modId, StringComparison.OrdinalIgnoreCase)
                || string.Equals(GetPckName(mod), modId, StringComparison.OrdinalIgnoreCase)
                || string.Equals(GetManifestName(GetManifest(mod)), modId, StringComparison.OrdinalIgnoreCase)
                || string.Equals(modAssembly?.GetName().Name, modId, StringComparison.OrdinalIgnoreCase);
        });
    }

    private static IEnumerable<Mod> GetKnownMods()
    {
        object? value = GetStaticMemberValue(typeof(ModManager), "LoadedMods", "AllMods", "Mods");
        if (value is not IEnumerable enumerable)
        {
            yield break;
        }

        foreach (object? item in enumerable)
        {
            if (item is Mod mod)
            {
                yield return mod;
            }
        }
    }

    private static Assembly? GetAssembly(Mod? mod)
    {
        return GetInstanceMemberValue(mod, "assembly", "Assembly") as Assembly;
    }

    private static ModManifest? GetManifest(Mod? mod)
    {
        return GetInstanceMemberValue(mod, "manifest", "Manifest") as ModManifest;
    }

    private static string? GetPckName(Mod? mod)
    {
        return GetInstanceMemberValue(mod, "pckName", "PckName") as string;
    }

    private static string? GetManifestId(ModManifest? manifest)
    {
        return GetInstanceMemberValue(manifest, "id", "Id") as string;
    }

    private static string? GetManifestName(ModManifest? manifest)
    {
        return GetInstanceMemberValue(manifest, "name", "Name") as string;
    }

    private static string? GetManifestVersion(ModManifest? manifest)
    {
        return GetInstanceMemberValue(manifest, "version", "Version") as string;
    }

    private static object? GetStaticMemberValue(Type type, params string[] memberNames)
    {
        foreach (string memberName in memberNames)
        {
            PropertyInfo? property = type.GetProperty(memberName, StaticFlags);
            if (property != null)
            {
                return property.GetValue(null);
            }

            FieldInfo? field = type.GetField(memberName, StaticFlags);
            if (field != null)
            {
                return field.GetValue(null);
            }
        }

        return null;
    }

    private static object? GetInstanceMemberValue(object? instance, params string[] memberNames)
    {
        if (instance == null)
        {
            return null;
        }

        Type type = instance.GetType();
        foreach (string memberName in memberNames)
        {
            PropertyInfo? property = type.GetProperty(memberName, InstanceFlags);
            if (property != null)
            {
                return property.GetValue(instance);
            }

            FieldInfo? field = type.GetField(memberName, InstanceFlags);
            if (field != null)
            {
                return field.GetValue(instance);
            }
        }

        return null;
    }
}
