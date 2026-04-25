using System.Reflection;
using JmcModLib.Config;
using JmcModLib.Config.Storage;
using MegaCrit.Sts2.Core.Logging;

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
