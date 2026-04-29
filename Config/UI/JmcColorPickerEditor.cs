using Godot;
using JmcModLib.Config;
using JmcModLib.Config.Serialization;

namespace JmcModLib.Config.UI;

internal sealed class JmcColorPickerEditor : HBoxContainer
{
    private const float PreviewSize = 44f;
    private const float SwatchSize = 34f;
    private const float SelectButtonWidth = 150f;
    private const float BasePopupWidth = 680f;
    private const float BasePopupHeight = 620f;
    private const float BaseViewportHeight = 1080f;
    private const int InlineFontSize = 24;
    private const int BaseControlFontSize = 24;
    private const string PopupFontHookMetaKey = "JmcModLib_ColorPicker_FontHooked";

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
        ApplyFontSize(hexLabel, InlineFontSize);
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
            ApplyFontSize(selectButton, InlineFontSize);
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

        popup.Ready += () => ApplyFontSizeRecursive(popup, metrics.FontSize, metrics.ControlMinimumHeight);
        tree.Root.AddChild(popup);
        int styledControls = ApplyFontSizeRecursive(popup, metrics.FontSize, metrics.ControlMinimumHeight);
        Callable.From(() => ApplyFontSizeRecursive(popup, metrics.FontSize, metrics.ControlMinimumHeight)).CallDeferred();
        popup.MinSize = metrics.PopupSize;
        popup.Size = metrics.PopupSize;
        popup.PopupCentered(metrics.PopupSize);
        ModLogger.Info(
            $"JmcColorPicker popup opened. Viewport={metrics.ViewportSize} Window={metrics.WindowSize} Screen={metrics.ScreenSize} Dpi={metrics.ScreenDpi} Popup={metrics.PopupSize} Font={metrics.FontSize} Scale={metrics.LayoutScale:F2} StyledControls={styledControls}");
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
        Vector2I windowSize = DisplayServer.WindowGetSize();
        Vector2I screenSize = GetCurrentScreenSize(viewportSize, windowSize);
        int screenDpi = GetCurrentScreenDpi();
        int fontSize = ResolveDpiAwareControlFontSize(screenSize, viewportSize, windowSize);
        float layoutScale = Math.Clamp(fontSize / (float)BaseControlFontSize, 1.0f, 1.75f);
        float viewportScale = Math.Clamp(viewportSize.Y / BaseViewportHeight, 0.85f, 1.25f);
        float scale = MathF.Max(viewportScale, layoutScale);

        int width = ClampDimension(
            MathF.Max(BasePopupWidth * scale, viewportSize.X * 0.78f),
            min: 720f,
            max: MathF.Max(360f, viewportSize.X * 0.92f));
        int height = ClampDimension(
            MathF.Max(BasePopupHeight * scale, viewportSize.Y * 0.82f),
            min: 620f,
            max: MathF.Max(320f, viewportSize.Y * 0.94f));
        var popupSize = new Vector2I(width, height);
        fontSize = ResolveDpiAwareControlFontSize(screenSize, viewportSize, windowSize, popupSize);
        layoutScale = Math.Clamp(fontSize / (float)BaseControlFontSize, 1.0f, 1.75f);

        int margin = Math.Max(18, (int)MathF.Round(18f * layoutScale));
        int spacing = Math.Max(12, (int)MathF.Round(12f * layoutScale));
        float closeButtonWidth = MathF.Round(180f * layoutScale);
        float closeButtonHeight = MathF.Round(56f * layoutScale);
        float controlMinimumHeight = MathF.Round(MathF.Max(44f * layoutScale, fontSize * 1.85f));
        Vector2 pickerMinimumSize = new(
            Math.Max(300f, width - margin * 2f),
            Math.Max(240f, height - margin * 2f - closeButtonHeight - spacing));

