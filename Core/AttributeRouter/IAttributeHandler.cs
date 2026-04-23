using System.Reflection;
using JmcModLib.Reflection;

namespace JmcModLib.Core.AttributeRouter;

/// <summary>
/// Handles a discovered attribute on a reflected accessor.
/// </summary>
public interface IAttributeHandler
{
    void Handle(Assembly assembly, ReflectionAccessorBase accessor, Attribute attribute);

    Action<Assembly, IReadOnlyList<ReflectionAccessorBase>>? Unregister { get; }
}
