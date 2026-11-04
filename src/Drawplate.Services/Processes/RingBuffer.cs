using System;
using System.Text;

namespace Drawplate.Services.Processes;

/// <summary>Ring buffer holding the last 64 KiB of stderr output.</summary>
public sealed class RingBuffer
{
    private readonly byte[] _buffer;
    private int _writePos;
    private int _count;
    private bool _wrapped;

    public RingBuffer(int capacity = 65536)
    {
        _buffer = new byte[capacity];
    }

    public void Write(ReadOnlySpan<byte> data)
    {
        foreach (var b in data)
        {
            _buffer[_writePos] = b;
            _writePos = (_writePos + 1) % _buffer.Length;
            if (_count < _buffer.Length)
            {
                _count++;
            }
            else
            {
                _wrapped = true;
            }
        }
    }

    public string ReadTail()
    {
        if (_count == 0)
        {
            return "";
        }

        var start = _wrapped ? _writePos : 0;
        var len = _count;
        var result = new StringBuilder(len);
        for (var i = 0; i < len; i++)
        {
            result.Append((char)_buffer[(start + i) % _buffer.Length]);
        }
        return result.ToString();
    }
}
