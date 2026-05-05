using Godot;
using JmcModLib.Config.Entry;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Modding;

namespace JmcModLib.Config.UI;

internal sealed partial class ModSettingsPanel
{
    private void RebuildContent()
    {
        if (listRoot == null)
        {
            return;
        }

        foreach (Node child in listRoot.GetChildren())
        {
            listRoot.RemoveChild(child);
            child.QueueFree();
        }

        bindings.Clear();
        listeningKeybind = null;
        _firstControl = null;

        List<Mod> modsWithConfig = [.. ModManager.Mods
            .Where(ModConfigUiBridge.HasConfig)
            .OrderBy(static mod => mod.manifest?.name ?? mod.manifest?.id ?? string.Empty, StringComparer.OrdinalIgnoreCase)];

        var focusableControls = new List<Control>();
        RefreshTitleActions(modsWithConfig, focusableControls);

        if (modsWithConfig.Count == 0)
        {
            listRoot.AddChild(BuildNotice(ModSettingsText.NoConfigMods()));
            RefreshPanelSizeAfterLayout();
            return;
        }

        foreach (Mod mod in modsWithConfig)
        {
            listRoot.AddChild(BuildModSection(mod, focusableControls));
        }

        UpdateFocusMap(focusableControls);
        RefreshPanelSizeAfterLayout();
    }
    private VBoxContainer BuildModSection(Mod mod, List<Control> focusableControls)
    {
        string name = mod.manifest?.name ?? mod.manifest?.id ?? "Unknown Mod";
        string version = mod.manifest?.version ?? "unknown";
        string author = mod.manifest?.author ?? "unknown";
        string? description = mod.manifest?.description;
        bool isCollapsed = IsSectionCollapsed(mod);

        var wrapper = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ShrinkBegin
        };
        wrapper.AddThemeConstantOverride("separation", 8);

        var header = new HBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        header.AddThemeConstantOverride("separation", 18);
        wrapper.AddChild(header);

        Control title = CreateStyledText($"[gold]{name}[/gold] [color=#aab7bc]v{version}[/color]");
        title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        header.AddChild(title);

        Control collapseButton = BuildCollapseButton(mod, isCollapsed);
        header.AddChild(collapseButton);
        focusableControls.Add(collapseButton);

        if (mod.assembly != null)
        {
            Control resetButton = BuildResetButton(mod.assembly);
            header.AddChild(resetButton);
            focusableControls.Add(resetButton);
        }

        if (!isCollapsed)
        {
            wrapper.AddChild(CreateStyledText($"[color=#aab7bc]{ModSettingsText.AuthorLabel()}:[/color] {author}"));

            if (!string.IsNullOrWhiteSpace(description))
            {
                wrapper.AddChild(CreateStyledText($"[color=#d0d8dc]{description}[/color]"));
            }

            if (mod.assembly == null)
            {
                wrapper.AddChild(BuildNotice(ModSettingsText.NoManagedAssembly()));
                wrapper.AddChild(new HSeparator());
                return wrapper;
            }

            IReadOnlyCollection<ConfigEntry> entries = ConfigManager.GetEntries(mod.assembly);
            if (entries.Count == 0)
            {
                wrapper.AddChild(BuildNotice(ModSettingsText.NoConfigEntries()));
                wrapper.AddChild(new HSeparator());
                return wrapper;
            }

            List<string> groups = [.. ConfigManager.GetGroups(mod.assembly)];
            bool hideDefaultGroupHeader = groups.Count == 1 && groups[0] == ConfigAttribute.DefaultGroup;

            foreach (string group in groups)
            {
                IReadOnlyCollection<ConfigEntry> groupEntries = [.. ConfigManager.GetEntries(group, mod.assembly)];
                if (!hideDefaultGroupHeader)
                {
                    wrapper.AddChild(BuildGroupHeader(mod.assembly, group, groupEntries));
                }

                foreach (ConfigEntry entry in groupEntries)
                {
                    wrapper.AddChild(BuildEntryRow(entry, focusableControls));
                }
            }
        }

        wrapper.AddChild(new HSeparator());
        return wrapper;
    }
    private MegaRichTextLabel BuildNotice(string text)
    {
        return CreateStyledText($"[color=#d0d8dc]{text}[/color]");
    }

    private VBoxContainer BuildGroupHeader(System.Reflection.Assembly assembly, string group, IReadOnlyCollection<ConfigEntry> entries)
    {
        var wrapper = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };

        wrapper.AddChild(CreateStyledText($"[color=#e0b24f]{ConfigLocalization.GetGroupName(assembly, group, entries)}[/color]"));
        wrapper.AddChild(new HSeparator());
        return wrapper;
    }

    private VBoxContainer BuildEntryRow(ConfigEntry entry, List<Control> focusableControls)
    {
        if (entry.UIAttribute is UIKeybindAttribute keybindAttribute)
        {
            return BuildKeybindEntryRow(entry, keybindAttribute, focusableControls);
        }

        var wrapper = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        wrapper.AddThemeConstantOverride("separation", 6);

        var topRow = new HBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Pass
        };
        wrapper.AddChild(topRow);

        var labelColumn = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ShrinkBegin
        };
        topRow.AddChild(labelColumn);

        labelColumn.AddChild(CreateStyledText($"[b]{ConfigLocalization.GetDisplayName(entry)}[/b]"));

        Control editor = BuildEditor(entry, focusableControls);
        editor.SizeFlagsHorizontal = SizeFlags.ShrinkEnd;
        topRow.AddChild(editor);
        AttachEntryHoverTip(topRow, entry);

        wrapper.AddChild(new HSeparator());
        return wrapper;
    }

    private VBoxContainer BuildKeybindEntryRow(
        ConfigEntry entry,
        UIKeybindAttribute keybindAttribute,
        List<Control> focusableControls)
    {
        var wrapper = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        wrapper.AddThemeConstantOverride("separation", 6);

        Control editor = BuildKeybindEditor(entry, keybindAttribute, focusableControls);
        wrapper.AddChild(editor);
        AttachEntryHoverTip(editor, entry);

        wrapper.AddChild(new HSeparator());
        return wrapper;
    }
}
