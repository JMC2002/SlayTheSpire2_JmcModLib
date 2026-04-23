namespace JmcModLib.Core;

internal static class BuildFormatFlags
{
    private static readonly IReadOnlyDictionary<LogPrefixFlags, string> SupportedFlags = new Dictionary<LogPrefixFlags, string>
    {
        { LogPrefixFlags.Timestamp, "Show timestamp" },
    };

    internal static IReadOnlyDictionary<LogPrefixFlags, string> GetSupportedFlags()
    {
        return SupportedFlags;
    }
}
