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

internal sealed class JmcSettingsButton : NSettingsButton
{
    private string text = string.Empty;
    private Action? onPressed;
    private UIButtonColor color;
    private bool hideImage;

    public static JmcSettingsButton Create(
        Control template,
        string text,
        Action onPressed,
        UIButtonColor color = UIButtonColor.Default,
        bool hideImage = false)
    {
        JmcSettingsButton button = new()
        {
            Name = "JmcSettingsButton",
            text = text,
            onPressed = onPressed,
            color = color,
            hideImage = hideImage
        };
        NativeTemplateCloner.ApplyControlTemplate(template, button);
        return button;
    }

    public override void _Ready()
    {
        ConnectSignals();
        GetNodeOrNull<MegaLabel>("Label")?.SetTextAutoSize(text);
        GetNodeOrNull<MegaRichTextLabel>("Label")?.SetTextAutoSize(text);

        Control? image = GetNodeOrNull<Control>("Image");
        image?.Visible = !hideImage;
        ApplyColor(image);

        Connect(NClickableControl.SignalName.Released, Callable.From<NButton>(_ => onPressed?.Invoke()));
    }

    private void ApplyColor(Control? image)
    {
        if (!JmcButtonColor.TryGetTint(color, out Color tint))
        {
            return;
        }

        Control? target = image ?? NativeTemplateCloner.FindDescendantByName<Control>(this, "Image");
        if (target == null)
        {
            Modulate = tint;
            return;
        }

        target.Modulate = tint;
        target.SelfModulate = tint;
    }
}
