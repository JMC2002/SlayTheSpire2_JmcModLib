using System.Reflection;
using JmcModLib.Reflection;
using MegaCrit.Sts2.Core.Modding;

namespace JmcModLib.Utils;

public sealed class ModDependencyResult
{
    public bool IsLoaded { get; init; }

    public bool VersionMatch { get; init; }

    public bool AllMethodsAvailable { get; init; }

    public Version? ActualVersion { get; init; }

    public List<string> MissingMethods { get; init; } = [];

    public Type? ModType { get; init; }

    public Assembly? Assembly { get; init; }

    public Mod? Mod { get; init; }

    public ModManifest? Manifest => Mod?.manifest;

    public bool IsFullyAvailable => IsLoaded && VersionMatch && AllMethodsAvailable;

    public string GetSummary()
    {
        if (!IsLoaded)
        {
            return "Mod not loaded.";
        }

        if (!VersionMatch)
        {
            return $"Version mismatch. Actual version: {ActualVersion?.ToString() ?? "unknown"}.";
        }

        if (!AllMethodsAvailable)
        {
            return $"Missing methods: {string.Join(", ", MissingMethods)}";
        }

        return "Fully available.";
    }
}

public sealed record MethodSignature(string Name, Type[]? ParameterTypes = null);

public sealed class ModDependencyChecker
{
    private readonly string modId;
    private readonly string typeName;
    private readonly Version? requiredVersion;
    private readonly List<MethodSignature> requiredMethods = [];
    private readonly Dictionary<string, MethodAccessor> methodCache = new(StringComparer.Ordinal);

    private Assembly? cachedAssembly;
    private Type? cachedType;
    private Mod? cachedMod;
    private ModDependencyResult? cachedResult;

    public ModDependencyChecker(string modId, string typeName, Version? requiredVersion = null)
    {
        this.modId = modId;
        this.typeName = typeName;
        this.requiredVersion = requiredVersion;
    }

    public ModDependencyChecker RequireMethod(string methodName, Type[]? parameterTypes = null)
    {
        requiredMethods.Add(new MethodSignature(methodName, parameterTypes));
        return this;
    }

    public ModDependencyChecker RequireMethods(params MethodSignature[] methods)
    {
        requiredMethods.AddRange(methods);
        return this;
    }

    public ModDependencyResult Check()
    {
        if (cachedResult != null)
        {
            return cachedResult;
        }

        cachedMod = ModRuntime.FindLoadedMod(modId);
        cachedAssembly = cachedMod?.assembly ?? FindAssembly(modId);
        cachedType = ResolveType(cachedAssembly, typeName);

        bool isLoaded = cachedAssembly != null || cachedType != null;
        Version? actualVersion = isLoaded ? GetActualVersion(cachedMod, cachedAssembly, cachedType) : null;
        bool versionMatch = isLoaded
            && (requiredVersion == null || (actualVersion != null && actualVersion >= requiredVersion));

        List<string> missingMethods = [];
        bool allMethodsAvailable = false;

        if (cachedType != null)
        {
            allMethodsAvailable = CheckMethods(cachedType, missingMethods);
        }
        else if (isLoaded)
        {
            missingMethods.Add($"Type '{typeName}' was not found.");
        }

        cachedResult = new ModDependencyResult
        {
            IsLoaded = isLoaded,
            VersionMatch = versionMatch,
            AllMethodsAvailable = isLoaded && cachedType != null && allMethodsAvailable,
            ActualVersion = actualVersion,
            MissingMethods = missingMethods,
            ModType = cachedType,
            Assembly = cachedAssembly,
            Mod = cachedMod
        };

        return cachedResult;
    }

    public bool IsAvailable()
    {
        return Check().IsFullyAvailable;
    }

    public MethodAccessor? GetMethod(string methodName)
    {
        if (cachedResult == null)
        {
            Check();
        }

        return methodCache.TryGetValue(methodName, out MethodAccessor? accessor) ? accessor : null;
    }

