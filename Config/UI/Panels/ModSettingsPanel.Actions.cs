using System.Globalization;
using JmcModLib.Config;
using JmcModLib.Config.Entry;
using JmcModLib.Config.Serialization;
using Godot;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Nodes.Screens.Settings;

namespace JmcModLib.Config.UI;

internal sealed partial class ModSettingsPanel
{
    private void InvokeButtonEntry(ButtonEntry entry)
    {
        try
        {
            entry.Invoke();
        }
        catch (Exception ex)
        {
            ModLogger.Error($"Failed to invoke button entry {entry.Key}", ex, entry.Assembly);
        }
    }

    private void ResetModConfig(System.Reflection.Assembly assembly)
    {
        ConfigManager.ResetAssembly(assembly);
        RebuildContent();
    }

    private void RefreshTitleActions(List<Mod> mods, List<Control> focusableControls)
    {
        if (titleActions == null)
        {
            return;
        }

        foreach (Node child in titleActions.GetChildren())
        {
            titleActions.RemoveChild(child);
            child.QueueFree();
        }

        if (mods.Count == 0)
        {
            return;
        }

        bool hasExpanded = mods.Any(mod => !IsSectionCollapsed(mod));

        Control button = BuildGlobalCollapseButton(mods, hasExpanded);
        button.SizeFlagsHorizontal = SizeFlags.ShrinkEnd;
        button.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        titleActions.AddChild(button);
        focusableControls.Add(button);
    }

    private Control BuildCollapseButton(Mod mod, bool isCollapsed)
    {
        string label = isCollapsed ? ModSettingsText.Expand() : ModSettingsText.Collapse();

        void ToggleSection()
        {
            ModSettingsUiState.SetSectionCollapsed(GetSectionKey(mod), !isCollapsed);
            RebuildContent();
        }

        return BuildCompactActionButton(label, CollapseButtonWidth, ToggleSection);
    }

    private Control BuildGlobalCollapseButton(IReadOnlyCollection<Mod> mods, bool hasExpanded)
    {
        string label = hasExpanded ? ModSettingsText.CollapseAll() : ModSettingsText.ExpandAll();

        void ToggleAll()
        {
            SetAllSectionsCollapsed(mods, hasExpanded);
            RebuildContent();
        }

        return BuildCompactActionButton(label, GlobalButtonWidth, ToggleAll);
    }

    private Control BuildCompactActionButton(
        string label,
        float width,
        Action onPressed,
        UIButtonColor color = UIButtonColor.Default)
    {
        Control? template = nativeTemplates?.GetButtonTemplate(color);
        if (template != null)
        {
            Control nativeButton = JmcSettingsButton.Create(template, label, onPressed, color, hideImage: false);
            nativeButton.CustomMinimumSize = new Vector2(width, ActionButtonHeight);
            nativeButton.SizeFlagsHorizontal = SizeFlags.ShrinkEnd;
            nativeButton.SizeFlagsVertical = SizeFlags.ShrinkCenter;
            return nativeButton;
        }

        var button = new Button
        {
            Text = label,
            FocusMode = FocusModeEnum.All,
            CustomMinimumSize = new Vector2(width, ActionButtonHeight),
            SizeFlagsHorizontal = SizeFlags.ShrinkEnd,
            SizeFlagsVertical = SizeFlags.ShrinkCenter
        };
        ApplyFallbackButtonColor(button, color);
        button.Pressed += onPressed;
        return button;
    }

    private static void ApplyFallbackButtonColor(Button button, UIButtonColor color)
    {
        if (!JmcButtonColor.TryGetTint(color, out Color tint))
        {
            return;
        }

        button.Modulate = tint;
        button.SelfModulate = tint;
    }
    private Control BuildResetButton(System.Reflection.Assembly assembly)
    {
        Control? template = nativeTemplates?.GetButtonTemplate(UIButtonColor.Reset);
        if (template != null)
        {
            Control button = JmcSettingsButton.Create(
                template,
                ModSettingsText.ResetMod(),
                () => ResetModConfig(assembly),
                UIButtonColor.Reset,
                hideImage: false);
            button.SizeFlagsHorizontal = SizeFlags.ShrinkEnd;
            button.SizeFlagsVertical = SizeFlags.ShrinkCenter;
            return button;
        }

        var resetButton = new Button
        {
            Text = ModSettingsText.ResetMod(),
            FocusMode = FocusModeEnum.All,
            CustomMinimumSize = new Vector2(150f, 42f)
        };
        resetButton.Pressed += () => ResetModConfig(assembly);
        return resetButton;
    }
}
