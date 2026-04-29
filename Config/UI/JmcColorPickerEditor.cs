using Godot;
using JmcModLib.Config;

namespace JmcModLib.Config.UI;

internal sealed class JmcColorPickerEditor : HBoxContainer
{
    private const float PreviewSize = 44f;
    private const float SwatchSize = 34f;
    private const float SelectButtonWidth = 150f;
    private const float BasePopupWidth = 680f;
    private const float BasePopupHeight = 620f;
    private const float BaseViewportHeight = 1080f;

    private UIColorAttribute attribute = new();
    private Action<Color>? onChanged;
    private Color value = Colors.White;
    private ColorRect? preview;
    private Label? hexLabel;
    private Button? selectButton;

    public Control? PrimaryFocusableControl => selectButton;

    public static JmcColorPickerEditor Create(Color value, UIColorAttribute attribute, Action<Color> onChanged)
    {
        var editor = new JmcColorPickerEditor
        {
            Name = "JmcColorPickerEditor",
            attribute = attribute,
            onChanged = onChanged,
            value = NormalizeAlpha(value, attribute.AllowAlpha),
            SizeFlagsHorizontal = SizeFlags.ShrinkEnd,
            SizeFlagsVertical = SizeFlags.ShrinkCenter
        };

        editor.Build();
        return editor;
    }

    public void SetValue(Color color)
    {
        value = NormalizeAlpha(color, attribute.AllowAlpha);
        UpdateDisplay();
    }

    private void Build()
    {
        AddThemeConstantOverride("separation", 8);

        preview = new ColorRect
        {
            CustomMinimumSize = new Vector2(PreviewSize, PreviewSize),
            SizeFlagsVertical = SizeFlags.ShrinkCenter
        };
        AddChild(preview);

        hexLabel = new Label
        {
            CustomMinimumSize = new Vector2(110f, 0f),
            VerticalAlignment = VerticalAlignment.Center
        };
        AddChild(hexLabel);

        foreach (Color preset in ResolvePresetColors(attribute).Take(8))
        {
            AddChild(CreateSwatchButton(preset));
        }

        if (attribute.AllowCustom)
        {
            selectButton = new Button
            {
                Text = ModSettingsText.ColorSelect(),
                FocusMode = FocusModeEnum.All,
                CustomMinimumSize = new Vector2(SelectButtonWidth, 44f),
                SizeFlagsVertical = SizeFlags.ShrinkCenter
            };
            selectButton.Pressed += OpenColorPicker;
            AddChild(selectButton);
        }

        UpdateDisplay();
    }

    private Button CreateSwatchButton(Color color)
    {
        var button = new Button
        {
            Text = string.Empty,
            FocusMode = FocusModeEnum.All,
            CustomMinimumSize = new Vector2(SwatchSize, SwatchSize),
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
            TooltipText = JmcColorValue.ToHex(color, attribute.AllowAlpha)
        };
        button.Modulate = NormalizeAlpha(color, attribute.AllowAlpha);
        button.Pressed += () => ApplyValue(color);
        return button;
    }

    private void OpenColorPicker()
    {
        SceneTree? tree = GetTree();
        if (tree?.Root == null)
        {
            return;
        }

        var popup = new PopupPanel
        {
            Name = "JmcColorPickerPopup",
            Exclusive = true
        };

        Viewport viewport = GetViewport();
        PopupMetrics metrics = ResolvePopupMetrics(viewport);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", metrics.Margin);
        margin.AddThemeConstantOverride("margin_top", metrics.Margin);
        margin.AddThemeConstantOverride("margin_right", metrics.Margin);
        margin.AddThemeConstantOverride("margin_bottom", metrics.Margin);
        popup.AddChild(margin);

        var content = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        content.AddThemeConstantOverride("separation", metrics.Spacing);
        margin.AddChild(content);

        var picker = new ColorPicker
        {
            Color = value,
            EditAlpha = attribute.AllowAlpha,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            CustomMinimumSize = metrics.PickerMinimumSize
        };
        picker.ColorChanged += ApplyValue;
        content.AddChild(picker);

        var closeButton = new Button
        {
            Text = ModSettingsText.Close(),
            FocusMode = FocusModeEnum.All,
            CustomMinimumSize = metrics.CloseButtonSize,
            SizeFlagsHorizontal = SizeFlags.ShrinkEnd
        };
        closeButton.Pressed += popup.QueueFree;
        content.AddChild(closeButton);

        tree.Root.AddChild(popup);
        popup.PopupCentered(metrics.PopupSize);
    }

