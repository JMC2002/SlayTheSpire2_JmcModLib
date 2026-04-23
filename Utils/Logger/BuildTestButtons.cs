using System.Reflection;

namespace JmcModLib.Core;

internal readonly record struct LoggerTestAction(string Name, Action<Assembly> Invoke);

internal static class BuildTestButtons
{
    internal static IReadOnlyList<LoggerTestAction> CreateDefaultActions()
    {
        return
        [
            new LoggerTestAction("Trace", static asm => ModLogger.Trace("Logger test trace.", asm)),
            new LoggerTestAction("Debug", static asm => ModLogger.Debug("Logger test debug.", asm)),
            new LoggerTestAction("Info", static asm => ModLogger.Info("Logger test info.", asm)),
            new LoggerTestAction("Warn", static asm => ModLogger.Warn("Logger test warning.", new InvalidOperationException("Logger test warning exception."), asm)),
            new LoggerTestAction("Error", static asm => ModLogger.Error("Logger test error.", new InvalidOperationException("Logger test error exception."), asm)),
            new LoggerTestAction("Fatal", static asm =>
            {
                try
                {
                    ModLogger.Fatal(new InvalidOperationException("Logger test fatal exception."), "Logger test fatal.", asm);
                }
                catch (Exception ex)
                {
                    ModLogger.Error("Caught fatal exception during logger test.", ex, asm);
                }
            }),
        ];
    }
}
