using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens.PauseMenu;
using MegaCrit.Sts2.Core.Runs;
using System.Reflection;

namespace JmcModLib.UI.PauseMenu;

/// <summary>
/// 暂停菜单按钮在可见性判断、启用判断和点击回调中使用的上下文。
/// </summary>
/// <remarks>
/// <para>
/// 普通 MOD 通常只需要读取运行状态属性。<see cref="Menu"/> 和 <see cref="Button"/> 暴露的是原生节点，
/// 修改它们可能影响暂停菜单行为，请仅在确有需要时使用。
/// </para>
/// </remarks>
public sealed class PauseMenuButtonContext
{
    internal PauseMenuButtonContext(
        ModContext mod,
        IRunState? runState,
        NPauseMenu menu,
        NButton button)
    {
        Mod = mod;
        Assembly = mod.Assembly;
        RunState = runState;
        Menu = menu;
        Button = button;
        IsMultiplayerClient = ReadIsMultiplayerClient();
        IsRunInProgress = ReadIsRunInProgress();
        IsGameOver = runState?.IsGameOver ?? ReadIsGameOver();
    }

    /// <summary>
    /// 按钮所属 MOD 的注册上下文。
    /// </summary>
    public ModContext Mod { get; }

    /// <summary>
    /// 按钮所属 MOD 的托管程序集。
    /// </summary>
    public Assembly Assembly { get; }

    /// <summary>
    /// 当前暂停菜单绑定的运行状态；某些生命周期刷新中可能为空。
    /// </summary>
    public IRunState? RunState { get; }

    /// <summary>
    /// 当前原生暂停菜单节点。
    /// </summary>
    public NPauseMenu Menu { get; }

    /// <summary>
    /// 当前按钮节点。
    /// </summary>
    public NButton Button { get; }

    /// <summary>
    /// 当前运行是否为多人客户端。
    /// </summary>
    public bool IsMultiplayerClient { get; }

    /// <summary>
    /// 当前是否处于运行中。
    /// </summary>
    public bool IsRunInProgress { get; }

    /// <summary>
    /// 当前运行是否已经进入游戏结束状态。
    /// </summary>
    public bool IsGameOver { get; }

    private static bool ReadIsMultiplayerClient()
    {
        try
        {
            return RunManager.Instance?.NetService?.Type == NetGameType.Client;
        }
        catch
        {
            return false;
        }
    }

    private static bool ReadIsRunInProgress()
    {
        try
        {
            return RunManager.Instance?.IsInProgress == true;
        }
        catch
        {
            return false;
        }
    }

    private static bool ReadIsGameOver()
    {
        try
        {
            return RunManager.Instance?.IsGameOver == true;
        }
        catch
        {
            return false;
        }
    }
}
