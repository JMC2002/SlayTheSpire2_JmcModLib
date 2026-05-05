using Godot;
using JmcModLib.Config.Entry;
using MegaCrit.Sts2.addons.mega_text;

namespace JmcModLib.Config.UI;

internal sealed partial class ModSettingsPanel
{
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
        if (!IsGodotObjectValid(this))
        {
            return;
        }

        RefreshStaticText();

        if (Visible && IsGodotObjectValid(listRoot))
        {
            RebuildContent();
        }
    }

    private void RefreshStaticText()
    {
        MegaRichTextLabel? currentTitle = titleLabel;
        if (currentTitle != null && GodotObject.IsInstanceValid(currentTitle))
        {
            currentTitle.Text = $"[gold]{ModSettingsText.Title()}[/gold]";
        }
        else
        {
            titleLabel = null;
        }

        MegaRichTextLabel? currentDescription = descriptionLabel;
        if (currentDescription != null && GodotObject.IsInstanceValid(currentDescription))
        {
            currentDescription.Text = $"[color=#aab7bc]{ModSettingsText.Description()}[/color]";
        }
        else
        {
            descriptionLabel = null;
        }
    }
}
