using System.Globalization;
using JmcModLib.Config.Entry;
using Godot;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Nodes.Screens.Settings;

namespace JmcModLib.Config.UI;

internal sealed class ModSettingsPanel : NSettingsPanel
{
    private readonly Dictionary<string, Action<object?>> bindings = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, bool> CollapsedSections = new(StringComparer.OrdinalIgnoreCase);

    private const float MinPadding = 50f;
    private const float ContentWidth = 1120f;
    private const int IntroFontSize = 24;
    private const float CollapseButtonWidth = 240f;
    private const float GlobalButtonWidth = 260f;
    private const float ActionButtonHeight = 56f;

    private CenterContainer? centerRoot;
    private VBoxContainer? root;
    private VBoxContainer? listRoot;
    private HBoxContainer? titleActions;
    private MegaRichTextLabel? titleLabel;
    private MegaRichTextLabel? descriptionLabel;
    private SettingsUiTemplates? nativeTemplates;
    private bool suppressControlEvents;

    public static ModSettingsPanel Create()
    {
        return new ModSettingsPanel
        {
            Name = "JmcModLibModSettingsPanel",
            Visible = false
        };
    }

    public override void _Ready()
    {
        nativeTemplates = SettingsUiTemplates.Resolve(this);
        BuildLayout();
        Connect(CanvasItem.SignalName.VisibilityChanged, Callable.From(OnVisibilityChange));
        GetViewport().Connect(Viewport.SignalName.SizeChanged, Callable.From(RefreshPanelSize));
        ConfigManager.ValueChanged += OnConfigValueChanged;
        ConfigManager.EntryRegistered += OnEntryRegistered;
        ConfigManager.AssemblyRegistered += OnAssemblyChanged;
        ConfigManager.AssemblyUnregistered += OnAssemblyChanged;
        L10n.SubscribeToLocaleChange(OnLocaleChanged);
        RefreshPanelSize();
        RebuildContent();
    }

    public override void _ExitTree()
    {
        ConfigManager.ValueChanged -= OnConfigValueChanged;
        ConfigManager.EntryRegistered -= OnEntryRegistered;
        ConfigManager.AssemblyRegistered -= OnAssemblyChanged;
        ConfigManager.AssemblyUnregistered -= OnAssemblyChanged;
        L10n.UnsubscribeToLocaleChange(OnLocaleChanged);
        base._ExitTree();
    }

    protected override void OnVisibilityChange()
    {
        if (!Visible)
        {
            return;
        }

        RebuildContent();
        RefreshPanelSize();

        Tween tween = CreateTween().SetParallel();
        tween.TweenProperty(this, "modulate", Colors.White, 0.35).From(Colors.Transparent)
            .SetEase(Tween.EaseType.Out)
            .SetTrans(Tween.TransitionType.Cubic);
    }

