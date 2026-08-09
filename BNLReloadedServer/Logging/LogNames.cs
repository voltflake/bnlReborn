namespace BNLReloadedServer.Logging;

/// <summary>
/// The wire spelling of the two enums. Lowercase names go into the JSONL file and out of the
/// control panel API, so they are written once here rather than through ToString() on a hot path.
/// </summary>
public static class LogNames
{
    private static readonly string[] Levels = ["debug", "info", "warn", "error"];

    private static readonly string[] Cats =
    [
        "raw", "server", "conn", "net", "match", "catalogue", "map", "panel", "perf", "player"
    ];

    public static string Of(LogLevel level) => Levels[(int)level];

    public static string Of(LogCat cat) => Cats[(int)cat];

    public static LogLevel Level(string? name) =>
        Array.IndexOf(Levels, name) is var i && i >= 0 ? (LogLevel)i : LogLevel.Info;

    public static LogCat Cat(string? name) =>
        Array.IndexOf(Cats, name) is var i && i >= 0 ? (LogCat)i : LogCat.Raw;

    /// <summary>Parses the config's log_level. Anything unrecognised means Info.</summary>
    public static LogLevel ParseLevel(string? name) =>
        Level(name?.Trim().ToLowerInvariant());
}
