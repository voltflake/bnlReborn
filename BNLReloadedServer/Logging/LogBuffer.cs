using System.Text;
using System.Text.Json;
using BNLReloadedServer.ControlPanel;
using BNLReloadedServer.Database;

namespace BNLReloadedServer.Logging;

/// <summary>
/// The in-memory tail of the log plus its file on disk. Records are appended, never mutated, and
/// handed to readers by sequence number — <see cref="Since"/> lets the control-panel event stream
/// push only the lines that actually happened rather than the whole buffer.
/// </summary>
public static class LogBuffer
{
    private const int MaxLines = 10000;
    private const long MaxFileBytes = 5 * 1024 * 1024;

    private static readonly string LogFilePath = Path.Combine(Databases.LogsFolderPath, "console.jsonl");
    private static readonly string RotatedFilePath = Path.Combine(Databases.LogsFolderPath, "console.1.jsonl");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private static readonly object LockObj = new();
    private static readonly Queue<LogRecord> Records = new();
    private static StreamWriter? _file;
    private static long _seq;

    /// <summary>
    /// Identifies this process run. Sequence numbers restart from 1 when the server does, so a
    /// reader that sees a new boot id knows its cursor means nothing and refetches from scratch.
    /// </summary>
    public static long Boot { get; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    static LogBuffer()
    {
        try
        {
            Directory.CreateDirectory(Databases.LogsFolderPath);
            Reload();
            OpenLogFile();
        }
        catch
        {
            // A log that cannot write to disk still serves the panel from memory.
        }
    }

    public static void Append(in LogRecord record)
    {
        var appended = false;
        lock (LockObj)
        {
            var stamped = record with { Seq = ++_seq };
            Records.Enqueue(stamped);
            appended = true;
            while (Records.Count > MaxLines)
                Records.Dequeue();

            try
            {
                if (_file == null) return;
                _file.WriteLine(Serialize(stamped));
                if (_file.BaseStream.Length > MaxFileBytes)
                {
                    _file.Dispose();
                    OpenLogFile();
                }
            }
            catch
            {
                // ignored — never let logging take the server down
            }
        }
        if (appended) ControlPanelEvents.Publish(ControlPanelEvent.Logs);
    }

    /// <summary>Everything newer than <paramref name="seq"/>. Pass 0 for the whole buffer.</summary>
    public static List<LogRecord> Since(long seq)
    {
        lock (LockObj)
        {
            var result = new List<LogRecord>();
            foreach (var record in Records)
                if (record.Seq > seq)
                    result.Add(record);
            return result;
        }
    }

    public static long LastSeq
    {
        get
        {
            lock (LockObj) return _seq;
        }
    }

    private static string Serialize(in LogRecord r) => JsonSerializer.Serialize(new
    {
        seq = r.Seq,
        ts = r.Ts,
        lvl = LogNames.Of(r.Level),
        cat = LogNames.Of(r.Cat),
        msg = r.Msg,
        detail = r.Detail
    }, JsonOptions);

    /// <summary>
    /// Restores the tail of the previous run so the panel is not blank after a restart. Sequence
    /// numbers are reassigned as the lines are read back: the old ones belong to a boot that is
    /// over, and this run needs them to start at 1 and only ever grow.
    /// </summary>
    private static void Reload()
    {
        if (!File.Exists(LogFilePath)) return;

        var lines = File.ReadAllLines(LogFilePath);
        foreach (var line in lines.Length > MaxLines ? lines[^MaxLines..] : lines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            try
            {
                using var doc = JsonDocument.Parse(line);
                var root = doc.RootElement;
                Records.Enqueue(new LogRecord(
                    ++_seq,
                    root.GetProperty("ts").GetInt64(),
                    LogNames.Level(root.GetProperty("lvl").GetString()),
                    LogNames.Cat(root.GetProperty("cat").GetString()),
                    root.GetProperty("msg").GetString() ?? string.Empty,
                    root.TryGetProperty("detail", out var d) ? d.GetString() : null));
            }
            catch (JsonException)
            {
                // A line torn by a hard kill, or a leftover from the old plain-text format.
            }
        }
    }

    /// <summary>
    /// Opens the log for appending, rolling a full one over to console.1.jsonl first. The old
    /// buffer truncated the file to whatever happened to be in memory instead, so 5 MB of history
    /// collapsed to 10 000 lines every time it filled up.
    /// </summary>
    private static void OpenLogFile()
    {
        if (File.Exists(LogFilePath) && new FileInfo(LogFilePath).Length > MaxFileBytes)
            File.Move(LogFilePath, RotatedFilePath, overwrite: true);

        // No BOM: this file is meant to be read line by line by jq and friends, and a
        // byte-order mark would sit in front of the first record's opening brace.
        _file = new StreamWriter(LogFilePath, append: true, new UTF8Encoding(false)) { AutoFlush = true };
    }
}
