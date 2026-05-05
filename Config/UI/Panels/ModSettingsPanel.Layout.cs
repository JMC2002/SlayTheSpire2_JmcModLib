using Godot;
using MegaCrit.Sts2.addons.mega_text;

namespace JmcModLib.Config.UI;

internal sealed partial class ModSettingsPanel
{
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
        Size = new Vector2(width, MathF.Max(minimumSize.Y, 1f));
        Position = new Vector2(Mathf.Max((parentSize.X - Size.X) * 0.5f, 0f), Position.Y);
    }

    private void RefreshPanelSizeAfterLayout()
    {
        RefreshPanelSize();
        Callable.From(RefreshPanelSize).CallDeferred();
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
}
