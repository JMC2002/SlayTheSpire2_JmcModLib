using JmcModLib.Config.UI;
using MegaCrit.Sts2.Core.Platform.Steam;
using Steamworks;

namespace JmcModLib.Input;

/// <summary>
/// Steam Input Digital Action 后端，用于轮询 JML 生成的逻辑动作并分发到热键系统。
/// </summary>
internal sealed class SteamInputBackend : IJmcInputBackend
{
    private const int MaxControllers = 16;

    private readonly Dictionary<string, InputDigitalActionHandle_t> actionHandles = new(StringComparer.Ordinal);
    private readonly HashSet<string> pressedActions = new(StringComparer.Ordinal);
    private bool handleCacheDirty = true;
    private bool unavailableLogged;
    private bool waitingControllerLogged;

    /// <inheritdoc />
    public string Name => "SteamInput";

    /// <inheritdoc />
    public void Initialize()
    {
        JmcInputActionRegistry.ActionsChanged += OnActionsChanged;
    }

    /// <inheritdoc />
    public void Process()
    {
        if (!JmcSteamInputManifestInstaller.IsManifestInstalled || !SteamInitializer.Initialized)
        {
            ReleaseAll();
            return;
        }

        IReadOnlyList<JmcInputActionDescriptor> actions = JmcInputActionRegistry.GetActions();
        if (actions.Count == 0)
        {
            ReleaseAll();
            return;
        }

        try
        {
            if (!TryGetController(out InputHandle_t controllerHandle))
            {
                ReleaseAll();
                LogWaitingControllerOnce();
                return;
            }

            waitingControllerLogged = false;
            EnsureActionHandles(actions);
            foreach (JmcInputActionDescriptor action in actions)
            {
                if (!actionHandles.TryGetValue(action.ActionId, out InputDigitalActionHandle_t actionHandle)
                    || actionHandle.Equals(default))
                {
                    continue;
                }

                InputDigitalActionData_t data = SteamInput.GetDigitalActionData(controllerHandle, actionHandle);
                bool pressed = data.bState == 1;
                bool wasPressed = pressedActions.Contains(action.ActionId);
                if (pressed == wasPressed)
                {
                    continue;
                }

                if (pressed)
                {
                    _ = pressedActions.Add(action.ActionId);
                    ModLogger.Info($"JML Steam Input 动作触发：{action.ActionId}", action.Assembly);
                }
                else
                {
                    _ = pressedActions.Remove(action.ActionId);
                }

                _ = JmcHotkeyManager.HandleInputAction(action.ActionId, pressed, null);
            }
        }
        catch (InvalidOperationException ex)
        {
            ReleaseAll();
            LogUnavailableOnce($"Steam Input 暂不可用，JML 热键已回退到 Godot 输入事件：{ex.Message}");
        }
        catch (Exception ex)
        {
            ReleaseAll();
            ModLogger.Error("轮询 JML Steam Input 动作失败。", ex);
        }
    }

    /// <inheritdoc />
    public void Shutdown()
    {
        JmcInputActionRegistry.ActionsChanged -= OnActionsChanged;
        actionHandles.Clear();
        pressedActions.Clear();
    }

    private void EnsureActionHandles(IReadOnlyList<JmcInputActionDescriptor> actions)
    {
        if (!handleCacheDirty)
        {
            return;
        }

        actionHandles.Clear();
        int cachedCount = 0;
        int missingCount = 0;
        foreach (JmcInputActionDescriptor action in actions)
        {
            try
            {
                InputDigitalActionHandle_t handle = SteamInput.GetDigitalActionHandle(action.ActionId);
                actionHandles[action.ActionId] = handle;
                if (handle.Equals(default))
                {
                    missingCount++;
                    ModLogger.Warn($"Steam Input 未返回 JML 动作句柄：{action.ActionId}", action.Assembly);
                }
                else
                {
                    cachedCount++;
                }
            }
            catch (InvalidOperationException ex)
            {
                missingCount++;
                ModLogger.Warn($"无法获取 JML Steam Input 动作句柄：{action.ActionId}。{ex.Message}", action.Assembly);
            }
        }

        ModLogger.Info($"JML Steam Input 动作句柄缓存完成：成功 {cachedCount}，失败 {missingCount}。");
        handleCacheDirty = false;
    }

    private static bool TryGetController(out InputHandle_t controllerHandle)
    {
        InputHandle_t[] controllers = new InputHandle_t[MaxControllers];
        int count = SteamInput.GetConnectedControllers(controllers);
        if (count <= 0)
        {
            controllerHandle = default;
            return false;
        }

        controllerHandle = controllers[0];
        return true;
    }

    private void ReleaseAll()
    {
        foreach (string actionId in pressedActions.ToArray())
        {
            _ = JmcHotkeyManager.HandleInputAction(actionId, pressed: false, null);
        }

        pressedActions.Clear();
    }

    private void OnActionsChanged()
    {
        handleCacheDirty = true;
    }

    private void LogUnavailableOnce(string message)
    {
        if (unavailableLogged)
        {
            return;
        }

        unavailableLogged = true;
        ModLogger.Warn(message);
    }

    private void LogWaitingControllerOnce()
    {
        if (waitingControllerLogged)
        {
            return;
        }

        waitingControllerLogged = true;
        ModLogger.Info("JML Steam Input 后端正在等待 Steam 控制器句柄。");
    }
}
