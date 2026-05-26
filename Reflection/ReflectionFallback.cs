// 文件用途：集中记录动态反射访问器降级，避免受限运行时重复刷屏。
using System.Reflection;

namespace JmcModLib.Reflection;

internal static class ReflectionFallback
{
    private static int dynamicAccessorFallbackLogged;

    public static void LogDynamicAccessorFallback(MemberInfo member, Exception exception)
    {
        if (Interlocked.Exchange(ref dynamicAccessorFallbackLogged, 1) == 1)
        {
            return;
        }

        string owner = member.DeclaringType?.FullName ?? member.Module.Name;
        ModLogger.Warn(
            $"当前运行时无法使用部分动态反射访问器，已回退到普通反射调用。配置、按钮与热键会继续注册。首个失败成员：{owner}.{member.Name}，原因：{exception.GetType().Name}: {exception.Message}",
            member.Module.Assembly);
    }
}
