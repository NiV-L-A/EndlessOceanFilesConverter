using System.Buffers.Binary;
using System.Diagnostics;
using System.Text;

namespace EndlessOceanFilesConverter
{
    [DebuggerDisplay("{BaseStream.Position}")]
    public class EndianBinaryReader : BinaryReader
    {
        public enum Endianness
        {
            Little,
            Big,
        }

        private Endianness _endianness = Endianness.Little;

        public void SetEndianness(Endianness newEndianness)
        {
            _endianness = newEndianness;
        }

        public Endianness GetEndianness()
        {
            return _endianness;
        }

        public EndianBinaryReader(byte[] input) : base(new MemoryStream(input))
        {
        }

        public EndianBinaryReader(byte[] input, Endianness endianness) : base(new MemoryStream(input))
        {
            _endianness = endianness;
        }

        public EndianBinaryReader(Stream input) : base(input)
        {
        }

        public EndianBinaryReader(Stream input, Encoding encoding) : base(input, encoding)
        {
        }

        public EndianBinaryReader(Stream input, Encoding encoding, bool leaveOpen) : base(
            input, encoding, leaveOpen)
        {
        }

        public EndianBinaryReader(Stream input, Endianness endianness) : base(input)
        {
            _endianness = endianness;
        }

        public EndianBinaryReader(Stream input, Encoding encoding, Endianness endianness) :
            base(input, encoding)
        {
            _endianness = endianness;
        }

        public EndianBinaryReader(Stream input, Encoding encoding, bool leaveOpen,
            Endianness endianness) : base(input, encoding, leaveOpen)
        {
            _endianness = endianness;
        }

        public void Skip(long v)
        {
            this.BaseStream.Seek(v, SeekOrigin.Current);
        }

        public void Seek(long v, SeekOrigin seekOrigin)
        {
            this.BaseStream.Seek(v, seekOrigin);
        }

        public override short ReadInt16() => ReadInt16(_endianness);
        public short ReadInt16(Endianness endianness) => endianness == Endianness.Little
            ? BinaryPrimitives.ReadInt16LittleEndian(ReadBytes(sizeof(short)))
            : BinaryPrimitives.ReadInt16BigEndian(ReadBytes(sizeof(short)));

        public override ushort ReadUInt16() => ReadUInt16(_endianness);
        public ushort ReadUInt16(Endianness endianness) => endianness == Endianness.Little
            ? BinaryPrimitives.ReadUInt16LittleEndian(ReadBytes(sizeof(ushort)))
            : BinaryPrimitives.ReadUInt16BigEndian(ReadBytes(sizeof(ushort)));

        public override int ReadInt32() => ReadInt32(_endianness);
        public int ReadInt32(Endianness endianness) => endianness == Endianness.Little
            ? BinaryPrimitives.ReadInt32LittleEndian(ReadBytes(sizeof(int)))
            : BinaryPrimitives.ReadInt32BigEndian(ReadBytes(sizeof(int)));

        public override uint ReadUInt32() => ReadUInt32(_endianness);
        public uint ReadUInt32(Endianness endianness) => endianness == Endianness.Little
            ? BinaryPrimitives.ReadUInt32LittleEndian(ReadBytes(sizeof(uint)))
            : BinaryPrimitives.ReadUInt32BigEndian(ReadBytes(sizeof(uint)));

        public override long ReadInt64() => ReadInt64(_endianness);
        public long ReadInt64(Endianness endianness) => endianness == Endianness.Little
            ? BinaryPrimitives.ReadInt64LittleEndian(ReadBytes(sizeof(long)))
            : BinaryPrimitives.ReadInt64BigEndian(ReadBytes(sizeof(long)));

        public override ulong ReadUInt64() => ReadUInt64(_endianness);
        public ulong ReadUInt64(Endianness endianness) => endianness == Endianness.Little
            ? BinaryPrimitives.ReadUInt64LittleEndian(ReadBytes(sizeof(ulong)))
            : BinaryPrimitives.ReadUInt64BigEndian(ReadBytes(sizeof(ulong)));

        public override float ReadSingle() => ReadSingle(_endianness);
        public float ReadSingle(Endianness endianness) => endianness == Endianness.Little
            ? BinaryPrimitives.ReadSingleLittleEndian(ReadBytes(sizeof(float)))
            : BinaryPrimitives.ReadSingleBigEndian(ReadBytes(sizeof(float)));

        public string ReadString(uint maxLength)
        {
            // Returns a string from the current position until either a 0x00 byte is encountered or the length of the string is >= MaxLength.
            // In either case, advance the current position by MaxLength.
            string str = "";
            char ch;
            while ((ch = (char)PeekChar()) != 0x00 && (str.Length < maxLength))
            {
                ch = ReadChar();
                str += ch;
            }
            BaseStream.Seek(maxLength - str.Length, SeekOrigin.Current);
            return str;
        }
    }
}
