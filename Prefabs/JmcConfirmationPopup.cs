using Godot;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Multiplayer;
using System.Reflection;

namespace JmcModLib.Prefabs;

/// <summary>
/// 通过游戏原生的 <see cref="NGenericPopup"/> 与 <see cref="NModalContainer"/> 显示通用弹窗。
/// </summary>
public static class JmcConfirmationPopup
{
    private const string MainMenuTable = "main_menu_ui";
    private const string ConfirmKey = "GENERIC_POPUP.confirm";
    private const string CancelKey = "GENERIC_POPUP.cancel";

    public static bool IsAvailable =>
        NModalContainer.Instance is { OpenModal: null };

    /// <summary>
    /// 显示原生双按钮确认弹窗。
    /// </summary>
    /// <param name="title">弹窗标题。原生标题使用普通 <c>MegaLabel</c>，不建议写富文本。</param>
    /// <param name="body">弹窗正文。原生正文使用 <c>MegaRichTextLabel</c>，支持游戏富文本标签。</param>
    /// <param name="confirmText">可选确认按钮文本；留空时使用游戏默认确认文本。</param>
    /// <param name="cancelText">可选取消按钮文本；留空时使用游戏默认取消文本。</param>
    /// <param name="showBackstop">是否显示弹窗背后的原生深色模态遮罩。</param>
    /// <param name="assembly">可选所属程序集，用于日志上下文。</param>
    /// <returns>按下确认按钮返回 <see langword="true"/>；取消、关闭或弹窗不可用时返回 <see langword="false"/>。</returns>
    public static Task<bool> ShowConfirmationAsync(
        string title,
        string body,
        string? confirmText = null,
        string? cancelText = null,
        bool showBackstop = true,
        Assembly? assembly = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(body);

        return ShowCoreAsync(
            (popup, completion) =>
            {
                popup.SetText(title, body);
                InitConfirmationButtons(
                    popup,
                    DefaultConfirmText(),
                    DefaultCancelText(),
                    confirmText,
                    cancelText,
                    completion);
            },
            showBackstop,
            assembly);
    }

    /// <summary>
    /// 显示只有一个确认按钮的原生提示弹窗。
    /// </summary>
    /// <param name="title">弹窗标题。</param>
    /// <param name="body">弹窗正文。原生正文标签支持富文本。</param>
    /// <param name="okText">可选确认按钮文本；留空时使用游戏默认确认文本。</param>
    /// <param name="showBackstop">是否显示弹窗背后的原生深色模态遮罩。</param>
    /// <param name="assembly">可选所属程序集，用于日志上下文。</param>
    /// <returns>按下按钮返回 <see langword="true"/>；关闭或弹窗不可用时返回 <see langword="false"/>。</returns>
    public static Task<bool> ShowMessageAsync(
        string title,
        string body,
        string? okText = null,
        bool showBackstop = true,
        Assembly? assembly = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(body);

        return ShowCoreAsync(
            (popup, completion) =>
            {
                popup.SetText(title, body);
                InitMessageButton(
                    popup,
                    DefaultConfirmText(),
                    okText,
                    completion);
            },
            showBackstop,
            assembly);
    }

    /// <summary>
    /// 使用本地化文本显示原生双按钮确认弹窗。
    /// </summary>
    /// <param name="title">本地化弹窗标题。</param>
    /// <param name="body">本地化弹窗正文。原生正文标签支持富文本。</param>
    /// <param name="confirmText">可选本地化确认按钮文本；留空时使用游戏默认确认文本。</param>
    /// <param name="cancelText">可选本地化取消按钮文本；留空时使用游戏默认取消文本。</param>
    /// <param name="showBackstop">是否显示弹窗背后的原生深色模态遮罩。</param>
    /// <param name="assembly">可选所属程序集，用于日志上下文。</param>
    /// <returns>按下确认按钮返回 <see langword="true"/>；取消、关闭或弹窗不可用时返回 <see langword="false"/>。</returns>
    public static Task<bool> ShowConfirmationAsync(
        LocString title,
        LocString body,
        LocString? confirmText = null,
        LocString? cancelText = null,
        bool showBackstop = true,
        Assembly? assembly = null)
    {
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(body);

        return ShowCoreAsync(
            (popup, completion) =>
            {
                popup.SetText(title, body);
                InitConfirmationButtons(
                    popup,
                    confirmText ?? DefaultConfirmText(),
                    cancelText ?? DefaultCancelText(),
                    confirmOverride: null,
                    cancelOverride: null,
                    completion);
            },
            showBackstop,
            assembly);
    }