    public bool TryInvoke(string methodName, object? instance, out object? result, params object?[] args)
    {
        result = null;
        MethodAccessor? accessor = GetMethod(methodName);
        if (accessor == null)
        {
            return false;
        }

        try
        {
            result = accessor.Invoke(instance, args);
            return true;
        }
        catch (Exception ex)
        {
            ModLogger.Error($"Failed to invoke {modId}.{methodName}.", ex);
            return false;
        }
    }

    public bool TryInvokeVoid(string methodName, object? instance, params object?[] args)
    {
        MethodAccessor? accessor = GetMethod(methodName);
        if (accessor == null)
        {
            return false;
        }

        try
        {
            accessor.Invoke(instance, args);
            return true;
        }
        catch (Exception ex)
        {
            ModLogger.Error($"Failed to invoke {modId}.{methodName}.", ex);
            return false;
        }
    }

    public void ResetCache()
    {
        cachedAssembly = null;
        cachedMod = null;
        cachedResult = null;
        cachedType = null;
        methodCache.Clear();
    }

    private static Assembly? FindAssembly(string modId)
    {
        return AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(assembly =>
            string.Equals(assembly.GetName().Name, modId, StringComparison.OrdinalIgnoreCase)
            || assembly.FullName?.Contains(modId, StringComparison.OrdinalIgnoreCase) == true);
    }

    private static Type? ResolveType(Assembly? assembly, string typeName)
    {
        if (assembly != null)
        {
            Type? directType = assembly.GetType(typeName, throwOnError: false);
            if (directType != null)
            {
                return directType;
            }
        }

        Type? globalType = Type.GetType(typeName, throwOnError: false);
        if (globalType != null)
        {
            return globalType;
        }

        return AppDomain.CurrentDomain.GetAssemblies()
            .Select(loadedAssembly => loadedAssembly.GetType(typeName, throwOnError: false))
            .FirstOrDefault(type => type != null);
    }

    private static Version? GetActualVersion(Mod? mod, Assembly? assembly, Type? type)
    {
        string? manifestVersion = mod?.manifest?.version;
        if (Version.TryParse(manifestVersion, out Version? parsedManifestVersion))
        {
            return parsedManifestVersion;
        }

        Version? assemblyVersion = assembly?.GetName().Version;
        if (assemblyVersion != null)
        {
            return assemblyVersion;
        }

        if (type == null)
        {
            return null;
        }

        string[] versionFieldNames = ["VERSION", "Version", "version", "MOD_VERSION"];
        foreach (string fieldName in versionFieldNames)
        {
            FieldInfo? field = type.GetField(fieldName, BindingFlags.Public | BindingFlags.Static);
            if (field == null)
            {
                continue;
            }

            object? value = field.GetValue(null);
            switch (value)
            {
                case Version version:
                    return version;
                case string text when Version.TryParse(text, out Version? parsedVersion):
                    return parsedVersion;
            }
        }

        return null;
    }

    private bool CheckMethods(Type type, List<string> missingMethods)
    {
        bool allAvailable = true;

        foreach (MethodSignature method in requiredMethods)
        {
            try
            {
                methodCache[method.Name] = MethodAccessor.Get(type, method.Name, method.ParameterTypes);
            }
            catch (MissingMethodException)
            {
                allAvailable = false;
                missingMethods.Add($"{method.Name}({FormatParameters(method.ParameterTypes)})");
            }
        }

        return allAvailable;
    }

    private static string FormatParameters(Type[]? parameterTypes)
    {
        if (parameterTypes == null || parameterTypes.Length == 0)
        {
            return string.Empty;
        }

        return string.Join(", ", parameterTypes.Select(type => type.Name));
    }
}

public static class ModDependencyExtensions
{
    public static ModDependencyChecker ForMod(string modId, string typeName, string? versionString = null)
    {
        Version? version = null;
        if (!string.IsNullOrWhiteSpace(versionString) && Version.TryParse(versionString, out Version? parsed))
        {
            version = parsed;
        }

        return new ModDependencyChecker(modId, typeName, version);
    }
}
