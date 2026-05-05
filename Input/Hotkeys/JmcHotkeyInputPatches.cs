using Godot;
using HarmonyLib;
using JmcModLib.Input;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.CommonUi;

namespace JmcModLib.Config.UI;

[HarmonyPatch(typeof(NInputManager), nameof(NInputManager._UnhandledKeyInput))]
internal static class JmcHotkeyKeyboardInputPatch
{
    [HarmonyPostfix]
    private static void Postfix(NInputManager __instance, InputEvent inputEvent)
    {
        if (inputEvent is InputEventKey)
        {
            JmcHotkeyInputPatchDispatcher.Handle(__instance, inputEvent);
        }
    }
}

[HarmonyPatch(typeof(NInputManager), nameof(NInputManager._UnhandledInput))]
internal static class JmcHotkeyControllerInputPatch
{
    [HarmonyPostfix]
    private static void Postfix(NInputManager __instance, InputEvent inputEvent)
    {
        if (inputEvent is not InputEventKey)
        {
            JmcHotkeyInputPatchDispatcher.Handle(__instance, inputEvent);
        }
    }
}

internal static class JmcHotkeyInputPatchDispatcher
{
    private static int activeLogWritten;

    public static void Handle(Node inputOwner, InputEvent inputEvent)
    {
        if (!JmcHotkeyManager.IsInitialized)
        {
            return;
        }

        try
        {
            if (NGame.Instance?.Transition.InTransition == true)
            {
                return;
            }

            if (Interlocked.Exchange(ref activeLogWritten, 1) == 0)
            {
                ModLogger.Info("JmcModLib hotkey input bridge active.");
            }

            _ = JmcHotkeyManager.HandleInput(inputEvent, inputOwner.GetViewport());
        }
        catch (Exception ex)
        {
            ModLogger.Error("JmcModLib hotkey input bridge failed.", ex);
        }
    }
}

[HarmonyPatch(typeof(NControllerManager), nameof(NControllerManager._Process))]
internal static class JmcHotkeyProcessPatch
{
    [HarmonyPostfix]
    private static void Postfix()
    {
        if (!JmcHotkeyManager.IsInitialized)
        {
            return;
        }

        try
        {
            JmcInputManager.Process();
        }
        catch (Exception ex)
        {
            ModLogger.Error("JmcModLib input backend process failed.", ex);
        }
    }
}
