using System.Text;

namespace JmcModLib.Input;

/// <summary>
/// 针对 Steam Input VDF 的轻量合并器，读取游戏原始 IGA 并输出可被运行时覆盖 API 接受的 Action Manifest。
/// </summary>
internal static class SteamInputManifestMerger
{
    public static string Merge(
        string originalText,
        IReadOnlyList<JmcInputActionDescriptor> actions,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> localization)
    {
        ArgumentNullException.ThrowIfNull(originalText);
        ArgumentNullException.ThrowIfNull(actions);
        ArgumentNullException.ThrowIfNull(localization);

        if (actions.Count == 0)
        {
            return originalText;
        }

        string result = originalText.ReplaceLineEndings("\n");
        result = MergeButtonActions(result, actions);
        result = MergeLocalization(result, localization);
        return BuildActionManifest(result);
    }

    private static string MergeButtonActions(string text, IReadOnlyList<JmcInputActionDescriptor> actions)
    {
        if (!TryFindBlock(text, "actions", 0, text.Length, out VdfBlock actionsBlock)
            || !TryFindBlock(text, "Controls", actionsBlock.ContentStart, actionsBlock.CloseBrace, out VdfBlock controlsBlock)
            || !TryFindBlock(text, "Button", controlsBlock.ContentStart, controlsBlock.CloseBrace, out VdfBlock buttonBlock))
        {
            ModLogger.Warn("Steam Input manifest 结构不符合预期，找不到 actions/Controls/Button。");
            return text;
        }

        string buttonContent = text[buttonBlock.ContentStart..buttonBlock.CloseBrace];
        StringBuilder addition = new();
        foreach (JmcInputActionDescriptor action in actions)
        {
            if (ContainsVdfKey(buttonContent, action.ActionId))
            {
                continue;
            }

            addition.Append("\t\t\t\t\"")
                .Append(EscapeVdf(action.ActionId))
                .Append("\"\t\t\t\t\"#")
                .Append(EscapeVdf(action.LocalizationKey))
                .AppendLine("\"");
        }

        return addition.Length == 0
            ? text
            : text.Insert(FindLineStart(text, buttonBlock.CloseBrace), addition.ToString());
    }

    private static string MergeLocalization(
        string text,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> localization)
    {
        foreach ((string language, IReadOnlyDictionary<string, string> entries) in localization)
        {
            text = MergeLanguageLocalization(text, language, entries);
        }

        return text;
    }

    private static string MergeLanguageLocalization(
        string text,
        string language,
        IReadOnlyDictionary<string, string> entries)
    {
        if (!TryFindBlock(text, "localization", 0, text.Length, out VdfBlock localizationBlock))
        {
            ModLogger.Warn("Steam Input manifest 结构不符合预期，找不到 localization。");
            return text;
        }

        if (TryFindBlock(text, language, localizationBlock.ContentStart, localizationBlock.CloseBrace, out VdfBlock languageBlock))
        {
            string languageContent = text[languageBlock.ContentStart..languageBlock.CloseBrace];
            StringBuilder addition = new();
            foreach ((string key, string value) in entries)
            {
                if (ContainsVdfKey(languageContent, key))
                {
                    continue;
                }

                addition.Append("\t\t\t\"")
                    .Append(EscapeVdf(key))
                    .Append("\"\t\t\t\"")
                    .Append(EscapeVdf(value))
                    .AppendLine("\"");
            }

            return addition.Length == 0
                ? text
                : text.Insert(FindLineStart(text, languageBlock.CloseBrace), addition.ToString());
        }

        StringBuilder block = new();
        block.Append("\t\t\"")
            .Append(EscapeVdf(language))
            .AppendLine("\"");
        block.AppendLine("\t\t{");
        foreach ((string key, string value) in entries)
        {
            block.Append("\t\t\t\"")
                .Append(EscapeVdf(key))
                .Append("\"\t\t\t\"")
                .Append(EscapeVdf(value))
                .AppendLine("\"");
        }

        block.AppendLine("\t\t}");
        return text.Insert(FindLineStart(text, localizationBlock.CloseBrace), block.ToString());
    }

