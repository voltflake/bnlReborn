using BNLReloadedServer.Logging;

namespace BNLReloadedServer.Servers;

public class SessionReader(IServiceDispatcher dispatcher, string onError)
{
    private bool _packetInBuffer;
    private const int BodyMaxSize = 100000000;
    private MemoryStream _buffer = new();
    private long _lastPacketTicks = Environment.TickCount64;

    public TimeSpan SinceLastPacket =>
        TimeSpan.FromMilliseconds(Environment.TickCount64 - Interlocked.Read(ref _lastPacketTicks));

    public void ProcessPacket(byte[] buffer, long offset, long size)
    {
        // Anything arriving counts, including a fragment that does not complete a packet yet:
        // the question this answers is whether the other end is still there.
        Interlocked.Exchange(ref _lastPacketTicks, Environment.TickCount64);

        MemoryStream memStream;
        if (_packetInBuffer)
        {
            if (_buffer.Length > BodyMaxSize)
            {
                WipeBuffer();
                return;
            }

            var bufferPos = _buffer.Position;
            _buffer.Seek(_buffer.Length - _buffer.Position, SeekOrigin.Current);
            _buffer.Write(buffer, (int)offset, (int)size);
            _buffer.Position = bufferPos;
            memStream = new MemoryStream(_buffer.GetBuffer(), (int)_buffer.Position, (int)_buffer.Length);
        }
        else
        {
            memStream = new MemoryStream(buffer, (int)offset, (int)size);
        }

        using var reader = new BinaryReader(memStream);
        try
        {
            while (reader.BaseStream.Position < reader.BaseStream.Length)
            {
                // The first part of every packet is an 7 bit encoded int of its length.
                var startPosition = reader.BaseStream.Position;
                var startLength = reader.BaseStream.Length;

                var packetLength = reader.Read7BitEncodedInt();
                if (reader.BaseStream.Position + packetLength > reader.BaseStream.Length)
                {
                    if (Math.Max(startLength - startPosition, 0) > 0)
                    {
                        _packetInBuffer = true;
                        memStream.Position = startPosition;
                        _buffer.SetLength(0);
                        memStream.CopyTo(_buffer);
                        _buffer.Position = 0;
                    }

                    break;
                }

                var currentPosition = reader.BaseStream.Position;

                Log.Debug(LogCat.Net, $"Packet length: {packetLength}");

                if (!dispatcher.Dispatch(reader))
                {
                    // The dispatcher has already said what it could not route; this says what the
                    // bytes were, which is the part that identifies a peer talking another protocol.
                    Log.Warn(LogCat.Net, "Rejected frame: " +
                                         Hex(reader, startPosition, currentPosition + packetLength));

                    if (_packetInBuffer)
                        WipeBuffer();
                    break;
                }

                if (reader.BaseStream.Position < currentPosition + packetLength)
                {
                    reader.ReadBytes((int)(currentPosition + packetLength - reader.BaseStream.Position));
                }

                if (_packetInBuffer)
                    WipeBuffer();
            }
        }
        catch (EndOfStreamException)
        {
            Log.Warn(LogCat.Net, onError);
        }
        catch (Exception e)
        {
            Log.Error(LogCat.Net, "Packet processing failed", e);
        }
    }

    // Enough to see the length prefix, the service byte and the start of a body — a peer speaking
    // something else entirely gives itself away in the first few.
    private const int HexPreviewBytes = 32;

    private static string Hex(BinaryReader reader, long start, long end)
    {
        try
        {
            var stream = reader.BaseStream;

            // A frame that declares length zero still made the dispatcher read a byte, and that
            // byte is the one worth seeing, so never stop short of where the dispatcher got to.
            var stop = Math.Min(Math.Max(end, stream.Position), stream.Length);
            var available = stop - start;
            var count = (int)Math.Min(available, HexPreviewBytes);
            if (count <= 0) return "<empty>";

            // Reading from here is safe: the caller stops parsing this buffer either way.
            stream.Position = start;
            var preview = string.Join(' ', reader.ReadBytes(count).Select(b => b.ToString("X2")));
            return available > count ? $"{preview} ... ({available} bytes)" : preview;
        }
        catch (Exception)
        {
            return "<unreadable>";
        }
    }

    private void WipeBuffer()
    {
        _buffer = new MemoryStream();
        _packetInBuffer = false;
    }
}
