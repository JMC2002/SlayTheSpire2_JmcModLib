using System.Text;
using Godot;
using MegaCrit.Sts2.Core.ControllerInput;
using MegaCrit.Sts2.Core.Platform.Steam;
using Steamworks;

namespace JmcModLib.Input;

/// <summary>
/// 在游戏初始化 Steam Input 前生成并安装 JML 合并版 Steam Input manifest。
/// </summary>
internal static class JmcSteamInputManifestInstaller
{
    private const string OriginalManifestFileName = "game_actions_2868840.vdf";
    private const string GeneratedManifestFileName = "steam_input_manifest.jml.vdf";

    private static readonly IReadOnlyDictionary<string, string> SteamLanguageToJml = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["english"] = "eng",
        ["schinese"] = "zhs",
        ["french"] = "fra",
        ["italian"] = "ita",
        ["german"] = "deu",
        ["spanish"] = "spa",
        ["japanese"] = "jpn",
        ["koreana"] = "kor",
        ["polish"] = "pol",
        ["brazilian"] = "ptb",
        ["russian"] = "rus",
        ["latam"] = "esp",
        ["thai"] = "tha",
        ["turkish"] = "tur"
    };

    private static readonly IReadOnlyDictionary<string, string> SetTitles = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["english"] = "All Controls",
        ["schinese"] = "全部操作",
        ["french"] = "Toutes les commandes",
        ["italian"] = "Tutti i controlli",
        ["german"] = "Alle Steuerungen",
        ["spanish"] = "Todos los controles",
        ["japanese"] = "すべての操作",
        ["koreana"] = "전체 조작",
        ["polish"] = "Wszystkie sterowania",
        ["brazilian"] = "Todos os controles",
        ["russian"] = "Все элементы управления",
        ["latam"] = "Todos los controles",
        ["thai"] = "การควบคุมทั้งหมด",
        ["turkish"] = "Tüm Kontroller"
    };

    private static int installAttempted;
    private static int restartWarningLogged;

    static JmcSteamInputManifestInstaller()
    {
        JmcInputActionRegistry.ActionsChanged += OnActionsChanged;
    }

    public static bool IsManifestInstalled { get; private set; }

    public static string? GeneratedManifestPath { get; private set; }

    public static void InstallBeforeSteamInputInit()
    {
        _ = typeof(JmcSteamInputManifestInstaller);
        if (Interlocked.Exchange(ref installAttempted, 1) == 1)
        {
            return;
        }

        if (!SteamInitializer.Initialized)
        {
            ModLogger.Debug("Steamworks 尚未初始化，跳过 JML Steam Input manifest 安装。");
            return;
        }

        IReadOnlyList<JmcInputActionDescriptor> actions = JmcInputActionRegistry.GetActions();
        if (actions.Count == 0)
        {
            ModLogger.Debug("当前没有可暴露给 Steam Input 的 JML 热键动作，保持游戏原始 manifest。");
            return;
        }

        try
        {
            if (!TryFindOriginalManifest(out string originalPath))
            {
                ModLogger.Warn("未找到游戏原始 Steam Input manifest，跳过 JML Steam Input 接入。");
                return;
            }

            string generatedPath = ResolveGeneratedManifestPath();
            Directory.CreateDirectory(Path.GetDirectoryName(generatedPath)!);

            string originalText = File.ReadAllText(originalPath, Encoding.UTF8);
            string mergedText = SteamInputManifestMerger.Merge(
                originalText,
                actions,
                BuildLocalization(actions));

            File.WriteAllText(
                generatedPath,
                mergedText.ReplaceLineEndings("\r\n"),
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            string steamPath = Path.GetFullPath(generatedPath);
            if (!SteamInput.SetInputActionManifestFilePath(steamPath))
            {
                ModLogger.Warn($"Steam 拒绝安装 JML Steam Input Action Manifest：{generatedPath}");
                return;
            }

            GeneratedManifestPath = generatedPath;
            IsManifestInstalled = true;
            ModLogger.Info($"JML Steam Input Action Manifest 已生成并安装：{generatedPath}，动作数：{actions.Count}");
            foreach (JmcInputActionDescriptor action in actions)
            {
                ModLogger.Debug($"JML Steam Input 动作：{action.ActionId} => {action.Entry.DisplayName}", action.Assembly);
            }
        }
        catch (Exception ex)
        {
            IsManifestInstalled = false;
            ModLogger.Error("生成或安装 JML Steam Input manifest 失败，保留游戏原始输入配置。", ex);
        }
    }

    private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> BuildLocalization(
        IReadOnlyList<JmcInputActionDescriptor> actions)
    {
        var result = new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.Ordinal);
        foreach ((string steamLanguage, string jmlLanguage) in SteamLanguageToJml)
        {
            var entries = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Set_Title"] = SetTitles.TryGetValue(steamLanguage, out string? setTitle)
                    ? setTitle
                    : "All Controls"
            };

            foreach (JmcInputActionDescriptor action in actions)
            {
                string title = action.GetDisplayName(jmlLanguage);
                if (string.IsNullOrWhiteSpace(title))
                {
                    title = action.Entry.DisplayName;
                }

                entries[action.LocalizationKey] = title;
            }

            result[steamLanguage] = entries;
        }

        return result;
    }

    private static bool TryFindOriginalManifest(out string path)
    {
        foreach (string root in EnumerateSearchRoots())
        {
            foreach (string directory in WalkUp(root, maxDepth: 6))
            {
                string candidate = Path.Combine(directory, "controller_config", OriginalManifestFileName);
                if (File.Exists(candidate))
                {
                    path = candidate;
                    return true;
                }
            }
        }

        path = string.Empty;
        return false;
    }

    private static IEnumerable<string> EnumerateSearchRoots()
    {
        yield return AppContext.BaseDirectory;

        string? gameAssemblyDirectory = Path.GetDirectoryName(typeof(SteamControllerInputStrategy).Assembly.Location);
        if (!string.IsNullOrWhiteSpace(gameAssemblyDirectory))
        {
            yield return gameAssemblyDirectory;
        }

        string currentDirectory = Directory.GetCurrentDirectory();
        if (!string.IsNullOrWhiteSpace(currentDirectory))
        {
            yield return currentDirectory;
        }
    }

    private static IEnumerable<string> WalkUp(string root, int maxDepth)
    {
        DirectoryInfo? directory = Directory.Exists(root)
            ? new DirectoryInfo(root)
            : new DirectoryInfo(Path.GetDirectoryName(root) ?? root);

        for (int i = 0; directory != null && i <= maxDepth; i++)
        {
            yield return directory.FullName;
            directory = directory.Parent;
        }
    }

    private static string ResolveGeneratedManifestPath()
    {
        string userDataDir = OS.GetUserDataDir();
        if (string.IsNullOrWhiteSpace(userDataDir))
        {
            userDataDir = Path.Combine(
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData),
                "SlayTheSpire2");
        }

        return Path.Combine(userDataDir, "mods", "JmcModLib", "steam_input", GeneratedManifestFileName);
    }

    private static void OnActionsChanged()
    {
        if (!IsManifestInstalled || Interlocked.Exchange(ref restartWarningLogged, 1) == 1)
        {
            return;
        }

        ModLogger.Warn("JML Steam Input 动作在 manifest 安装后发生变化；新的手柄动作需要重启游戏后才会出现在 Steam 输入中。");
    }
}
