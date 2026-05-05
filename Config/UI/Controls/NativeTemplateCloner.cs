using Godot;

namespace JmcModLib.Config.UI;

internal static class NativeTemplateCloner
{
    public static T? FindDescendantByName<T>(Node? root, string name) where T : Node
    {
        if (root == null)
        {
            return null;
        }

        if (root.Name == name && root is T match)
        {
            return match;
        }

        foreach (Node child in root.GetChildren())
        {
            T? nested = FindDescendantByName<T>(child, name);
            if (nested != null)
            {
                return nested;
            }
        }

        return null;
    }

    public static void ApplyControlTemplate(Control source, Control target)
    {
        target.FocusMode = source.FocusMode;
        target.CustomMinimumSize = source.CustomMinimumSize;
        target.SizeFlagsHorizontal = source.SizeFlagsHorizontal;
        target.SizeFlagsVertical = source.SizeFlagsVertical;
        target.GrowHorizontal = source.GrowHorizontal;
        target.GrowVertical = source.GrowVertical;
        target.MouseFilter = source.MouseFilter;
        target.PivotOffset = source.PivotOffset;
        target.Scale = source.Scale;
        target.Rotation = source.Rotation;
        target.Modulate = source.Modulate;
        target.SelfModulate = source.SelfModulate;
        target.Theme = source.Theme;
        target.ThemeTypeVariation = source.ThemeTypeVariation;

        foreach (Node child in source.GetChildren())
        {
            target.AddChild(child.Duplicate());
        }
    }
}
