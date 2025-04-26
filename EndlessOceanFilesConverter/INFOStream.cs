namespace EndlessOceanFilesConverter
{
    class INFOStream
    {
        public Header_t Header;
        public List<File_t> Files;

        public INFOStream(byte[] data)
        {
            if (data[0] == 0x2A)
            {
                // EO1, encrypted
                data = DecryptData(data);
            }

            using EndianBinaryReader br = new(data, EndianBinaryReader.Endianness.Little);
            Header = new(br);
            Files = new(Header.FileCount);
            for (int i = 0; i < Header.FileCount; i++)
            {
                Files.Add(new(br));
            }
        }

        public class Header_t
        {
            public string Key;
            public ushort Alignment;
            public int FileCount;
            public Header_t(EndianBinaryReader br)
            {
                br.Skip(0x1);
                Key = br.ReadString(0xF);
                br.BaseStream.Seek(0x24, SeekOrigin.Begin);
                Alignment = br.ReadUInt16();
                if (Alignment == 0)
                {
                    // eo1 uses 0x800 as default alignment
                    Alignment = 0x800;
                }
                br.BaseStream.Seek(0x2C, SeekOrigin.Begin);
                FileCount = br.ReadInt32();
            }
        }

        public class File_t
        {
            public string Name;
            public int Size;
            public int Offset;
            public int SizeWithAlignment;
            public int SizeCopy;

            public File_t(EndianBinaryReader br)
            {
                Name = br.ReadString(0x20);
                Size = br.ReadInt32();
                Offset = br.ReadInt32();
                SizeWithAlignment = br.ReadInt32();
                SizeCopy = br.ReadInt32();
            }
        }

        public static byte[] DecryptData(byte[] data)
        {
            byte[] decryptedData = new byte[data.Length];
            Array.Copy(data, decryptedData, 0x10); // Copy the key

            for (int i = 0x10; i < data.Length; i++)
            {
                // 0b0010_0111 = 0x27 currentByte
                // 0b0111_0010 = 0x72 swap bits
                // 0b1000_1101 = 0x8D negate
                // 0x63 = 0x8D - data[currentPosition % 0x10]
                byte currentByte = data[i];
                currentByte = (byte)((currentByte << 4) | (currentByte >> 4));
                currentByte = (byte)~currentByte;
                currentByte = (byte)(currentByte - data[i % 0x10]);
                decryptedData[i] = currentByte;
            }

            return decryptedData;
        }

        //public static byte[] EncryptData(byte[] data)
        //{
        //    byte[] encryptedData = new byte[data.Length];
        //    Array.Copy(data, encryptedData, 0x10); // Copy the key

        //    for (int i = 0x10; i < data.Length; i++)
        //    {
        //        byte currentByte = data[i];
        //        currentByte = (byte)(currentByte + data[i % 0x10]);
        //        currentByte = (byte)~currentByte;
        //        currentByte = (byte)((currentByte << 4) | (currentByte >> 4));
        //        encryptedData[i] = currentByte;
        //    }
        //    return encryptedData;
        //}
    }
}
