using System.Text;
using BNLReloadedServer.Logging;

namespace BNLReloadedServer.ControlPanel;

public sealed class BroadcastingTextWriter(TextWriter inner) : TextWriter
{
    private readonly StringBuilder _pending = new();
    private readonly object _lock = new();

    public override Encoding Encoding => inner.Encoding;

    public override void Write(char value)
    {
        inner.Write(value);
        Accumulate(value.ToString());
    }

    public override void Write(string? value)
    {
        inner.Write(value);
        if (!string.IsNullOrEmpty(value)) Accumulate(value);
    }

    public override void WriteLine(string? value)
    {
        inner.WriteLine(value);
        Accumulate((value ?? string.Empty) + "\n");
    }

    public override void WriteLine()
    {
        inner.WriteLine();
        Accumulate("\n");
    }

    public override void Flush() => inner.Flush();

    private void Accumulate(string text)
    {
        lock (_lock)
        {
            foreach (var c in text)
            {
                if (c == '\n')
                {
                    var line = _pending.ToString().TrimEnd('\r');
                    _pending.Clear();
                    if (!string.IsNullOrWhiteSpace(line)) Log.Raw(line);
                }
                else
                {
                    _pending.Append(c);
                }
            }
        }
    }
}
