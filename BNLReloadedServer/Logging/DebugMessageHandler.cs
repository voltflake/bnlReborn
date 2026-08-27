using System.Runtime.CompilerServices;
using System.Text;

namespace BNLReloadedServer.Logging;

[InterpolatedStringHandler]
public ref struct DebugMessageHandler
{
    private readonly StringBuilder? _builder;

    public DebugMessageHandler(int literalLength, int formattedCount, out bool shouldAppend)
    {
        if (!Log.Enabled(LogLevel.Debug))
        {
            _builder = null;
            shouldAppend = false;
            return;
        }

        _builder = new StringBuilder(literalLength + formattedCount * 12);
        shouldAppend = true;
    }

    public readonly bool IsEnabled => _builder != null;

    public readonly void AppendLiteral(string value) => _builder!.Append(value);

    public readonly void AppendFormatted<T>(T value) => _builder!.Append(value?.ToString());

    public readonly void AppendFormatted<T>(T value, string? format) where T : IFormattable =>
        _builder!.Append(value?.ToString(format, null));

    public readonly override string ToString() => _builder?.ToString() ?? string.Empty;
}
