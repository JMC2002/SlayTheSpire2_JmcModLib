using Godot;
using Newtonsoft.Json;

namespace JmcModLib.Config.UI;

internal static class ModSettingsUiState
{
    private const string FileName = "JmcModLib.ui.json";
    private static readonly object SyncRoot = new();
    private static readonly string FilePath = ResolveFilePath();
    private static StateDocument? cachedState;

    public static bool IsSectionCollapsed(string sectionKey)
    {
        if (string.IsNullOrWhiteSpace(sectionKey))
        {
            return false;
        }

        StateDocument state = GetState();
        return state.CollapsedSections.TryGetValue(sectionKey, out bool collapsed) && collapsed;
    }

    public static void SetSectionCollapsed(string sectionKey, bool collapsed)
    {
        if (string.IsNullOrWhiteSpace(sectionKey))
        {
            return;
        }

        StateDocument state = GetState();
        if (collapsed)
        {
            state.CollapsedSections[sectionKey] = true;
        }
        else
        {
            state.CollapsedSections.Remove(sectionKey);
        }

        Save(state);
    }

    public static void SetSectionsCollapsed(IEnumerable<string> sectionKeys, bool collapsed)
    {
        ArgumentNullException.ThrowIfNull(sectionKeys);

        StateDocument state = GetState();
        foreach (string sectionKey in sectionKeys.Where(static key => !string.IsNullOrWhiteSpace(key)))
        {
            if (collapsed)
            {
                state.CollapsedSections[sectionKey] = true;
            }
            else
            {
                state.CollapsedSections.Remove(sectionKey);
            }
        }

        Save(state);
    }

    private static StateDocument GetState()
    {
        lock (SyncRoot)
        {
            return cachedState ??= Load();
        }
    }

    private static StateDocument Load()
    {
        try
        {
            if (!File.Exists(FilePath))
            {
                return new StateDocument();
            }

            string json = File.ReadAllText(FilePath);
            if (string.IsNullOrWhiteSpace(json))
            {
                return new StateDocument();
            }

            StateDocument state = JsonConvert.DeserializeObject<StateDocument>(json) ?? new StateDocument();
            state.CollapsedSections = new Dictionary<string, bool>(
                state.CollapsedSections.Where(static pair => pair.Value),
                StringComparer.OrdinalIgnoreCase);
            return state;
        }
        catch (Exception ex)
        {
            ModLogger.Warn($"Failed to load JmcModLib settings UI state from {FilePath}: {ex.Message}");
            return new StateDocument();
        }
    }

    private static void Save(StateDocument state)
    {
        lock (SyncRoot)
        {
            cachedState = state;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
                string json = JsonConvert.SerializeObject(state, Formatting.Indented);
                string tempFile = $"{FilePath}.tmp";
                File.WriteAllText(tempFile, json);
                File.Copy(tempFile, FilePath, overwrite: true);
                File.Delete(tempFile);
            }
            catch (Exception ex)
            {
                ModLogger.Warn($"Failed to save JmcModLib settings UI state to {FilePath}: {ex.Message}");
            }
        }
    }

    private static string ResolveFilePath()
    {
        return Path.Combine(ResolveRootDirectory(), FileName);
    }

    private static string ResolveRootDirectory()
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
            // Godot 尚未就绪时走后备目录。
        }

        string localAppData = System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData);
        return string.IsNullOrWhiteSpace(localAppData)
            ? Path.Combine(AppContext.BaseDirectory, "Config")
            : Path.Combine(localAppData, "JmcModLib_STS2", "Config");
    }

    private sealed class StateDocument
    {
        public Dictionary<string, bool> CollapsedSections { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
