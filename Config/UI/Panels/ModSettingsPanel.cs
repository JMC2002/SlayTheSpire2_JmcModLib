using System.Globalization;
using JmcModLib.Config;
using JmcModLib.Config.Entry;
using JmcModLib.Config.Serialization;
using Godot;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Nodes.Screens.Settings;

namespace JmcModLib.Config.UI;

internal sealed partial class ModSettingsPanel : NSettingsPanel
{

    private readonly Dictionary<string, Action<object?>> bindings = new(StringComparer.Ordinal);

    private const float ContentWidth = 1120f;
    private const int IntroFontSize = 24;
    private const float CollapseButtonWidth = 240f;
    private const float GlobalButtonWidth = 260f;
    private const float ActionButtonHeight = 56f;
    private const float KeybindEnableToggleWidth = 64f;
    private const float KeybindButtonWithToggleWidth = 1000f;

    private CenterContainer? centerRoot;
    private VBoxContainer? root;
    private VBoxContainer? listRoot;
    private HBoxContainer? titleActions;
    private MegaRichTextLabel? titleLabel;
    private MegaRichTextLabel? descriptionLabel;
    private SettingsUiTemplates? nativeTemplates;
    private JmcKeybindButton? listeningKeybind;
    private bool suppressControlEvents;

    public static ModSettingsPanel Create()
    {
        return new ModSettingsPanel
        {
            Name = "JmcModLibModSettingsPanel",
            Visible = false
        };
    }
}
