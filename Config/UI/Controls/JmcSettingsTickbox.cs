using Godot;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens.Settings;

namespace JmcModLib.Config.UI;

internal sealed class JmcSettingsTickbox : NButton
{
    private bool initialValue;
    private Action<bool>? onChanged;
    private bool isTicked;
    private Control? tickedImage;
    private Control? notTickedImage;
    private NSelectionReticle? selectionReticle;

    public static JmcSettingsTickbox Create(NSettingsTickbox template, bool initialValue, Action<bool> onChanged)
    {
        JmcSettingsTickbox tickbox = new()
        {
            Name = "JmcSettingsTickbox",
            initialValue = initialValue,
            onChanged = onChanged
        };
        NativeTemplateCloner.ApplyControlTemplate(template, tickbox);
        tickbox.FocusMode = FocusModeEnum.All;
        tickbox.MouseFilter = MouseFilterEnum.Stop;
        return tickbox;
    }

    public override void _Ready()
    {
        ConnectSignals();
        tickedImage = NativeTemplateCloner.FindDescendantByName<Control>(this, "Ticked");
        notTickedImage = NativeTemplateCloner.FindDescendantByName<Control>(this, "NotTicked");
        selectionReticle = GetNodeOrNull<NSelectionReticle>("SelectionReticle")
            ?? NativeTemplateCloner.FindDescendantByName<NSelectionReticle>(this, "SelectionReticle");
        if (tickedImage == null || notTickedImage == null)
        {
            ModLogger.Warn($"JmcSettingsTickbox template is missing visual nodes. Ticked={tickedImage != null}, NotTicked={notTickedImage != null}.");
        }

        SetValue(initialValue);
    }

    public void SetValue(bool value)
    {
        ApplyValue(value, notify: false);
    }

    protected override void OnRelease()
    {
        base.OnRelease();
        ApplyValue(!isTicked, notify: true);
    }

    protected override void OnFocus()
    {
        base.OnFocus();
        if (NControllerManager.Instance?.IsUsingController == true)
        {
            selectionReticle?.OnSelect();
        }
    }

    protected override void OnUnfocus()
    {
        base.OnUnfocus();
        selectionReticle?.OnDeselect();
    }

    private void ApplyValue(bool value, bool notify)
    {
        isTicked = value;

        SetVisibleIfValid(tickedImage, value);

        SetVisibleIfValid(notTickedImage, !value);

        if (notify)
        {
            onChanged?.Invoke(value);
        }
    }

    private static void SetVisibleIfValid(Control? control, bool visible)
    {
        if (control != null && GodotObject.IsInstanceValid(control))
        {
            control.Visible = visible;
        }
    }
}
