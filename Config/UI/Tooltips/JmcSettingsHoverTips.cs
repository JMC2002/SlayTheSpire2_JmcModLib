using Godot;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.Core.Nodes.Screens.Settings;
using System.Reflection;

namespace JmcModLib.Config.UI;

internal static class JmcSettingsHoverTips
{
    private static readonly PropertyInfo? TitleProperty =
        typeof(HoverTip).GetProperty(nameof(HoverTip.Title), BindingFlags.Instance | BindingFlags.Public);

    private static readonly PropertyInfo? IdProperty =
        typeof(HoverTip).GetProperty(nameof(HoverTip.Id), BindingFlags.Instance | BindingFlags.Public);

    public static void Attach(Control owner, string title, string description)
    {
        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(description))
        {
            return;
        }

        if (owner.MouseFilter == Control.MouseFilterEnum.Ignore)
        {
            owner.MouseFilter = Control.MouseFilterEnum.Pass;
        }

        HoverTip tip = Create(title.Trim(), description.Trim());
        owner.Connect(Control.SignalName.MouseEntered, Callable.From(() => Show(owner, tip)));
        owner.Connect(Control.SignalName.MouseExited, Callable.From(() => Hide(owner)));
        owner.Connect(Control.SignalName.FocusEntered, Callable.From(() => Show(owner, tip)));
        owner.Connect(Control.SignalName.FocusExited, Callable.From(() => Hide(owner)));
        owner.Connect(Node.SignalName.TreeExiting, Callable.From(() => Hide(owner)));
    }

    private static HoverTip Create(string title, string description)
    {
        var tip = new HoverTip(new LocString("settings_ui", "FASTMODE"), description)
        {
            Id = $"JmcModLib.Settings.{title}.{description.GetHashCode(StringComparison.Ordinal)}"
        };

        SetPrivateProperty(ref tip, TitleProperty, title);
        SetPrivateProperty(ref tip, IdProperty, tip.Id);
        return tip;
    }

    private static void Show(Control owner, HoverTip tip)
    {
        try
        {
            NHoverTipSet.Remove(owner);
            NHoverTipSet hoverTipSet = NHoverTipSet.CreateAndShow(owner, tip);
            hoverTipSet.GlobalPosition = owner.GlobalPosition + NSettingsScreen.settingTipsOffset;
        }
        catch (Exception ex)
        {
            ModLogger.Warn("Failed to show native settings hover tip.", ex);
        }
    }

    private static void Hide(Control owner)
    {
        try
        {
            NHoverTipSet.Remove(owner);
        }
        catch
        {
            // The native hover-tip container may already be gone during scene teardown.
        }
    }

    private static void SetPrivateProperty<TValue>(ref TValue value, PropertyInfo? property, object? propertyValue)
        where TValue : struct
    {
        if (property?.SetMethod == null)
        {
            return;
        }

        object boxed = value;
        property.SetValue(boxed, propertyValue);
        value = (TValue)boxed;
    }
}