    private void BuildLayout()
    {
        this.SetAnchorsPreset(LayoutPreset.TopLeft);
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        SizeFlagsVertical = SizeFlags.ShrinkBegin;

        centerRoot = new CenterContainer
        {
            Name = "CenterRoot",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ShrinkBegin
        };
        AddChild(centerRoot);

        root = new VBoxContainer
        {
            Name = "VBoxContainer",
            SizeFlagsHorizontal = SizeFlags.ShrinkCenter,
            SizeFlagsVertical = SizeFlags.ShrinkBegin,
            CustomMinimumSize = new Vector2(ContentWidth, 0f)
        };
        root.AddThemeConstantOverride("separation", 14);
        centerRoot.AddChild(root);

        var titleRow = new HBoxContainer
        {
            Name = "TitleRow",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ShrinkBegin
        };
        titleRow.AddThemeConstantOverride("separation", 20);

        titleLabel = CreateStyledText($"[gold]{ModSettingsText.Title()}[/gold]");
        Control title = titleLabel;
        title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        title.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        titleRow.AddChild(title);

        titleActions = new HBoxContainer
        {
            Name = "TitleActions",
            SizeFlagsHorizontal = SizeFlags.ShrinkEnd,
            SizeFlagsVertical = SizeFlags.ShrinkCenter
        };
        titleActions.AddThemeConstantOverride("separation", 12);
        titleRow.AddChild(titleActions);
        root.AddChild(titleRow);

        descriptionLabel = CreateDescriptionText($"[color=#aab7bc]{ModSettingsText.Description()}[/color]");
        root.AddChild(descriptionLabel);

        root.AddChild(new HSeparator());

        listRoot = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ShrinkBegin
        };
        listRoot.AddThemeConstantOverride("separation", 16);
        root.AddChild(listRoot);
    }

    private void RebuildContent()
    {
        if (listRoot == null)
        {
            return;
        }

        foreach (Node child in listRoot.GetChildren())
        {
            child.QueueFree();
        }

        bindings.Clear();
        _firstControl = null;

        List<Mod> modsWithConfig = [.. ModManager.Mods
            .Where(ModConfigUiBridge.HasConfig)
            .OrderBy(static mod => mod.manifest?.name ?? mod.manifest?.id ?? string.Empty, StringComparer.OrdinalIgnoreCase)];

        var focusableControls = new List<Control>();
        RefreshTitleActions(modsWithConfig, focusableControls);

        if (modsWithConfig.Count == 0)
        {
            listRoot.AddChild(BuildNotice(ModSettingsText.NoConfigMods()));
            RefreshPanelSize();
            return;
        }

        foreach (Mod mod in modsWithConfig)
        {
            listRoot.AddChild(BuildModSection(mod, focusableControls));
        }

        UpdateFocusMap(focusableControls);
        RefreshPanelSize();
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
        var wrapper = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        wrapper.AddThemeConstantOverride("separation", 6);

        var topRow = new HBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        wrapper.AddChild(topRow);

        var labelColumn = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ShrinkBegin
        };
        topRow.AddChild(labelColumn);

        labelColumn.AddChild(CreateStyledText($"[b]{ConfigLocalization.GetDisplayName(entry)}[/b]"));

        string description = ConfigLocalization.GetDescription(entry);
        if (!string.IsNullOrWhiteSpace(description))
        {
            labelColumn.AddChild(CreateStyledText($"[color=#aab7bc]{description}[/color]"));
        }

        if (entry.Attribute.RestartRequired)
        {
            labelColumn.AddChild(CreateStyledText($"[color=#e0b24f]{ModSettingsText.RestartRequired()}[/color]"));
        }

        Control editor = BuildEditor(entry, focusableControls);
        editor.SizeFlagsHorizontal = SizeFlags.ShrinkEnd;
        topRow.AddChild(editor);

        wrapper.AddChild(new HSeparator());
        return wrapper;
    }

    private Control BuildEditor(ConfigEntry entry, List<Control> focusableControls)
    {
        if (entry is ButtonEntry buttonEntry)
        {
            return BuildButtonEditor(buttonEntry, focusableControls);
        }

        Type valueType = Nullable.GetUnderlyingType(entry.ValueType) ?? entry.ValueType;
        UIConfigAttribute? uiAttribute = entry.UIAttribute;

        if (valueType == typeof(bool))
        {
            return BuildBooleanEditor(entry, focusableControls);
        }

        if (uiAttribute is UIDropdownAttribute dropdownAttribute)
        {
            return BuildDropdownEditor(entry, dropdownAttribute, valueType, focusableControls);
        }

        if (valueType.IsEnum)
        {
            return BuildDropdownEditor(entry, null, valueType, focusableControls);
        }

        if (valueType == typeof(string))
        {
            return BuildStringEditor(entry, focusableControls);
        }

        if (IsNumericType(valueType) && uiAttribute is ISliderConfigAttribute sliderAttribute)
        {
            return BuildSliderEditor(entry, sliderAttribute, valueType, focusableControls);
        }

        if (IsNumericType(valueType))
        {
            return BuildSpinBoxEditor(entry, valueType, focusableControls);
        }

        Control unsupported = CreateStyledText($"[color=#d07f7f]{ModSettingsText.UnsupportedType(valueType.Name)}[/color]");
        unsupported.CustomMinimumSize = new Vector2(240f, 0f);
        return unsupported;
    }

    private Control BuildBooleanEditor(ConfigEntry entry, List<Control> focusableControls)
    {
        if (nativeTemplates?.TickboxTemplate != null)
        {
            var tickbox = JmcSettingsTickbox.Create(
                nativeTemplates.TickboxTemplate,
                ToBoolean(entry.GetValue()),
                toggled =>
                {
                    if (!suppressControlEvents)
                    {
                        TrySetEntryValue(entry, toggled);
                    }
                });

            bindings[CreateBindingKey(entry)] = rawValue =>
            {
                suppressControlEvents = true;
                tickbox.SetValue(ToBoolean(rawValue));
                suppressControlEvents = false;
            };

            focusableControls.Add(tickbox);
            return tickbox;
        }

        var checkbox = new CheckBox
        {
            FocusMode = FocusModeEnum.All,
            CustomMinimumSize = new Vector2(220f, 0f),
            ButtonPressed = ToBoolean(entry.GetValue())
        };

        checkbox.Toggled += toggled =>
        {
            if (!suppressControlEvents)
            {
                TrySetEntryValue(entry, toggled);
            }
        };

        bindings[CreateBindingKey(entry)] = rawValue =>
        {
            suppressControlEvents = true;
            checkbox.ButtonPressed = ToBoolean(rawValue);
            suppressControlEvents = false;
        };

        focusableControls.Add(checkbox);
        return checkbox;
    }

    private LineEdit BuildStringEditor(ConfigEntry entry, List<Control> focusableControls)
    {
        var lineEdit = new LineEdit
        {
            FocusMode = FocusModeEnum.All,
            CustomMinimumSize = new Vector2(260f, 0f),
            Text = entry.GetValue()?.ToString() ?? string.Empty
        };

        void Commit()
        {
            if (!suppressControlEvents)
            {
                TrySetEntryValue(entry, lineEdit.Text);
            }
        }

        lineEdit.TextSubmitted += _ => Commit();
        lineEdit.FocusExited += Commit;

        bindings[CreateBindingKey(entry)] = rawValue =>
        {
            suppressControlEvents = true;
            lineEdit.Text = rawValue?.ToString() ?? string.Empty;
            suppressControlEvents = false;
        };

        focusableControls.Add(lineEdit);
        return lineEdit;
    }

    private Control BuildDropdownEditor(
        ConfigEntry entry,
        UIDropdownAttribute? dropdownAttribute,
        Type valueType,
        List<Control> focusableControls)
    {
        IReadOnlyList<string> options = valueType.IsEnum
            ? [.. Enum.GetNames(valueType).Where(option => dropdownAttribute?.Exclude.Contains(option, StringComparer.OrdinalIgnoreCase) != true)]
            : dropdownAttribute?.Options.Count > 0
                ? dropdownAttribute.Options
                : [entry.GetValue()?.ToString() ?? string.Empty];

        if (options.Count == 0)
        {
            options = [entry.GetValue()?.ToString() ?? string.Empty];
        }

        if (nativeTemplates?.DropdownTemplate != null && nativeTemplates.DropdownItemTemplate != null)
        {
            IReadOnlyList<JmcDropdownOption> localizedOptions = [.. options.Select(option =>
                new JmcDropdownOption(ConfigLocalization.GetOptionText(entry, option), option))];

            var nativeDropdown = JmcSettingsDropdown.Create(
                nativeTemplates.DropdownTemplate,
                nativeTemplates.DropdownItemTemplate,
                localizedOptions,
                entry.GetValue()?.ToString() ?? string.Empty,
                selectedText =>
                {
                    if (suppressControlEvents)
                    {
                        return;
                    }

                    object? converted = valueType.IsEnum
                        ? Enum.Parse(valueType, selectedText, ignoreCase: true)
                        : selectedText;
                    TrySetEntryValue(entry, converted);
                });

            bindings[CreateBindingKey(entry)] = rawValue =>
            {
                suppressControlEvents = true;
                nativeDropdown.SetValue(rawValue?.ToString() ?? string.Empty);
                suppressControlEvents = false;
            };

            focusableControls.Add(nativeDropdown);
            return nativeDropdown;
        }

        var dropdown = new OptionButton
        {
            FocusMode = FocusModeEnum.All,
            CustomMinimumSize = new Vector2(260f, 0f)
        };

        for (int i = 0; i < options.Count; i++)
        {
            dropdown.AddItem(ConfigLocalization.GetOptionText(entry, options[i]), i);
        }

        dropdown.ItemSelected += index =>
        {
            if (suppressControlEvents)
            {
                return;
            }

            string selectedText = options[(int)index];
            object? converted = valueType.IsEnum
                ? Enum.Parse(valueType, selectedText, ignoreCase: true)
                : selectedText;
            TrySetEntryValue(entry, converted);
        };

        bindings[CreateBindingKey(entry)] = rawValue =>
        {
            string selectedText = rawValue?.ToString() ?? string.Empty;
            int index = options
                .Select((text, i) => new { text, i })
                .FirstOrDefault(item => string.Equals(item.text, selectedText, StringComparison.OrdinalIgnoreCase))?.i ?? 0;

            suppressControlEvents = true;
            dropdown.Select(index);
            suppressControlEvents = false;
        };

        bindings[CreateBindingKey(entry)](entry.GetValue());
        focusableControls.Add(dropdown);
        return dropdown;
    }

    private SpinBox BuildSpinBoxEditor(ConfigEntry entry, Type valueType, List<Control> focusableControls)
    {
        var spinBox = new SpinBox
        {
            FocusMode = FocusModeEnum.All,
            CustomMinimumSize = new Vector2(220f, 0f),
            MinValue = GetMinNumericValue(valueType),
            MaxValue = GetMaxNumericValue(valueType),
            Step = valueType == typeof(float) || valueType == typeof(double) || valueType == typeof(decimal) ? 0.1 : 1.0
        };

        spinBox.ValueChanged += value =>
        {
            if (suppressControlEvents)
            {
                return;
            }

            object converted = ConfigValueConverter.Convert(value, valueType)!;
            TrySetEntryValue(entry, converted);
        };

        bindings[CreateBindingKey(entry)] = rawValue =>
        {
            double numericValue = System.Convert.ToDouble(ConfigValueConverter.Convert(rawValue, valueType), CultureInfo.InvariantCulture);
            suppressControlEvents = true;
            spinBox.Value = numericValue;
            suppressControlEvents = false;
        };

        bindings[CreateBindingKey(entry)](entry.GetValue());
        focusableControls.Add(spinBox);
        return spinBox;
    }

    private Control BuildSliderEditor(
        ConfigEntry entry,
        ISliderConfigAttribute sliderAttribute,
        Type valueType,
        List<Control> focusableControls)
    {
        if (nativeTemplates?.SliderTemplate != null)
        {
            var nativeSlider = JmcSettingsSlider.Create(
                nativeTemplates.SliderTemplate,
                sliderAttribute.Min,
                sliderAttribute.Max,
                sliderAttribute.Step,
                System.Convert.ToDouble(ConfigValueConverter.Convert(entry.GetValue(), valueType), CultureInfo.InvariantCulture),
                value => FormatNumericValue(value, valueType),
                value =>
                {
                    if (suppressControlEvents)
                    {
                        return;
                    }

                    object converted = ConfigValueConverter.Convert(value, valueType)!;
                    TrySetEntryValue(entry, converted);
                });

            bindings[CreateBindingKey(entry)] = rawValue =>
            {
                double numericValue = System.Convert.ToDouble(ConfigValueConverter.Convert(rawValue, valueType), CultureInfo.InvariantCulture);
                suppressControlEvents = true;
                nativeSlider.SetValue(numericValue);
                suppressControlEvents = false;
            };

            focusableControls.Add(nativeSlider);
            return nativeSlider;
        }

        var wrapper = new HBoxContainer
        {
            CustomMinimumSize = new Vector2(320f, 0f)
        };

        var slider = new HSlider
        {
            FocusMode = FocusModeEnum.All,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            MinValue = sliderAttribute.Min,
            MaxValue = sliderAttribute.Max,
            Step = sliderAttribute.Step
        };
        wrapper.AddChild(slider);

        var valueLabel = new Label
        {
            CustomMinimumSize = new Vector2(70f, 0f),
            HorizontalAlignment = HorizontalAlignment.Right
        };
        wrapper.AddChild(valueLabel);

        slider.ValueChanged += value =>
        {
            valueLabel.Text = FormatNumericValue(value, valueType);
            if (suppressControlEvents)
            {
                return;
            }

            object converted = ConfigValueConverter.Convert(value, valueType)!;
            TrySetEntryValue(entry, converted);
        };

        bindings[CreateBindingKey(entry)] = rawValue =>
        {
            double numericValue = System.Convert.ToDouble(ConfigValueConverter.Convert(rawValue, valueType), CultureInfo.InvariantCulture);
            suppressControlEvents = true;
            slider.Value = numericValue;
            valueLabel.Text = FormatNumericValue(numericValue, valueType);
            suppressControlEvents = false;
        };

        bindings[CreateBindingKey(entry)](entry.GetValue());
        focusableControls.Add(slider);
        return wrapper;
    }

    private Control BuildButtonEditor(ButtonEntry entry, List<Control> focusableControls)
    {
        Control button = BuildCompactActionButton(
            ConfigLocalization.GetButtonText(entry),
            GlobalButtonWidth,
            () => InvokeButtonEntry(entry),
            entry.Color);
        focusableControls.Add(button);
        return button;
    }

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
            CollapsedSections[GetSectionKey(mod)] = !isCollapsed;
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

    private MegaRichTextLabel CreateStyledText(string text)
    {
        if (nativeTemplates?.RichLabelTemplate != null)
        {
            MegaRichTextLabel label = (MegaRichTextLabel)nativeTemplates.RichLabelTemplate.Duplicate();
            label.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            label.FitContent = true;
            label.ScrollActive = false;
            label.Text = text;
            return label;
        }

        return new MegaRichTextLabel
        {
            BbcodeEnabled = true,
            FitContent = true,
            ScrollActive = false,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            Text = text
        };
    }

    private MegaRichTextLabel CreateDescriptionText(string text)
    {
        MegaRichTextLabel label;
        if (nativeTemplates?.RichLabelTemplate != null)
        {
            label = (MegaRichTextLabel)nativeTemplates.RichLabelTemplate.Duplicate();
        }
        else
        {
            label = new MegaRichTextLabel
            {
                BbcodeEnabled = true
            };
        }

        label.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        label.FitContent = true;
        label.ScrollActive = false;
        label.AutoSizeEnabled = false;
        label.MinFontSize = IntroFontSize;
        label.MaxFontSize = IntroFontSize;
        label.Text = text;
        label.Call("SetFontSize", IntroFontSize);
        return label;
    }

    private void TrySetEntryValue(ConfigEntry entry, object? rawValue)
    {
        try
        {
            object? converted = ConfigValueConverter.Convert(rawValue, entry.ValueType);
            entry.SetValue(converted);
        }
        catch (Exception ex)
        {
            ModLogger.Error($"Failed to update config entry {entry.Key}", ex, entry.Assembly);
            if (bindings.TryGetValue(CreateBindingKey(entry), out Action<object?>? updateBinding))
            {
                updateBinding(entry.GetValue());
            }
        }
    }

    private void OnConfigValueChanged(ConfigEntry entry, object? value)
    {
        if (bindings.TryGetValue(CreateBindingKey(entry), out Action<object?>? updateBinding))
        {
            updateBinding(value);
        }
    }

    private void OnEntryRegistered(ConfigEntry _)
    {
        if (Visible)
        {
            RebuildContent();
        }
    }

    private void OnAssemblyChanged(System.Reflection.Assembly _)
    {
        if (Visible)
        {
            RebuildContent();
        }
    }

    private void OnLocaleChanged()
    {
        RefreshStaticText();

        if (Visible)
        {
            RebuildContent();
        }
    }

    private void RefreshStaticText()
    {
        if (titleLabel != null)
        {
            titleLabel.Text = $"[gold]{ModSettingsText.Title()}[/gold]";
        }

        if (descriptionLabel != null)
        {
            descriptionLabel.Text = $"[color=#aab7bc]{ModSettingsText.Description()}[/color]";
        }
    }

    private void RefreshPanelSize()
    {
        if (root == null || centerRoot == null)
        {
            return;
        }

        Control? parent = GetParent<Control>();
        if (parent == null)
        {
            return;
        }

        Vector2 parentSize = parent.Size;
        Vector2 minimumSize = centerRoot.GetMinimumSize();
        float width = Math.Min(parentSize.X, Math.Max(ContentWidth, minimumSize.X));
        Size = minimumSize.Y + MinPadding >= parentSize.Y
            ? new Vector2(width, minimumSize.Y + parentSize.Y * 0.4f)
            : new Vector2(width, minimumSize.Y);
        Position = new Vector2(Mathf.Max((parentSize.X - Size.X) * 0.5f, 0f), Position.Y);
    }

    private void UpdateFocusMap(List<Control> focusableControls)
    {
        if (focusableControls.Count == 0)
        {
            _firstControl = null;
            return;
        }

        _firstControl = focusableControls[0];

        for (int i = 0; i < focusableControls.Count; i++)
        {
            Control current = focusableControls[i];
            Control previous = i > 0 ? focusableControls[i - 1] : current;
            Control next = i < focusableControls.Count - 1 ? focusableControls[i + 1] : current;

            current.FocusMode = FocusModeEnum.All;
            current.FocusNeighborLeft = current.GetPath();
            current.FocusNeighborRight = current.GetPath();
            current.FocusNeighborTop = previous.GetPath();
            current.FocusNeighborBottom = next.GetPath();
        }
    }

    private static string CreateBindingKey(ConfigEntry entry)
    {
        return $"{entry.Assembly.FullName}::{entry.Key}";
    }

    private static string GetSectionKey(Mod mod)
    {
        return mod.manifest?.id
            ?? mod.manifest?.name
            ?? mod.assembly?.FullName
            ?? "unknown";
    }

    private static bool IsSectionCollapsed(Mod mod)
    {
        return CollapsedSections.TryGetValue(GetSectionKey(mod), out bool isCollapsed) && isCollapsed;
    }

    private static void SetAllSectionsCollapsed(IEnumerable<Mod> mods, bool collapsed)
    {
        foreach (Mod mod in mods)
        {
            CollapsedSections[GetSectionKey(mod)] = collapsed;
        }
    }
    private static bool IsNumericType(Type type)
    {
        return type == typeof(byte)
            || type == typeof(sbyte)
            || type == typeof(short)
            || type == typeof(ushort)
            || type == typeof(int)
            || type == typeof(uint)
            || type == typeof(long)
            || type == typeof(ulong)
            || type == typeof(float)
            || type == typeof(double)
            || type == typeof(decimal);
    }

    private static double GetMinNumericValue(Type type)
    {
        if (type == typeof(byte)) return byte.MinValue;
        if (type == typeof(sbyte)) return sbyte.MinValue;
        if (type == typeof(short)) return short.MinValue;
        if (type == typeof(ushort)) return ushort.MinValue;
        if (type == typeof(int)) return int.MinValue;
        if (type == typeof(uint)) return uint.MinValue;
        if (type == typeof(long)) return long.MinValue;
        if (type == typeof(ulong)) return 0;
        return -1000000;
    }

    private static double GetMaxNumericValue(Type type)
    {
        if (type == typeof(byte)) return byte.MaxValue;
        if (type == typeof(sbyte)) return sbyte.MaxValue;
        if (type == typeof(short)) return short.MaxValue;
        if (type == typeof(ushort)) return ushort.MaxValue;
        if (type == typeof(int)) return int.MaxValue;
        if (type == typeof(uint)) return uint.MaxValue;
        if (type == typeof(long)) return long.MaxValue;
        if (type == typeof(ulong)) return 1000000;
        return 1000000;
    }

    private static string FormatNumericValue(double value, Type valueType)
    {
        if (valueType == typeof(float) || valueType == typeof(double) || valueType == typeof(decimal))
        {
            return value.ToString("0.##", CultureInfo.InvariantCulture);
        }

        return Math.Round(value).ToString(CultureInfo.InvariantCulture);
    }

    private static bool ToBoolean(object? value)
    {
        return value switch
        {
            bool b => b,
            null => false,
            _ => System.Convert.ToBoolean(value, CultureInfo.InvariantCulture)
        };
    }
}




