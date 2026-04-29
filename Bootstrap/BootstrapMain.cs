using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Runtime.Loader;
using System.Text.Json;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;

namespace JmcModLib.Bootstrap;

[ModInitializer(nameof(Initialize))]
public static class BootstrapMain
{
    private const string DescriptorFileName = "JmcModLib.runtime.json";

    public static void Initialize()
    {
        string modDirectory = ResolveModDirectory();
        RuntimeDescriptor descriptor = RuntimeDescriptor.Load(Path.Combine(modDirectory, DescriptorFileName));

        BootstrapDependencyResolver.Install(modDirectory, descriptor);

        Assembly runtimeAssembly = BootstrapDependencyResolver.LoadRuntimeAssembly(descriptor);
        InvokeRuntimeInitializer(runtimeAssembly, descriptor);

        BootstrapLog.Info($"Runtime loaded: {runtimeAssembly.FullName}");
    }

    private static void InvokeRuntimeInitializer(Assembly runtimeAssembly, RuntimeDescriptor descriptor)
    {
        Type runtimeType = runtimeAssembly.GetType(descriptor.InitializerType, throwOnError: true)
            ?? throw new TypeLoadException($"Could not find runtime initializer type {descriptor.InitializerType}.");

        MethodInfo method = runtimeType.GetMethod(
                descriptor.InitializerMethod,
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(descriptor.InitializerType, descriptor.InitializerMethod);

        try
        {
            method.Invoke(null, null);
        }
        catch (TargetInvocationException ex) when (ex.InnerException != null)
        {
            ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
        }
    }

    private static string ResolveModDirectory()
    {
        string? location = Assembly.GetExecutingAssembly().Location;
        if (!string.IsNullOrWhiteSpace(location))
        {
            string? directory = Path.GetDirectoryName(location);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                return directory;
            }
        }

        return AppContext.BaseDirectory;
    }
}

internal sealed class RuntimeDescriptor
{
    public string RuntimeAssembly { get; set; } = "JmcModLib.Runtime.dll";

    public string InitializerType { get; set; } = "JmcModLib.MainFile";

    public string InitializerMethod { get; set; } = "Initialize";

    public List<string> Dependencies { get; set; } = [];

    public List<string> ProbeDirectories { get; set; } = ["."];

    public bool ProbeAllDlls { get; set; } = true;

    public static RuntimeDescriptor Load(string descriptorPath)
    {
        if (!File.Exists(descriptorPath))
        {
            BootstrapLog.Warn($"{Path.GetFileName(descriptorPath)} not found; using default runtime descriptor.");
            return new RuntimeDescriptor();
        }

        try
        {
            string json = File.ReadAllText(descriptorPath);
            RuntimeDescriptor descriptor = JsonSerializer.Deserialize<RuntimeDescriptor>(
                    json,
                    new JsonSerializerOptions
                    {
                        AllowTrailingCommas = true,
                        PropertyNameCaseInsensitive = true,
                        ReadCommentHandling = JsonCommentHandling.Skip
                    })
                ?? new RuntimeDescriptor();

            descriptor.Normalize();
            return descriptor;
        }
        catch (Exception ex)
        {
            BootstrapLog.Error($"Failed to read {descriptorPath}; using default runtime descriptor.\n{ex}");
            return new RuntimeDescriptor();
        }
    }

    public void Normalize()
    {
        RuntimeAssembly = NormalizeText(RuntimeAssembly, "JmcModLib.Runtime.dll");
        InitializerType = NormalizeText(InitializerType, "JmcModLib.MainFile");
        InitializerMethod = NormalizeText(InitializerMethod, "Initialize");
        Dependencies = NormalizeList(Dependencies);
        ProbeDirectories = NormalizeList(ProbeDirectories);
        if (ProbeDirectories.Count == 0)
        {
            ProbeDirectories.Add(".");
        }
    }

    private static string NormalizeText(string? text, string fallback)
    {
        return string.IsNullOrWhiteSpace(text) ? fallback : text.Trim();
    }

    private static List<string> NormalizeList(IEnumerable<string>? values)
    {
        return values?
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(static value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList() ?? [];
    }
}

internal static class BootstrapDependencyResolver
{
    private static readonly ConcurrentDictionary<string, string> AssemblyPaths = new(StringComparer.OrdinalIgnoreCase);
    private static readonly List<string> ProbeDirectories = [];
    private static readonly object ConfigureLock = new();

    [ThreadStatic]
    private static HashSet<string>? resolvingNames;

