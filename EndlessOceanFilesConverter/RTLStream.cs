using System.Diagnostics;
using System.Numerics;
using static EndlessOceanFilesConverter.Utils;

namespace EndlessOceanFilesConverter
{
    public class RTLStream
    {
        public string FilePath;
        public string FileName;
        public string Magic;
        public ushort Offset;
        public ushort ColumnCount;
        public ushort RowCount;
        public float ChunkSize;
        public float XChunkStart;
        public float ZChunkStart;

        public List<Rod_t> Rod;

        public ERROR Error;
        public RTLStream(EndianBinaryReader br, string filePath)
        {
            br.SetEndianness(EndianBinaryReader.Endianness.Big);
            FilePath = filePath;
            FileName = Path.GetFileName(filePath);
            Magic = br.ReadString(4);
            if (Magic != "RTL0")
            {
                Error = ERROR.RTL_WRONG_MAGIC;
                return;
            }
            br.Skip(2);
            Offset = br.ReadUInt16();
            ColumnCount = br.ReadUInt16();
            RowCount = br.ReadUInt16();
            ChunkSize = br.ReadSingle();
            XChunkStart = br.ReadSingle();
            ZChunkStart = br.ReadSingle();

            br.Seek(Offset, SeekOrigin.Begin);
            Rod = new();
            for (int i = 0; i < RowCount; i++)
            {
                for (int j = 0; j < ColumnCount; j++)
                {
                    ushort rodIndex = br.ReadUInt16();
                    if (rodIndex == 0xFFFF)
                    {
                        continue;
                    }
                    Rod.Add(new(rodIndex, new((j * ChunkSize) + XChunkStart, 0, (i * ChunkSize) + ZChunkStart)));
                }
            }
        }

        [DebuggerDisplay("{RodIndex} {Translation}")]
        public class Rod_t
        {
            public ushort RodIndex;
            public Vector3 Translation;
            public Rod_t(ushort rodIndex, Vector3 trans)
            {
                RodIndex = rodIndex;
                Translation = trans;
            }
        }
    }
}