        return new PopupMetrics(
            popupSize,
            viewportSize,
            windowSize,
            screenSize,
            screenDpi,
            layoutScale,
            margin,
            spacing,
            pickerMinimumSize,
            new Vector2(closeButtonWidth, closeButtonHeight),
            fontSize,
            controlMinimumHeight);
    }

    private static int ApplyFontSizeRecursive(Node node, int fontSize, float controlMinimumHeight)
    {
        return ApplyFontSizeRecursive(node, fontSize, controlMinimumHeight, []);
    }

    private static int ApplyFontSizeRecursive(
        Node node,
        int fontSize,
        float controlMinimumHeight,
        HashSet<ulong> visited)
    {
        if (!visited.Add(node.GetInstanceId()))
        {
            return 0;
        }

        int styledControls = 0;
        if (node is Window window)
        {
            ApplyFontSize(window, fontSize);
            ApplyPopupMetrics(window, fontSize, controlMinimumHeight);
            HookPopupFontRefresh(window, fontSize, controlMinimumHeight);
            styledControls++;
        }

        if (node is Control control)
        {
            ApplyFontSize(control, fontSize);
            styledControls += ApplyOwnedPopupFontSize(control, fontSize, controlMinimumHeight, visited);
            styledControls++;
            ApplyControlMetrics(control, controlMinimumHeight);
        }

        foreach (Node child in node.GetChildren(includeInternal: true))
        {
            styledControls += ApplyFontSizeRecursive(child, fontSize, controlMinimumHeight, visited);
        }

        return styledControls;
    }

    private static void ApplyControlMetrics(Control control, float controlMinimumHeight)
    {
        switch (control)
        {
            case Label or Button or LineEdit or SpinBox or OptionButton or CheckBox or CheckButton:
                control.CustomMinimumSize = new Vector2(
                    control.CustomMinimumSize.X,
                    MathF.Max(control.CustomMinimumSize.Y, controlMinimumHeight));
                break;
            case HSlider:
                control.CustomMinimumSize = new Vector2(
                    control.CustomMinimumSize.X,
                    MathF.Max(control.CustomMinimumSize.Y, controlMinimumHeight * 0.70f));
                break;
            case VSlider:
                control.CustomMinimumSize = new Vector2(
                    MathF.Max(control.CustomMinimumSize.X, controlMinimumHeight * 0.70f),
                    control.CustomMinimumSize.Y);
                break;
        }
    }

    private static int ApplyOwnedPopupFontSize(
        Control control,
        int fontSize,
        float controlMinimumHeight,
        HashSet<ulong> visited)
    {
        return control switch
        {
            OptionButton optionButton => ApplyFontSizeRecursive(optionButton.GetPopup(), fontSize, controlMinimumHeight, visited),
            MenuButton menuButton => ApplyFontSizeRecursive(menuButton.GetPopup(), fontSize, controlMinimumHeight, visited),
            _ => 0
        };
    }

    private static void ApplyFontSize(Control control, int fontSize)
    {
        control.AddThemeFontSizeOverride("font_size", fontSize);
        control.AddThemeFontSizeOverride("normal_font_size", fontSize);
        control.AddThemeFontSizeOverride("bold_font_size", fontSize);
        control.AddThemeFontSizeOverride("italics_font_size", fontSize);
        control.AddThemeFontSizeOverride("bold_italics_font_size", fontSize);
        control.AddThemeFontSizeOverride("mono_font_size", fontSize);
    }

    private static void ApplyFontSize(Window window, int fontSize)
    {
        window.AddThemeFontSizeOverride("font_size", fontSize);
        window.AddThemeFontSizeOverride("normal_font_size", fontSize);
        window.AddThemeFontSizeOverride("bold_font_size", fontSize);
        window.AddThemeFontSizeOverride("italics_font_size", fontSize);
        window.AddThemeFontSizeOverride("bold_italics_font_size", fontSize);
        window.AddThemeFontSizeOverride("mono_font_size", fontSize);
    }

    private static void ApplyPopupMetrics(Window window, int fontSize, float controlMinimumHeight)
    {
        int separation = Math.Max(10, (int)MathF.Round(fontSize * 0.42f));
        int padding = Math.Max(18, (int)MathF.Round(fontSize * 0.70f));

        window.AddThemeConstantOverride("v_separation", separation);
        window.AddThemeConstantOverride("h_separation", separation);
        window.AddThemeConstantOverride("item_start_padding", padding);
        window.AddThemeConstantOverride("item_end_padding", padding);

        if (window is PopupMenu popupMenu)
        {
            popupMenu.AddThemeConstantOverride("v_separation", separation);
            popupMenu.AddThemeConstantOverride("h_separation", separation);
            popupMenu.AddThemeConstantOverride("item_start_padding", padding);
            popupMenu.AddThemeConstantOverride("item_end_padding", padding);
            popupMenu.MinSize = new Vector2I(
                Math.Max(popupMenu.MinSize.X, (int)MathF.Round(fontSize * 8f)),
                Math.Max(popupMenu.MinSize.Y, (int)MathF.Round(controlMinimumHeight * 1.10f)));

            for (int index = 0; index < popupMenu.ItemCount; index++)
            {
                PopupMenu? submenu = popupMenu.GetItemSubmenuNode(index);
                if (submenu != null)
                {
                    ApplyFontSizeRecursive(submenu, fontSize, controlMinimumHeight);
                }
            }
        }
    }

    private static void HookPopupFontRefresh(Window window, int fontSize, float controlMinimumHeight)
    {
        if (window.HasMeta(PopupFontHookMetaKey))
        {
            return;
        }

        window.SetMeta(PopupFontHookMetaKey, true);
        window.AboutToPopup += () =>
        {
            ApplyFontSizeRecursive(window, fontSize, controlMinimumHeight);
            Callable.From(() => ApplyFontSizeRecursive(window, fontSize, controlMinimumHeight)).CallDeferred();
        };
    }

    private static int ResolveDpiAwareControlFontSize(
        Vector2I screenSize,
        Vector2 viewportSize,
        Vector2I windowSize,
        Vector2I popupSize = default)
    {
        float screenBase = screenSize.Y > 0 ? screenSize.Y / 84f : 0f;
        float popupBase = popupSize.Y > 0 ? popupSize.Y / 42f : 0f;
        float windowBase = windowSize.Y > 0 ? MathF.Min(windowSize.Y / 96f, 42f) : 0f;
        float viewportBase = viewportSize.Y > 0 ? viewportSize.Y / 45f : 0f;
        float baseSize = MathF.Max(16f, MathF.Max(screenBase, MathF.Max(popupBase, MathF.Max(windowBase, viewportBase))));
        int logFontSize = Math.Clamp(
            (int)MathF.Round(baseSize * GetGentleDpiFactor()),
            18,
            42);

        return Math.Clamp(Math.Max(BaseControlFontSize, logFontSize - 2), BaseControlFontSize, 38);
    }

    private static int GetCurrentScreenDpi()
    {
        try
        {
            return DisplayServer.ScreenGetDpi(GetCurrentScreen());
        }
        catch
        {
            return 96;
        }
    }

    private static Vector2I GetCurrentScreenSize(Vector2 viewportSize, Vector2I windowSize)
    {
        try
        {
            Vector2I size = DisplayServer.ScreenGetSize(GetCurrentScreen());
            if (size.X > 0 && size.Y > 0)
            {
                return size;
            }
        }
        catch
        {
        }

        if (windowSize.X > 0 && windowSize.Y > 0)
        {
            return windowSize;
        }

        return new Vector2I((int)MathF.Round(viewportSize.X), (int)MathF.Round(viewportSize.Y));
    }

    private static int GetCurrentScreen()
    {
        try
        {
            int screen = DisplayServer.WindowGetCurrentScreen(0);
            if (screen >= 0)
            {
                return screen;
            }
        }
        catch
        {
        }

        try
        {
            return DisplayServer.GetPrimaryScreen();
        }
        catch
        {
            return 0;
        }
    }

    private static float GetGentleDpiFactor()
    {
        int dpi = GetCurrentScreenDpi();
        if (dpi <= 96)
        {
            return 1.0f;
        }

        float dpiScale = Math.Clamp(dpi / 96f, 1.0f, 4.0f);
        return 1.0f + (MathF.Sqrt(dpiScale) - 1.0f) * 0.35f;
    }

    private static int ClampDimension(float preferred, float min, float max)
    {
        float effectiveMin = Math.Min(min, max);
        return (int)MathF.Round(Math.Clamp(preferred, effectiveMin, max));
    }

    private readonly record struct PopupMetrics(
        Vector2I PopupSize,
        Vector2 ViewportSize,
        Vector2I WindowSize,
        Vector2I ScreenSize,
        int ScreenDpi,
        float LayoutScale,
        int Margin,
        int Spacing,
        Vector2 PickerMinimumSize,
        Vector2 CloseButtonSize,
        int FontSize,
        float ControlMinimumHeight);

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
