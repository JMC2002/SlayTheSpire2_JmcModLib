using System.Globalization;
using System.Reflection;
using Godot;
using JmcModLib.Config.Entry;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Screens.ScreenContext;

namespace JmcModLib.Config.UI;

internal sealed class ModConfigPopup : Control, IScreenContext
{
    private readonly Dictionary<string, Action<object?>> bindings = new(StringComparer.Ordinal);

    private Assembly? assembly;
    private Button? closeButton;
    private VBoxContainer? contentRoot;
    private bool suppressControlEvents;

    public required Mod Mod { get; init; }

    public Control? DefaultFocusedControl => closeButton;

    public static ModConfigPopup Create(Mod mod)
    {
        return new ModConfigPopup
        {
            Mod = mod
        };
    }

    public override void _Ready()
    {
        assembly = Mod.assembly;
        BuildLayout();
        RebuildContent();
        ConfigManager.ValueChanged += OnConfigValueChanged;
    }

    public override void _ExitTree()
    {
        ConfigManager.ValueChanged -= OnConfigValueChanged;
        base._ExitTree();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        base._UnhandledInput(@event);

        if (@event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape })
        {
            CloseModal();
            GetViewport()?.SetInputAsHandled();
        }
    }

    private void BuildLayout()
    {
        this.SetAnchorsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;

        var center = new CenterContainer();
        center.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(center);

        var panel = new PanelContainer
        {
            CustomMinimumSize = new Vector2(920f, 680f),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        center.AddChild(panel);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 28);
        margin.AddThemeConstantOverride("margin_top", 24);
        margin.AddThemeConstantOverride("margin_right", 28);
        margin.AddThemeConstantOverride("margin_bottom", 24);
        panel.AddChild(margin);

        var root = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        margin.AddChild(root);

        var title = new MegaRichTextLabel
        {
            BbcodeEnabled = true,
            FitContent = true,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        string modName = Mod.manifest?.name ?? Mod.manifest?.id ?? "Unknown Mod";
        title.Text = $"[gold]{modName}[/gold] Config";
        root.AddChild(title);

        var subtitle = new RichTextLabel
        {
            BbcodeEnabled = true,
            FitContent = true,
            ScrollActive = false,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        subtitle.Text = BuildSubtitle();
        root.AddChild(subtitle);

        root.AddChild(new HSeparator());

        var scroll = new ScrollContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
        };
        root.AddChild(scroll);

        contentRoot = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        scroll.AddChild(contentRoot);

        root.AddChild(new HSeparator());

        var footer = new HBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        root.AddChild(footer);

        var hint = new RichTextLabel
        {
            BbcodeEnabled = true,
            FitContent = true,
            ScrollActive = false,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        hint.Text = "[color=#aab7bc]Changes are saved immediately.[/color]";
        footer.AddChild(hint);

        var resetButton = new Button
        {
            Text = "Reset",
            CustomMinimumSize = new Vector2(120f, 42f)
        };
        resetButton.Pressed += OnResetPressed;
        footer.AddChild(resetButton);

        closeButton = new Button
        {
            Text = "Close",
            CustomMinimumSize = new Vector2(120f, 42f)
        };
        closeButton.Pressed += CloseModal;
        footer.AddChild(closeButton);
    }

    private string BuildSubtitle()
    {
        string author = Mod.manifest?.author ?? "unknown";
        string version = Mod.manifest?.version ?? "unknown";
        return $"[b]Author[/b]: {author}\n[b]Version[/b]: {version}";
    }

    private void RebuildContent()
    {
        if (contentRoot == null)
        {
            return;
        }

        foreach (Node child in contentRoot.GetChildren())
        {
            child.QueueFree();
        }

        bindings.Clear();

        if (assembly == null)
        {
            contentRoot.AddChild(BuildNotice("This mod does not have a loaded assembly, so its config cannot be shown."));
            return;
        }

        IReadOnlyCollection<ConfigEntry> entries = ConfigManager.GetEntries(assembly);
        if (entries.Count == 0)
        {
            contentRoot.AddChild(BuildNotice("This mod has not registered any configurable entries."));
            return;
        }

        List<string> groups = ConfigManager.GetGroups(assembly).ToList();
        bool hideDefaultGroupHeader = groups.Count == 1 && groups[0] == ConfigAttribute.DefaultGroup;

        foreach (string group in groups)
        {
            if (!hideDefaultGroupHeader)
            {
                contentRoot.AddChild(BuildGroupHeader(group));
            }

            foreach (ConfigEntry entry in ConfigManager.GetEntries(group, assembly))
            {
                contentRoot.AddChild(BuildEntryRow(entry));
            }
        }
    }

    private Control BuildNotice(string text)
    {
        return new RichTextLabel
        {
            BbcodeEnabled = true,
            FitContent = true,
            ScrollActive = false,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            Text = $"[color=#d0d8dc]{text}[/color]"
        };
    }

    private Control BuildGroupHeader(string group)
    {
        var wrapper = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };

        var label = new RichTextLabel
        {
            BbcodeEnabled = true,
            FitContent = true,
            ScrollActive = false,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            Text = $"[gold]{group}[/gold]"
        };
        wrapper.AddChild(label);
        wrapper.AddChild(new HSeparator());
        return wrapper;
    }

    private Control BuildEntryRow(ConfigEntry entry)
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

        var title = new RichTextLabel
        {
            BbcodeEnabled = true,
            FitContent = true,
            ScrollActive = false,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            Text = $"[b]{entry.DisplayName}[/b]"
        };
        labelColumn.AddChild(title);

        if (!string.IsNullOrWhiteSpace(entry.Attribute.Description))
        {
            labelColumn.AddChild(new RichTextLabel
            {
                BbcodeEnabled = true,
                FitContent = true,
                ScrollActive = false,
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                Text = $"[color=#aab7bc]{entry.Attribute.Description}[/color]"
            });
        }

        if (entry.Attribute.RestartRequired)
        {
            labelColumn.AddChild(new RichTextLabel
            {
                BbcodeEnabled = true,
                FitContent = true,
                ScrollActive = false,
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                Text = "[color=#e0b24f]Requires restart to fully apply.[/color]"
            });
        }

        Control editor = BuildEditor(entry);
        editor.SizeFlagsHorizontal = SizeFlags.ShrinkEnd;
        topRow.AddChild(editor);

        wrapper.AddChild(new HSeparator());
        return wrapper;
    }

    private Control BuildEditor(ConfigEntry entry)
    {
        Type valueType = Nullable.GetUnderlyingType(entry.ValueType) ?? entry.ValueType;
        UIConfigAttribute? uiAttribute = entry.UIAttribute;

        if (valueType == typeof(bool))
        {
            return BuildBooleanEditor(entry);
        }

        if (uiAttribute is UIDropdownAttribute dropdownAttribute)
        {
            return BuildDropdownEditor(entry, dropdownAttribute, valueType);
        }

        if (valueType.IsEnum)
        {
            return BuildDropdownEditor(entry, null, valueType);
        }

        if (valueType == typeof(string))
        {
            return BuildStringEditor(entry);
        }

        if (IsNumericType(valueType) && uiAttribute is ISliderConfigAttribute sliderAttribute)
        {
            return BuildSliderEditor(entry, sliderAttribute, valueType);
        }

        if (IsNumericType(valueType))
        {
            return BuildSpinBoxEditor(entry, valueType);
        }

        return new RichTextLabel
        {
            BbcodeEnabled = true,
            FitContent = true,
            ScrollActive = false,
            CustomMinimumSize = new Vector2(240f, 0f),
            Text = $"[color=#d07f7f]Unsupported type: {valueType.Name}[/color]"
        };
    }

    private Control BuildBooleanEditor(ConfigEntry entry)
    {
        var checkbox = new CheckBox
        {
            CustomMinimumSize = new Vector2(220f, 0f),
            ButtonPressed = ToBoolean(entry.GetValue())
        };

        checkbox.Toggled += toggled =>
        {
            if (suppressControlEvents)
            {
                return;
            }

            TrySetEntryValue(entry, toggled);
        };

        bindings[entry.Key] = rawValue =>
        {
            suppressControlEvents = true;
            checkbox.ButtonPressed = ToBoolean(rawValue);
            suppressControlEvents = false;
        };

        return checkbox;
    }

    private Control BuildStringEditor(ConfigEntry entry)
    {
        var lineEdit = new LineEdit
        {
            CustomMinimumSize = new Vector2(260f, 0f),
            Text = entry.GetValue()?.ToString() ?? string.Empty
        };

        void Commit()
        {
            if (suppressControlEvents)
            {
                return;
            }

            TrySetEntryValue(entry, lineEdit.Text);
        }

        lineEdit.TextSubmitted += _ => Commit();
        lineEdit.FocusExited += Commit;

        bindings[entry.Key] = rawValue =>
        {
            suppressControlEvents = true;
            lineEdit.Text = rawValue?.ToString() ?? string.Empty;
            suppressControlEvents = false;
        };

        return lineEdit;
    }

    private Control BuildDropdownEditor(ConfigEntry entry, UIDropdownAttribute? dropdownAttribute, Type valueType)
    {
        var dropdown = new OptionButton
        {
            CustomMinimumSize = new Vector2(260f, 0f)
        };

        IReadOnlyList<string> options = valueType.IsEnum
            ? Enum.GetNames(valueType)
                .Where(option => dropdownAttribute?.Exclude.Contains(option, StringComparer.OrdinalIgnoreCase) != true)
                .ToArray()
            : dropdownAttribute?.Options.Count > 0
                ? dropdownAttribute.Options
                : [entry.GetValue()?.ToString() ?? string.Empty];

        for (int i = 0; i < options.Count; i++)
        {
            dropdown.AddItem(options[i], i);
        }

        dropdown.ItemSelected += index =>
        {
            if (suppressControlEvents)
            {
                return;
            }

            string selectedText = dropdown.GetItemText((int)index);
            object? converted = valueType.IsEnum
                ? Enum.Parse(valueType, selectedText, ignoreCase: true)
                : selectedText;
            TrySetEntryValue(entry, converted);
        };

        bindings[entry.Key] = rawValue =>
        {
            string selectedText = rawValue?.ToString() ?? string.Empty;
            int index = options
                .Select((text, i) => new { text, i })
                .FirstOrDefault(item => string.Equals(item.text, selectedText, StringComparison.OrdinalIgnoreCase))?.i ?? 0;

            suppressControlEvents = true;
            dropdown.Select(index);
            suppressControlEvents = false;
        };

        bindings[entry.Key](entry.GetValue());
        return dropdown;
    }

    private Control BuildSpinBoxEditor(ConfigEntry entry, Type valueType)
    {
        var spinBox = new SpinBox
        {
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

        bindings[entry.Key] = rawValue =>
        {
            double numericValue = System.Convert.ToDouble(ConfigValueConverter.Convert(rawValue, valueType), CultureInfo.InvariantCulture);
            suppressControlEvents = true;
            spinBox.Value = numericValue;
            suppressControlEvents = false;
        };

        bindings[entry.Key](entry.GetValue());
        return spinBox;
    }

    private Control BuildSliderEditor(ConfigEntry entry, ISliderConfigAttribute sliderAttribute, Type valueType)
    {
        var wrapper = new HBoxContainer
        {
            CustomMinimumSize = new Vector2(320f, 0f)
        };

        var slider = new HSlider
        {
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

        bindings[entry.Key] = rawValue =>
        {
            double numericValue = System.Convert.ToDouble(ConfigValueConverter.Convert(rawValue, valueType), CultureInfo.InvariantCulture);
            suppressControlEvents = true;
            slider.Value = numericValue;
            valueLabel.Text = FormatNumericValue(numericValue, valueType);
            suppressControlEvents = false;
        };

        bindings[entry.Key](entry.GetValue());
        return wrapper;
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
            ModLogger.Error($"Failed to update config entry {entry.Key}", ex, assembly);
            if (bindings.TryGetValue(entry.Key, out Action<object?>? updateBinding))
            {
                updateBinding(entry.GetValue());
            }
        }
    }

    private void OnResetPressed()
    {
        if (assembly == null)
        {
            return;
        }

        ConfigManager.ResetAssembly(assembly);
        RebuildContent();
    }

    private void CloseModal()
    {
        NModalContainer.Instance?.Clear();
    }

    private void OnConfigValueChanged(ConfigEntry entry, object? value)
    {
        if (assembly == null || entry.Assembly != assembly)
        {
            return;
        }

        if (bindings.TryGetValue(entry.Key, out Action<object?>? updateBinding))
        {
            updateBinding(value);
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
        if (type == typeof(byte))
        {
            return byte.MinValue;
        }

        if (type == typeof(sbyte))
        {
            return sbyte.MinValue;
        }

        if (type == typeof(short))
        {
            return short.MinValue;
        }

        if (type == typeof(ushort))
        {
            return ushort.MinValue;
        }

        if (type == typeof(int))
        {
            return int.MinValue;
        }

        if (type == typeof(uint))
        {
            return uint.MinValue;
        }

        if (type == typeof(long))
        {
            return long.MinValue;
        }

        if (type == typeof(ulong))
        {
            return 0;
        }

        if (type == typeof(float))
        {
            return -1000000;
        }

        if (type == typeof(double))
        {
            return -1000000;
        }

        if (type == typeof(decimal))
        {
            return -1000000;
        }

        return -1000000;
    }

    private static double GetMaxNumericValue(Type type)
    {
        if (type == typeof(byte))
        {
            return byte.MaxValue;
        }

        if (type == typeof(sbyte))
        {
            return sbyte.MaxValue;
        }

        if (type == typeof(short))
        {
            return short.MaxValue;
        }

        if (type == typeof(ushort))
        {
            return ushort.MaxValue;
        }

        if (type == typeof(int))
        {
            return int.MaxValue;
        }

        if (type == typeof(uint))
        {
            return uint.MaxValue;
        }

        if (type == typeof(long))
        {
            return long.MaxValue;
        }

        if (type == typeof(ulong))
        {
            return 1000000;
        }

        if (type == typeof(float))
        {
            return 1000000;
        }

        if (type == typeof(double))
        {
            return 1000000;
        }

        if (type == typeof(decimal))
        {
            return 1000000;
        }

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
