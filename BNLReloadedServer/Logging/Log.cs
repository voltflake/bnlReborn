using System.Runtime.CompilerServices;
using System.Text;
using BNLReloadedServer.ControlPanel;

namespace BNLReloadedServer.Logging;

/// <summary>
/// The server's logging front door. A call site states a level and a category and the record
/// carries them all the way to the control panel — nothing downstream has to guess severity from
/// the wording, which is what the panel used to do.
/// </summary>
public static class Log
{
    private const string Reset = "\u001b[0m";
    private const string Dim = "\u001b[2m";
    private const string Yellow = "\u001b[33m";
    private const string Red = "\u001b[31m";

    /// <summary>The terminal, held separately so writing a record does not loop back through
    /// <see cref="BroadcastingTextWriter"/> and log itself a second time.</summary>
    private static TextWriter _stdout = Console.Out;

    private static readonly object ConsoleLock = new();
    private static bool _colour;

    /// <summary>Lines below this are dropped at the call site. Set from config at startup.</summary>
    public static LogLevel MinLevel { get; set; } = LogLevel.Info;

    /// <summary>
    /// Captures the real stdout and puts the broadcasting writer in its place, so anything still
    /// using Console.WriteLine is picked up as a <see cref="LogCat.Raw"/> line instead of vanishing
    /// from the panel.
    /// </summary>
    public static void Attach()
    {
        _stdout = Console.Out;
        _colour = !Console.IsOutputRedirected;
        Console.SetOut(new BroadcastingTextWriter(_stdout));
    }

    public static bool Enabled(LogLevel level) => level >= MinLevel;

    public static void Debug(LogCat cat, string message) => Write(LogLevel.Debug, cat, message, null);

    /// <summary>
    /// The interpolated form. <see cref="DebugMessageHandler"/> refuses to build the string at
    /// all when debug lines are off, so a per-packet call costs nothing on a quiet server.
    /// </summary>
    public static void Debug(LogCat cat, ref DebugMessageHandler message)
    {
        if (!message.IsEnabled) return;
        Write(LogLevel.Debug, cat, message.ToString(), null);
    }

    public static void Info(LogCat cat, string message) => Write(LogLevel.Info, cat, message, null);

    public static void Warn(LogCat cat, string message) => Write(LogLevel.Warn, cat, message, null);

    public static void Error(LogCat cat, string message) => Write(LogLevel.Error, cat, message, null);

    /// <summary>
    /// The stack trace goes in the record's detail rather than the message, so one failure stays
    /// one line in the panel and one entry in the file.
    /// </summary>
    public static void Error(LogCat cat, string message, Exception e) =>
        Write(LogLevel.Error, cat, message, e.ToString());

    /// <summary>Used by <see cref="BroadcastingTextWriter"/> for unconverted Console writes.</summary>
    internal static void Raw(string line) => Record(LogLevel.Info, LogCat.Raw, line, null);

    private static void Write(LogLevel level, LogCat cat, string message, string? detail)
    {
        if (!Enabled(level)) return;
        Record(level, cat, message, detail);
        Print(level, cat, message, detail);
    }

    private static void Record(LogLevel level, LogCat cat, string message, string? detail) =>
        LogBuffer.Append(new LogRecord(
            0,                                              // LogBuffer assigns the sequence number
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            level, cat, message, detail));

    /// <summary>
    /// The terminal view. Local time here on purpose: the record keeps epoch milliseconds so every
    /// reader can re-zone it, but somebody watching this terminal is in the machine's timezone.
    /// </summary>
    private static void Print(LogLevel level, LogCat cat, string message, string? detail)
    {
        var line = new StringBuilder(message.Length + 48);
        line.Append(DateTime.Now.ToString("HH:mm:ss.fff")).Append(' ');

        if (_colour) line.Append(Colour(level));
        line.Append(LogNames.Of(level).ToUpperInvariant().PadRight(5)).Append(' ');
        if (_colour) line.Append(Reset).Append(Dim);
        line.Append(LogNames.Of(cat).PadRight(9));
        if (_colour) line.Append(Reset);
        line.Append(' ').Append(message);

        if (detail != null) line.Append(Environment.NewLine).Append(Indent(detail));

        lock (ConsoleLock)
        {
            _stdout.WriteLine(line.ToString());
        }
    }

    private static string Colour(LogLevel level) => level switch
    {
        LogLevel.Debug => Dim,
        LogLevel.Warn => Yellow,
        LogLevel.Error => Red,
        _ => Reset
    };

    private static string Indent(string detail) =>
        string.Join(Environment.NewLine,
            detail.Split('\n').Select(l => "    " + l.TrimEnd('\r')));
}
