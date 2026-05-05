// 文件用途：维护子 MOD 注册表，负责注册生命周期、上下文查询与注册事件分发。
using JmcModLib.Config;
using JmcModLib.Reflection;
using System.Collections.Concurrent;
using System.Reflection;

namespace JmcModLib.Core;

/// <summary>
/// 维护 STS2 子 MOD 与托管程序集之间的注册上下文，并分发生命周期事件。
/// </summary>
/// <remarks>
/// <para>
/// 注册时会为目标程序集启用 JML 默认服务，包括按程序集隔离的 <see cref="ModLogger"/>、
/// 配置管理器和 Attribute 扫描管线。子 MOD 通常只需要在入口处调用一次
/// <see cref="Register(bool, string, string?, string?, Assembly?)"/>，最后通过
/// <see cref="RegistryBuilder.Done"/> 完成扫描。
/// </para>
/// <example>
/// <code><![CDATA[
/// ModRegistry.Register(true, VersionInfo.Name, VersionInfo.Name, VersionInfo.Version)?
///     .Done();
/// ]]></code>
/// </example>
/// </remarks>
public static class ModRegistry
{
    private static readonly ConcurrentDictionary<Assembly, ModContext> Contexts = new();

    /// <summary>
    /// 当某个 MOD 完成注册并即将被 AttributeRouter 扫描时触发。
    /// </summary>
    public static event Action<ModContext>? OnRegistered;

    /// <summary>
    /// 当某个 MOD 从注册表移除后触发。
    /// </summary>
    public static event Action<ModContext>? OnUnregistered;

    /// <summary>
    /// 注册一个 MOD 程序集，并返回可继续链式补充设置的构建器。
    /// </summary>
    /// <param name="modId">MOD 的稳定标识，通常与 manifest 的 <c>id</c> 一致。</param>
    /// <param name="displayName">显示名称；留空时优先从 manifest 或程序集名称回退读取。</param>
    /// <param name="version">版本号；留空时优先从 manifest 或程序集版本回退读取。</param>
    /// <param name="assembly">所属程序集；留空时自动解析调用方程序集。</param>
    /// <returns>用于继续设置显示名、版本、手动按钮并最终调用 <see cref="RegistryBuilder.Done"/> 的构建器。</returns>
    /// <example>
    /// <code><![CDATA[
    /// ModRegistry.Register("BetterMap", "Better Map", "1.2.0")
    ///     .Done();
    /// ]]></code>
    /// </example>
    public static RegistryBuilder Register(
        string modId,
        string? displayName = null,
        string? version = null,
        Assembly? assembly = null)
    {
        assembly = ResolveAssembly(assembly);
        _ = Contexts.AddOrUpdate(
            assembly,
            asm => CreateContext(asm, modId, displayName, version),
            (asm, existing) => UpdateContext(existing, asm, modId, displayName, version));
        EnsureDefaultServices(assembly);

        return new RegistryBuilder(assembly);
    }

    /// <summary>
    /// 按是否延迟完成注册来注册 MOD。
    /// </summary>
    /// <param name="deferredCompletion">
    /// 为 <see langword="true"/> 时返回构建器，调用方需要自行调用 <see cref="RegistryBuilder.Done"/>；
    /// 为 <see langword="false"/> 时立即完成注册并返回 <see langword="null"/>。
    /// </param>
    /// <param name="modId">MOD 的稳定标识，通常与 manifest 的 <c>id</c> 一致。</param>
    /// <param name="displayName">显示名称；留空时优先从 manifest 或程序集名称回退读取。</param>
    /// <param name="version">版本号；留空时优先从 manifest 或程序集版本回退读取。</param>
    /// <param name="assembly">所属程序集；留空时自动解析调用方程序集。</param>
    /// <returns>延迟完成时返回构建器；立即完成时返回 <see langword="null"/>。</returns>
    /// <example>
    /// <code><![CDATA[
    /// ModRegistry.Register(true, VersionInfo.Name, VersionInfo.Name, VersionInfo.Version)?
    ///     .RegisterButton("重载配置", ReloadConfig, "重载")
    ///     .Done();
    /// ]]></code>
    /// </example>
    public static RegistryBuilder? Register(
        bool deferredCompletion,
        string modId,
        string? displayName = null,
        string? version = null,
        Assembly? assembly = null)
    {
        RegistryBuilder builder = Register(modId, displayName, version, assembly);
        if (deferredCompletion)
        {
            return builder;
        }

        builder.Done();
        return null;
    }

