using JmcModLib.Config.Storage;
using JmcModLib.Config.UI;
using JmcModLib.Reflection;
using MegaCrit.Sts2.Core.Logging;
using System.Reflection;

namespace JmcModLib.Config.Entry;

internal sealed class ButtonEntry : ConfigEntry
{
    private readonly Action action;

    private ButtonEntry(
        Assembly assembly,
        string storageKey,
        string group,
        string displayName,
        string buttonText,
        string? buttonTextKey,
        UIButtonColor color,
        Action action,
        ConfigAttribute attribute)
        : base(assembly, storageKey, group, displayName, attribute, null)
    {
        this.action = action ?? throw new ArgumentNullException(nameof(action));
        ButtonText = buttonText;
        ButtonTextKey = buttonTextKey;
        Color = color;
    }

    public string ButtonText { get; }

    public string? ButtonTextKey { get; }

    public UIButtonColor Color { get; }

    public override Type ValueType => typeof(void);

    public override object? DefaultValue => null;

    public static ButtonEntry FromMethod(Assembly assembly, MethodAccessor method, UIButtonAttribute attribute)
    {
        ArgumentNullException.ThrowIfNull(method);
        ArgumentNullException.ThrowIfNull(attribute);

        MethodInfo methodInfo = method.MemberInfo;
        if (!UIButtonAttribute.IsValidMethod(methodInfo, out LogLevel? level, out string? errorMessage))
        {
            throw new ArgumentException($"Invalid UIButton method {methodInfo.DeclaringType?.FullName}.{methodInfo.Name}: {errorMessage}");
        }

        LogValidation(level, errorMessage, assembly);

        Action action = method.TypedDelegate is Action typedAction
            ? typedAction
            : method.InvokeStaticVoid;

        Type declaringType = methodInfo.DeclaringType
            ?? throw new ArgumentException($"UIButton method {methodInfo.Name} does not have a declaring type.");
        string storageKey = ResolveStorageKey(attribute.Key, declaringType, methodInfo.Name);
        string group = ResolveGroup(attribute.Group);
        string displayName = ResolveDisplayName(attribute.Description, methodInfo.Name);

        return Create(
            assembly,
            storageKey,
            group,
            displayName,
            attribute.ButtonText,
            attribute.ButtonTextKey,
            attribute.Color,
            action,
            attribute.HelpText,
            attribute.LocTable,
            attribute.DisplayNameKey,
            attribute.DescriptionKey,
            attribute.GroupKey,
            attribute.Order);
    }

    public static ButtonEntry Create(
        Assembly assembly,
        string displayName,
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
        ArgumentNullException.ThrowIfNull(assembly);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);

        return Create(
            assembly,
            string.IsNullOrWhiteSpace(storageKey) ? displayName.Trim() : storageKey.Trim(),
            ResolveGroup(group),
            displayName.Trim(),
            buttonText,
            buttonTextKey,
            color,
            action,
            helpText,
            locTable,
            displayNameKey,
            helpTextKey,
            groupKey,
            order);
    }

    public void Invoke()
    {
        action.Invoke();
    }

    public override object? GetValue()
    {
        return null;
    }

    public override void SetValue(object? value)
    {
        Invoke();
    }

    public override bool Reset()
    {
        return false;
    }

    internal override void SyncFromStorage(IConfigStorage storage)
    {
    }

    internal override void SyncFromSource(IConfigStorage storage)
    {
    }

    private static ButtonEntry Create(
        Assembly assembly,
        string storageKey,
        string group,
        string displayName,
        string buttonText,
        string? buttonTextKey,
        UIButtonColor color,
        Action action,
        string? helpText,
        string? locTable,
        string? displayNameKey,
        string? helpTextKey,
        string? groupKey,
        int order)
    {
        var descriptor = new ConfigAttribute(displayName, group: group)
        {
            Key = storageKey,
            Description = helpText,
            LocTable = locTable,
            DisplayNameKey = displayNameKey,
            DescriptionKey = helpTextKey,
            GroupKey = groupKey,
            Order = order
        };

        return new ButtonEntry(
            assembly,
            storageKey,
            group,
            displayName,
            string.IsNullOrWhiteSpace(buttonText) ? "按钮" : buttonText.Trim(),
            buttonTextKey,
            color,
            action,
            descriptor);
    }

    private static string ResolveStorageKey(string? key, Type declaringType, string methodName)
    {
        return string.IsNullOrWhiteSpace(key)
            ? ConfigEntry.CreateStorageKey(declaringType, methodName)
            : key.Trim();
    }

    private static string ResolveGroup(string? group)
    {
        return string.IsNullOrWhiteSpace(group) ? ConfigAttribute.DefaultGroup : group.Trim();
    }

    private static string ResolveDisplayName(string? displayName, string fallbackName)
    {
        return string.IsNullOrWhiteSpace(displayName) ? fallbackName : displayName.Trim();
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
