namespace BNLReloadedServer.Logging;

public enum LogLevel
{
    Debug = 0,

    Info = 1,

    Warn = 2,

    Error = 3
}

public enum LogCat
{
    Raw,

    Server,

    Conn,

    Net,

    Match,

    Catalogue,

    Map,

    Panel,

    Perf,

    Player
}

public readonly record struct LogRecord(
    long Seq,
    long Ts,
    LogLevel Level,
    LogCat Cat,
    string Msg,
    string? Detail);