    /// <summary>
    /// 从对象形式的元数据中读取 MOD 标识、显示名和版本并完成注册。
    /// </summary>
    /// <param name="deferredCompletion">
    /// 为 <see langword="true"/> 时返回构建器；为 <see langword="false"/> 时立即完成注册。
    /// </param>
    /// <param name="modInfo">
    /// 可包含 <c>id</c>/<c>Id</c>/<c>modId</c>/<c>ModId</c>、
    /// <c>displayName</c>/<c>DisplayName</c>/<c>name</c>/<c>Name</c>、
    /// <c>version</c>/<c>Version</c> 字段或属性的元数据对象。
    /// </param>
    /// <param name="displayName">显式显示名称，优先级高于 <paramref name="modInfo"/>。</param>
    /// <param name="version">显式版本号，优先级高于 <paramref name="modInfo"/>。</param>
    /// <param name="assembly">所属程序集；留空时自动解析调用方程序集。</param>
    /// <returns>延迟完成时返回构建器；立即完成时返回 <see langword="null"/>。</returns>
    public static RegistryBuilder? Register(
        bool deferredCompletion,
        object? modInfo,
        string? displayName = null,
        string? version = null,
        Assembly? assembly = null)
    {
        assembly = ResolveAssembly(assembly);
        string? resolvedDisplayName = displayName
            ?? ReadMetadataString(modInfo, "displayName", "DisplayName", "name", "Name");
        string? resolvedVersion = version
            ?? ReadMetadataString(modInfo, "version", "Version");
        string resolvedModId =
            ReadMetadataString(modInfo, "id", "Id", "modId", "ModId")
            ?? resolvedDisplayName
            ?? assembly.GetName().Name
            ?? VersionInfo.GetName(assembly);

        return Register(deferredCompletion, resolvedModId, resolvedDisplayName, resolvedVersion, assembly);
    }

    /// <summary>
    /// 使用类型 <typeparamref name="T"/> 所在程序集初始化注册上下文。
    /// </summary>
    /// <typeparam name="T">位于目标 MOD 程序集中的任意类型，常用入口 <c>MainFile</c>。</typeparam>
    /// <param name="modId">MOD 的稳定标识，通常与 manifest 的 <c>id</c> 一致。</param>
    /// <param name="displayName">显示名称；留空时优先从 manifest 或程序集名称回退读取。</param>
    /// <param name="version">版本号；留空时优先从 manifest 或程序集版本回退读取。</param>
    /// <returns>用于继续设置并完成注册的构建器。</returns>
    /// <remarks>
    /// 该方法承接旧版独立启动包装的 <c>Init&lt;T&gt;</c> 用法，但入口统一在 <see cref="ModRegistry"/> 下。
    /// </remarks>
    /// <example>
    /// <code><![CDATA[
    /// ModRegistry.Init<MainFile>(VersionInfo.Name, VersionInfo.Name, VersionInfo.Version)
    ///     .Done();
    /// ]]></code>
    /// </example>
    public static RegistryBuilder Init<T>(string modId, string? displayName = null, string? version = null)
    {
        return Init(typeof(T).Assembly, modId, displayName, version);
    }

