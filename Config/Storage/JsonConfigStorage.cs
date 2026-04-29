using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Godot;
using JmcModLib.Config;

namespace JmcModLib.Config.Storage;

/// <summary>
/// JSON storage backend for mod configuration files.
/// </summary>
public sealed class JsonConfigStorage : IConfigStorage
{
    private sealed class ConfigDocument
    {
        public Dictionary<string, Dictionary<string, JsonElement>> Groups { get; set; } = new(StringComparer.Ordinal);
    }

    private static readonly JsonSerializerOptions SerializerOptions = CreateSerializerOptions();

    private readonly ConcurrentDictionary<Assembly, ConfigDocument> cache = new();
    private readonly ConcurrentDictionary<Assembly, byte> dirtyAssemblies = new();
    private readonly string rootDirectory;

    public JsonConfigStorage(string? rootDirectory = null)
    {
        this.rootDirectory = string.IsNullOrWhiteSpace(rootDirectory)
            ? ResolveDefaultRootDirectory()
            : rootDirectory;

        Directory.CreateDirectory(this.rootDirectory);
    }

    public string GetFileName(Assembly? assembly = null)
    {
        assembly = ResolveAssembly(assembly);
        string modId = ModRegistry.GetModId(assembly);
        return $"{SanitizeFileName(modId)}.json";
    }

    public string GetFilePath(Assembly? assembly = null)
    {
        assembly = ResolveAssembly(assembly);
        return Path.Combine(rootDirectory, GetFileName(assembly));
    }

    public bool Exists(Assembly? assembly = null)
    {
        return File.Exists(GetFilePath(assembly));
    }

    public void Save(string key, string group, object? value, Assembly? assembly = null)
    {
        assembly = ResolveAssembly(assembly);
        ConfigDocument document = GetOrLoadDocument(assembly);

        if (!document.Groups.TryGetValue(group, out Dictionary<string, JsonElement>? groupValues))
        {
            groupValues = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
            document.Groups[group] = groupValues;
        }

        groupValues[key] = SerializeValue(value);
        dirtyAssemblies[assembly] = 0;
    }

    public bool TryLoad(string key, string group, Type valueType, out object? value, Assembly? assembly = null)
    {
        ArgumentNullException.ThrowIfNull(valueType);

        assembly = ResolveAssembly(assembly);
        ConfigDocument document = GetOrLoadDocument(assembly);

        if (!document.Groups.TryGetValue(group, out Dictionary<string, JsonElement>? groupValues)
            || !groupValues.TryGetValue(key, out JsonElement rawValue))
        {
            value = null;
            return false;
        }

        try
        {
            value = rawValue.ValueKind == JsonValueKind.Null
                ? null
                : rawValue.Deserialize(valueType, SerializerOptions);
            return true;
        }
        catch (Exception ex)
        {
            ModLogger.Error($"Failed to deserialize config value {group}/{key}.", ex, assembly);
            value = null;
            return false;
        }
    }

    public void Flush(Assembly? assembly = null)
    {
        assembly = ResolveAssembly(assembly);
        if (!dirtyAssemblies.ContainsKey(assembly))
        {
            return;
        }

        ConfigDocument document = GetOrLoadDocument(assembly);
        string filePath = GetFilePath(assembly);
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);

        string json = JsonSerializer.Serialize(document, SerializerOptions);
        string tempFile = $"{filePath}.tmp";
        File.WriteAllText(tempFile, json);
        File.Copy(tempFile, filePath, overwrite: true);
        File.Delete(tempFile);

        _ = dirtyAssemblies.TryRemove(assembly, out _);
    }

    private ConfigDocument GetOrLoadDocument(Assembly assembly)
    {
        return cache.GetOrAdd(assembly, LoadDocument);
    }

    private ConfigDocument LoadDocument(Assembly assembly)
    {
        string filePath = GetFilePath(assembly);
        if (!File.Exists(filePath))
        {
            return new ConfigDocument();
        }

        try
        {
            string json = File.ReadAllText(filePath);
            if (string.IsNullOrWhiteSpace(json))
            {
                return new ConfigDocument();
            }

            return JsonSerializer.Deserialize<ConfigDocument>(json, SerializerOptions) ?? new ConfigDocument();
        }
        catch (Exception ex)
        {
            ModLogger.Error($"Failed to read config file {filePath}.", ex, assembly);
            return new ConfigDocument();
        }
    }

    private static JsonElement SerializeValue(object? value)
    {
        return value == null
            ? JsonSerializer.SerializeToElement<object?>(null, SerializerOptions)
            : JsonSerializer.SerializeToElement(value, value.GetType(), SerializerOptions);
    }

    private static string ResolveDefaultRootDirectory()
    {
        try
        {
            string userDataDir = OS.GetUserDataDir();
            if (!string.IsNullOrWhiteSpace(userDataDir))
            {
                return Path.Combine(userDataDir, "mods", "config");
            }
        }
        catch
        {
            // Ignore Godot runtime failures and fall back below.
        }

        string localAppData = System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrWhiteSpace(localAppData))
        {
            return Path.Combine(localAppData, "JmcModLib_STS2", "Config");
        }

        return Path.Combine(AppContext.BaseDirectory, "Config");
    }

    private static string SanitizeFileName(string rawName)
    {
        char[] invalidChars = Path.GetInvalidFileNameChars();
        return string.Concat(rawName.Select(ch => invalidChars.Contains(ch) ? '_' : ch));
    }

    private static JsonSerializerOptions CreateSerializerOptions()
    {
        var options = new JsonSerializerOptions
        {
            AllowTrailingCommas = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            IncludeFields = true,
            WriteIndented = true
        };

        options.Converters.Add(new JsonStringEnumConverter());
        options.Converters.Add(new GodotColorJsonConverter());
        return options;
    }

    private static Assembly ResolveAssembly(Assembly? assembly)
    {
        return AssemblyResolver.Resolve(assembly, typeof(JsonConfigStorage));
    }
}
