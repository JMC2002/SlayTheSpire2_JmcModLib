using Godot;
using HarmonyLib;
using JmcModLib.Config.UI;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens.Capstones;
using MegaCrit.Sts2.Core.Nodes.Screens.PauseMenu;
using MegaCrit.Sts2.Core.Runs;
using System.Runtime.CompilerServices;
using GodotDictionary = Godot.Collections.Dictionary;

namespace JmcModLib.UI.PauseMenu;

internal static class PauseMenuBridge
{
    private const string NodeNamePrefix = "JmcModLibPauseMenu_";
    private static readonly StringName AssemblyMetaKey = new("jmcmodlib_pause_menu_assembly");
    private static readonly StringName KeyMetaKey = new("jmcmodlib_pause_menu_key");
    private static readonly StringName ConnectedMetaKey = new("jmcmodlib_pause_menu_connected");
    private static readonly Variant CallableKey = Variant.CreateFrom("callable");
    private static readonly ConditionalWeakTable<NPauseMenu, MenuState> MenuStates = [];
    private static readonly string[] TemplateCandidates = ["Settings", "Resume", "SaveAndQuit"];
    private static readonly string[] ExitActionNames = ["GiveUp", "Disconnect", "SaveAndQuit"];

    public static void Refresh(NPauseMenu menu, IRunState? runState = null, bool scheduleDeferred = false)
    {
        if (menu == null)
        {
            return;
        }

        MenuState state = MenuStates.GetOrCreateValue(menu);
        if (runState != null)
        {
            state.RunState = runState;
            state.IsClosingToMenu = false;
        }

        try
        {
            RefreshCore(menu, state);
        }
        catch (Exception ex)
        {
            ModLogger.Warn("刷新暂停菜单扩展按钮失败，本次将跳过 JML 按钮注入。", ex);
        }

        if (scheduleDeferred)
        {
            ScheduleDeferredRefresh(menu, state);
        }
    }

    private static void RefreshCore(NPauseMenu menu, MenuState state)
    {
        Control? container = GetButtonContainer(menu);
        if (container == null)
        {
            if (!state.LayoutWarningLogged)
            {
                ModLogger.Warn("暂停菜单结构与预期不符，找不到 %ButtonContainer。");
                state.LayoutWarningLogged = true;
            }

            return;
        }

        state.FocusLoops ??= DetectNativeFocusLoop(container);
        IReadOnlyList<PauseMenuButtonEntry> entries = PauseMenuRegistry.GetAllEntriesSorted();
        RemoveStaleJmlNodes(container, entries);

        if (entries.Count == 0)
        {
            RebuildFocusNeighbors(container, state);
            return;
        }

        NPauseMenuButton? template = ResolveTemplate(container);
        if (template == null)
        {
            if (!state.TemplateWarningLogged)
            {
                ModLogger.Warn("暂停菜单结构与预期不符，找不到可克隆的原生按钮模板。");
                state.TemplateWarningLogged = true;
            }

            RebuildFocusNeighbors(container, state);
            return;
        }

        foreach (PauseMenuButtonEntry entry in entries)
        {
            NPauseMenuButton? button = EnsureButton(menu, container, template, entry);
            if (button == null)
            {
                continue;
            }

            RefreshButton(menu, button, entry, state.RunState, state.IsClosingToMenu);
        }

        ReorderJmlNodes(container, entries);
        RebuildFocusNeighbors(container, state);
    }

    private static Control? GetButtonContainer(NPauseMenu menu)
    {
        try
        {
            return menu.GetNodeOrNull<Control>("%ButtonContainer");
        }
        catch
        {
            return null;
        }
    }

    private static NPauseMenuButton? ResolveTemplate(Control container)
    {
        foreach (string name in TemplateCandidates)
        {
            NPauseMenuButton? candidate = container.GetNodeOrNull<NPauseMenuButton>(name);
            if (candidate != null)
            {
                return candidate;
            }
        }

        return container.GetChildren().OfType<NPauseMenuButton>().FirstOrDefault();
    }

    private static void RemoveStaleJmlNodes(Control container, IReadOnlyList<PauseMenuButtonEntry> entries)
    {
        HashSet<(string AssemblyFullName, string Key)> activeIds = entries
            .Select(static entry => (entry.Assembly.FullName ?? string.Empty, entry.Key))
            .ToHashSet();

        foreach (Node child in container.GetChildren())
        {
            if (!TryReadIdentity(child, out string assemblyFullName, out string key))
            {
                continue;
            }

            if (!activeIds.Contains((assemblyFullName, key)))
            {
                child.QueueFree();
            }
        }
    }

