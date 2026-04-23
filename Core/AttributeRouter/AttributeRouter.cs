using System.Collections.Concurrent;
using System.Reflection;
using JmcModLib.Reflection;

namespace JmcModLib.Core.AttributeRouter;

/// <summary>
/// Scans registered mod assemblies and routes discovered attributes to handlers.
/// </summary>
public static class AttributeRouter
{
    private static readonly ConcurrentDictionary<Type, List<IAttributeHandler>> Handlers = new();
    private static readonly ConcurrentDictionary<Assembly, byte> ScannedAssemblies = new();
    private static readonly ConcurrentDictionary<Assembly, ConcurrentDictionary<IAttributeHandler, List<ReflectionAccessorBase>>> HandlerRecords = new();

    private static int initialized;

    public static event Action<Assembly>? AssemblyScanned;

    public static event Action<Assembly>? AssemblyUnscanned;

    public static bool IsInitialized => Volatile.Read(ref initialized) == 1;

    public static void Init()
    {
        if (Interlocked.Exchange(ref initialized, 1) == 1)
        {
            return;
        }

        ModRegistry.OnRegistered += OnModRegistered;
        ModRegistry.OnUnregistered += OnModUnregistered;
        ModLogger.Debug("AttributeRouter initialized.");
    }

    public static void Dispose()
    {
        if (Interlocked.Exchange(ref initialized, 0) == 0)
        {
            return;
        }

        ModRegistry.OnRegistered -= OnModRegistered;
        ModRegistry.OnUnregistered -= OnModUnregistered;

        foreach (Assembly assembly in ScannedAssemblies.Keys.ToArray())
        {
            UnscanAssembly(assembly);
        }

        Handlers.Clear();
        HandlerRecords.Clear();
        ModLogger.Debug("AttributeRouter disposed.");
    }

    public static void RegisterHandler<TAttribute>(IAttributeHandler handler)
        where TAttribute : Attribute
    {
        ArgumentNullException.ThrowIfNull(handler);

        List<IAttributeHandler> list = Handlers.GetOrAdd(typeof(TAttribute), static _ => []);
        lock (list)
        {
            if (!list.Contains(handler))
            {
                list.Add(handler);
            }
        }
    }

    public static void RegisterHandler<TAttribute>(Action<Assembly, ReflectionAccessorBase, TAttribute> action)
        where TAttribute : Attribute
    {
        RegisterHandler<TAttribute>(new SimpleAttributeHandler<TAttribute>(action));
    }

    public static bool UnregisterHandler(IAttributeHandler handler)
    {
        ArgumentNullException.ThrowIfNull(handler);

        bool removed = false;
        foreach ((_, List<IAttributeHandler> list) in Handlers)
        {
            lock (list)
            {
                removed |= list.Remove(handler);
            }
        }

        return removed;
    }

    public static void ScanAssembly(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        Init();

        if (!ScannedAssemblies.TryAdd(assembly, 0))
        {
            return;
        }

        var records = new ConcurrentDictionary<IAttributeHandler, List<ReflectionAccessorBase>>();
        HandlerRecords[assembly] = records;

        ModLogger.Debug($"AttributeRouter scanning {ModRegistry.GetTag(assembly)}");

        foreach (Type type in GetTypesSafe(assembly))
        {
            if (!IsScannableType(type))
            {
                continue;
            }

            DispatchAccessor(assembly, TypeAccessor.Get(type), records);

            foreach (MethodAccessor method in MethodAccessor.GetAll(type))
            {
                DispatchAccessor(assembly, method, records);
            }

            foreach (MemberAccessor member in MemberAccessor.GetAll(type))
            {
                DispatchAccessor(assembly, member, records);
            }
        }

        AssemblyScanned?.Invoke(assembly);
        ModLogger.Debug($"AttributeRouter finished scanning {ModRegistry.GetTag(assembly)}");
    }

    public static void UnscanAssembly(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        if (HandlerRecords.TryRemove(assembly, out ConcurrentDictionary<IAttributeHandler, List<ReflectionAccessorBase>>? records))
        {
            foreach ((IAttributeHandler handler, List<ReflectionAccessorBase> accessors) in records)
            {
                Action<Assembly, IReadOnlyList<ReflectionAccessorBase>>? unregister = handler.Unregister;
                if (unregister == null)
                {
                    continue;
                }

                try
                {
                    unregister(assembly, accessors.AsReadOnly());
                }
                catch (Exception ex)
                {
                    ModLogger.Error($"AttributeRouter failed to unregister {handler.GetType().Name}", ex, assembly);
                }
            }
        }

        _ = ScannedAssemblies.TryRemove(assembly, out _);
        AssemblyUnscanned?.Invoke(assembly);
    }

    private static void OnModRegistered(ModContext context)
    {
        try
        {
            ScanAssembly(context.Assembly);
        }
        catch (Exception ex)
        {
            ModLogger.Error($"AttributeRouter failed while scanning {context.Tag}", ex, context.Assembly);
        }
    }

    private static void OnModUnregistered(ModContext context)
    {
        try
        {
            UnscanAssembly(context.Assembly);
        }
        catch (Exception ex)
        {
            ModLogger.Error($"AttributeRouter failed while unscanning {context.Tag}", ex, context.Assembly);
        }
    }

    private static void DispatchAccessor(
        Assembly assembly,
        ReflectionAccessorBase accessor,
        ConcurrentDictionary<IAttributeHandler, List<ReflectionAccessorBase>> records)
    {
        Attribute[] attributes = accessor.GetAllAttributes();
        if (attributes.Length == 0)
        {
            return;
        }

        foreach (Attribute attribute in attributes)
        {
            if (!Handlers.TryGetValue(attribute.GetType(), out List<IAttributeHandler>? handlers))
            {
                continue;
            }

            List<IAttributeHandler> snapshot;
            lock (handlers)
            {
                snapshot = [.. handlers];
            }

            foreach (IAttributeHandler handler in snapshot)
            {
                try
                {
                    handler.Handle(assembly, accessor, attribute);
                    List<ReflectionAccessorBase> list = records.GetOrAdd(handler, static _ => []);
                    lock (list)
                    {
                        list.Add(accessor);
                    }
                }
                catch (Exception ex)
                {
                    ModLogger.Error(
                        $"AttributeRouter handler {handler.GetType().Name} failed on {attribute.GetType().Name}",
                        ex,
                        assembly);
                }
            }
        }
    }

    private static IEnumerable<Type> GetTypesSafe(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(static type => type != null)!;
        }
        catch
        {
            return [];
        }
    }

    private static bool IsScannableType(Type? type)
    {
        return type != null
            && !type.IsInterface
            && !type.IsArray
            && !type.IsPointer
            && !type.IsByRef
            && !type.IsByRefLike
            && !type.ContainsGenericParameters;
    }
}

public sealed class SimpleAttributeHandler<TAttribute> : IAttributeHandler
    where TAttribute : Attribute
{
    private readonly Action<Assembly, ReflectionAccessorBase, TAttribute> action;

    public SimpleAttributeHandler(Action<Assembly, ReflectionAccessorBase, TAttribute> action)
    {
        this.action = action ?? throw new ArgumentNullException(nameof(action));
    }

    public Action<Assembly, IReadOnlyList<ReflectionAccessorBase>>? Unregister => null;

    public void Handle(Assembly assembly, ReflectionAccessorBase accessor, Attribute attribute)
    {
        if (attribute is TAttribute typedAttribute)
        {
            action(assembly, accessor, typedAttribute);
        }
    }
}
