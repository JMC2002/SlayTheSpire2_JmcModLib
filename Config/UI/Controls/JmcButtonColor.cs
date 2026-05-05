using Godot;

namespace JmcModLib.Config.UI;

internal static class JmcButtonColor
{
    public static bool TryGetTint(UIButtonColor color, out Color tint)
    {
        tint = color switch
        {
            UIButtonColor.Green => new Color("65A83A"),
            UIButtonColor.Red => new Color("B94A3F"),
            UIButtonColor.Gold => new Color("D69A35"),
            UIButtonColor.Blue => new Color("3C6F8F"),
            _ => Colors.White
        };

        return color is UIButtonColor.Green
            or UIButtonColor.Red
            or UIButtonColor.Gold
            or UIButtonColor.Blue;
    }
}
