using System.Runtime.CompilerServices;
using System.Text;

namespace BNLReloadedServer.Logging;

/// <summary>
/// Makes <c>Log.Debug(cat, $"...")</c> free when debug lines are switched off.
///
/// An interpolated string argument is normally built before the call, so the twenty-odd
/// per-packet debug lines would allocate a string and format their arguments on every
/// packet only for the call to drop it. The compiler hands the interpolation to this
/// handler instead, and the <c>shouldAppend</c> flag stops it before a single character
/// is formatted — which is what the old <c>if (DebugMode())</c> around each call site did,
/// without the call sites having to remember.
/// </summary>
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
