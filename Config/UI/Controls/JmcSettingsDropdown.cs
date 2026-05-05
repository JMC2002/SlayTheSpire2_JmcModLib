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

internal readonly record struct JmcDropdownOption(string Text, string Value);

internal sealed class JmcDropdownItem : NDropdownItem
{
    private string text = string.Empty;
    private string value = string.Empty;

    public string Value => value;

    public static JmcDropdownItem Create(NDropdownItem template, string text, string value)
    {
        JmcDropdownItem item = new()
        {
            Name = "JmcDropdownItem",
            text = text,
            value = value
        };
        NativeTemplateCloner.ApplyControlTemplate(template, item);
        item.FocusMode = FocusModeEnum.All;
        item.MouseFilter = MouseFilterEnum.Stop;
        return item;
    }

    public override void _Ready()
    {
        base._Ready();
        Text = text;
    }
}

internal sealed class JmcSettingsDropdown : NButton
{
    private const int PopupZIndex = 4000;

    private IReadOnlyList<JmcDropdownOption> options = [];
    private string selectedValue = string.Empty;
    private NDropdownItem? itemTemplate;
    private Action<string>? onChanged;
    private bool suppressChanged;
    private bool isOpen;
    private MegaLabel? currentLabel;
    private Control? currentHighlight;
    private Control? dropdownContainer;
    private Control? dropdownItems;
    private NButton? dismisser;
    private NSelectionReticle? selectionReticle;
    private Vector2 dropdownLocalPosition;
    private bool dropdownOriginalTopLevel;
    private int dropdownOriginalZIndex;
    private bool dropdownOriginalZAsRelative;
    private Vector2 dismisserLocalPosition;
    private Vector2 dismisserOriginalSize;
    private bool dismisserOriginalTopLevel;
    private int dismisserOriginalZIndex;
    private bool dismisserOriginalZAsRelative;

    public static JmcSettingsDropdown Create(
        NSettingsDropdown template,
        NDropdownItem itemTemplate,
        IReadOnlyList<string> options,
        string selectedValue,
        Action<string> onChanged)
    {
        return Create(
            template,
            itemTemplate,
            [.. options.Select(static option => new JmcDropdownOption(option, option))],
            selectedValue,
            onChanged);
    }

    public static JmcSettingsDropdown Create(
        NSettingsDropdown template,
        NDropdownItem itemTemplate,
        IReadOnlyList<JmcDropdownOption> options,
        string selectedValue,
        Action<string> onChanged)
    {
        JmcSettingsDropdown dropdown = new()
        {
            Name = "JmcSettingsDropdown",
            options = options,
            selectedValue = selectedValue,
            itemTemplate = itemTemplate,
            onChanged = onChanged
        };
        NativeTemplateCloner.ApplyControlTemplate(template, dropdown);
        dropdown.FocusMode = FocusModeEnum.All;
        dropdown.MouseFilter = MouseFilterEnum.Stop;
        return dropdown;
    }

    public override void _Ready()
    {
        ConnectSignals();
        currentLabel = GetNodeOrNull<MegaLabel>("Label")
            ?? NativeTemplateCloner.FindDescendantByName<MegaLabel>(this, "Label");
        currentHighlight = GetNodeOrNull<Control>("Highlight")
            ?? NativeTemplateCloner.FindDescendantByName<Control>(this, "Highlight");
        dropdownContainer = GetNodeOrNull<Control>("DropdownContainer")
            ?? NativeTemplateCloner.FindDescendantByName<Control>(this, "DropdownContainer");
        dropdownItems = dropdownContainer?.GetNodeOrNull<Control>("VBoxContainer")
            ?? NativeTemplateCloner.FindDescendantByName<Control>(dropdownContainer, "VBoxContainer");
        dismisser = GetNodeOrNull<NButton>("Dismisser")
            ?? NativeTemplateCloner.FindDescendantByName<NButton>(this, "Dismisser");
        selectionReticle = GetNodeOrNull<NSelectionReticle>("SelectionReticle")
            ?? NativeTemplateCloner.FindDescendantByName<NSelectionReticle>(this, "SelectionReticle");

        if (dismisser != null)
        {
            dismisserOriginalTopLevel = dismisser.TopLevel;
            dismisserOriginalZIndex = dismisser.ZIndex;
            dismisserOriginalZAsRelative = dismisser.ZAsRelative;
            dismisserLocalPosition = dismisser.Position;
            dismisserOriginalSize = dismisser.Size;
            dismisser.Connect(NClickableControl.SignalName.Released, Callable.From<NButton>(_ => CloseDropdown()));
            dismisser.Visible = false;
        }

        if (dropdownContainer != null)
        {
            dropdownOriginalTopLevel = dropdownContainer.TopLevel;
            dropdownOriginalZIndex = dropdownContainer.ZIndex;
            dropdownOriginalZAsRelative = dropdownContainer.ZAsRelative;
            dropdownLocalPosition = dropdownContainer.Position;
            dropdownContainer.Visible = false;
        }

        if (currentLabel == null || dropdownContainer == null || dropdownItems == null)
        {
            ModLogger.Warn(
                $"JmcSettingsDropdown template is missing required nodes. Label={currentLabel != null}, Container={dropdownContainer != null}, Items={dropdownItems != null}.");
        }

        PopulateItems();
        SetValue(selectedValue);
    }

    public override void _Process(double delta)
    {
        if (isOpen)
        {
            PositionPopupLayer();
        }
    }

