using Godot;
using MegaCrit.Sts2.Core.Logging;
using System.Reflection;

namespace JmcModLib.Config.UI;

/// <summary>
/// 将一个静态无参方法绑定到已有的热键配置成员。
/// </summary>
/// <param name="bindingMember">保存热键值的静态字段或静态属性名称，类型必须是 <see cref="Key"/> 或 <see cref="JmcKeyBinding"/>。</param>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class JmcHotkeyAttribute(string bindingMember) : Attribute
{
    /// <summary>
    /// 保存热键值的静态字段或静态属性名称。
    /// </summary>
    public string BindingMember { get; } = bindingMember;

    /// <summary>
    /// 运行时注册键；留空时会根据方法名自动生成。
    /// </summary>
    public string? Key { get; set; }

    /// <summary>
    /// 触发热键后是否吃掉本次输入，默认吃掉。
    /// </summary>
    public bool ConsumeInput { get; set; } = true;

    /// <summary>
    /// 是否要求组合键修饰键完全一致，默认要求完全一致。
    /// </summary>
    public bool ExactModifiers { get; set; } = true;

    /// <summary>
    /// 是否允许键盘长按产生的 Echo 输入触发热键。
    /// </summary>
    public bool AllowEcho { get; set; }

    /// <summary>
    /// 热键防抖时间，单位为毫秒。
    /// </summary>
    public ulong DebounceMs { get; set; } = 150;
}

/// <summary>
/// 创建一个可在设置界面修改的热键项，并将其绑定到静态无参方法。
/// </summary>
/// <param name="displayName">设置界面中显示的回退名称。</param>
/// <param name="group">设置项所属分组。</param>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class UIHotkeyAttribute(
    string displayName,
    string group = ConfigAttribute.DefaultGroup) : Attribute
{
    /// <summary>
    /// 设置界面中显示的回退名称。
    /// </summary>
    public string DisplayName { get; } = displayName;

    /// <summary>
    /// 设置项所属分组。
    /// </summary>
    public string Group { get; } = group;

    /// <summary>
    /// 配置存储键；留空时会根据方法名自动生成。
    /// </summary>
    public string? Key { get; set; }

    /// <summary>
    /// 设置项说明的回退文本。
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// 本地化表名；留空时使用 JML 默认设置表。
    /// </summary>
    public string? LocTable { get; set; }

    /// <summary>
    /// 显示名称的本地化键。
    /// </summary>
    public string? DisplayNameKey { get; set; }

    /// <summary>
    /// 说明文本的本地化键。
    /// </summary>
    public string? DescriptionKey { get; set; }

    /// <summary>
    /// 分组名称的本地化键。
    /// </summary>
    public string? GroupKey { get; set; }

    /// <summary>
    /// 设置项排序值，数值越小越靠前。
    /// </summary>
    public int Order { get; set; }

    /// <summary>
    /// 修改后是否需要重启游戏或重新进入相关流程才完全生效。
    /// </summary>
    public bool RestartRequired { get; set; }

    /// <summary>
    /// 默认键盘按键。
    /// </summary>
    public Key DefaultKeyboard { get; set; } = Godot.Key.None;

    /// <summary>
    /// 默认键盘组合修饰键。
    /// </summary>
    public JmcKeyModifiers DefaultModifiers { get; set; }

    /// <summary>
    /// 默认手柄输入 Action 名称。
    /// </summary>
    public string DefaultController { get; set; } = string.Empty;

    /// <summary>
    /// 是否允许在设置界面绑定键盘按键。
    /// </summary>
    public bool AllowKeyboard { get; set; } = true;

    /// <summary>
    /// 是否允许手柄绑定；Steam Input 可用时，JML 会为此热键生成 Steam Digital Action，实际手柄按键建议在 Steam 输入中绑定。
    /// </summary>
    public bool AllowController { get; set; }

    /// <summary>
    /// 触发热键后是否吃掉本次输入，默认吃掉。
    /// </summary>
    public bool ConsumeInput { get; set; } = true;

    /// <summary>
    /// 是否要求组合键修饰键完全一致，默认要求完全一致。
    /// </summary>
    public bool ExactModifiers { get; set; } = true;

    /// <summary>
    /// 是否允许键盘长按产生的 Echo 输入触发热键。
    /// </summary>
    public bool AllowEcho { get; set; }

    /// <summary>
    /// 热键防抖时间，单位为毫秒。
    /// </summary>
    public ulong DebounceMs { get; set; } = 150;
}

internal static class HotkeyMethodValidator
{
    public static bool IsValidMethod(MethodInfo method, out LogLevel? level, out string? errorMessage)
    {
        ArgumentNullException.ThrowIfNull(method);

        level = null;
        errorMessage = null;

        if (!method.IsStatic)
        {
            level = LogLevel.Error;
            errorMessage = "Hotkey method must be static.";
            return false;
        }

        ParameterInfo[] parameters = method.GetParameters();
        if (parameters.Length != 0)
        {
            level = LogLevel.Error;
            errorMessage = $"Hotkey method must have no parameters, but {parameters.Length} were found.";
            return false;
        }

        if (method.ReturnType != typeof(void))
        {
            level = LogLevel.Warn;
            errorMessage = $"Hotkey method {method.Name} should return void. The return value will be ignored.";
        }

        return true;
    }
}
