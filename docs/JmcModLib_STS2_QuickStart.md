# JmcModLib STS2 快速入门

版本基准：JmcModLib 1.0.96

本文面向第一次接入 JmcModLib 的 STS2 子 MOD 作者。推荐原则只有一条：能自动推断就自动推断。普通子 MOD 不需要手动传 MOD 名称、版本、程序集，也不需要手动初始化日志或配置系统。

完整 API 参考见 [JmcModLib_STS2_API.md](JmcModLib_STS2_API.md)。完整示例可参考 [JmcModLibDemo/MainFile.cs](../../JmcModLibDemo/MainFile.cs) 和 [JmcModLibDemo/Core/DemoSettings.cs](../../JmcModLibDemo/Core/DemoSettings.cs)。

## 1. 注册入口

在 MOD 入口里调用：

```csharp
using Godot;
using JmcModLib.Core;
using JmcModLib.Utils;
using MegaCrit.Sts2.Core.Modding;

namespace MyMod;

[ModInitializer(nameof(Initialize))]
public partial class MainFile : Node
{
    public static void Initialize()
    {
        ModRegistry.Register<MainFile>();

        ModLogger.Info("MyMod initialized.");
    }
}
```

`MainFile` 用来定位当前 MOD 程序集。JML 会自动推断 MOD ID、显示名和版本，并自动完成日志、配置系统和 Attribute 扫描。

如果需要在 Attribute 扫描前补充手动按钮或自定义存储（通常用于子MOD托管），使用同名流式入口：

```csharp
ModRegistry.Register<MainFile>(true)?
    .RegisterButton(
        "刷新缓存",
        ReloadCache,
        "执行",
        group: "调试",
        storageKey: "button.reload_cache")
    .Done();
```

`Register<MainFile>(true)` 会返回 builder；只有调用 `.Done()` 后才会触发 Attribute 扫描。

## 2. 配置与设置 UI

推荐用 Attribute 声明静态字段或静态属性。`ModRegistry.Register<MainFile>()` 会自动扫描它们、读取配置文件、写回默认值并生成设置 UI。

```csharp
using JmcModLib.Config;
using JmcModLib.Config.UI;

namespace MyMod;

public static class MySettings
{
    [UIToggle]
    [Config(
        "启用功能",
        group: "基础",
        Description = "关闭后跳过本 MOD 的主要逻辑。",
        Key = "general.enabled",
        Order = 10)]
    public static bool Enabled = true;

    [UIInput(32)]
    [Config(
        "显示文本",
        group: "基础",
        Description = "显示在自定义面板上的文本。",
        Key = "general.display_text",
        Order = 20)]
    public static string DisplayText = "Hello JML";

    [UIDropdown("Small", "Normal", "Large")]
    [Config(
        "界面尺寸",
        group: "外观",
        Description = "控制自定义界面的整体尺寸。",
        Key = "appearance.size",
        Order = 10)]
    public static string Size = "Normal";

    [UISlider(0.5, 2.0, 0.1)]
    [Config(
        "缩放倍率",
        group: "外观",
        Description = "影响自定义界面的缩放。",
        Key = "appearance.scale",
        Order = 20)]
    public static double Scale = 1.0;
}
```

大多数配置不需要 `onChanged`。只有需要刷新缓存、重建界面、通知游戏对象时，再给 `[Config]` 设置回调方法名。

```csharp
[UIToggle]
[Config(
    "启用调试输出",
    onChanged: nameof(OnDebugChanged),
    group: "调试",
    Key = "debug.enabled")]
public static bool DebugEnabled = false;

private static void OnDebugChanged(bool enabled)
{
    ModLogger.Info($"调试输出：{enabled}");
}
```

## 3. 按钮

推荐用 `[UIButton]` 声明无参数静态方法。它会自动出现在 MOD 设置 UI 中。

```csharp
using JmcModLib.Config.UI;
using JmcModLib.Utils;

public static class MySettings
{
    [UIButton(
        "清理缓存",
        "执行",
        "调试",
        Key = "button.clear_cache",
        HelpText = "清理本 MOD 的运行期缓存。",
        Color = UIButtonColor.Gold,
        Order = 10)]
    public static void ClearCache()
    {
        ModLogger.Info("缓存已清理。");
    }
}
```

只有按钮来自启动期动态条件，或者不适合写成 Attribute 时，才用 `Register<MainFile>(true)?.RegisterButton(...).Done()`。

## 4. 热键与 Steam Input

