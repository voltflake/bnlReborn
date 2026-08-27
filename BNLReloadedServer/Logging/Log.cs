using System.Runtime.CompilerServices;
using System.Text;
using BNLReloadedServer.ControlPanel;

namespace BNLReloadedServer.Logging;

public static class Log
{
    private const string Reset = "\u001b[0m";
    private const string Dim = "\u001b[2m";
    private const string Yellow = "\u001b[33m";
    private const string Red = "\u001b[31m";

    private static TextWriter _stdout = Console.Out;

    private static readonly object ConsoleLock = new();
    private static bool _colour;

    public static LogLevel MinLevel { get; set; } = LogLevel.Info;

    public static void Attach()
    {
        _stdout = Console.Out;
        _colour = !Console.IsOutputRedirected;
        Console.SetOut(new BroadcastingTextWriter(_stdout));
    }

    public static bool Enabled(LogLevel level) => level >= MinLevel;

    // Who the current thread is serving, for the stretch of work that answers one peer. Warnings
    // raised in there name the address without every call site having to carry it down.
    // Thread-bound rather than AsyncLocal on purpose: an AsyncLocal would ride into every task
    // started while handling a packet and keep stamping that address on it long after the session
    // is gone. Everything this is meant to label is raised synchronously inside ProcessPacket.
    [ThreadStatic] private static string? _peer;

    // A struct, and returned as one, so the scope this opens on every received buffer allocates
    // nothing.
    public static PeerScope WithPeer(string? peer)
    {
        var scope = new PeerScope(_peer);
        _peer = peer;
        return scope;
    }

    public readonly struct PeerScope(string? previous) : IDisposable
    {
        public void Dispose() => _peer = previous;
    }

    // The last group of a session guid. Long enough to tell sessions apart in a log, and still a
    // substring of the full id, so grepping it finds the lines that print the whole thing.
    public static string ShortId(Guid id)
    {
        var text = id.ToString();
        return text[(text.LastIndexOf('-') + 1)..];
    }

    // A null Enum? interpolates to an empty string, which is exactly the case worth logging:
    // an id byte outside the enum. Keep the raw byte so an unknown id is still identifiable.
    public static string EnumName<T>(T? value, byte raw) where T : struct, Enum =>
        value?.ToString() ?? $"unknown({raw})";

    public static void Debug(LogCat cat, string message) => Write(LogLevel.Debug, cat, message, null);

    public static void Debug(LogCat cat, ref DebugMessageHandler message)
    {
        if (!message.IsEnabled) return;
        Write(LogLevel.Debug, cat, message.ToString(), null);
    }

    public static void Info(LogCat cat, string message) => Write(LogLevel.Info, cat, message, null);

    public static void Warn(LogCat cat, string message) => Write(LogLevel.Warn, cat, message, null);

    public static void Error(LogCat cat, string message) => Write(LogLevel.Error, cat, message, null);

    public static void Error(LogCat cat, string message, Exception e) =>
        Write(LogLevel.Error, cat, message, e.ToString());

    internal static void Raw(string line) => Record(LogLevel.Info, LogCat.Raw, line, null);

    private static void Write(LogLevel level, LogCat cat, string message, string? detail)
    {
        if (!Enabled(level)) return;

        // Only on the levels somebody investigates: debug lines repeat per packet, and the address
        // is already on the connect line above them.
        if (level >= LogLevel.Warn && _peer is { } peer)
            message = $"{message} (peer {peer})";

        Record(level, cat, message, detail);
        Print(level, cat, message, detail);
    }

    private static void Record(LogLevel level, LogCat cat, string message, string? detail) =>
        LogBuffer.Append(new LogRecord(
            0,                                              // LogBuffer assigns the sequence number
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            level, cat, message, detail));

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
