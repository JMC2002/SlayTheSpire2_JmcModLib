using HarmonyLib;
using MegaCrit.Sts2.Core.ControllerInput;

namespace JmcModLib.Input;

/// <summary>
/// Steam Input 初始化补丁：必须在游戏调用 SteamInput.Init 前安装 JML 生成的 manifest。
/// </summary>
[HarmonyPatch(typeof(SteamControllerInputStrategy), nameof(SteamControllerInputStrategy.Init))]
internal static class SteamInputPatches
{
    public static void Prefix()
    {
        JmcSteamInputManifestInstaller.InstallBeforeSteamInputInit();
    }
}
