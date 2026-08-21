namespace BNLReloadedServer.Logging;

/// <summary>
/// How much attention a line deserves. The config's log_level drops everything below it at the
/// call site, so a quiet server does no formatting work for lines nobody will read.
/// </summary>
public enum LogLevel
{
    /// Packet and dispatch chatter. Volume is proportional to traffic — off unless you are debugging.
    Debug = 0,

    /// Something happened that an operator would want in the history: lifecycle, matches, saves.
    Info = 1,

    /// The server carried on, but on bad data or a rejected request. Somebody should look eventually.
    Warn = 2,

    /// An operation failed. Everything with a stack trace lands here.
    Error = 3
}

/// <summary>
/// Which part of the server spoke. Categories exist so streams can be pulled apart — the panel
/// filters on them, and a category can be given its own view without touching any call site.
/// </summary>
public enum LogCat
{
    /// Written to stdout by something that does not use <see cref="Log"/> yet.
    Raw,

    /// Process and listener lifecycle.
    Server,

    /// Sessions connecting and disconnecting.
    Conn,

    /// Packet framing and service dispatch.
    Net,

    /// Lobbies, matchmaking and zones.
    Match,

    /// Catalogue load, replication and CouchDB.
    Catalogue,

    /// Map files and the map editor.
    Map,

    /// The control panel itself.
    Panel,

    /// Tick rate and slow actions. Its own category rather than Warn: under load these repeat
    /// continuously, and mixing them into the warnings would make the warning count meaningless.
    Perf,

    /// Player data and accounts.
    Player
}

/// <param name="Seq">Monotonic within one process run, starting at 1. The panel resumes its event stream with it.</param>
/// <param name="Ts">Unix milliseconds UTC. Every consumer re-zones it themselves.</param>
/// <param name="Detail">Stack traces and other multi-line payloads, kept out of the line itself.</param>
public readonly record struct LogRecord(
    long Seq,
    long Ts,
    LogLevel Level,
    LogCat Cat,
    string Msg,
    string? Detail);