    /// <summary>
    /// 使用本地化文本显示只有一个确认按钮的原生提示弹窗。
    /// </summary>
    /// <param name="title">本地化弹窗标题。</param>
    /// <param name="body">本地化弹窗正文。原生正文标签支持富文本。</param>
    /// <param name="okText">可选本地化确认按钮文本；留空时使用游戏默认确认文本。</param>
    /// <param name="showBackstop">是否显示弹窗背后的原生深色模态遮罩。</param>
    /// <param name="assembly">可选所属程序集，用于日志上下文。</param>
    /// <returns>按下按钮返回 <see langword="true"/>；关闭或弹窗不可用时返回 <see langword="false"/>。</returns>
    public static Task<bool> ShowMessageAsync(
        LocString title,
        LocString body,
        LocString? okText = null,
        bool showBackstop = true,
        Assembly? assembly = null)
    {
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(body);

        return ShowCoreAsync(
            (popup, completion) =>
            {
                popup.SetText(title, body);
                InitMessageButton(
                    popup,
                    okText ?? DefaultConfirmText(),
                    completion);
            },
            showBackstop,
            assembly);
    }

    private static Task<bool> ShowCoreAsync(
        Action<NVerticalPopup, TaskCompletionSource<bool>> configure,
        bool showBackstop,
        Assembly? assembly)
    {
        ArgumentNullException.ThrowIfNull(configure);

        Assembly resolvedAssembly = AssemblyResolver.Resolve(assembly, typeof(JmcConfirmationPopup));
        NModalContainer? modalContainer = NModalContainer.Instance;
        if (modalContainer == null)
        {
            ModLogger.Warn("Cannot show confirmation popup because NModalContainer is not ready.", resolvedAssembly);
            return Task.FromResult(false);
        }

        if (modalContainer.OpenModal != null)
        {
            ModLogger.Warn("Cannot show confirmation popup because another modal is already open.", resolvedAssembly);
            return Task.FromResult(false);
        }

        NGenericPopup? popup = NGenericPopup.Create();
        if (popup == null)
        {
            ModLogger.Warn("Cannot create native NGenericPopup.", resolvedAssembly);
            return Task.FromResult(false);
        }

        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        popup.Connect(Node.SignalName.TreeExiting, Callable.From(() => completion.TrySetResult(false)));

        bool addedToModal = false;
        try
        {
            // Native popups initialize child button visuals in _Ready, so add to the tree
            // before setting button text or connecting button callbacks.
            modalContainer.Add(popup, showBackstop);
            if (!ReferenceEquals(modalContainer.OpenModal, popup))
            {
                completion.TrySetResult(false);
                popup.QueueFree();
                ModLogger.Warn("Cannot show confirmation popup because another modal opened first.", resolvedAssembly);
                return completion.Task;
            }

            addedToModal = true;
            configure(popup.GetNode<NVerticalPopup>("VerticalPopup"), completion);
            return completion.Task;
        }
        catch (Exception ex)
        {
            completion.TrySetResult(false);
            if (addedToModal && ReferenceEquals(modalContainer.OpenModal, popup))
            {
                modalContainer.Clear();
            }
            else
            {
                popup.QueueFree();
            }

            ModLogger.Error("Failed to show confirmation popup.", ex, resolvedAssembly);
            return completion.Task;
        }
    }

    private static void InitConfirmationButtons(
        NVerticalPopup popup,
        LocString confirmLoc,
        LocString cancelLoc,
        string? confirmOverride,
        string? cancelOverride,
        TaskCompletionSource<bool> completion)
    {
        popup.InitYesButton(confirmLoc, _ => completion.TrySetResult(true));
        if (!string.IsNullOrWhiteSpace(confirmOverride))
        {
            popup.YesButton.SetText(confirmOverride.Trim());
        }

        popup.InitNoButton(cancelLoc, _ => completion.TrySetResult(false));
        if (!string.IsNullOrWhiteSpace(cancelOverride))
        {
            popup.NoButton.SetText(cancelOverride.Trim());
        }
    }

    private static void InitMessageButton(
        NVerticalPopup popup,
        LocString buttonLoc,
        string? buttonOverride,
        TaskCompletionSource<bool> completion)
    {
        popup.InitYesButton(buttonLoc, _ => completion.TrySetResult(true));
        if (!string.IsNullOrWhiteSpace(buttonOverride))
        {
            popup.YesButton.SetText(buttonOverride.Trim());
        }

        popup.HideNoButton();
    }

    private static void InitMessageButton(
        NVerticalPopup popup,
        LocString buttonLoc,
        TaskCompletionSource<bool> completion)
    {
        popup.InitYesButton(buttonLoc, _ => completion.TrySetResult(true));
        popup.HideNoButton();
    }

    private static LocString DefaultConfirmText()
    {
        return new LocString(MainMenuTable, ConfirmKey);
    }

    private static LocString DefaultCancelText()
    {
        return new LocString(MainMenuTable, CancelKey);
    }
}