    private static NPauseMenuButton? EnsureButton(
        NPauseMenu menu,
        Control container,
        NPauseMenuButton template,
        PauseMenuButtonEntry entry)
    {
        NPauseMenuButton? existing = FindButton(container, entry);
        if (existing != null)
        {
            return existing;
        }

        try
        {
            var button = (NPauseMenuButton)template.Duplicate();
            button.Name = BuildNodeName(entry);
            button.SetMeta(AssemblyMetaKey, entry.Assembly.FullName ?? string.Empty);
            button.SetMeta(KeyMetaKey, entry.Key);
            ClearReleasedConnections(button);
            button.Connect(NClickableControl.SignalName.Released, Callable.From<NButton>(pressed => OnButtonReleased(menu, pressed)));
            button.SetMeta(ConnectedMetaKey, true);
            container.AddChild(button);
            return button;
        }
        catch (Exception ex)
        {
            ModLogger.Warn($"创建暂停菜单按钮 {entry.Key} 失败。", ex, entry.Assembly);
            return null;
        }
    }

    private static NPauseMenuButton? FindButton(Control container, PauseMenuButtonEntry entry)
    {
        string assemblyFullName = entry.Assembly.FullName ?? string.Empty;
        foreach (Node child in container.GetChildren())
        {
            if (child is NPauseMenuButton button
                && TryReadIdentity(button, out string nodeAssembly, out string nodeKey)
                && string.Equals(nodeAssembly, assemblyFullName, StringComparison.Ordinal)
                && string.Equals(nodeKey, entry.Key, StringComparison.Ordinal))
            {
                EnsureConnected(button);
                return button;
            }
        }

        return null;
    }

    private static void EnsureConnected(NPauseMenuButton button)
    {
        if (button.HasMeta(ConnectedMetaKey) && button.GetMeta(ConnectedMetaKey).AsBool())
        {
            return;
        }

        ClearReleasedConnections(button);
        button.Connect(NClickableControl.SignalName.Released, Callable.From<NButton>(pressed => OnButtonReleased(null, pressed)));
        button.SetMeta(ConnectedMetaKey, true);
    }

    private static void RefreshButton(
        NPauseMenu menu,
        NPauseMenuButton button,
        PauseMenuButtonEntry entry,
        IRunState? runState,
        bool forceDisabled)
    {
        ApplyText(button, PauseMenuLocalization.GetText(entry));
        ApplyColor(button, entry.Color);

        PauseMenuButtonContext context = entry.CreateContext(menu, button, runState);
        bool visible = entry.EvaluateVisible(context);
        bool enabled = visible && !forceDisabled && entry.EvaluateEnabled(context);

        button.Visible = visible;
        button.SetEnabled(enabled);
    }

    public static void BeginNativeSaveAndQuit(NPauseMenu menu)
    {
        if (menu == null)
        {
            return;
        }

        MenuState state = MenuStates.GetOrCreateValue(menu);
        state.IsClosingToMenu = true;

        Control? container = GetButtonContainer(menu);
        if (container == null)
        {
            return;
        }

        foreach (Node child in container.GetChildren())
        {
            if (child is NPauseMenuButton button && TryReadIdentity(button, out _, out _))
            {
                button.Disable();
            }
        }

        RebuildFocusNeighbors(container, state);
    }

    private static void ApplyText(NPauseMenuButton button, string text)
    {
        MegaLabel? label = button.GetNodeOrNull<MegaLabel>("Label");
        label?.SetTextAutoSize(text);
    }

    private static void ApplyColor(NPauseMenuButton button, UIButtonColor color)
    {
        Control? image = button.GetNodeOrNull<Control>("ButtonImage")
            ?? NativeTemplateCloner.FindDescendantByName<Control>(button, "ButtonImage");
        if (image == null)
        {
            return;
        }

        if (!JmcButtonColor.TryGetTint(color, out Color tint))
        {
            tint = Colors.White;
        }

        image.Modulate = tint;
        image.SelfModulate = tint;
    }

    private static void ReorderJmlNodes(Control container, IReadOnlyList<PauseMenuButtonEntry> entries)
    {
        foreach (IGrouping<PauseMenuButtonAnchor, PauseMenuButtonEntry> group in entries.GroupBy(static entry => entry.Anchor))
        {
            int insertIndex = ResolveInsertIndex(container, group.Key);
            foreach (PauseMenuButtonEntry entry in group)
            {
                NPauseMenuButton? button = FindButton(container, entry);
                if (button == null)
                {
                    continue;
                }

                MoveChild(container, button, insertIndex);
                insertIndex = button.GetIndex() + 1;
            }
        }
    }

