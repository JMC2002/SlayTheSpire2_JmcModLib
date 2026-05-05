using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Modding;
using System.Reflection;

namespace JmcModLib;

[ModInitializer(nameof(Initialize))]
public partial class MainFile : Node
{
    public static void Initialize()
    {
        ModRegistry.Register(true, VersionInfo.Name, VersionInfo.Name, VersionInfo.Version)?
            .Done();

        ModLogger.Info("======================================");
        ModLogger.Info($" {VersionInfo.Name} Mod 正在启动...");
        ModLogger.Info("======================================");

        Harmony harmony = new(VersionInfo.Name);
        harmony.PatchAll(Assembly.GetExecutingAssembly());
        ModLogger.Info("Harmony 补丁已应用。");
    }
}
