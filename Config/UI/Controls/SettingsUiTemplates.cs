using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.ControllerInput;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens.ModdingScreen;
using MegaCrit.Sts2.Core.Nodes.Screens.Settings;
using Godot;
using JmcModLib.Reflection;
using JmcModLib.Utils;

namespace JmcModLib.Config.UI;

internal sealed class SettingsUiTemplates
{
    public NSettingsTickbox? TickboxTemplate { get; private init; }

    public NSettingsSlider? SliderTemplate { get; private init; }

    public NSettingsDropdown? DropdownTemplate { get; private init; }

    public NDropdownItem? DropdownItemTemplate { get; private init; }

    public NSettingsButton? ButtonTemplate { get; private init; }

    public NSettingsButton? ResetButtonTemplate { get; private init; }

    public Control? CompactButtonTemplate { get; private init; }

    public Control? KeybindTemplate { get; private init; }

    public MegaRichTextLabel? RichLabelTemplate { get; private init; }

    public MegaRichTextLabel? DescriptionLabelTemplate { get; private init; }

    public bool HasStyledControls =>
        TickboxTemplate != null
        && SliderTemplate != null
        && DropdownTemplate != null
        && DropdownItemTemplate != null
        && ButtonTemplate != null;

    public Control? GetButtonTemplate(UIButtonColor color)
    {
        return color switch
        {
            UIButtonColor.Reset => ResetButtonTemplate ?? ButtonTemplate ?? CompactButtonTemplate,
            _ => ButtonTemplate ?? CompactButtonTemplate
        };
    }

    public static SettingsUiTemplates Resolve(Node context)
    {
        NSettingsScreen? screen = FindAncestor<NSettingsScreen>(context);
        if (screen == null)
        {
            return new SettingsUiTemplates();
        }

        NSettingsTickbox? tickboxTemplate = FindDescendant<NSettingsTickbox>(
            GetPanelRow(screen, "%GeneralSettings", "ShowRunTimer"));
        NSettingsSlider? sliderTemplate = FindDescendant<NSettingsSlider>(
            GetPanelRow(screen, "%SoundSettings", "MasterVolume"));
        NSettingsDropdown? displayDropdown =
            FindDescendant<NDisplayDropdown>(screen)
            ?? FindDescendantByName<NSettingsDropdown>(screen, "DisplayDropdown")
            ?? FindDescendant<NSettingsDropdown>(GetPanelRow(screen, "%GraphicsSettings", "DisplaySelection"));
        NSettingsDropdown? resolutionDropdown =
            FindDescendant<NResolutionDropdown>(screen)
            ?? FindDescendantByName<NSettingsDropdown>(screen, "ResolutionDropdown")
            ?? FindDescendant<NSettingsDropdown>(GetPanelRow(screen, "%GraphicsSettings", "WindowedResolution"));
        NSettingsDropdown? aspectRatioDropdown =
            FindDescendant<NAspectRatioDropdown>(screen)
            ?? FindDescendantByName<NSettingsDropdown>(screen, "AspectRatioDropdown")
            ?? FindDescendant<NSettingsDropdown>(GetPanelRow(screen, "%GraphicsSettings", "AspectRatio"));
        NSettingsDropdown? languageDropdown =
            FindDescendant<NLanguageDropdown>(screen)
            ?? FindDescendantByName<NSettingsDropdown>(screen, "LanguageDropdown")
            ?? FindDescendant<NSettingsDropdown>(GetPanelRow(screen, "%GeneralSettings", "LanguageLine"));
        NSettingsDropdown? dropdownTemplate = displayDropdown ?? resolutionDropdown ?? aspectRatioDropdown ?? languageDropdown;
        NDropdownItem? dropdownItemTemplate = ResolveDropdownItemTemplate(
            displayDropdown,
            resolutionDropdown,
            languageDropdown);
        NSettingsButton? buttonTemplate =
            screen.GetNodeOrNull<NSettingsButton>("%FeedbackButton")
            ?? FindDescendantByName<NSettingsButton>(screen, "FeedbackButton");
        NSettingsButton? resetButtonTemplate =
            FindDescendant<NResetGameplayButton>(GetPanelRow(screen, "%GeneralSettings", "ResetGameplay"))
            ?? FindDescendant<NResetGraphicsButton>(GetPanelRow(screen, "%GraphicsSettings", "ResetGraphics"))
            ?? FindDescendant<NResetTutorialsButton>(GetPanelRow(screen, "%GeneralSettings", "ResetTutorials"))
            ?? FindDescendantByName<NSettingsButton>(screen, "ResetGameplay")
            ?? FindDescendantByName<NSettingsButton>(screen, "ResetGraphics")
            ?? FindDescendantByName<NSettingsButton>(screen, "ResetTutorials");
        NSettingsPanel? inputSettingsPanel = screen.GetNodeOrNull<NSettingsPanel>("%InputSettings");
        Control? compactButtonTemplate =
            inputSettingsPanel?.GetNodeOrNull<Control>("%ResetToDefaultButton")
            ?? FindDescendantByName<Control>(inputSettingsPanel, "ResetToDefaultButton")
            ?? FindDescendantByName<Control>(screen, "ResetToDefaultButton");
        Control? keybindTemplate =
            FindDescendant<NInputSettingsEntry>(inputSettingsPanel?.Content)
            ?? InstantiateInputSettingsEntryTemplate();
        MegaRichTextLabel? richLabelTemplate = GetPanelRow(screen, "%GeneralSettings", "ShowRunTimer")
            ?.GetNodeOrNull<MegaRichTextLabel>("Label");
        MegaRichTextLabel? descriptionLabelTemplate = ResolveModDescriptionTemplate();

        if (compactButtonTemplate == null)
        {
            ModLogger.Warn("Could not find ResetToDefaultButton; compact mod setting buttons will use FeedbackButton as a fallback.");
        }

        if (buttonTemplate == null)
        {
            ModLogger.Warn("Could not find FeedbackButton; mod setting action buttons will fall back to plain Godot buttons.");
        }

        if (resetButtonTemplate == null)
        {
            ModLogger.Warn("Could not find a native reset settings button; reset-style mod buttons will fall back to FeedbackButton.");
        }

        if (dropdownTemplate == null || dropdownItemTemplate == null)
        {
            ModLogger.Warn(
                $"Could not find native settings dropdown templates; dropdown configs will use the fallback OptionButton. Dropdown={dropdownTemplate != null}, Item={dropdownItemTemplate != null}.");
        }
        else
        {
            ModLogger.Info(
                $"Resolved native settings dropdown template: Dropdown={dropdownTemplate.GetType().Name}, Item={dropdownItemTemplate.GetType().Name}.");
        }

        if (keybindTemplate == null)
        {
            ModLogger.Warn("Could not find native input settings entry template; keybind configs will use a fallback button.");
        }

        return new SettingsUiTemplates
        {
            TickboxTemplate = tickboxTemplate,
            SliderTemplate = sliderTemplate,
            DropdownTemplate = dropdownTemplate,
            DropdownItemTemplate = dropdownItemTemplate,
            ButtonTemplate = buttonTemplate,
            ResetButtonTemplate = resetButtonTemplate,
            CompactButtonTemplate = compactButtonTemplate,
            KeybindTemplate = keybindTemplate,
            RichLabelTemplate = richLabelTemplate,
            DescriptionLabelTemplate = descriptionLabelTemplate
        };
    }

