using System.Text;
using System.Text.Json;
using BNLReloadedServer.ControlPanel;
using BNLReloadedServer.Database;

namespace BNLReloadedServer.Logging;

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
                // Disk persistence is optional for the panel. A read-only or temporarily
                // unavailable log directory must not suppress the in-memory event that keeps
                // authenticated consoles live.
                if (_file != null)
                {
                    _file.WriteLine(Serialize(stamped));
                    if (_file.BaseStream.Length > MaxFileBytes)
                    {
                        _file.Dispose();
                        OpenLogFile();
                    }
                }
            }
            catch
            {
                // ignored — never let logging take the server down
            }
        }
        if (appended) ControlPanelEvents.Publish(ControlPanelEvent.Logs);
    }

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

    private static void OpenLogFile()
    {
        if (File.Exists(LogFilePath) && new FileInfo(LogFilePath).Length > MaxFileBytes)
            File.Move(LogFilePath, RotatedFilePath, overwrite: true);

        // No BOM: this file is meant to be read line by line by jq and friends, and a
        // byte-order mark would sit in front of the first record's opening brace.
        _file = new StreamWriter(LogFilePath, append: true, new UTF8Encoding(false)) { AutoFlush = true };
    }
}
