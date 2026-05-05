// 文件用途：提供注册后的链式配置入口，用于补充 MOD 元数据和手动配置 UI 项。
using JmcModLib.Config;
using JmcModLib.Config.Storage;
using JmcModLib.Config.UI;
using System.Reflection;

namespace JmcModLib.Core;

/// <summary>
/// 表示一次 MOD 注册过程中的链式补充设置。
/// </summary>
/// <remarks>
/// <para>
/// <see cref="ModRegistry.Register(string, string?, string?, Assembly?)"/> 会自动启用默认日志、
/// 配置管理和 Attribute 扫描服务；此构建器只负责补充显示名、版本、自定义存储和手动按钮等注册期信息。
/// 所有补充设置完成后必须调用 <see cref="Done"/>，否则 Attribute 标记的配置、按钮和热键不会被扫描。
/// </para>
/// <example>
/// <code><![CDATA[
/// ModRegistry.Register(true, VersionInfo.Name, VersionInfo.Name, VersionInfo.Version)?
///     .RegisterButton("重载配置", ReloadConfig, "重载")
///     .Done();
/// ]]></code>
/// </example>
/// </remarks>
public sealed class RegistryBuilder
{
    private readonly Assembly assembly;
    private bool completed;

    internal RegistryBuilder(Assembly assembly)
    {
        this.assembly = assembly;
    }

    /// <summary>
    /// 覆盖当前注册上下文中的显示名称。
    /// </summary>
    /// <param name="displayName">新的显示名称；空白值会被忽略。</param>
    /// <returns>当前构建器，用于继续链式调用。</returns>
    public RegistryBuilder WithDisplayName(string displayName)
    {
        ModRegistry.UpdateDisplayName(assembly, displayName);
        return this;
    }

    /// <summary>
    /// 覆盖当前注册上下文中的版本号。
    /// </summary>
    /// <param name="version">新的版本号；空白值会被忽略。</param>
    /// <returns>当前构建器，用于继续链式调用。</returns>
    public RegistryBuilder WithVersion(string version)
    {
        ModRegistry.UpdateVersion(assembly, version);
        return this;
    }

    /// <summary>
    /// 为当前 MOD 设置自定义配置存储。
    /// </summary>
    /// <param name="storage">配置存储实现。</param>
    /// <returns>当前构建器，用于继续链式调用。</returns>
    /// <remarks>
    /// 需要在 <see cref="Done"/> 之前调用，确保 Attribute 扫描创建配置项时能读取到自定义存储。
    /// </remarks>
    /// <example>
    /// <code><![CDATA[
    /// ModRegistry.Register(true, VersionInfo.Name)?
    ///     .WithConfigStorage(new NewtonsoftConfigStorage(customRoot))
    ///     .Done();
    /// ]]></code>
    /// </example>
    public RegistryBuilder WithConfigStorage(IConfigStorage storage)
    {
        ConfigManager.SetStorage(storage, assembly);
        return this;
    }

    /// <summary>
    /// 手动向当前 MOD 的设置界面注册一个按钮项，并返回生成的配置键。
    /// </summary>
    /// <param name="key">输出生成的配置键，可用于后续查询。</param>
    /// <param name="description">按钮项的显示名称或描述文本。</param>
    /// <param name="action">点击按钮时执行的动作。</param>
    /// <param name="buttonText">按钮上的文本。</param>
    /// <param name="group">按钮所在设置分组。</param>
    /// <param name="storageKey">持久化键；留空时由描述文本生成。</param>
    /// <param name="helpText">悬停提示或帮助文本。</param>
    /// <param name="locTable">本地化表名；留空时使用默认设置界面表。</param>
    /// <param name="displayNameKey">显示名称本地化键。</param>
    /// <param name="helpTextKey">帮助文本本地化键。</param>
    /// <param name="buttonTextKey">按钮文本本地化键。</param>
    /// <param name="groupKey">分组名称本地化键。</param>
    /// <param name="order">同组内排序值，越小越靠前。</param>
    /// <param name="color">按钮颜色风格。</param>
    /// <returns>当前构建器，用于继续链式调用。</returns>
    /// <example>
    /// <code><![CDATA[
    /// ModRegistry.Register(true, VersionInfo.Name)?
    ///     .RegisterButton(
    ///         out string reloadKey,
    ///         "重载配置",
    ///         ReloadConfig,
    ///         "重载",
    ///         group: "调试",
    ///         storageKey: "button.reload")
    ///     .Done();
    /// ]]></code>
    /// </example>
    public RegistryBuilder RegisterButton(
        out string key,
        string description,
        Action action,
        string buttonText = "按钮",
        string group = ConfigAttribute.DefaultGroup,
        string? storageKey = null,
        string? helpText = null,
        string? locTable = null,
        string? displayNameKey = null,
        string? helpTextKey = null,
        string? buttonTextKey = null,
        string? groupKey = null,
        int order = 0,
        UIButtonColor color = UIButtonColor.Default)
    {
        key = ConfigManager.RegisterButton(
            description,
            action,
            buttonText,
            group,
            assembly,
            storageKey,
            helpText,
            locTable,
            displayNameKey,
            helpTextKey,
            buttonTextKey,
            groupKey,
            order,
            color);
        return this;
    }

    /// <summary>
    /// 手动向当前 MOD 的设置界面注册一个按钮项。
    /// </summary>
    /// <param name="description">按钮项的显示名称或描述文本。</param>
    /// <param name="action">点击按钮时执行的动作。</param>
    /// <param name="buttonText">按钮上的文本。</param>
    /// <param name="group">按钮所在设置分组。</param>
    /// <param name="storageKey">持久化键；留空时由描述文本生成。</param>
    /// <param name="helpText">悬停提示或帮助文本。</param>
    /// <param name="locTable">本地化表名；留空时使用默认设置界面表。</param>
    /// <param name="displayNameKey">显示名称本地化键。</param>
    /// <param name="helpTextKey">帮助文本本地化键。</param>
    /// <param name="buttonTextKey">按钮文本本地化键。</param>
    /// <param name="groupKey">分组名称本地化键。</param>
    /// <param name="order">同组内排序值，越小越靠前。</param>
    /// <param name="color">按钮颜色风格。</param>
    /// <returns>当前构建器，用于继续链式调用。</returns>
    public RegistryBuilder RegisterButton(
        string description,
        Action action,
        string buttonText = "按钮",
        string group = ConfigAttribute.DefaultGroup,
        string? storageKey = null,
        string? helpText = null,
        string? locTable = null,
        string? displayNameKey = null,
        string? helpTextKey = null,
        string? buttonTextKey = null,
        string? groupKey = null,
        int order = 0,
        UIButtonColor color = UIButtonColor.Default)
    {
        return RegisterButton(
            out _,
            description,
            action,
            buttonText,
            group,
            storageKey,
            helpText,
            locTable,
            displayNameKey,
            helpTextKey,
            buttonTextKey,
            groupKey,
            order,
            color);
    }

    /// <summary>
    /// 完成当前 MOD 的注册，并触发 Attribute 扫描。
    /// </summary>
    /// <returns>当前 MOD 的注册上下文。</returns>
    /// <remarks>
    /// 方法可重复调用；第一次调用会触发 <see cref="ModRegistry.OnRegistered"/>，后续调用只返回已有上下文。
    /// </remarks>
    public ModContext Done()
    {
        ModContext context = ModRegistry.GetContext(assembly)
            ?? throw new InvalidOperationException($"{assembly.GetName().Name} has not been registered.");

        if (completed)
        {
            return context;
        }

        ModRegistry.Complete(assembly);
        completed = true;
        return context;
    }
}
