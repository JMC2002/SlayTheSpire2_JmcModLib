// 文件用途：统一推断调用方程序集，避免各模块重复处理 Assembly 为空的情况。
using System.Diagnostics;
using System.Reflection;

namespace JmcModLib.Core;

internal static class AssemblyResolver
{
    private static readonly Assembly FallbackAssembly = typeof(AssemblyResolver).Assembly;

    public static Assembly Resolve(Assembly? assembly, params Type[] skippedDeclaringTypes)
    {
        if (assembly != null)
        {
            return assembly;
        }

        StackFrame[]? frames = new StackTrace(skipFrames: 1, fNeedFileInfo: false).GetFrames();
        if (frames == null)
        {
            return FallbackAssembly;
        }

        foreach (StackFrame frame in frames)
        {
            Type? declaringType = frame.GetMethod()?.DeclaringType;
            if (declaringType == null || ShouldSkip(declaringType, skippedDeclaringTypes))
            {
                continue;
            }

            return declaringType.Assembly;
        }

        return FallbackAssembly;
    }

    private static bool ShouldSkip(Type declaringType, Type[] skippedDeclaringTypes)
    {
        if (declaringType == typeof(AssemblyResolver))
        {
            return true;
        }

        foreach (Type skippedType in skippedDeclaringTypes)
        {
            if (declaringType == skippedType || declaringType.DeclaringType == skippedType)
            {
                return true;
            }
        }

        return false;
    }
}