    private static AssemblyLoadContext? loadContext;
    private static int installed;

    public static void Install(string modDirectory, RuntimeDescriptor descriptor)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modDirectory);
        ArgumentNullException.ThrowIfNull(descriptor);

        lock (ConfigureLock)
        {
            loadContext ??= AssemblyLoadContext.GetLoadContext(typeof(BootstrapDependencyResolver).Assembly)
                ?? AssemblyLoadContext.Default;

            AddProbeDirectories(modDirectory, descriptor.ProbeDirectories);
            AddDependencyPath(modDirectory, descriptor.RuntimeAssembly);

            foreach (string dependency in descriptor.Dependencies)
            {
                AddDependencyPath(modDirectory, dependency);
            }

            if (descriptor.ProbeAllDlls)
            {
                AddAllDllsFromProbeDirectories();
            }

            if (Interlocked.Exchange(ref installed, 1) == 0)
            {
                loadContext.Resolving += ResolveFromLoadContext;
                AppDomain.CurrentDomain.AssemblyResolve += ResolveFromAppDomain;
                BootstrapLog.Info("Dependency resolver installed.");
            }
        }
    }

    public static Assembly LoadRuntimeAssembly(RuntimeDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        string? runtimePath = FindDependencyPath(descriptor.RuntimeAssembly);
        if (runtimePath == null)
        {
            throw new FileNotFoundException($"Could not find JmcModLib runtime assembly: {descriptor.RuntimeAssembly}");
        }

        AssemblyName runtimeName = AssemblyName.GetAssemblyName(runtimePath);
        Assembly? loaded = FindLoadedAssembly(runtimeName);
        if (loaded != null)
        {
            return loaded;
        }

        Assembly assembly = (loadContext ?? AssemblyLoadContext.Default).LoadFromAssemblyPath(runtimePath);
        BootstrapLog.Info($"Loaded runtime assembly from {runtimePath}");
        return assembly;
    }

    private static Assembly? ResolveFromLoadContext(AssemblyLoadContext context, AssemblyName requestedAssembly)
    {
        return ResolveAssembly(context, requestedAssembly);
    }

    private static Assembly? ResolveFromAppDomain(object? sender, ResolveEventArgs args)
    {
        return ResolveAssembly(loadContext ?? AssemblyLoadContext.Default, new AssemblyName(args.Name));
    }

    private static Assembly? ResolveAssembly(AssemblyLoadContext context, AssemblyName requestedAssembly)
    {
        string simpleName = requestedAssembly.Name ?? string.Empty;
        if (string.IsNullOrWhiteSpace(simpleName))
        {
            return null;
        }

        resolvingNames ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!resolvingNames.Add(simpleName))
        {
            return null;
        }

        try
        {
            Assembly? loaded = FindLoadedAssembly(requestedAssembly);
            if (loaded != null)
            {
                return loaded;
            }

            string? assemblyPath = FindDependencyPath(simpleName);
            if (assemblyPath == null)
            {
                return null;
            }

            AssemblyName candidateName = AssemblyName.GetAssemblyName(assemblyPath);
            if (!IsCompatible(candidateName, requestedAssembly))
            {
                BootstrapLog.Warn($"Skipped dependency {assemblyPath}; requested {requestedAssembly.FullName}, found {candidateName.FullName}.");
                return null;
            }

            Assembly assembly = context.LoadFromAssemblyPath(assemblyPath);
            BootstrapLog.Info($"Resolved dependency {requestedAssembly.Name} from {assemblyPath}");
            return assembly;
        }
        catch (Exception ex) when (ex is IOException or BadImageFormatException or UnauthorizedAccessException or FileLoadException)
        {
            BootstrapLog.Error($"Failed to resolve dependency {requestedAssembly.FullName}: {ex}");
            return null;
        }
        finally
        {
            _ = resolvingNames.Remove(simpleName);
        }
    }

    private static void AddProbeDirectories(string modDirectory, IEnumerable<string> probeDirectories)
    {
        foreach (string probeDirectory in probeDirectories)
        {
            string fullPath = ResolvePath(modDirectory, probeDirectory);
            if (Directory.Exists(fullPath) && !ProbeDirectories.Contains(fullPath, StringComparer.OrdinalIgnoreCase))
            {
                ProbeDirectories.Add(fullPath);
            }
        }
    }

    private static void AddDependencyPath(string modDirectory, string dependency)
    {
        foreach (string candidate in EnumerateDependencyCandidates(modDirectory, dependency))
        {
            if (File.Exists(candidate))
            {
                AddAssemblyPath(candidate);
                return;
            }
        }

        BootstrapLog.Warn($"Dependency file was not found: {dependency}");
    }

    private static IEnumerable<string> EnumerateDependencyCandidates(string modDirectory, string dependency)
    {
        string normalizedDependency = dependency.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
            ? dependency
            : $"{dependency}.dll";

        if (Path.IsPathRooted(normalizedDependency))
        {
            yield return normalizedDependency;
            yield break;
        }

        yield return Path.GetFullPath(Path.Combine(modDirectory, normalizedDependency));

        foreach (string probeDirectory in ProbeDirectories)
        {
            yield return Path.GetFullPath(Path.Combine(probeDirectory, normalizedDependency));
        }
    }

    private static void AddAllDllsFromProbeDirectories()
    {
        foreach (string probeDirectory in ProbeDirectories)
        {
            foreach (string assemblyPath in Directory.EnumerateFiles(probeDirectory, "*.dll", SearchOption.TopDirectoryOnly))
            {
                AddAssemblyPath(assemblyPath);
            }
        }
    }

    private static void AddAssemblyPath(string assemblyPath)
    {
        try
        {
            AssemblyName assemblyName = AssemblyName.GetAssemblyName(assemblyPath);
            if (!string.IsNullOrWhiteSpace(assemblyName.Name))
            {
                AssemblyPaths[assemblyName.Name] = assemblyPath;
            }
        }
        catch (Exception ex) when (ex is IOException or BadImageFormatException or UnauthorizedAccessException)
        {
            BootstrapLog.Warn($"Skipped invalid dependency {assemblyPath}: {ex.Message}");
        }
    }

    private static string? FindDependencyPath(string assemblyNameOrPath)
    {
        string simpleName = NormalizeAssemblyName(assemblyNameOrPath);
        if (!string.IsNullOrWhiteSpace(simpleName) && AssemblyPaths.TryGetValue(simpleName, out string? mappedPath))
        {
            return mappedPath;
        }

        string fileName = assemblyNameOrPath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
            ? Path.GetFileName(assemblyNameOrPath)
            : $"{simpleName}.dll";

        foreach (string probeDirectory in ProbeDirectories)
        {
            string candidate = Path.Combine(probeDirectory, fileName);
            if (File.Exists(candidate))
            {
                AddAssemblyPath(candidate);
                return candidate;
            }
        }

        return null;
    }

    private static Assembly? FindLoadedAssembly(AssemblyName requestedAssembly)
    {
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            AssemblyName loadedName = assembly.GetName();
            if (IsCompatible(loadedName, requestedAssembly))
            {
                return assembly;
            }
        }

        return null;
    }

    private static bool IsCompatible(AssemblyName candidate, AssemblyName requested)
    {
        if (!string.Equals(candidate.Name, requested.Name, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        byte[]? requestedToken = requested.GetPublicKeyToken();
        byte[]? candidateToken = candidate.GetPublicKeyToken();
        if (requestedToken is { Length: > 0 }
            && (candidateToken is not { Length: > 0 } || !requestedToken.SequenceEqual(candidateToken)))
        {
            return false;
        }

        if (requested.Version == null || candidate.Version == null)
        {
            return true;
        }

        return candidate.Version.CompareTo(requested.Version) >= 0;
    }

    private static string ResolvePath(string baseDirectory, string path)
    {
        return Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(baseDirectory, path));
    }

    private static string NormalizeAssemblyName(string assemblyName)
    {
        if (string.IsNullOrWhiteSpace(assemblyName))
        {
            return string.Empty;
        }

        string trimmed = assemblyName.Trim();
        if (trimmed.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
        {
            return Path.GetFileNameWithoutExtension(trimmed);
        }

        return trimmed.Contains(',')
            ? new AssemblyName(trimmed).Name ?? string.Empty
            : trimmed;
    }
}

internal static class BootstrapLog
{
    private const string Prefix = "[JmcModLib.Bootstrap] ";

    public static void Info(string message)
    {
        Write(LogLevel.Info, message);
    }

    public static void Warn(string message)
    {
        Write(LogLevel.Warn, message);
    }

    public static void Error(string message)
    {
        Write(LogLevel.Error, message);
    }

    private static void Write(LogLevel level, string message)
    {
        try
        {
            Log.LogMessage(level, LogType.Generic, Prefix + message);
        }
        catch
        {
            try
            {
                Console.WriteLine(Prefix + message);
            }
            catch
            {
                // Logging must never block dependency loading.
            }
        }
    }
}
