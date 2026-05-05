// 文件用途：封装 STS2 运行时 MOD/manifest 查询，帮助 JML 从游戏加载状态推断 MOD 信息。
using JmcModLib.Reflection;
using MegaCrit.Sts2.Core.Modding;
using System.Collections;
using System.Reflection;

namespace JmcModLib.Core;

public static class ModRuntime
{
    public static Mod? TryGetLoadedMod(Assembly? assembly = null)
    {
        assembly = ResolveAssembly(assembly);
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
        assembly = ResolveAssembly(assembly);
        return GetPckName(TryGetLoadedMod(assembly))
            ?? assembly.GetName().Name
            ?? VersionInfo.Name;
    }

    public static string GetDisplayName(Assembly? assembly = null)
    {
        assembly = ResolveAssembly(assembly);
        return GetManifestName(TryGetManifest(assembly))
            ?? assembly.GetName().Name
            ?? VersionInfo.Name;
    }

    public static Version? GetLoadedVersion(Assembly? assembly = null)
    {
        assembly = ResolveAssembly(assembly);
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
            MemberAccessor? accessor = TryGetMember(type, memberName);
            if (accessor is { IsStatic: true })
            {
                return accessor.GetValue(null);
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
            MemberAccessor? accessor = TryGetMember(type, memberName);
            if (accessor is { IsStatic: false })
            {
                return accessor.GetValue(instance);
            }
        }

        return null;
    }

    private static MemberAccessor? TryGetMember(Type type, string memberName)
    {
        for (Type? current = type; current != null; current = current.BaseType)
        {
            try
            {
                return MemberAccessor.Get(current, memberName);
            }
            catch (MissingMemberException)
            {
            }
        }

        return null;
    }

    private static Assembly ResolveAssembly(Assembly? assembly)
    {
        return AssemblyResolver.Resolve(assembly, typeof(ModRuntime));
    }
}