    private static int ResolveInsertIndex(Control container, PauseMenuButtonAnchor anchor)
    {
        return anchor switch
        {
            PauseMenuButtonAnchor.AfterResume => IndexAfter(container, "Resume"),
            PauseMenuButtonAnchor.AfterSettings => IndexAfter(container, "Settings"),
            PauseMenuButtonAnchor.AfterCompendium => IndexAfter(container, "Compendium"),
            PauseMenuButtonAnchor.BeforeExitActions => IndexBeforeFirst(container, ExitActionNames),
            PauseMenuButtonAnchor.End => container.GetChildCount(),
            _ => container.GetChildCount()
        };
    }

    private static int IndexAfter(Control container, string nodeName)
    {
        Node? node = container.GetNodeOrNull<Node>(nodeName);
        return node == null ? container.GetChildCount() : node.GetIndex() + 1;
    }

    private static int IndexBeforeFirst(Control container, IReadOnlyCollection<string> nodeNames)
    {
        int index = container.GetChildCount();
        foreach (Node child in container.GetChildren())
        {
            if (nodeNames.Contains(child.Name.ToString(), StringComparer.Ordinal))
            {
                index = Math.Min(index, child.GetIndex());
            }
        }

        return index;
    }

    private static void MoveChild(Control container, Node child, int targetIndex)
    {
        int childCount = container.GetChildCount();
        if (childCount <= 1)
        {
            return;
        }

        int insertIndex = Math.Clamp(targetIndex, 0, childCount);
        int clampedIndex = child.GetIndex() < insertIndex
            ? insertIndex - 1
            : insertIndex;
        clampedIndex = Math.Clamp(clampedIndex, 0, childCount - 1);
        if (child.GetIndex() != clampedIndex)
        {
            container.MoveChild(child, clampedIndex);
        }
    }

    private static void RebuildFocusNeighbors(Control container, MenuState state)
    {
        List<NPauseMenuButton> buttons = container
            .GetChildren()
            .OfType<NPauseMenuButton>()
            .Where(static button => button.Visible && button.FocusMode != Control.FocusModeEnum.None)
            .ToList();

        if (buttons.Count == 0)
        {
            return;
        }

        state.FocusLoops ??= DetectFocusLoop(buttons);
        bool focusLoops = state.FocusLoops.Value;
        for (int i = 0; i < buttons.Count; i++)
        {
            NPauseMenuButton button = buttons[i];
            NodePath selfPath = button.GetPath();
            button.FocusNeighborLeft = selfPath;
            button.FocusNeighborRight = selfPath;
            button.FocusNeighborTop = i > 0
                ? buttons[i - 1].GetPath()
                : focusLoops ? buttons[^1].GetPath() : selfPath;
            button.FocusNeighborBottom = i < buttons.Count - 1
                ? buttons[i + 1].GetPath()
                : focusLoops ? buttons[0].GetPath() : selfPath;
        }
    }

    private static bool DetectFocusLoop(IReadOnlyList<NPauseMenuButton> buttons)
    {
        if (buttons.Count <= 1)
        {
            return false;
        }

        NPauseMenuButton first = buttons[0];
        NPauseMenuButton last = buttons[^1];
        return SamePath(first.FocusNeighborTop, last.GetPath())
            || SamePath(last.FocusNeighborBottom, first.GetPath());
    }

    private static bool DetectNativeFocusLoop(Control container)
    {
        NPauseMenuButton? first = container.GetNodeOrNull<NPauseMenuButton>("Resume");
        NPauseMenuButton? last = container.GetNodeOrNull<NPauseMenuButton>("SaveAndQuit");
        if (first == null || last == null)
        {
            List<NPauseMenuButton> buttons = container
                .GetChildren()
                .OfType<NPauseMenuButton>()
                .Where(static button => !TryReadIdentity(button, out _, out _))
                .ToList();
            return DetectFocusLoop(buttons);
        }

        return SamePath(first.FocusNeighborTop, last.GetPath())
            || SamePath(last.FocusNeighborBottom, first.GetPath());
    }

    private static bool SamePath(NodePath left, NodePath right)
    {
        return string.Equals(left.ToString(), right.ToString(), StringComparison.Ordinal);
    }

    private static void ClearReleasedConnections(NPauseMenuButton button)
    {
        foreach (GodotDictionary connection in button.GetSignalConnectionList(NClickableControl.SignalName.Released))
        {
            try
            {
                Callable callable = connection[CallableKey].AsCallable();
                if (button.IsConnected(NClickableControl.SignalName.Released, callable))
                {
                    button.Disconnect(NClickableControl.SignalName.Released, callable);
                }
            }
            catch (Exception ex)
            {
                ModLogger.Warn("清理暂停菜单按钮旧信号连接失败。", ex);
            }
        }
    }