键盘热键推荐拆成“配置值 + 行为方法”：用户能在设置 UI 里改按键，运行时由 `[JmcHotkey]` 触发动作。

```csharp
using Godot;
using JmcModLib.Config;
using JmcModLib.Config.UI;
using JmcModLib.Utils;

public static class MySettings
{
    [UIKeybind]
    [Config(
        "打开面板",
        group: "热键",
        Description = "按下后打开或关闭自定义面板。",
        Key = "keybind.open_panel",
        Order = 10)]
    public static Key OpenPanelKey = Key.F8;

    [JmcHotkey(nameof(OpenPanelKey), ConsumeInput = false)]
    public static void TogglePanel()
    {
        ModLogger.Info("打开或关闭面板。");
    }
}
```

如果希望一行生成设置项、运行时热键，并额外暴露给 Steam Input，使用 `[UIHotkey]`：

```csharp
[UIHotkey(
    "打开面板",
    "热键",
    Key = "keybind.open_panel_steam",
    Description = "同时支持键盘和 Steam Input。",
    DefaultKeyboard = Key.F9,
    DefaultController = "controller_right_trigger",
    AllowController = true,
    ConsumeInput = false,
    Order = 20)]
public static void TogglePanelFromSteamInput()
{
    ModLogger.Info("Steam Input 热键触发。");
}
```

## 5. 本地化

推荐使用约定 key，不要在每个 Attribute 上手动写本地化 key。把设置 UI 文本放到：

```text
MyMod/MyMod/localization/zhs/settings_ui.json
MyMod/MyMod/localization/eng/settings_ui.json
```

常用 key 约定：

```text
EXTENSION.JMCMODLIB.CONFIG.<ModId>.<StorageKey>.NAME
EXTENSION.JMCMODLIB.CONFIG.<ModId>.<StorageKey>.DESCRIPTION
EXTENSION.JMCMODLIB.CONFIG.<ModId>.<StorageKey>.BUTTON
EXTENSION.JMCMODLIB.CONFIG.<ModId>.GROUP.<GroupName>
```

示例：

```json
{
  "EXTENSION.JMCMODLIB.CONFIG.MyMod.general.enabled.NAME": "启用功能",
  "EXTENSION.JMCMODLIB.CONFIG.MyMod.general.enabled.DESCRIPTION": "关闭后跳过本 MOD 的主要逻辑。",
  "EXTENSION.JMCMODLIB.CONFIG.MyMod.GROUP.general": "基础"
}
```

只有业务代码需要主动创建游戏 `LocString` 时，再使用 `L10n.Create(...)` 或游戏原生 `LocString`。

## 6. 日志

入口注册后可以直接使用 `ModLogger`。不需要单独初始化日志。

```csharp
ModLogger.Info("普通信息");
ModLogger.Warn("可恢复的问题");
ModLogger.Error("需要排查的错误");
ModLogger.Error("带异常的错误", exception);
```

JML 会自动按调用方程序集解析日志上下文，输出时带上对应 MOD 标签。

## 7. 原生确认弹窗

需要确认、提示或单按钮弹窗时，使用 `JmcConfirmationPopup`。推荐正文使用本地化文本。

```csharp
using JmcModLib.Prefabs;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;

[UIButton(
    "危险操作",
    "执行",
    "调试",
    Key = "button.dangerous_action",
    HelpText = "执行前会弹出确认框。",
    Color = UIButtonColor.Red)]
public static void RunDangerousAction()
{
    TaskHelper.RunSafely(RunDangerousActionAsync());
}

private static async Task RunDangerousActionAsync()
{
    bool confirmed = await JmcConfirmationPopup.ShowConfirmationAsync(
        new LocString("settings_ui", "EXTENSION.MYMOD.POPUP.DANGER.title"),
        new LocString("settings_ui", "EXTENSION.MYMOD.POPUP.DANGER.body"),
        new LocString("settings_ui", "EXTENSION.MYMOD.POPUP.DANGER.confirm"),
        new LocString("settings_ui", "EXTENSION.MYMOD.POPUP.DANGER.cancel"));

    if (!confirmed)
    {
        return;
    }

    ModLogger.Info("危险操作已确认。");
}
```

## 下一步

- 查符号、参数和扩展点：读 [API 参考手册](JmcModLib_STS2_API.md)。
- 看完整可运行示例：读 [JmcModLibDemo/Core/DemoSettings.cs](../../JmcModLibDemo/Core/DemoSettings.cs)。
- 特殊场景才考虑显式元数据注册、自定义配置存储、反射访问器和运行时信息 API。
