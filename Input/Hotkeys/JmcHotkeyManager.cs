using Godot;
using JmcModLib.Config.Entry;
using JmcModLib.Core.AttributeRouter;
using JmcModLib.Input;
using JmcModLib.Reflection;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Debug;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using System.Reflection;

namespace JmcModLib.Config.UI;

/// <summary>
/// JML 运行时热键分发器，用于处理 MOD 自有的可配置热键。
/// </summary>
public static class JmcHotkeyManager
{
    private const string RelayName = "JmcModLibHotkeyInputRelay";
    private static readonly Dictionary<string, JmcHotkeyRegistration> Registrations = new(StringComparer.Ordinal);
    private static readonly object SyncRoot = new();
    private static JmcHotkeyInputRelay? relay;
    private static bool installScheduled;
    private static int initialized;

    /// <summary>
    /// 热键系统是否已经初始化。
    /// </summary>
    public static bool IsInitialized => Volatile.Read(ref initialized) == 1;

    /// <summary>
    /// 初始化热键系统。通常由 <c>UseConfig()</c> 自动调用，子 MOD 一般不需要手动调用。
    /// </summary>
    public static void Init()
    {
        if (Interlocked.Exchange(ref initialized, 1) == 1)
        {
            return;
        }

        ModRegistry.OnUnregistered += OnModUnregistered;
        JmcInputManager.Initialize();
    }

    /// <summary>
    /// 注册一个运行时热键，并通过 <see cref="JmcKeyBinding"/> 动态读取当前绑定值。
    /// </summary>
    /// <param name="key">热键注册键，同一 MOD 内应保持唯一。</param>
    /// <param name="bindingGetter">用于读取当前热键绑定值的委托。</param>
    /// <param name="action">热键触发时执行的动作。</param>
    /// <param name="consumeInput">触发后是否吃掉本次输入。</param>
    /// <param name="exactModifiers">是否要求键盘修饰键完全一致。</param>
    /// <param name="allowEcho">是否允许键盘 Echo 输入触发。</param>
    /// <param name="debounceMs">防抖时间，单位为毫秒。</param>
    /// <param name="assembly">所属程序集；留空时自动解析调用方程序集。</param>
    public static void Register(
        string key,
        Func<JmcKeyBinding> bindingGetter,
        Action action,
        bool consumeInput = true,
        bool exactModifiers = true,
        bool allowEcho = false,
        ulong debounceMs = 150,
        Assembly? assembly = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(bindingGetter);
        ArgumentNullException.ThrowIfNull(action);

        Assembly resolvedAssembly = AssemblyResolver.Resolve(assembly, typeof(JmcHotkeyManager));
        RegisterInternal(
            resolvedAssembly,
            key.Trim(),
            bindingGetter,
            action,
            new HotkeyOptions(consumeInput, exactModifiers, allowEcho, debounceMs));
    }

    /// <summary>
    /// 注册一个仅使用键盘按键的运行时热键。
    /// </summary>
    /// <param name="key">热键注册键，同一 MOD 内应保持唯一。</param>
    /// <param name="keyGetter">用于读取当前键盘按键的委托。</param>
    /// <param name="action">热键触发时执行的动作。</param>
    /// <param name="consumeInput">触发后是否吃掉本次输入。</param>
    /// <param name="exactModifiers">是否要求键盘修饰键完全一致。</param>
    /// <param name="allowEcho">是否允许键盘 Echo 输入触发。</param>
    /// <param name="debounceMs">防抖时间，单位为毫秒。</param>
    /// <param name="assembly">所属程序集；留空时自动解析调用方程序集。</param>
    public static void Register(
        string key,
        Func<Key> keyGetter,
        Action action,
        bool consumeInput = true,
        bool exactModifiers = true,
        bool allowEcho = false,
        ulong debounceMs = 150,
        Assembly? assembly = null)
    {
        ArgumentNullException.ThrowIfNull(keyGetter);
        Register(
            key,
            () => new JmcKeyBinding(keyGetter()),
            action,
            consumeInput,
            exactModifiers,
            allowEcho,
            debounceMs,
            assembly);
    }

    /// <summary>
    /// 注销当前 MOD 中指定注册键的运行时热键。
    /// </summary>
    /// <param name="key">热键注册键。</param>
    /// <param name="assembly">所属程序集；留空时自动解析调用方程序集。</param>
    /// <returns>如果成功移除了热键，返回 <see langword="true"/>。</returns>
    public static bool Unregister(string key, Assembly? assembly = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        Assembly resolvedAssembly = AssemblyResolver.Resolve(assembly, typeof(JmcHotkeyManager));
        lock (SyncRoot)
        {
            return Registrations.Remove(CreateRegistrationId(resolvedAssembly, key.Trim()));
        }
    }