    private static MegaRichTextLabel? ResolveModDescriptionTemplate()
    {
        NModdingScreen? moddingScreen = NModdingScreen.Create();
        try
        {
            NModInfoContainer? modInfoContainer = FindDescendant<NModInfoContainer>(moddingScreen);
            MegaRichTextLabel? description = modInfoContainer?.GetNodeOrNull<MegaRichTextLabel>("ModDescription");
            return description == null
                ? null
                : (MegaRichTextLabel)description.Duplicate();
        }
        finally
        {
            moddingScreen?.Free();
        }
    }

    private static Control? InstantiateInputSettingsEntryTemplate()
    {
        try
        {
            return ResourceLoader
                .Load<PackedScene>(
                    "res://scenes/screens/settings_screen/input_settings_entry.tscn",
                    null,
                    ResourceLoader.CacheMode.Reuse)
                ?.Instantiate<Control>(PackedScene.GenEditState.Disabled);
        }
        catch (Exception ex)
        {
            ModLogger.Warn("Failed to instantiate native input settings entry template.", ex);
            return null;
        }
    }

    private static Node? GetPanelRow(NSettingsScreen screen, string panelPath, string rowName)
    {
        NSettingsPanel? panel = screen.GetNodeOrNull<NSettingsPanel>(panelPath);
        return panel?.Content?.GetNodeOrNull<Node>(rowName);
    }

    private static NDropdownItem? ResolveDropdownItemTemplate(params NSettingsDropdown?[] dropdowns)
    {
        foreach (NSettingsDropdown? dropdown in dropdowns)
        {
            NDropdownItem? item = FindDescendant<NDropdownItem>(dropdown);
            if (item != null)
            {
                return item;
            }
        }

        foreach (NSettingsDropdown? dropdown in dropdowns)
        {
            NDropdownItem? item = InstantiateDropdownItemTemplate(dropdown);
            if (item != null)
            {
                return item;
            }
        }

        return null;
    }

    private static NDropdownItem? InstantiateDropdownItemTemplate(NSettingsDropdown? dropdown)
    {
        if (dropdown == null)
        {
            return null;
        }

        MemberAccessor? field = GetMemberInHierarchy(dropdown.GetType(), "_dropdownItemScene");
        if (field?.GetValue(dropdown) is not PackedScene scene)
        {
            return null;
        }

        try
        {
            return scene.Instantiate<NDropdownItem>(PackedScene.GenEditState.Disabled);
        }
        catch (Exception ex)
        {
            ModLogger.Warn($"Failed to instantiate dropdown item template from {dropdown.GetType().FullName}.", ex);
            return null;
        }
    }

    private static MemberAccessor? GetMemberInHierarchy(Type type, string name)
    {
        for (Type? current = type; current != null; current = current.BaseType)
        {
            try
            {
                return MemberAccessor.Get(current, name);
            }
            catch (MissingMemberException)
            {
            }
        }

        return null;
    }

    private static T? FindAncestor<T>(Node? node) where T : Node
    {
        Node? current = node;
        while (current != null)
        {
            if (current is T found)
            {
                return found;
            }

            current = current.GetParent();
        }

        return null;
    }

    private static T? FindDescendant<T>(Node? root) where T : Node
    {
        if (root == null)
        {
            return null;
        }

        if (root is T match)
        {
            return match;
        }

        foreach (Node child in root.GetChildren())
        {
            T? nested = FindDescendant<T>(child);
            if (nested != null)
            {
                return nested;
            }
        }

        return null;
    }

    private static T? FindDescendantByName<T>(Node? root, string name) where T : Node
    {
        if (root == null)
        {
            return null;
        }

        if (root.Name == name && root is T match)
        {
            return match;
        }

        foreach (Node child in root.GetChildren())
        {
            T? nested = FindDescendantByName<T>(child, name);
            if (nested != null)
            {
                return nested;
            }
        }

        return null;
    }
}
