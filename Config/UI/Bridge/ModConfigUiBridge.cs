using Godot;
using HarmonyLib;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Screens.ModdingScreen;

namespace JmcModLib.Config.UI;

internal static class ModConfigUiBridge
{
    private const string LinkPrefix = "jmcmodlib://config/";
    private static readonly StringName HookedMetaKey = new("jmcmodlib_config_link_hooked");

    internal static bool HasConfig(Mod? mod)
    {
        if (mod?.assembly == null)
        {
            return false;
        }

        return ConfigManager.GetEntries(mod.assembly).Count > 0;
    }

    internal static void UpdateModInfoContainer(NModInfoContainer container, Mod mod)
    {
        _ = container;
        _ = mod;
        // Intentionally left blank. Config now lives in Settings > 模组设置.
    }

    private static void EnsureLinkHook(MegaRichTextLabel descriptionLabel)
    {
        if (descriptionLabel.HasMeta(HookedMetaKey))
        {
            return;
        }

        descriptionLabel.MetaClicked += OnMetaClicked;
        descriptionLabel.SetMeta(HookedMetaKey, true);
    }

    private static void OnMetaClicked(Variant meta)
    {
        string raw = meta.ToString();
        if (string.IsNullOrWhiteSpace(raw) || !raw.StartsWith(LinkPrefix, StringComparison.Ordinal))
        {
            return;
        }

        string modId = raw[LinkPrefix.Length..];
        if (string.IsNullOrWhiteSpace(modId))
        {
            return;
        }

        Mod? mod = ModManager.Mods.FirstOrDefault(m =>
            string.Equals(m.manifest?.id, modId, StringComparison.OrdinalIgnoreCase));
        if (mod == null || !HasConfig(mod))
        {
            return;
        }

        NModalContainer? modalContainer = NModalContainer.Instance;
        if (modalContainer == null)
        {
            ModLogger.Warn("Could not open config popup because NModalContainer was not available.");
            return;
        }

        if (modalContainer.OpenModal != null)
        {
            return;
        }

        modalContainer.Add(ModConfigPopup.Create(mod));
    }

    private static string BuildLink(string modId)
    {
        return $"{LinkPrefix}{modId}";
    }
}

[HarmonyPatch(typeof(NModInfoContainer), nameof(NModInfoContainer.Fill))]
internal static class ModConfigUiBridgePatches
{
    [HarmonyPostfix]
    private static void Postfix(NModInfoContainer __instance, Mod mod)
    {
        ModConfigUiBridge.UpdateModInfoContainer(__instance, mod);
    }
}
