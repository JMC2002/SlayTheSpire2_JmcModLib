using Godot;

namespace JmcModLib.Utils;

/// <summary>
/// Small Godot node helpers that replace the old Unity component helpers.
/// </summary>
public static class ComponentHelper
{
    public static bool TryAddChild<T>(
        Node parent,
        out T child,
        string? nodeName = null,
        Action<T>? initialize = null)
        where T : Node, new()
    {
        ArgumentNullException.ThrowIfNull(parent);

        string resolvedName = string.IsNullOrWhiteSpace(nodeName) ? typeof(T).Name : nodeName;
        child = parent.GetNodeOrNull<T>(new NodePath(resolvedName));
        if (child != null)
        {
            return false;
        }

        child = new T
        {
            Name = resolvedName
        };
        parent.AddChild(child);
        initialize?.Invoke(child);
        return true;
    }

    public static T GetOrAddChild<T>(
        Node parent,
        string? nodeName = null,
        Action<T>? initialize = null)
        where T : Node, new()
    {
        TryAddChild(parent, out T child, nodeName, initialize);
        return child;
    }

    public static T RequireChild<T>(Node parent, string nodePath) where T : Node
    {
        ArgumentNullException.ThrowIfNull(parent);
        ArgumentException.ThrowIfNullOrWhiteSpace(nodePath);

        return parent.GetNodeOrNull<T>(new NodePath(nodePath))
            ?? throw new InvalidOperationException(
                $"Could not find child node '{nodePath}' under '{parent.Name}'.");
    }

    public static T? FindChildOfType<T>(Node parent, bool recursive = true) where T : Node
    {
        ArgumentNullException.ThrowIfNull(parent);

        foreach (Node child in parent.GetChildren())
        {
            if (child is T typedChild)
            {
                return typedChild;
            }

            if (recursive)
            {
                T? nested = FindChildOfType<T>(child, recursive: true);
                if (nested != null)
                {
                    return nested;
                }
            }
        }

        return null;
    }
}