    /// <summary>
    /// 使用指定程序集初始化注册上下文。
    /// </summary>
    /// <param name="assembly">目标 MOD 程序集。</param>
    /// <param name="modId">MOD 的稳定标识，通常与 manifest 的 <c>id</c> 一致。</param>
    /// <param name="displayName">显示名称；留空时优先从 manifest 或程序集名称回退读取。</param>
    /// <param name="version">版本号；留空时优先从 manifest 或程序集版本回退读取。</param>
    /// <returns>用于继续设置并完成注册的构建器。</returns>
    public static RegistryBuilder Init(Assembly assembly, string modId, string? displayName = null, string? version = null)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        return Register(modId, displayName, version, assembly);
    }

    /// <summary>
    /// 使用类型 <typeparamref name="T"/> 所在程序集初始化注册上下文，并从程序集名称推断 MOD 标识。
    /// </summary>
    /// <typeparam name="T">位于目标 MOD 程序集中的任意类型，常用入口 <c>MainFile</c>。</typeparam>
    /// <returns>用于继续设置并完成注册的构建器。</returns>
    /// <example>
    /// <code><![CDATA[
    /// ModRegistry.Init<MainFile>()
    ///     .Done();
    /// ]]></code>
    /// </example>
    public static RegistryBuilder Init<T>()
    {
        Assembly assembly = typeof(T).Assembly;
        string modId = assembly.GetName().Name ?? typeof(T).Name;
        return Init(assembly, modId);
    }

    /// <summary>
    /// 使用类型 <typeparamref name="T"/> 所在程序集注册 MOD。
    /// </summary>
    /// <typeparam name="T">位于目标 MOD 程序集中的任意类型，常用入口 <c>MainFile</c>。</typeparam>
    /// <param name="modId">MOD 的稳定标识，通常与 manifest 的 <c>id</c> 一致。</param>
    /// <param name="displayName">显示名称；留空时优先从 manifest 或程序集名称回退读取。</param>
    /// <param name="version">版本号；留空时优先从 manifest 或程序集版本回退读取。</param>
    /// <returns>用于继续设置并完成注册的构建器。</returns>
    public static RegistryBuilder Register<T>(string modId, string? displayName = null, string? version = null)
    {
        return Register(modId, displayName, version, typeof(T).Assembly);
    }

    /// <summary>
    /// 使用类型 <typeparamref name="T"/> 所在程序集，并按是否延迟完成来注册 MOD。
    /// </summary>
    /// <typeparam name="T">位于目标 MOD 程序集中的任意类型，常用入口 <c>MainFile</c>。</typeparam>
    /// <param name="deferredCompletion">是否延迟到调用方显式执行 <see cref="RegistryBuilder.Done"/>。</param>
    /// <param name="modId">MOD 的稳定标识，通常与 manifest 的 <c>id</c> 一致。</param>
    /// <param name="displayName">显示名称；留空时优先从 manifest 或程序集名称回退读取。</param>
    /// <param name="version">版本号；留空时优先从 manifest 或程序集版本回退读取。</param>
    /// <returns>延迟完成时返回构建器；立即完成时返回 <see langword="null"/>。</returns>
    public static RegistryBuilder? Register<T>(
        bool deferredCompletion,
        string modId,
        string? displayName = null,
        string? version = null)
    {
        return Register(deferredCompletion, modId, displayName, version, typeof(T).Assembly);
    }

    /// <summary>
    /// 判断指定程序集是否已经注册。
    /// </summary>
    /// <param name="assembly">目标程序集；留空时自动解析调用方程序集。</param>
    /// <returns>已注册时返回 <see langword="true"/>。</returns>
    public static bool IsRegistered(Assembly? assembly = null)
    {
        return Contexts.ContainsKey(ResolveAssembly(assembly));
    }

    /// <summary>
    /// 尝试读取指定程序集的 MOD 上下文。
    /// </summary>
    /// <param name="context">成功时返回已注册的上下文。</param>
    /// <param name="assembly">目标程序集；留空时自动解析调用方程序集。</param>
    /// <returns>存在上下文时返回 <see langword="true"/>。</returns>
    public static bool TryGetContext(out ModContext? context, Assembly? assembly = null)
    {
        return Contexts.TryGetValue(ResolveAssembly(assembly), out context);
    }

    /// <summary>
    /// 获取指定程序集的 MOD 上下文。
    /// </summary>
    /// <param name="assembly">目标程序集；留空时自动解析调用方程序集。</param>
    /// <returns>已注册时返回上下文；否则返回 <see langword="null"/>。</returns>
    public static ModContext? GetContext(Assembly? assembly = null)
    {
        Contexts.TryGetValue(ResolveAssembly(assembly), out ModContext? context);
        return context;
    }

    /// <summary>
    /// 获取指定程序集的 MOD 标识；未注册时回退到程序集名称。
    /// </summary>
    /// <param name="assembly">目标程序集；留空时自动解析调用方程序集。</param>
    /// <returns>MOD 标识。</returns>
    public static string GetModId(Assembly? assembly = null)
    {
        ModContext? context = GetContext(assembly);
        return context?.ModId ?? VersionInfo.GetName(ResolveAssembly(assembly));
    }

    /// <summary>
    /// 获取指定程序集的显示名称；未注册时回退到程序集名称。
    /// </summary>
    /// <param name="assembly">目标程序集；留空时自动解析调用方程序集。</param>
    /// <returns>显示名称。</returns>
    public static string GetDisplayName(Assembly? assembly = null)
    {
        ModContext? context = GetContext(assembly);
        return context?.DisplayName ?? VersionInfo.GetName(ResolveAssembly(assembly));
    }

    /// <summary>
    /// 获取指定程序集的版本号；未注册时回退到程序集版本。
    /// </summary>
    /// <param name="assembly">目标程序集；留空时自动解析调用方程序集。</param>
    /// <returns>版本号字符串。</returns>
    public static string GetVersion(Assembly? assembly = null)
    {
        ModContext? context = GetContext(assembly);
        return context?.Version ?? VersionInfo.GetVersion(ResolveAssembly(assembly));
    }

    /// <summary>
    /// 获取用于日志输出的标签。
    /// </summary>
    /// <param name="assembly">目标程序集；留空时自动解析调用方程序集。</param>
    /// <returns>形如 <c>[DisplayName v1.0.0]</c> 的标签。</returns>
    public static string GetTag(Assembly? assembly = null)
    {
        string displayName = GetDisplayName(assembly);
        string version = GetVersion(assembly);
        return string.IsNullOrWhiteSpace(version) ? $"[{displayName}]" : $"[{displayName} v{version}]";
    }

    /// <summary>
    /// 注销指定程序集的 MOD 上下文，并清理 JML 持有的日志、配置与热键状态。
    /// </summary>
    /// <param name="assembly">目标程序集；留空时自动解析调用方程序集。</param>
    /// <returns>成功移除上下文时返回 <see langword="true"/>。</returns>
    public static bool Unregister(Assembly? assembly = null)
    {
        assembly = ResolveAssembly(assembly);
        if (!Contexts.TryRemove(assembly, out ModContext? context))
        {
            return false;
        }

        ModLogger.UnregisterAssembly(assembly);
        context.IsCompleted = false;
        OnUnregistered?.Invoke(context);
        return true;
    }

    internal static string GetLoggerContext(Assembly? assembly = null)
    {
        ModContext? context = GetContext(assembly);
        if (context != null)
        {
            return context.LoggerContext;
        }

        string displayName = VersionInfo.GetName(ResolveAssembly(assembly));
        string version = VersionInfo.GetVersion(ResolveAssembly(assembly));
        return string.IsNullOrWhiteSpace(version) ? displayName : $"{displayName} v{version}";
    }

    internal static void UpdateDisplayName(Assembly assembly, string displayName)
    {
        if (Contexts.TryGetValue(assembly, out ModContext? context) && !string.IsNullOrWhiteSpace(displayName))
        {
            context.DisplayName = displayName.Trim();
        }
    }

    internal static void UpdateVersion(Assembly assembly, string version)
    {
        if (Contexts.TryGetValue(assembly, out ModContext? context) && !string.IsNullOrWhiteSpace(version))
        {
            context.Version = version.Trim();
        }
    }

    internal static void Complete(Assembly assembly)
    {
        if (!Contexts.TryGetValue(assembly, out ModContext? context))
        {
            throw new InvalidOperationException($"{assembly.GetName().Name} has not been registered.");
        }

        if (context.IsCompleted)
        {
            return;
        }

        EnsureDefaultServices(assembly);
        context.IsCompleted = true;
        OnRegistered?.Invoke(context);
    }

    internal static void EnsureDefaultServices(Assembly assembly)
    {
        ModLogger.RegisterAssembly(assembly);
        ConfigManager.Init();
    }

    private static ModContext CreateContext(Assembly assembly, string modId, string? displayName, string? version)
    {
        string resolvedModId = ResolveModId(assembly, modId);
        var mod = ModRuntime.FindModById(resolvedModId);

        string resolvedDisplayName = string.IsNullOrWhiteSpace(displayName)
            ? mod?.manifest?.name ?? assembly.GetName().Name ?? resolvedModId
            : displayName.Trim();

        string resolvedVersion = string.IsNullOrWhiteSpace(version)
            ? mod?.manifest?.version
                ?? assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                ?? assembly.GetName().Version?.ToString()
                ?? "0.0.0"
            : version.Trim();

        return new ModContext(assembly, resolvedModId, resolvedDisplayName, resolvedVersion);
    }

    private static ModContext UpdateContext(
        ModContext existing,
        Assembly assembly,
        string modId,
        string? displayName,
        string? version)
    {
        string resolvedModId = ResolveModId(assembly, modId);
        var mod = ModRuntime.FindModById(resolvedModId);

        existing.ModId = resolvedModId;
        existing.DisplayName = string.IsNullOrWhiteSpace(displayName)
            ? existing.DisplayName
            : displayName.Trim();
        existing.Version = string.IsNullOrWhiteSpace(version)
            ? existing.Version
            : version.Trim();

        if (string.IsNullOrWhiteSpace(existing.DisplayName))
        {
            existing.DisplayName = mod?.manifest?.name ?? assembly.GetName().Name ?? resolvedModId;
        }

        if (string.IsNullOrWhiteSpace(existing.Version))
        {
            existing.Version = mod?.manifest?.version
                ?? assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                ?? assembly.GetName().Version?.ToString()
                ?? "0.0.0";
        }

        return existing;
    }

    private static string ResolveModId(Assembly assembly, string modId)
    {
        if (!string.IsNullOrWhiteSpace(modId))
        {
            return modId.Trim();
        }

        return assembly.GetName().Name ?? VersionInfo.GetName(assembly);
    }

    private static string? ReadMetadataString(object? metadata, params string[] memberNames)
    {
        if (metadata == null)
        {
            return null;
        }

        Type metadataType = metadata.GetType();
        foreach (string memberName in memberNames)
        {
            try
            {
                object? value = MemberAccessor.Get(metadataType, memberName).GetValue(metadata);
                if (value != null && !string.IsNullOrWhiteSpace(value.ToString()))
                {
                    return value.ToString()!.Trim();
                }
            }
            catch (MissingMemberException)
            {
            }
        }

        return null;
    }

    private static Assembly ResolveAssembly(Assembly? assembly)
    {
        return AssemblyResolver.Resolve(assembly, typeof(ModRegistry));
    }
}
