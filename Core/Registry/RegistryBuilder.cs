// 文件用途：提供注册后的链式配置入口，用于启用配置、按钮、日志等 JML 功能。
using JmcModLib.Config;
using JmcModLib.Config.Storage;
using JmcModLib.Config.UI;
using MegaCrit.Sts2.Core.Logging;
using System.Reflection;

namespace JmcModLib.Core;

public sealed class RegistryBuilder
{
    private readonly Assembly assembly;
    private bool completed;

    internal RegistryBuilder(Assembly assembly)
    {
        this.assembly = assembly;
    }

    public RegistryBuilder WithDisplayName(string displayName)
    {
        ModRegistry.UpdateDisplayName(assembly, displayName);
        return this;
    }

    public RegistryBuilder WithVersion(string version)
    {
        ModRegistry.UpdateVersion(assembly, version);
        return this;
    }

    public RegistryBuilder RegisterLogger(
        LogLevel minimumLevel = LogLevel.Info,
        LogPrefixFlags prefixFlags = LogPrefixFlags.Default,
        bool throwOnFatal = true,
        LogType logType = LogType.Generic,
        bool includeExceptionDetails = true,
        LogConfigUIFlags uIFlags = LogConfigUIFlags.None)
    {
        ModLogger.RegisterAssembly(
            assembly,
            minimumLevel,
            prefixFlags,
            throwOnFatal,
            logType,
            includeExceptionDetails,
            uIFlags);
        ModRegistry.MarkLoggerConfigured(assembly);
        return this;
    }

    public RegistryBuilder UseAttributeRouting()
    {
        JmcModLib.Core.AttributeRouter.AttributeRouter.Init();
        return this;
    }

    public RegistryBuilder UseConfig(IConfigStorage? storage = null)
    {
        UseAttributeRouting();
        ConfigManager.Init();

        if (storage != null)
        {
            ConfigManager.SetStorage(storage, assembly);
        }

        return this;
    }

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

    public ModContext Done()
    {
        ModContext context = ModRegistry.GetContext(assembly)
            ?? throw new InvalidOperationException($"{assembly.GetName().Name} has not been registered.");

        if (completed)
        {
            return context;
        }

        if (!context.LoggerConfigured)
        {
            RegisterLogger();
            context = ModRegistry.GetContext(assembly)
                ?? throw new InvalidOperationException($"{assembly.GetName().Name} has not been registered.");
        }

        ModRegistry.Complete(assembly);
        completed = true;
        return context;
    }
}