    public void SetValue(string value)
    {
        suppressChanged = true;
        selectedValue = value;
        currentLabel?.SetTextAutoSize(GetDisplayText(value));
        suppressChanged = false;
    }

    private void PopulateItems()
    {
        if (itemTemplate == null || dropdownItems == null)
        {
            return;
        }

        foreach (Node child in dropdownItems.GetChildren())
        {
            dropdownItems.RemoveChild(child);
            child.QueueFree();
        }

        foreach (JmcDropdownOption option in options)
        {
            JmcDropdownItem item = JmcDropdownItem.Create(itemTemplate, option.Text, option.Value);
            dropdownItems.AddChild(item);
            item.Connect(NDropdownItem.SignalName.Selected, Callable.From<NDropdownItem>(OnDropdownItemSelected));
        }

        dropdownItems.GetParent()?.CallDeferred("RefreshLayout");
    }

    protected override void OnRelease()
    {
        base.OnRelease();
        if (isOpen)
        {
            CloseDropdown();
        }
        else
        {
            OpenDropdown();
        }
    }

    protected override void OnFocus()
    {
        base.OnFocus();
        currentHighlight?.Modulate = new Color("3C5B6B");

        if (NControllerManager.Instance?.IsUsingController == true)
        {
            selectionReticle?.OnSelect();
        }
    }

    protected override void OnUnfocus()
    {
        base.OnUnfocus();
        currentHighlight?.Modulate = new Color("2C434F");

        selectionReticle?.OnDeselect();
    }

    private void OpenDropdown()
    {
        if (dropdownContainer == null)
        {
            return;
        }

        dropdownContainer.Visible = true;
        PreparePopupLayer();
        dismisser?.Visible = true;

        isOpen = true;
        GetParent()?.MoveChild(this, GetParent().GetChildCount() - 1);

        if (dropdownItems == null)
        {
            return;
        }

        List<NDropdownItem> items = [.. dropdownItems.GetChildren().OfType<NDropdownItem>()];
        for (int i = 0; i < items.Count; i++)
        {
            items[i].UnhoverSelection();
            items[i].FocusNeighborLeft = items[i].GetPath();
            items[i].FocusNeighborRight = items[i].GetPath();
            items[i].FocusNeighborTop = i > 0 ? items[i - 1].GetPath() : items[i].GetPath();
            items[i].FocusNeighborBottom = i < items.Count - 1 ? items[i + 1].GetPath() : items[i].GetPath();
            items[i].FocusMode = FocusModeEnum.All;
        }

        items.FirstOrDefault()?.TryGrabFocus();
    }

    private void CloseDropdown()
    {
        dismisser?.Visible = false;

        dropdownContainer?.Visible = false;

        RestorePopupLayer();
        isOpen = false;
        this.TryGrabFocus();
    }

    private void PreparePopupLayer()
    {
        if (dropdownContainer != null)
        {
            dropdownContainer.TopLevel = true;
            dropdownContainer.ZAsRelative = false;
            dropdownContainer.ZIndex = PopupZIndex + 10;
            dropdownContainer.MouseFilter = MouseFilterEnum.Stop;
            dropdownContainer.CallDeferred("RefreshLayout");
        }

        if (dismisser != null)
        {
            dismisser.TopLevel = true;
            dismisser.ZAsRelative = false;
            dismisser.ZIndex = PopupZIndex;
            dismisser.MouseFilter = MouseFilterEnum.Stop;

            Viewport? viewport = GetViewport();
            if (viewport != null)
            {
                Rect2 visibleRect = viewport.GetVisibleRect();
                dismisser.GlobalPosition = visibleRect.Position;
                dismisser.Size = visibleRect.Size;
            }
        }

        PositionPopupLayer();
    }

    private void RestorePopupLayer()
    {
        if (dropdownContainer != null)
        {
            dropdownContainer.TopLevel = dropdownOriginalTopLevel;
            dropdownContainer.ZIndex = dropdownOriginalZIndex;
            dropdownContainer.ZAsRelative = dropdownOriginalZAsRelative;
            dropdownContainer.Position = dropdownLocalPosition;
        }

        if (dismisser != null)
        {
            dismisser.TopLevel = dismisserOriginalTopLevel;
            dismisser.ZIndex = dismisserOriginalZIndex;
            dismisser.ZAsRelative = dismisserOriginalZAsRelative;
            dismisser.Position = dismisserLocalPosition;
            dismisser.Size = dismisserOriginalSize;
        }
    }

    private void PositionPopupLayer()
    {
        dropdownContainer?.GlobalPosition = GlobalPosition + dropdownLocalPosition;

        if (dismisser != null)
        {
            Viewport? viewport = GetViewport();
            if (viewport != null)
            {
                Rect2 visibleRect = viewport.GetVisibleRect();
                dismisser.GlobalPosition = visibleRect.Position;
                dismisser.Size = visibleRect.Size;
            }
        }
    }

    private void OnDropdownItemSelected(NDropdownItem dropdownItem)
    {
        string value = dropdownItem is JmcDropdownItem item ? item.Value : dropdownItem.Text;
        SetValue(value);
        CloseDropdown();
        if (!suppressChanged)
        {
            onChanged?.Invoke(value);
        }
    }

    private string GetDisplayText(string value)
    {
        return options.FirstOrDefault(option =>
            string.Equals(option.Value, value, StringComparison.OrdinalIgnoreCase)) is { Text: { Length: > 0 } text }
            ? text
            : value;
    }
}