    /// <summary>
    /// 注销指定程序集下的所有运行时热键。
    /// </summary>
    /// <param name="assembly">所属程序集；留空时自动解析调用方程序集。</param>
    public static void UnregisterAssembly(Assembly? assembly = null)
    {
        Assembly resolvedAssembly = AssemblyResolver.Resolve(assembly, typeof(JmcHotkeyManager));
        lock (SyncRoot)
        {
            foreach (string registrationId in Registrations
                .Where(pair => pair.Value.Assembly == resolvedAssembly)
                .Select(static pair => pair.Key)
                .ToArray())
            {
                _ = Registrations.Remove(registrationId);
            }
        }
    }

    internal static void RegisterInternal(
        Assembly assembly,
        string key,
        Func<JmcKeyBinding> bindingGetter,
        Action action,
        HotkeyOptions options)
    {
        Init();

        lock (SyncRoot)
        {
            Registrations[CreateRegistrationId(assembly, key)] = new JmcHotkeyRegistration(
                assembly,
                key,
                bindingGetter,
                action,
                options);
        }

        EnsureRelay();
        ModLogger.Info($"Registered hotkey {key}", assembly);
    }

    internal static bool HandleInput(InputEvent inputEvent, Viewport? viewport)
    {
        ArgumentNullException.ThrowIfNull(inputEvent);

        JmcHotkeyRegistration[] registrations;
        lock (SyncRoot)
        {
            if (Registrations.Count == 0)
            {
                return false;
            }

            registrations = [.. Registrations.Values];
        }

        if (JmcKeybindButton.HasActiveListener
            || JmcKeybindButton.HasRecentCapture
            || ShouldIgnoreInput(inputEvent, viewport))
        {
            return false;
        }

        bool handled = false;
        foreach (JmcHotkeyRegistration registration in registrations)
        {
            try
            {
                if (!registration.TryConsumePress(inputEvent))
                {
                    continue;
                }

                registration.Invoke();
                handled |= registration.Options.ConsumeInput;
            }
            catch (Exception ex)
            {
                ModLogger.Error($"Hotkey {registration.Key} failed.", ex, registration.Assembly);
            }
        }

        if (handled)
        {
            viewport?.SetInputAsHandled();
        }

        return handled;
    }