    private static string BuildActionManifest(string mergedIgaText)
    {
        if (!TryFindBlock(mergedIgaText, "actions", 0, mergedIgaText.Length, out VdfBlock actionsBlock)
            || !TryFindBlock(mergedIgaText, "localization", 0, mergedIgaText.Length, out VdfBlock localizationBlock))
        {
            ModLogger.Warn("Steam Input manifest 结构不符合预期，无法包装为 Action Manifest，保留原始 IGA 结构。");
            return mergedIgaText;
        }

        StringBuilder builder = new();
        builder.AppendLine("\"Action Manifest\"");
        builder.AppendLine("{");
        builder.AppendLine("\t\"configurations\"");
        builder.AppendLine("\t{");
        builder.AppendLine("\t}");
        AppendIndentedBlock(builder, mergedIgaText[actionsBlock.KeyIndex..(actionsBlock.CloseBrace + 1)], "\t");
        if (TryFindBlock(mergedIgaText, "action_layers", 0, mergedIgaText.Length, out VdfBlock actionLayersBlock))
        {
            AppendIndentedBlock(builder, mergedIgaText[actionLayersBlock.KeyIndex..(actionLayersBlock.CloseBrace + 1)], "\t");
        }

        AppendIndentedBlock(builder, mergedIgaText[localizationBlock.KeyIndex..(localizationBlock.CloseBrace + 1)], "\t");
        builder.AppendLine("}");
        return builder.ToString();
    }

    private static void AppendIndentedBlock(StringBuilder builder, string blockText, string indentation)
    {
        foreach (string line in blockText.Trim().Split('\n'))
        {
            builder.Append(indentation)
                .AppendLine(line.TrimEnd('\r'));
        }
    }

    private static bool TryFindBlock(
        string text,
        string key,
        int start,
        int end,
        out VdfBlock block)
    {
        string quotedKey = $"\"{key}\"";
        int keyIndex = text.IndexOf(quotedKey, start, StringComparison.Ordinal);
        while (keyIndex >= 0 && keyIndex < end)
        {
            int openBrace = text.IndexOf('{', keyIndex + quotedKey.Length);
            if (openBrace < 0 || openBrace >= end)
            {
                break;
            }

            int closeBrace = FindMatchingBrace(text, openBrace);
            if (closeBrace >= 0 && closeBrace <= end)
            {
                block = new VdfBlock(keyIndex, openBrace, openBrace + 1, closeBrace);
                return true;
            }

            keyIndex = text.IndexOf(quotedKey, keyIndex + quotedKey.Length, StringComparison.Ordinal);
        }

        block = default;
        return false;
    }

    private static int FindMatchingBrace(string text, int openBrace)
    {
        bool inString = false;
        bool escaped = false;
        int depth = 0;

        for (int i = openBrace; i < text.Length; i++)
        {
            char ch = text[i];
            if (escaped)
            {
                escaped = false;
                continue;
            }

            if (ch == '\\' && inString)
            {
                escaped = true;
                continue;
            }

            if (ch == '"')
            {
                inString = !inString;
                continue;
            }

            if (inString)
            {
                continue;
            }

            if (ch == '{')
            {
                depth++;
                continue;
            }

            if (ch == '}')
            {
                depth--;
                if (depth == 0)
                {
                    return i;
                }
            }
        }

        return -1;
    }

    private static bool ContainsVdfKey(string blockContent, string key)
    {
        return blockContent.Contains($"\"{EscapeVdf(key)}\"", StringComparison.Ordinal);
    }

    private static int FindLineStart(string text, int index)
    {
        int lineStart = Math.Clamp(index, 0, text.Length);
        while (lineStart > 0 && text[lineStart - 1] != '\n')
        {
            lineStart--;
        }

        return lineStart;
    }

    private static string EscapeVdf(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal);
    }

    private readonly record struct VdfBlock(int KeyIndex, int OpenBrace, int ContentStart, int CloseBrace);
}
