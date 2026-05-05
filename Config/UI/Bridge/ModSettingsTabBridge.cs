using Godot;
using HarmonyLib;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens.Settings;
using System.Reflection;

namespace JmcModLib.Config.UI;

internal static class ModSettingsTabBridge
{
    private static readonly FieldInfo TabsField = AccessTools.Field(typeof(NSettingsTabManager), "_tabs")
        ?? throw new MissingFieldException(typeof(NSettingsTabManager).FullName, "_tabs");

    private static readonly MethodInfo SwitchTabToMethod = AccessTools.Method(typeof(NSettingsTabManager), "SwitchTabTo")
        ?? throw new MissingMethodException(typeof(NSettingsTabManager).FullName, "SwitchTabTo");

    private const string TabName = "JmcModLibModSettingsTab";
    private static readonly StringName InstalledMetaKey = new("jmcmodlib_mod_settings_tab_installed");

    internal static void Install(NSettingsScreen screen)
    {
        if (screen == null)
        {
            return;
        }

        NSettingsTabManager? manager = screen.GetNodeOrNull<NSettingsTabManager>("%SettingsTabManager");
        NSettingsTab? templateTab = manager?.GetNodeOrNull<NSettingsTab>("Input");
        NSettingsPanel? templatePanel = screen.GetNodeOrNull<NSettingsPanel>("%InputSettings");

        if (manager == null || templateTab == null || templatePanel == null)
        {
            ModLogger.Warn("Failed to install the mod settings tab because the settings screen layout did not match expectations.");
            return;
        }

        if (TabsField.GetValue(manager) is not Dictionary<NSettingsTab, NSettingsPanel> tabs)
        {
            ModLogger.Warn("Failed to install the mod settings tab because the tab lookup table was unavailable.");
            return;
        }

        NSettingsTab? existingTab = tabs.Keys.FirstOrDefault(tab => tab.Name == TabName);
        if (existingTab != null)
        {
            ApplyLabel(existingTab, ModSettingsText.TabLabel());
            screen.SetMeta(InstalledMetaKey, true);
            return;
        }

        NSettingsTab newTab = (NSettingsTab)templateTab.Duplicate();
        newTab.Name = TabName;
        newTab.Deselect();
        newTab.Position = ComputeTabPosition(manager);
        manager.AddChild(newTab);
        manager.MoveChild(newTab, templateTab.GetIndex() + 1);
        ApplyLabel(newTab, ModSettingsText.TabLabel());
        newTab.Connect(NClickableControl.SignalName.Released, Callable.From<NButton>(_ => SwitchTo(manager, newTab)));

        ModSettingsPanel newPanel = ModSettingsPanel.Create();
        templatePanel.GetParent().AddChild(newPanel);
        CopyPanelLayout(templatePanel, newPanel);
        newPanel.Visible = false;

        tabs.Add(newTab, newPanel);
        screen.SetMeta(InstalledMetaKey, true);
    }

    private static void SwitchTo(NSettingsTabManager manager, NSettingsTab tab)
    {
        _ = SwitchTabToMethod.Invoke(manager, [tab]);
    }

    private static void ApplyLabel(NSettingsTab tab, string label)
    {
        MegaLabel? megaLabel = tab.GetNodeOrNull<MegaLabel>("Label");
        if (megaLabel != null)
        {
            megaLabel.SetTextAutoSize(label);
            return;
        }

        tab.CallDeferred(NSettingsTab.MethodName.SetLabel, label);
    }

    private static Vector2 ComputeTabPosition(NSettingsTabManager manager)
    {
        List<NSettingsTab> tabs = [.. manager
            .GetChildren()
            .OfType<NSettingsTab>()
            .OrderBy(static tab => tab.Position.X)];

        if (tabs.Count == 0)
        {
            return Vector2.Zero;
        }

        if (tabs.Count == 1)
        {
            return tabs[0].Position + new Vector2(tabs[0].Size.X + 20f, 0f);
        }

        NSettingsTab last = tabs[^1];
        NSettingsTab previous = tabs[^2];
        float deltaX = Math.Max(last.Size.X + 20f, last.Position.X - previous.Position.X);
        return last.Position + new Vector2(deltaX, 0f);
    }

    private static void CopyPanelLayout(NSettingsPanel templatePanel, NSettingsPanel newPanel)
    {
        newPanel.AnchorLeft = templatePanel.AnchorLeft;
        newPanel.AnchorTop = templatePanel.AnchorTop;
        newPanel.AnchorRight = templatePanel.AnchorRight;
        newPanel.AnchorBottom = templatePanel.AnchorBottom;

        newPanel.OffsetLeft = templatePanel.OffsetLeft;
        newPanel.OffsetTop = templatePanel.OffsetTop;
        newPanel.OffsetRight = templatePanel.OffsetRight;
        newPanel.OffsetBottom = templatePanel.OffsetBottom;

        newPanel.Position = templatePanel.Position;
        newPanel.Size = templatePanel.Size;
        newPanel.Scale = templatePanel.Scale;
        newPanel.Rotation = templatePanel.Rotation;
        newPanel.PivotOffset = templatePanel.PivotOffset;
        newPanel.CustomMinimumSize = templatePanel.CustomMinimumSize;
        newPanel.SizeFlagsHorizontal = templatePanel.SizeFlagsHorizontal;
        newPanel.SizeFlagsVertical = templatePanel.SizeFlagsVertical;
        newPanel.GrowHorizontal = templatePanel.GrowHorizontal;
        newPanel.GrowVertical = templatePanel.GrowVertical;
        newPanel.MouseFilter = templatePanel.MouseFilter;
    }
}

[HarmonyPatch(typeof(NSettingsScreen), nameof(NSettingsScreen._Ready))]
internal static class ModSettingsTabBridgePatches
{
    [HarmonyPostfix]
    private static void Postfix(NSettingsScreen __instance)
    {
        ModSettingsTabBridge.Install(__instance);
    }
}

[HarmonyPatch(typeof(NSettingsScreen), nameof(NSettingsScreen.OnSubmenuOpened))]
internal static class ModSettingsTabBridgeOpenPatches
{
    [HarmonyPostfix]
    private static void Postfix(NSettingsScreen __instance)
    {
        ModSettingsTabBridge.Install(__instance);
    }
}
