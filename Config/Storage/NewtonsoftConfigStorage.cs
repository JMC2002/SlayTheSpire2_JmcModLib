using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using Godot;
using JmcModLib.Config.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;

namespace JmcModLib.Config.Storage;

/// <summary>
/// Newtonsoft.Json based storage backend for mod configuration files.
/// </summary>
public sealed class NewtonsoftConfigStorage : IConfigStorage
{
    private sealed class ConfigDocument
    {
        public Dictionary<string, Dictionary<string, JToken?>> Groups { get; set; } = new(StringComparer.Ordinal);
    }

    private static readonly JsonSerializerSettings SerializerSettings = CreateSerializerSettings();

    private readonly ConcurrentDictionary<Assembly, ConfigDocument> cache = new();
    private readonly ConcurrentDictionary<Assembly, byte> dirtyAssemblies = new();
    private readonly string rootDirectory;

    public NewtonsoftConfigStorage(string? rootDirectory = null)
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

        if (!document.Groups.TryGetValue(group, out Dictionary<string, JToken?>? groupValues))
        {
            groupValues = new Dictionary<string, JToken?>(StringComparer.Ordinal);
            document.Groups[group] = groupValues;
        }

        groupValues[key] = value == null
            ? JValue.CreateNull()
            : JToken.FromObject(value, CreateSerializer());
        dirtyAssemblies[assembly] = 0;
    }

    public bool TryLoad(string key, string group, Type valueType, out object? value, Assembly? assembly = null)
    {
        ArgumentNullException.ThrowIfNull(valueType);

        assembly = ResolveAssembly(assembly);
        ConfigDocument document = GetOrLoadDocument(assembly);

        if (!document.Groups.TryGetValue(group, out Dictionary<string, JToken?>? groupValues)
            || !groupValues.TryGetValue(key, out JToken? rawValue))
        {
            value = null;
            return false;
        }

        if (rawValue is null || rawValue.Type is JTokenType.Null or JTokenType.Undefined)
        {
            value = null;
            return true;
        }

        try
        {
            value = rawValue.ToObject(valueType, CreateSerializer());
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

        string json = JsonConvert.SerializeObject(document, Formatting.Indented, SerializerSettings);
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

            ConfigDocument document = JsonConvert.DeserializeObject<ConfigDocument>(json, SerializerSettings)
                ?? new ConfigDocument();
            NormalizeDocument(document);
            return document;
        }
        catch (Exception ex)
        {
            ModLogger.Error($"Failed to read config file {filePath}.", ex, assembly);
            return new ConfigDocument();
        }
    }

    private static void NormalizeDocument(ConfigDocument document)
    {
        var normalizedGroups = new Dictionary<string, Dictionary<string, JToken?>>(StringComparer.Ordinal);
        foreach ((string group, Dictionary<string, JToken?>? values) in document.Groups)
        {
            normalizedGroups[group] = values == null
                ? new Dictionary<string, JToken?>(StringComparer.Ordinal)
                : new Dictionary<string, JToken?>(values, StringComparer.Ordinal);
        }

        document.Groups = normalizedGroups;
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

    private static JsonSerializer CreateSerializer()
    {
        return JsonSerializer.Create(SerializerSettings);
    }

    private static JsonSerializerSettings CreateSerializerSettings()
    {
        return new JsonSerializerSettings
        {
            DateParseHandling = DateParseHandling.None,
            MissingMemberHandling = MissingMemberHandling.Ignore,
            NullValueHandling = NullValueHandling.Include,
            ObjectCreationHandling = ObjectCreationHandling.Replace,
            TypeNameHandling = TypeNameHandling.None,
            Converters =
            {
                new StringEnumConverter(),
                new GodotColorNewtonsoftJsonConverter()
            }
        };
    }

    private static Assembly ResolveAssembly(Assembly? assembly)
    {
        return AssemblyResolver.Resolve(assembly, typeof(NewtonsoftConfigStorage));
    }

    private sealed class GodotColorNewtonsoftJsonConverter : JsonConverter<Color>
    {
        public override Color ReadJson(
            JsonReader reader,
            Type objectType,
            Color existingValue,
            bool hasExistingValue,
            JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
            {
                return Colors.White;
            }

            JToken token = JToken.Load(reader);
            return token.Type switch
            {
                JTokenType.String => JmcColorValue.Parse(token.Value<string>()),
                JTokenType.Object => ReadObject((JObject)token),
                _ => Colors.White
            };
        }

        public override void WriteJson(JsonWriter writer, Color value, JsonSerializer serializer)
        {
            writer.WriteValue(JmcColorValue.ToHex(value));
        }

        private static Color ReadObject(JObject value)
        {
            float r = ReadFloat(value, "r", fallback: 1f);
            float g = ReadFloat(value, "g", fallback: 1f);
            float b = ReadFloat(value, "b", fallback: 1f);
            float a = ReadFloat(value, "a", fallback: 1f);
            return new Color(r, g, b, a);
        }

        private static float ReadFloat(JObject value, string propertyName, float fallback)
        {
            JToken? token = value.GetValue(propertyName, StringComparison.OrdinalIgnoreCase);
            if (token == null)
            {
                return fallback;
            }

            return token.Type switch
            {
                JTokenType.Float or JTokenType.Integer => token.Value<float>(),
                JTokenType.String when float.TryParse(
                    token.Value<string>(),
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out float parsed) => parsed,
                _ => fallback
            };
        }
    }
}