    internal static bool HandleInputAction(string actionId, bool pressed, Viewport? viewport)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actionId);

        JmcHotkeyRegistration[] registrations;
        lock (SyncRoot)
        {
            if (Registrations.Count == 0)
            {
                return false;
            }

            registrations = [.. Registrations.Values];
        }

        if (JmcKeybindButton.HasActiveListener || JmcKeybindButton.HasRecentCapture)
        {
            return false;
        }

        bool handled = false;
        foreach (JmcHotkeyRegistration registration in registrations)
        {
            try
            {
                if (!registration.TryConsumeInputAction(actionId, pressed))
                {
                    continue;
                }

                registration.Invoke();
                handled |= registration.Options.ConsumeInput;
            }
            catch (Exception ex)
            {
                ModLogger.Error($"Hotkey {registration.Key} failed.", ex, registration.Assembly);
            }
        }

        if (handled)
        {
            viewport?.SetInputAsHandled();
        }

        return handled;
    }

    private static void EnsureRelay()
    {
        if (relay is { } existing && !existing.IsQueuedForDeletion() && existing.IsInsideTree())
        {
            return;
        }

        if (installScheduled)
        {
            return;
        }

        if (Engine.GetMainLoop() is not SceneTree tree || tree.Root == null)
        {
            ModLogger.Warn("Unable to install JmcModLib hotkey relay: current MainLoop is not a SceneTree.");
            return;
        }

        if (FindExistingRelay(tree) is { } existingRelay)
        {
            relay = existingRelay;
            relay.ActivateProcessing();
            return;
        }

        installScheduled = true;
        Callable.From(() => DeferredInstall(tree)).CallDeferred();
    }

    private static void DeferredInstall(SceneTree tree)
    {
        try
        {
            if (FindExistingRelay(tree) is { } existingRelay)
            {
                relay = existingRelay;
                relay.ActivateProcessing();
                installScheduled = false;
                return;
            }

            Node? parent = NGame.Instance?.IsInsideTree() == true ? NGame.Instance : tree.Root;
            if (parent == null)
            {
                installScheduled = false;
                ModLogger.Warn("Unable to install JmcModLib hotkey relay: parent node is null.");
                return;
            }

            var node = new JmcHotkeyInputRelay
            {
                Name = RelayName,
                ProcessMode = Node.ProcessModeEnum.Always
            };

            parent.AddChild(node);
            node.ActivateProcessing();
            relay = node;
            installScheduled = false;
            ModLogger.Debug($"JmcModLib hotkey relay installed. Parent={parent.GetPath()}");
        }
        catch (Exception ex)
        {
            relay = null;
            installScheduled = false;
            ModLogger.Error("JmcModLib hotkey relay installation failed.", ex);
        }
    }

    private static JmcHotkeyInputRelay? FindExistingRelay(SceneTree tree)
    {
        return tree.Root?.GetNodeOrNull<JmcHotkeyInputRelay>(RelayName)
            ?? NGame.Instance?.GetNodeOrNull<JmcHotkeyInputRelay>(RelayName);
    }

    private static bool ShouldIgnoreInput(InputEvent inputEvent, Viewport? viewport)
    {
        if (IsDevConsoleVisible())
        {
            return true;
        }

        if (inputEvent is not InputEventKey)
        {
            return false;
        }

        Control? focusOwner = viewport?.GuiGetFocusOwner();
        return focusOwner switch
        {
            LineEdit lineEdit => lineEdit.IsEditing(),
            NMegaTextEdit megaTextEdit => megaTextEdit.IsEditing(),
            TextEdit textEdit => textEdit.HasFocus(),
            _ => false
        };
    }

    private static bool IsDevConsoleVisible()
    {
        try
        {
            return NDevConsole.Instance.Visible;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static string CreateRegistrationId(Assembly assembly, string key)
    {
        return $"{assembly.FullName}|{key}";
    }

    private static void OnModUnregistered(ModContext context)
    {
        UnregisterAssembly(context.Assembly);
        JmcInputActionRegistry.UnregisterAssembly(context.Assembly);
    }
}

/// <summary>
/// 运行时热键的触发选项。
/// </summary>
/// <param name="ConsumeInput">触发后是否吃掉本次输入。</param>
/// <param name="ExactModifiers">是否要求键盘修饰键完全一致。</param>
/// <param name="AllowEcho">是否允许键盘 Echo 输入触发。</param>
/// <param name="DebounceMs">防抖时间，单位为毫秒。</param>
public readonly record struct HotkeyOptions(
    bool ConsumeInput = true,
    bool ExactModifiers = true,
    bool AllowEcho = false,
    ulong DebounceMs = 150);

internal sealed class JmcHotkeyInputRelay : Node
{
    public override void _Ready()
    {
        ActivateProcessing();
    }

    public override void _Input(InputEvent inputEvent)
    {
        _ = JmcHotkeyManager.HandleInput(inputEvent, GetViewport());
    }

    public override void _UnhandledInput(InputEvent inputEvent)
    {
        _ = JmcHotkeyManager.HandleInput(inputEvent, GetViewport());
    }

    public override void _UnhandledKeyInput(InputEvent inputEvent)
    {
        _ = JmcHotkeyManager.HandleInput(inputEvent, GetViewport());
    }

    public override void _Process(double delta)
    {
        JmcInputManager.Process();
    }

    public void ActivateProcessing()
    {
        ProcessMode = ProcessModeEnum.Always;
        SetProcess(true);
        SetProcessInput(true);
        SetProcessUnhandledInput(true);
        SetProcessUnhandledKeyInput(true);
    }
}

internal sealed class JmcHotkeyRegistration(
    Assembly assembly,
    string key,
    Func<JmcKeyBinding> bindingGetter,
    Action action,
    HotkeyOptions options)
{
    private ulong lastTriggeredTicks;
    private string? activePressId;

    public Assembly Assembly { get; } = assembly;

    public string Key { get; } = key;

    public HotkeyOptions Options { get; } = options;

    public bool TryConsumePress(InputEvent inputEvent)
    {
        JmcKeyBinding binding = bindingGetter();
        if (!binding.Enabled)
        {
            activePressId = null;
            return false;
        }

        if (Options.AllowEcho)
        {
            return binding.IsPressed(inputEvent, true, Options.ExactModifiers)
                && TryConsumeDebounce();
        }

        if (activePressId != null
            && (!binding.IsDown(Options.ExactModifiers) || IsActivePressExpired()))
        {
            activePressId = null;
        }

        if (binding.IsReleased(inputEvent))
        {
            activePressId = null;
            return false;
        }

        if (!binding.IsPressed(inputEvent, Options.AllowEcho, Options.ExactModifiers))
        {
            return false;
        }

        string pressId = CreatePressId(binding, inputEvent);
        if (activePressId == pressId)
        {
            return false;
        }

        if (!TryConsumeDebounce())
        {
            return false;
        }

        activePressId = pressId;
        return true;
    }

    public void Invoke()
    {
        action.Invoke();
    }

    public bool TryConsumeInputAction(string actionId, bool pressed)
    {
        if (!JmcInputActionRegistry.IsHotkeyBoundToAction(Assembly, Key, actionId))
        {
            return false;
        }

        JmcKeyBinding binding = bindingGetter();
        if (!binding.Enabled)
        {
            activePressId = null;
            return false;
        }

        string pressId = $"steam:{actionId}";
        if (!pressed)
        {
            if (activePressId == pressId)
            {
                activePressId = null;
            }

            return false;
        }

        if (activePressId == pressId || !TryConsumeDebounce())
        {
            return false;
        }

        activePressId = pressId;
        return true;
    }

    private bool TryConsumeDebounce()
    {
        ulong now = Time.GetTicksMsec();
        if (lastTriggeredTicks != 0 && now - lastTriggeredTicks < Options.DebounceMs)
        {
            return false;
        }

        lastTriggeredTicks = now;
        return true;
    }

    private bool IsActivePressExpired()
    {
        ulong now = Time.GetTicksMsec();
        ulong releaseFallbackMs = Math.Max(Options.DebounceMs * 4, 750UL);
        return lastTriggeredTicks != 0 && now - lastTriggeredTicks > releaseFallbackMs;
    }

    private static string CreatePressId(JmcKeyBinding binding, InputEvent inputEvent)
    {
        if (inputEvent is InputEventKey keyEvent)
        {
            return $"keyboard:{JmcKeyBinding.ReadKey(keyEvent)}:{JmcKeyBinding.ReadModifiers(keyEvent)}";
        }

        return binding.HasController
            ? $"controller:{binding.Controller}"
            : "unknown";
    }
}

internal sealed class JmcHotkeyAttributeHandler : IAttributeHandler
{
    public Action<Assembly, IReadOnlyList<ReflectionAccessorBase>>? Unregister => (assembly, _) =>
    {
        JmcHotkeyManager.UnregisterAssembly(assembly);
        JmcInputActionRegistry.UnregisterAssembly(assembly);
    };

    public void Handle(Assembly assembly, ReflectionAccessorBase accessor, Attribute attribute)
    {
        if (accessor is not MethodAccessor method || attribute is not JmcHotkeyAttribute hotkeyAttribute)
        {
            return;
        }

        try
        {
            Action action = HotkeyAttributeHandlerShared.BuildAction(method, assembly);
            MemberAccessor bindingMember = MemberAccessor.Get(method.DeclaringType, hotkeyAttribute.BindingMember);
            ValidateBindingMember(bindingMember);

            string key = HotkeyAttributeHandlerShared.ResolveHotkeyKey(hotkeyAttribute.Key, method);
            JmcHotkeyManager.RegisterInternal(
                assembly,
                key,
                () => JmcKeyBindingValue.FromValue(bindingMember.GetValue(null)),
                action,
                new HotkeyOptions(
                    hotkeyAttribute.ConsumeInput,
                    hotkeyAttribute.ExactModifiers,
                    hotkeyAttribute.AllowEcho,
                    hotkeyAttribute.DebounceMs));
            JmcInputActionRegistry.BindHotkeyToSourceMember(
                assembly,
                key,
                bindingMember.DeclaringType,
                bindingMember.Name);
        }
        catch (Exception ex)
        {
            ModLogger.Error(
                $"Failed to register hotkey for {method.DeclaringType.FullName}.{method.Name}",
                ex,
                assembly);
        }
    }

    private static void ValidateBindingMember(MemberAccessor member)
    {
        if (!member.IsStatic)
        {
            throw new ArgumentException(
                $"Hotkey binding member {member.DeclaringType.FullName}.{member.Name} must be static.");
        }

        if (!member.CanRead)
        {
            throw new ArgumentException(
                $"Hotkey binding member {member.DeclaringType.FullName}.{member.Name} must be readable.");
        }

        Type actualType = Nullable.GetUnderlyingType(member.ValueType) ?? member.ValueType;
        if (actualType != typeof(Key) && actualType != typeof(JmcKeyBinding))
        {
            throw new ArgumentException(
                $"Hotkey binding member {member.DeclaringType.FullName}.{member.Name} must be Godot.Key or JmcKeyBinding.");
        }
    }
}

internal sealed class UIHotkeyAttributeHandler : IAttributeHandler
{
    public Action<Assembly, IReadOnlyList<ReflectionAccessorBase>>? Unregister => (assembly, _) =>
    {
        JmcHotkeyManager.UnregisterAssembly(assembly);
        JmcInputActionRegistry.UnregisterAssembly(assembly);
    };

    public void Handle(Assembly assembly, ReflectionAccessorBase accessor, Attribute attribute)
    {
        if (accessor is not MethodAccessor method || attribute is not UIHotkeyAttribute hotkeyAttribute)
        {
            return;
        }

        try
        {
            Action action = HotkeyAttributeHandlerShared.BuildAction(method, assembly);
            string storageKey = HotkeyAttributeHandlerShared.ResolveHotkeyKey(hotkeyAttribute.Key, method);
            JmcKeyBinding currentValue = new(
                hotkeyAttribute.AllowKeyboard ? hotkeyAttribute.DefaultKeyboard : Key.None,
                hotkeyAttribute.AllowController ? hotkeyAttribute.DefaultController : string.Empty,
                hotkeyAttribute.AllowKeyboard ? hotkeyAttribute.DefaultModifiers : JmcKeyModifiers.None);

            string entryKey = ConfigManager.RegisterConfig(
                hotkeyAttribute.DisplayName,
                () => currentValue,
                value => currentValue = value,
                hotkeyAttribute.Group,
                uiAttribute: new UIKeybindAttribute(hotkeyAttribute.AllowController, hotkeyAttribute.AllowKeyboard),
                storageKey: storageKey,
                locTable: hotkeyAttribute.LocTable,
                displayNameKey: hotkeyAttribute.DisplayNameKey,
                groupKey: hotkeyAttribute.GroupKey,
                description: hotkeyAttribute.Description,
                descriptionKey: hotkeyAttribute.DescriptionKey,
                order: hotkeyAttribute.Order,
                restartRequired: hotkeyAttribute.RestartRequired,
                assembly: assembly);

            JmcHotkeyManager.RegisterInternal(
                assembly,
                entryKey,
                () => currentValue,
                action,
                new HotkeyOptions(
                    hotkeyAttribute.ConsumeInput,
                    hotkeyAttribute.ExactModifiers,
                    hotkeyAttribute.AllowEcho,
                    hotkeyAttribute.DebounceMs));
            if (hotkeyAttribute.AllowController)
            {
                JmcInputActionRegistry.BindHotkeyToEntryKey(assembly, entryKey, entryKey);
            }
        }
        catch (Exception ex)
        {
            ModLogger.Error(
                $"Failed to register UI hotkey for {method.DeclaringType.FullName}.{method.Name}",
                ex,
                assembly);
        }
    }
}

internal static class HotkeyAttributeHandlerShared
{
    public static Action BuildAction(MethodAccessor method, Assembly assembly)
    {
        MethodInfo methodInfo = method.MemberInfo;
        if (!HotkeyMethodValidator.IsValidMethod(methodInfo, out LogLevel? level, out string? errorMessage))
        {
            throw new ArgumentException($"Invalid hotkey method {methodInfo.DeclaringType?.FullName}.{methodInfo.Name}: {errorMessage}");
        }

        LogValidation(level, errorMessage, assembly);
        return method.TypedDelegate is Action typedAction ? typedAction : method.InvokeStaticVoid;
    }

    public static string ResolveHotkeyKey(string? key, MethodAccessor method)
    {
        MethodInfo methodInfo = method.MemberInfo;
        Type declaringType = methodInfo.DeclaringType
            ?? throw new ArgumentException($"Hotkey method {methodInfo.Name} does not have a declaring type.");

        return string.IsNullOrWhiteSpace(key)
            ? ConfigEntry.CreateStorageKey(declaringType, methodInfo.Name)
            : key.Trim();
    }

    private static void LogValidation(LogLevel? level, string? message, Assembly assembly)
    {
        if (level == null || string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        if (level.Value >= LogLevel.Error)
        {
            ModLogger.Error(message, assembly);
            return;
        }

        if (level.Value >= LogLevel.Warn)
        {
            ModLogger.Warn(message, assembly);
            return;
        }

        ModLogger.Info(message, assembly);
    }
}
