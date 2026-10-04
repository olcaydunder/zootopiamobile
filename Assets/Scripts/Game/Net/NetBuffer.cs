using System;
using System.Runtime.InteropServices;
using System.Text;

/// <summary>Reinterprets float bits without allocating.</summary>
[StructLayout(LayoutKind.Explicit)]
internal struct FloatBits
{
    [FieldOffset(0)] public float f;
    [FieldOffset(0)] public uint u;
}

/// <summary>Little-endian binary writer for network messages (plain C#, no Unity types).</summary>
public sealed class NetWriter
{
    private byte[] data;
    private int length;

    public NetWriter(int capacity = 256)
    {
        data = new byte[Math.Max(16, capacity)];
    }

    public int Length { get { return length; } }
    public byte[] Buffer { get { return data; } }

    public void Reset()
    {
        length = 0;
    }

    private void Ensure(int extra)
    {
        if (length + extra <= data.Length)
            return;
        int size = data.Length * 2;
        while (size < length + extra)
            size *= 2;
        Array.Resize(ref data, size);
    }

    public byte[] ToArray()
    {
        var copy = new byte[length];
        System.Buffer.BlockCopy(data, 0, copy, 0, length);
        return copy;
    }

    public void Byte(int v)
    {
        Ensure(1);
        data[length++] = (byte)v;
    }

    public void SByte(int v)
    {
        Byte((byte)(sbyte)Math.Max(-128, Math.Min(127, v)));
    }

    public void Bool(bool v)
    {
        Byte(v ? 1 : 0);
    }

    public void UShort(int v)
    {
        Ensure(2);
        data[length++] = (byte)v;
        data[length++] = (byte)(v >> 8);
    }

    public void Short(int v)
    {
        UShort((ushort)(short)Math.Max(short.MinValue, Math.Min(short.MaxValue, v)));
    }

    public void UInt(uint v)
    {
        Ensure(4);
        data[length++] = (byte)v;
        data[length++] = (byte)(v >> 8);
        data[length++] = (byte)(v >> 16);
        data[length++] = (byte)(v >> 24);
    }

    public void Int(int v)
    {
        UInt((uint)v);
    }

    public void Float(float v)
    {
        var b = new FloatBits();
        b.f = v;
        UInt(b.u);
    }

    /// <summary>UTF-8 text, at most <paramref name="maxBytes"/> bytes (cut on a character boundary).</summary>
    public void String(string s, int maxBytes = 64)
    {
        if (s == null)
            s = "";
        byte[] bytes = Encoding.UTF8.GetBytes(s);
        int n = Math.Min(bytes.Length, Math.Min(255, maxBytes));
        while (n > 0 && n < bytes.Length && (bytes[n] & 0xC0) == 0x80)
            n--;   // don't split a multi-byte character
        Byte(n);
        Ensure(n);
        System.Buffer.BlockCopy(bytes, 0, data, length, n);
        length += n;
    }

    public void Bytes(byte[] src, int offset, int count)
    {
        Ensure(count);
        System.Buffer.BlockCopy(src, offset, data, length, count);
        length += count;
    }
}

/// <summary>Reads what <see cref="NetWriter"/> wrote. Throws <see cref="NetFormatException"/> on a short or malformed message.</summary>
public sealed class NetReader
{
    private byte[] data;
    private int pos;
    private int end;

    public NetReader() { }

    public NetReader(byte[] buffer, int offset, int count)
    {
        Set(buffer, offset, count);
    }

    public void Set(byte[] buffer, int offset, int count)
    {
        data = buffer;
        pos = offset;
        end = offset + count;
    }

    public int Remaining { get { return end - pos; } }
    public int Position { get { return pos; } }

    private void Need(int n)
    {
        if (n < 0 || pos + n > end)
            throw new NetFormatException();
    }

    public int Byte()
    {
        Need(1);
        return data[pos++];
    }

    public int SByte()
    {
        return (sbyte)(byte)Byte();
    }

    public bool Bool()
    {
        return Byte() != 0;
    }

    public int UShort()
    {
        Need(2);
        int v = data[pos] | (data[pos + 1] << 8);
        pos += 2;
        return v;
    }

    public int Short()
    {
        return (short)(ushort)UShort();
    }

    public uint UInt()
    {
        Need(4);
        uint v = (uint)(data[pos] | (data[pos + 1] << 8) | (data[pos + 2] << 16) | (data[pos + 3] << 24));
        pos += 4;
        return v;
    }

    public int Int()
    {
        return (int)UInt();
    }

    public float Float()
    {
        var b = new FloatBits();
        b.u = UInt();
        return b.f;
    }

    public string String()
    {
        int n = Byte();
        Need(n);
        string s = Encoding.UTF8.GetString(data, pos, n);
        pos += n;
        return s;
    }

    public void Skip(int n)
    {
        Need(n);
        pos += n;
    }

    public byte[] Bytes(int n)
    {
        Need(n);
        var b = new byte[n];
        System.Buffer.BlockCopy(data, pos, b, 0, n);
        pos += n;
        return b;
    }
}

public sealed class NetFormatException : Exception
{
    public NetFormatException() : base("malformed network message") { }
}
