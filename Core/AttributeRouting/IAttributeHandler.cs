// 文件用途：定义 Attribute 扫描处理器接口，供配置、按钮、热键等模块接入扫描流程。
using JmcModLib.Reflection;
using System.Reflection;

namespace JmcModLib.Core.AttributeRouter;

/// <summary>
/// Handles a discovered attribute on a reflected accessor.
/// </summary>
public interface IAttributeHandler
{
    void Handle(Assembly assembly, ReflectionAccessorBase accessor, Attribute attribute);

    Action<Assembly, IReadOnlyList<ReflectionAccessorBase>>? Unregister { get; }
}