    private static bool TryReadIdentity(Node node, out string assemblyFullName, out string key)
    {
        assemblyFullName = string.Empty;
        key = string.Empty;
        if (!node.HasMeta(AssemblyMetaKey) || !node.HasMeta(KeyMetaKey))
        {
            return false;
        }

        assemblyFullName = node.GetMeta(AssemblyMetaKey).AsString();
        key = node.GetMeta(KeyMetaKey).AsString();
        return !string.IsNullOrWhiteSpace(assemblyFullName)
            && !string.IsNullOrWhiteSpace(key);
    }

    private static string BuildNodeName(PauseMenuButtonEntry entry)
    {
        return $"{NodeNamePrefix}{SanitizeName(entry.AssemblyName)}_{SanitizeName(entry.Key)}";
    }

    private static string SanitizeName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "_";
        }

        char[] chars = value.Trim().Select(static ch => char.IsLetterOrDigit(ch) ? ch : '_').ToArray();
        return new string(chars);
    }

    private static void ScheduleDeferredRefresh(NPauseMenu menu, MenuState state)
    {
        if (state.DeferredRefreshScheduled)
        {
            return;
        }

        state.DeferredRefreshScheduled = true;
        Callable.From(() =>
        {
            state.DeferredRefreshScheduled = false;
            if (GodotObject.IsInstanceValid(menu) && !menu.IsQueuedForDeletion())
            {
                Refresh(menu);
            }
        }).CallDeferred();
    }

    private static void OnButtonReleased(NPauseMenu? menu, NButton button)
    {
        NPauseMenu? resolvedMenu = menu ?? button.GetAncestorOfType<NPauseMenu>();
        if (resolvedMenu == null
            || !TryReadIdentity(button, out string assemblyFullName, out string key)
            || !PauseMenuRegistry.TryGetEntry(assemblyFullName, key, out PauseMenuButtonEntry? entry)
            || entry == null)
        {
            return;
        }

        MenuState state = MenuStates.GetOrCreateValue(resolvedMenu);
        PauseMenuButtonContext context = entry.CreateContext(resolvedMenu, button, state.RunState);
        if (!button.Visible || !button.IsEnabled)
        {
            return;
        }

        TaskHelper.RunSafely(InvokeButtonAsync(entry, context));
    }

    private static async Task InvokeButtonAsync(PauseMenuButtonEntry entry, PauseMenuButtonContext context)
    {
        try
        {
            await entry.InvokeAsync(context);
            if (entry.CloseMenuOnClick)
            {
                NCapstoneContainer.Instance?.Close();
            }
        }
        catch (Exception ex)
        {
            ModLogger.Error($"暂停菜单按钮 {entry.Key} 点击回调失败。", ex, entry.Assembly);
        }
    }

    private sealed class MenuState
    {
        public IRunState? RunState { get; set; }

        public bool? FocusLoops { get; set; }

        public bool DeferredRefreshScheduled { get; set; }

        public bool IsClosingToMenu { get; set; }

        public bool LayoutWarningLogged { get; set; }

        public bool TemplateWarningLogged { get; set; }
    }
}

[HarmonyPatch(typeof(NPauseMenu), nameof(NPauseMenu._Ready))]
internal static class PauseMenuReadyPatch
{
    [HarmonyPostfix]
    public static void Postfix(NPauseMenu __instance)
    {
        PauseMenuBridge.Refresh(__instance, scheduleDeferred: true);
    }
}

[HarmonyPatch(typeof(NPauseMenu), nameof(NPauseMenu.Initialize))]
internal static class PauseMenuInitializePatch
{
    [HarmonyPostfix]
    public static void Postfix(NPauseMenu __instance, IRunState runState)
    {
        PauseMenuBridge.Refresh(__instance, runState, scheduleDeferred: true);
    }
}

[HarmonyPatch(typeof(NPauseMenu), nameof(NPauseMenu.OnSubmenuOpened))]
internal static class PauseMenuOpenedPatch
{
    [HarmonyPostfix]
    public static void Postfix(NPauseMenu __instance)
    {
        PauseMenuBridge.Refresh(__instance, scheduleDeferred: true);
    }
}

[HarmonyPatch(typeof(NPauseMenu), "OnSaveAndQuitButtonPressed")]
internal static class PauseMenuSaveAndQuitPatch
{
    [HarmonyPrefix]
    public static void Prefix(NPauseMenu __instance)
    {
        PauseMenuBridge.BeginNativeSaveAndQuit(__instance);
    }
}
