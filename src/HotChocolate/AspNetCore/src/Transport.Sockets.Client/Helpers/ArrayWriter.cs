using System;
using System.Buffers;
using static HotChocolate.Transport.Sockets.SocketDefaults;

namespace HotChocolate.Transport.Sockets.Client.Helpers;

internal sealed class ArrayWriter
    : IBufferWriter<byte>
    , IDisposable
{
    private byte[] _buffer;
    private int _capacity;
    private int _start;
    private bool _disposed;

    public ArrayWriter()
    {
        _buffer = ArrayPool<byte>.Shared.Rent(BufferSize);
        _capacity = _buffer.Length;
        _start = 0;
    }

    public ReadOnlyMemory<byte> Body => _buffer.AsMemory().Slice(0, _start);

    public ArraySegment<byte> ToArraySegment() => new(_buffer, 0, _start);

    public void Advance(int count)
    {
        _start += count;
        _capacity -= count;
    }

    public Memory<byte> GetMemory(int sizeHint = 0)
    {
        var size = sizeHint < 1 ? BufferSize : sizeHint;
        EnsureBufferCapacity(size);
        return _buffer.AsMemory().Slice(_start, size);
    }

    public Span<byte> GetSpan(int sizeHint = 0)
    {
        var size = sizeHint < 1 ? BufferSize : sizeHint;
        EnsureBufferCapacity(size);
        return _buffer.AsSpan().Slice(_start, size);
    }

    private void EnsureBufferCapacity(int neededCapacity)
    {
        if (_capacity < neededCapacity)
        {
            var buffer = _buffer;

            var newSize = buffer.Length * 2;
            if (neededCapacity > buffer.Length)
            {
                newSize += neededCapacity;
            }

            _buffer = ArrayPool<byte>.Shared.Rent(newSize);
            _capacity += _buffer.Length - buffer.Length;

            buffer.AsSpan().CopyTo(_buffer);
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            ArrayPool<byte>.Shared.Return(_buffer);
            _buffer = Array.Empty<byte>();
            _disposed = true;
        }
    }
}