    private void ApplyValue(Color color)
    {
        value = NormalizeAlpha(color, attribute.AllowAlpha);
        UpdateDisplay();
        onChanged?.Invoke(value);
    }

    private void UpdateDisplay()
    {
        if (preview != null)
        {
            preview.Color = value;
        }

        if (hexLabel != null)
        {
            hexLabel.Text = JmcColorValue.ToHex(value, attribute.AllowAlpha);
        }
    }

    private static IReadOnlyList<Color> ResolvePresetColors(UIColorAttribute attribute)
    {
        List<Color> colors = [];
        foreach (string preset in attribute.Presets)
        {
            if (!string.IsNullOrWhiteSpace(preset))
            {
                colors.Add(JmcColorValue.Parse(preset));
            }
        }

        colors.AddRange(attribute.Palette switch
        {
            UIColorPalette.Basic => BasicPalette(),
            UIColorPalette.CardRarity => CardRarityPalette(),
            UIColorPalette.Rainbow => RainbowPalette(),
            UIColorPalette.Game => GamePalette(),
            _ => []
        });

        return [.. colors.DistinctBy(color => JmcColorValue.ToHex(color, includeAlpha: true))];
    }

    private static Color NormalizeAlpha(Color color, bool allowAlpha)
    {
        return allowAlpha ? color : new Color(color.R, color.G, color.B, 1f);
    }

    private static PopupMetrics ResolvePopupMetrics(Viewport viewport)
    {
        Vector2 viewportSize = viewport.GetVisibleRect().Size;
        float scale = Math.Clamp(viewportSize.Y / BaseViewportHeight, 0.8f, 1.8f);

        int width = ClampDimension(
            BasePopupWidth * scale,
            min: 520f,
            max: MathF.Max(360f, viewportSize.X * 0.86f));
        int height = ClampDimension(
            BasePopupHeight * scale,
            min: 440f,
            max: MathF.Max(320f, viewportSize.Y * 0.84f));

        int margin = Math.Max(12, (int)MathF.Round(18f * scale));
        int spacing = Math.Max(8, (int)MathF.Round(12f * scale));
        float closeButtonWidth = MathF.Round(180f * scale);
        float closeButtonHeight = MathF.Round(44f * scale);
        Vector2 pickerMinimumSize = new(
            Math.Max(300f, width - margin * 2f),
            Math.Max(240f, height - margin * 2f - closeButtonHeight - spacing));

        return new PopupMetrics(
            new Vector2I(width, height),
            margin,
            spacing,
            pickerMinimumSize,
            new Vector2(closeButtonWidth, closeButtonHeight));
    }

    private static int ClampDimension(float preferred, float min, float max)
    {
        float effectiveMin = Math.Min(min, max);
        return (int)MathF.Round(Math.Clamp(preferred, effectiveMin, max));
    }

    private readonly record struct PopupMetrics(
        Vector2I PopupSize,
        int Margin,
        int Spacing,
        Vector2 PickerMinimumSize,
        Vector2 CloseButtonSize);

    private static Color[] BasicPalette()
    {
        return
        [
            Colors.White,
            Colors.Black,
            new Color("808080"),
            new Color("FF0000"),
            new Color("00FF00"),
            new Color("0000FF")
        ];
    }

    private static Color[] GamePalette()
    {
        return
        [
            new Color("E0B24F"),
            new Color("65A83A"),
            new Color("B94A3F"),
            new Color("3C6F8F"),
            new Color("D0D8DC"),
            new Color("AAB7BC")
        ];
    }

    private static Color[] CardRarityPalette()
    {
        return
        [
            new Color("BFC7C9"),
            new Color("66A9D6"),
            new Color("D69A35"),
            new Color("B46EE0")
        ];
    }

    private static Color[] RainbowPalette()
    {
        return
        [
            new Color("E24A4A"),
            new Color("E08A3E"),
            new Color("E0B24F"),
            new Color("65A83A"),
            new Color("3C9A8F"),
            new Color("3C6F8F"),
            new Color("8A63D2")
        ];
    }
}
