using System.Diagnostics;
using static EndlessOceanFilesConverter.Utils;
namespace EndlessOceanFilesConverter
{
    [DebuggerDisplay("{MagicRF,nq}{Version,nq}{Type,nq} | {FileCount}")]
    public class RFHeader_t
    {
        public string MagicRF;
        public string Version;
        public string Type;
        public ushort FileCount;
        public ushort FileListSize;
        public ushort Flag;
        public uint HeaderSize;
        public List<FileInfo_t> Files;

        public ERROR Error;

        public RFHeader_t(EndianBinaryReader br)
        {
            EndianBinaryReader.Endianness originalEndianness = br.GetEndianness();
            br.SetEndianness(EndianBinaryReader.Endianness.Little);

            MagicRF = br.ReadString(2);
            Version = br.ReadString(1);
            if (MagicRF != "RF")
            {
                Error = ERROR.RF_WRONG_MAGIC;
                return;
            }
            Type = br.ReadString(3);
            FileCount = br.ReadUInt16();
            FileListSize = br.ReadUInt16();
            Flag = br.ReadUInt16();
            HeaderSize = br.ReadUInt32();
            Files = new();

            for (int i = 0; i < FileCount; i++)
            {
                Files.Add(new(br, Version));
            }

            // If there're duplicate names
            // life/view/d093.mdl
            // life/view/d104.mdl
            // life/view/d109.mdl
            for (int i = 0; i < FileCount; i++)
            {
                int idx = 0;
                for (int j = i + 1; j < FileCount; j++)
                {
                    if (Files[i].Name == Files[j].Name)
                    {
                        Files[j].Name += $"_{idx}";
                        idx++;
                    }
                }
            }

            // d105.mdl
            for (int i = 0; i < FileCount; i++)
            {
                if (Files[i].Type == FILETYPE.TDL)
                {
                    if (Files[i].Name.EndsWith('.'))
                    {
                        Files[i].Name = Files[i].Name + "tdl";
                    }
                }
            }

            br.SetEndianness(originalEndianness);
        }

        public FileInfo_t? GetTextureFileInfoAtIndex(int index)
        {
            FileInfo_t? File = null;
            int count = 0;
            if (Version == "P")
            {
                for (int i = 0; i < Files.Count; i++)
                {
                    if (Files[i].Type == FILETYPE.TDL)
                    {
                        if (count == index)
                        {
                            File = Files[i];
                            break;
                        }
                        count++;
                    }
                }
            }
            else
            {
                for (int i = 0; i < Files.Count; i++)
                {
                    if (Files[i].Type == FILETYPE.TDL || Files[i].Type == FILETYPE.TXS)
                    {
                        if (count == index)
                        {
                            File = Files[i];
                            break;
                        }
                        count++;
                    }
                }
            }

            return File;
        }

        public FileInfo_t? GetFileInfoAtIndex(int index, FILETYPE fileType)
        {
            FileInfo_t? File = null;
            int count = 0;
            for (int i = 0; i < Files.Count; i++)
            {
                if (Files[i].Type == fileType)
                {
                    if (count == index)
                    {
                        File = Files[i];
                        break;
                    }
                    count++;
                }
            }

            return File;
        }
    }

    [DebuggerDisplay("{Name} | {Type} | IsInFile: {IsInFile}")]
    public class FileInfo_t
    {
        public string Name;
        public uint Size;
        public uint Offset;
        public FILETYPE Type;
        public bool IsInFile;

        public FileInfo_t(EndianBinaryReader br, string MagicRFVersion)
        {
            if (MagicRFVersion != "P") //RF2
            {
                Name = br.ReadString(0x14);
                Size = br.ReadUInt32();
                Offset = br.ReadUInt32();
                Type = (FILETYPE)br.ReadByte();
                br.Skip(1);
                IsInFile = br.ReadBoolean();
                br.Skip(1);
            }
            else //RFP
            {
                Name = br.ReadString(0x10);
                Offset = br.ReadUInt32();
                Size = br.ReadUInt32();
                br.BaseStream.Seek(0x4, SeekOrigin.Current);
                Type = (FILETYPE)br.ReadByte();
                br.Skip(1);
                IsInFile = br.ReadBoolean();
                br.Skip(1);
            }
        }

        public byte[] Read(EndianBinaryReader br, long baseAddress)
        {
            long temp = br.BaseStream.Position;
            br.BaseStream.Seek(baseAddress + Offset, SeekOrigin.Begin);
            byte[] data = br.ReadBytes((int)Size);
            br.BaseStream.Seek(temp, SeekOrigin.Begin);
            return data;
        }

        public byte[] Read(EndianBinaryReader br)
        {
            return Read(br, 0);
        }
    }

    public enum FILETYPE
    {
        VDL = 0,
        TDL = 1,
        TXS = 2,
        MDL = 5,
        MOT = 6,
        MOL = 7
    }
}
