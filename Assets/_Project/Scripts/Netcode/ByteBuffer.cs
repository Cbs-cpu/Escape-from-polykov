using System;
using System.Runtime.InteropServices;

namespace Polykov.Netcode
{
    /// <summary>Allocation-free little-endian writer over a caller-owned buffer.</summary>
    public struct ByteWriter
    {
        private readonly byte[] _buffer;

        public int Length { get; private set; }

        public ByteWriter(byte[] buffer)
        {
            _buffer = buffer;
            Length = 0;
        }

        public void WriteByte(byte value)
        {
            Ensure(1);
            _buffer[Length++] = value;
        }

        public void WriteSByte(sbyte value) => WriteByte(unchecked((byte)value));

        public void WriteUShort(ushort value)
        {
            Ensure(2);
            _buffer[Length++] = (byte)value;
            _buffer[Length++] = (byte)(value >> 8);
        }

        public void WriteShort(short value) => WriteUShort(unchecked((ushort)value));

        public void WriteUInt(uint value)
        {
            Ensure(4);
            _buffer[Length++] = (byte)value;
            _buffer[Length++] = (byte)(value >> 8);
            _buffer[Length++] = (byte)(value >> 16);
            _buffer[Length++] = (byte)(value >> 24);
        }

        public void WriteFloat(float value) => WriteUInt(new FloatBits { Float = value }.Bits);

        private void Ensure(int bytes)
        {
            if (Length + bytes > _buffer.Length) throw new InvalidOperationException("ByteWriter buffer full");
        }
    }

    /// <summary>Allocation-free little-endian reader.</summary>
    public struct ByteReader
    {
        private readonly byte[] _buffer;
        private readonly int _length;

        public int Position { get; private set; }
        public int Remaining => _length - Position;

        public ByteReader(byte[] buffer, int length)
        {
            _buffer = buffer;
            _length = length;
            Position = 0;
        }

        public byte ReadByte()
        {
            Ensure(1);
            return _buffer[Position++];
        }

        public sbyte ReadSByte() => unchecked((sbyte)ReadByte());

        public ushort ReadUShort()
        {
            Ensure(2);
            int v = _buffer[Position] | (_buffer[Position + 1] << 8);
            Position += 2;
            return (ushort)v;
        }

        public short ReadShort() => unchecked((short)ReadUShort());

        public uint ReadUInt()
        {
            Ensure(4);
            uint v = (uint)(_buffer[Position] | (_buffer[Position + 1] << 8) | (_buffer[Position + 2] << 16) | (_buffer[Position + 3] << 24));
            Position += 4;
            return v;
        }

        public float ReadFloat() => new FloatBits { Bits = ReadUInt() }.Float;

        private void Ensure(int bytes)
        {
            if (Position + bytes > _length) throw new InvalidOperationException("ByteReader out of data");
        }
    }
}

namespace Polykov.Netcode
{
    /// <summary>Float/uint reinterpretation that works on every .NET profile Unity uses.</summary>
    [StructLayout(LayoutKind.Explicit)]
    internal struct FloatBits
    {
        [FieldOffset(0)] public float Float;
        [FieldOffset(0)] public uint Bits;
    }
}
