using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.Diagnostics;

namespace EndlessOceanFilesConverter
{
    [DebuggerDisplay("{FileName} | Format: {Header.Magic}")]
    public class TDLStream
    {
        public long AbsoluteOffset;
        public string FilePath;
        public string FileName;

        public Header_t Header;
        public Data_t Data;

        public TDLStream(byte[] data, string fileName)
        {
            using MemoryStream ms = new(data);
            using EndianBinaryReader br = new(ms);
            Init(br, fileName);
        }

        public TDLStream(EndianBinaryReader br, string filePath)
        {
            FilePath = filePath;
            Init(br, Path.GetFileName(filePath));
        }

        void Init(EndianBinaryReader br, string fileName)
        {
            br.SetEndianness(EndianBinaryReader.Endianness.Big);
            AbsoluteOffset = br.BaseStream.Position;
            FileName = fileName;

            Header = new(br);
            br.Seek(AbsoluteOffset + Header.PixelDataStart, SeekOrigin.Begin);
            Data = new(br, Header);
        }

        public void SaveAsPNG()
        {
            if (FilePath == null || FilePath == "")
            {
                return;
            }

            SaveAsPNG(FilePath);
        }

        public void SaveAsPNG(string filePath)
        {
            Image<Rgba32> image = new(Header.TotalWidth, Header.TotalHeight);
            for (int row = 0; row < Header.TotalHeight; row++)
            {
                for (int col = 0; col < Header.TotalWidth; col++)
                {
                    var value = Data.RGBA[row][col];
                    Rgba32 pixel = new(value.R, value.G, value.B, value.A);
                    image[col, row] = pixel;
                }
            }

            image.Save($"{filePath}.png");
        }

        public class Header_t
        {
            public string Magic;
            public uint unk01;
            public ushort TotalWidth;
            public ushort TotalHeight;
            public ushort TextureCount;
            public ushort MipmapCount;
            public FORMAT Format;
            public byte unk02;
            public ushort PaletteSize;
            public uint PixelDataStart;
            public uint PaletteStart;
            public List<TextureHeader_t> TextureHeader;

            public Header_t(EndianBinaryReader br)
            {
                Magic = br.ReadString(4);
                unk01 = br.ReadUInt32();
                TotalWidth = br.ReadUInt16();
                TotalHeight = br.ReadUInt16();
                TextureCount = br.ReadUInt16();
                MipmapCount = br.ReadUInt16();
                Format = (FORMAT)br.ReadByte();
                unk02 = br.ReadByte();
                PaletteSize = br.ReadUInt16();
                PixelDataStart = br.ReadUInt32();
                PaletteStart = br.ReadUInt32();

                TextureHeader = new(TextureCount);
                for (int i = 0; i < TextureCount; i++)
                {
                    TextureHeader.Add(new(br));
                }
            }
        }

        public class TextureHeader_t
        {
            public uint unk01;
            public ushort Width;
            public ushort Height;
            public ushort WidthOffset;
            public ushort HeightOffset;

            public TextureHeader_t(EndianBinaryReader br)
            {
                unk01 = br.ReadUInt32();
                Width = br.ReadUInt16();
                Height = br.ReadUInt16();
                WidthOffset = br.ReadUInt16();
                HeightOffset = br.ReadUInt16();
            }
        }

        public class Data_t
        {
            public List<System.Drawing.Color[]> RGBA;

            public Data_t(EndianBinaryReader br, Header_t Header)
            {
                switch (Header.Format)
                {
                    case FORMAT.I8:
                        I8_t I8 = new(br, Header.TotalHeight, Header.TotalWidth);
                        RGBA = I8.RGBA;
                        break;
                    case FORMAT.IA8:
                        IA8_t IA8 = new(br, Header.TotalHeight, Header.TotalWidth);
                        RGBA = IA8.RGBA;
                        break;
                    case FORMAT.C8:
                        C8_t C8 = new(br, Header.TotalHeight, Header.TotalWidth, Header.PaletteSize);
                        RGBA = C8.RGBA;
                        break;
                    case FORMAT.RGB5A3:
                        RGB5A3_t RGB5A3 = new(br, Header.TotalHeight, Header.TotalWidth);
                        RGBA = RGB5A3.RGBA;
                        break;
                    case FORMAT.RGBA8:
                        RGBA8_t RGBA8 = new(br, Header.TotalHeight, Header.TotalWidth);
                        RGBA = RGBA8.RGBA;
                        break;
                    case FORMAT.CMPR:
                        CMPR_t CMPR = new(br, Header.TotalHeight, Header.TotalWidth);
                        RGBA = CMPR.RGBA;
                        break;
                }
            }
        }

        public enum FORMAT
        {
            // The Format field is an index to a table in main.dol (in eo2 at 0x55ADA8),
            // which lists the image formats
            // Game value -> TPL value -> Name
            // 0x0 -> 0x0 -> I4
            // 0x1 -> 0x1 -> I8
            // 0x2 -> 0x2 -> IA4
            // 0x3 -> 0x3 -> IA8
            // 0x4 -> 0x8 -> C4
            // 0x5 -> 0x9 -> C8
            // 0x6 -> 0xA -> C14X2
            // 0x7 -> 0x4 -> RGB565
            // 0x8 -> 0x5 -> RGB5A3
            // 0x9 -> 0x6 -> RGBA8
            // 0xA -> 0xE -> CMPR

            //I4 = 0x0, // none found in the games
            I8 = 0x1,
            //IA4 = 0x2, // none found in the games
            IA8 = 0x3,
            //C4 = 0x4, // none found in the games
            C8 = 0x5,
            //C14X2 = 0x6, // none found in the games
            //RGB565 = 0x7, // none found in the games (but used for CMPR)
            RGB5A3 = 0x8,
            RGBA8 = 0x9,
            CMPR = 0xA,
        }
    }

    public class I8_t
    {
        public List<System.Drawing.Color[]> RGBA;

        public I8_t(EndianBinaryReader br, ushort height, ushort width)
        {
            RGBA = new();
            int blocksCountHeight = height / 4;
            int blocksCountWidth = width / 8;

            for (int i = 0; i < blocksCountHeight; i++)
            {
                System.Drawing.Color[][] rows = new System.Drawing.Color[4][];
                for (int j = 0; j < 4; j++)
                {
                    rows[j] = new System.Drawing.Color[width];
                }

                for (int j = 0; j < blocksCountWidth; j++)
                {
                    for (int k = 0; k < 4; k++)
                    {
                        for (int l = 0; l < 8; l++)
                        {
                            rows[k][j * 8 + l] = ConvertToColor(br.ReadByte());
                        }
                    }
                }

                RGBA.AddRange(rows);
            }
        }

        System.Drawing.Color ConvertToColor(byte value)
        {
            byte a = 0xFF;
            byte r = value;
            byte g = value;
            byte b = value;
            return System.Drawing.Color.FromArgb(a, r, g, b);
        }
    }

    public class IA8_t
    {
        public List<System.Drawing.Color[]> RGBA;

        public IA8_t(EndianBinaryReader br, ushort height, ushort width)
        {
            RGBA = new();
            int blocksCountHeight = height / 4;
            int blocksCountWidth = width / 4;

            for (int i = 0; i < blocksCountHeight; i++)
            {
                System.Drawing.Color[][] rows = new System.Drawing.Color[4][];
                for (int j = 0; j < 4; j++)
                {
                    rows[j] = new System.Drawing.Color[width];
                }

                for (int block = 0; block < blocksCountWidth; block++)
                {
                    for (int row = 0; row < 4; row++)
                    {
                        for (int pixel = 0; pixel < 4; pixel++)
                        {
                            rows[row][block * 4 + pixel] = ConvertToColor(br.ReadByte(), br.ReadByte());
                        }
                    }
                }

                RGBA.AddRange(rows);
            }
        }

        System.Drawing.Color ConvertToColor(byte alpha, byte rgb)
        {
            return System.Drawing.Color.FromArgb(alpha, rgb, rgb, rgb);
        }
    }

    public class C8_t
    {
        public List<System.Drawing.Color[]> RGBA;

        public C8_t(EndianBinaryReader br, ushort height, ushort width, ushort paletteSize)
        {
            RGBA = new();
            int blocksCountHeight = height / 4;
            int blocksCountWidth = width / 8;
            List<byte[]> paletteIndex = new();
            for (int i = 0; i < blocksCountHeight; i++)
            {
                byte[][] rows = new byte[4][];
                for (int j = 0; j < 4; j++)
                {
                    rows[j] = new byte[width];
                }

                for (int block = 0; block < blocksCountWidth; block++)
                {
                    for (int row = 0; row < 4; row++)
                    {
                        for (int pixel = 0; pixel < 8; pixel++)
                        {
                            rows[row][block * 8 + pixel] = br.ReadByte();
                        }
                    }
                }

                paletteIndex.AddRange(rows);
            }

            RGB5A3_t paletteValues = new(br, paletteSize);
            for (int i = 0; i < blocksCountHeight; i++)
            {
                System.Drawing.Color[][] rows = new System.Drawing.Color[4][];
                for (int j = 0; j < 4; j++)
                {
                    rows[j] = new System.Drawing.Color[width];
                }

                for (int block = 0; block < blocksCountWidth; block++)
                {
                    for (int row = 0; row < 4; row++)
                    {
                        for (int pixel = 0; pixel < 8; pixel++)
                        {
                            byte pixelIndex = paletteIndex[i * 4 + row][block * 8 + pixel];
                            System.Drawing.Color color = paletteValues.RGBA[0][pixelIndex];
                            rows[row][block * 8 + pixel] = color;
                        }
                    }
                }

                RGBA.AddRange(rows);
            }
        }
    }

    public class RGB5A3_t
    {
        public List<System.Drawing.Color[]> RGBA;

        // This constructor is used for the C8 format
        public RGB5A3_t(EndianBinaryReader br, ushort size)
        {
            RGBA = new();
        
            int length = size / 2;
            System.Drawing.Color[] values = new System.Drawing.Color[length];
            for (int i = 0; i < length; i++)
            {
                values[i] = ConvertToColor(br.ReadUInt16());
            }
            RGBA.Add(values);
        }

        public RGB5A3_t(EndianBinaryReader br, ushort height, ushort width)
        {
            RGBA = new();
            int blocksCountHeight = height / 4;
            int blocksCountWidth = width / 4;

            for (int i = 0; i < blocksCountHeight; i++)
            {
                System.Drawing.Color[][] rows = new System.Drawing.Color[4][];
                for (int j = 0; j < 4; j++)
                {
                    rows[j] = new System.Drawing.Color[width];
                }

                for (int block = 0; block < blocksCountWidth; block++)
                {
                    for (int row = 0; row < 4; row++)
                    {
                        for (int pixel = 0; pixel < 4; pixel++)
                        {
                            rows[row][block * 4 + pixel] = ConvertToColor(br.ReadUInt16());
                        }
                    }
                }

                RGBA.AddRange(rows);
            }
        }

        System.Drawing.Color ConvertToColor(ushort value)
        {
            if ((value & 0x8000) == 0)
            {
                // 0AAARRRRGGGGBBBB
                // A = AAA * 0x20
                // R = RRRR * 0x11
                // G = GGGG * 0x11
                // B = BBBB * 0x11
                int a = ((value >> 12) & 0x07) * 0x20;
                int r = ((value >> 8) & 0x0F) * 0x11;
                int g = ((value >> 4) & 0x0F) * 0x11;
                int b = (value & 0x0F) * 0x11;

                return System.Drawing.Color.FromArgb(a, r, g, b);
            }
            else
            {
                // 1RRRRRGGGGGBBBBB
                // A = 0xFF
                // R = RRRRR * 0x8
                // G = GGGGG * 0x8
                // B = BBBBB * 0x8
                int a = 0xFF;
                int r = ((value >> 10) & 0x1F) * 0x8;
                int g = ((value >> 5) & 0x1F) * 0x8;
                int b = (value & 0x1F) * 0x8;

                return System.Drawing.Color.FromArgb(a, r, g, b);
            }
        }
    }

    public class RGBA8_t
    {
        public List<System.Drawing.Color[]> RGBA;

        public RGBA8_t(EndianBinaryReader br, ushort height, ushort width)
        {
            RGBA = new(height);
            int blocksCountHeight = height / 4;
            int blocksCountWidth = width / 4;
            for (int i = 0; i < blocksCountHeight; i++)
            {
                System.Drawing.Color[][] rows = new System.Drawing.Color[4][];
                for (int j = 0; j < 4; j++)
                {
                    rows[j] = new System.Drawing.Color[width];
                }

                for (int j = 0; j < blocksCountWidth; j++)
                {
                    byte[] data = br.ReadBytes(0x40);
                    for (int row = 0; row < 4; row++)
                    {
                        for (int pixel = 0; pixel < 4; pixel++)
                        {
                            // ARARARARARARARAR
                            // ARARARARARARARAR
                            // GBGBGBGBGBGBGBGB
                            // GBGBGBGBGBGBGBGB
                            var index = 0x02 * pixel + 0x08 * row;
                            rows[row][j * 4 + pixel] = System.Drawing.Color.FromArgb
                            (
                                data[index], data[index + 0x01], // AR
                                data[index + 0x20], data[index + 0x21] // GB
                            );
                        }
                    }
                }
                RGBA.AddRange(rows);
            }
        }
    }

    public class CMPR_t
    {
        public List<System.Drawing.Color[]> RGBA;

        public CMPR_t(EndianBinaryReader br, ushort height, ushort width)
        {
            RGBA = new(height);
            int blocksCountHeight = height / 8;
            int blocksCountWidth = width / 8;

            for (int i = 0; i < blocksCountHeight; i++)
            {
                System.Drawing.Color[][] rows = new System.Drawing.Color[8][];
                for (int j = 0; j < 8; j++)
                {
                    rows[j] = new System.Drawing.Color[width];
                }

                // Each block is 0x20 bytes, which is 8x8 pixels.
                // These 0x20 bytes are separated in 2 regions:
                // the first 0x10 bytes are for row 1 to 4, and the last 0x10 bytes are for row 5 to 8
                // Each region is composed of 2 sub-regions, 0x8 bytes each
                // The first 4 bytes are 2 ushorts which represents 2 RGB565 values for the first 2 palette entries
                // (there are 2 more palette entries which are calculated from these first 2 palette values)
                // The next 4 bytes are indices for the current palette in this sub-region.
                // Because there are 4 palette values available, the minimum index is 0 (0b00) and the maximum index is 3 (0b11).
                // These 4 bytes need to be read 2 bits at a time. So, each byte (2 bits + 2 bits + 2 bits + 2 bits) represents 4 pixels of the current row.

                // "byte" and "bNN" here refers to the byte of the indices
                // byte01 = first 4 pixels of row 1
                // byte02 = first 4 pixels of row 2
                // byte03 = first 4 pixels of row 3
                // byte04 = first 4 pixels of row 4
                // byte05 = next 4 pixels of row 1
                // byte06 = next 4 pixels of row 2
                // byte07 = next 4 pixels of row 3
                // byte08 = next 4 pixels of row 4

                // byte09 = first 4 pixels of row 5
                // byte10 = first 4 pixels of row 6
                // byte11 = first 4 pixels of row 7
                // byte12 = first 4 pixels of row 8
                // byte13 = next 4 pixels of row 5
                // byte14 = next 4 pixels of row 6
                // byte15 = next 4 pixels of row 7
                // byte16 = next 4 pixels of row 8

                // RowN: pixel1 pixel2 pixel3 pixel4 | pixel5 pixel6 pixel7 pixel8
                // Row1: b01 b01 b01 b01 | b05 b05 b05 b05
                // Row2: b02 b02 b02 b02 | b06 b06 b06 b06
                // Row3: b03 b03 b03 b03 | b07 b07 b07 b07
                // Row4: b04 b04 b04 b04 | b08 b08 b08 b08
                // ---------------------------------------
                // Row5: b09 b09 b09 b09 | b13 b13 b13 b13
                // Row6: b10 b10 b10 b10 | b14 b14 b14 b14
                // Row7: b11 b11 b11 b11 | b15 b15 b15 b15
                // Row8: b12 b12 b12 b12 | b16 b16 b16 b16

                for (int block = 0; block < blocksCountWidth; block++)
                {
                    // First 4 rows
                    for (int j = 0; j < 2; j++)
                    {
                        var palette1 = br.ReadUInt16();
                        var palette2 = br.ReadUInt16();
                        System.Drawing.Color[] palette = GetPalette(palette1, palette2);

                        // For the 4 bytes
                        for (int k = 0; k < 4; k++)
                        {
                            byte IndicesRowK = br.ReadByte();

                            // For every 2 bits in the byte
                            for (int l = 0; l < 4; l++)
                            {
                                var ShiftAmount = 6 - 2 * l;
                                var paletteIndex = (byte)((IndicesRowK >> ShiftAmount) & 0b11);
                                System.Drawing.Color pixel = palette[paletteIndex];
                                rows[k][(block * 8) + (j * 4 + l)] = pixel;
                            }
                        }
                    }

                    // Next 4 rows
                    for (int j = 0; j < 2; j++)
                    {
                        var palette1 = br.ReadUInt16();
                        var palette2 = br.ReadUInt16();
                        System.Drawing.Color[] palette = GetPalette(palette1, palette2);

                        // For the 4 bytes
                        for (int k = 4; k < 8; k++)
                        {
                            byte IndicesRowK = br.ReadByte();

                            // For every 2 bits in the byte
                            for (int l = 0; l < 4; l++)
                            {
                                var ShiftAmount = 6 - 2 * l;
                                var paletteIndex = (byte)((IndicesRowK >> ShiftAmount) & 0b11);
                                System.Drawing.Color pixel = palette[paletteIndex];
                                rows[k][(block * 8) + (j * 4 + l)] = pixel;
                            }
                        }
                    }
                }

                RGBA.AddRange(rows);
            }
        }

        // Get the remaining 2 palette values from the first two
        System.Drawing.Color[] GetPalette(ushort palette1, ushort palette2)
        {
            System.Drawing.Color[] palette = new System.Drawing.Color[4];
            palette[0] = RGB565_t.GetColor(palette1);
            palette[1] = RGB565_t.GetColor(palette2);

            if (palette1 > palette2)
            {
                palette[2] = System.Drawing.Color.FromArgb
                (
                    0xFF,
                    (byte)(((palette[0].R * 2) + palette[1].R) / 3),
                    (byte)(((palette[0].G * 2) + palette[1].G) / 3),
                    (byte)(((palette[0].B * 2) + palette[1].B) / 3)
                );

                palette[3] = System.Drawing.Color.FromArgb
                (
                    0xFF,
                    (byte)(((palette[1].R * 2) + palette[0].R) / 3),
                    (byte)(((palette[1].G * 2) + palette[0].G) / 3),
                    (byte)(((palette[1].B * 2) + palette[0].B) / 3)
                );
            }
            else
            {
                palette[2] = System.Drawing.Color.FromArgb
                (
                    0xFF,
                    (byte)((palette[0].R + palette[1].R) / 2),
                    (byte)((palette[0].G + palette[1].G) / 2),
                    (byte)((palette[0].B + palette[1].B) / 2)
                );

                palette[3] = System.Drawing.Color.FromArgb(0, 0, 0, 0);
            }

            return palette;
        }
    }

    // While the game does not use this format directly, it is used by CMPR
    public static class RGB565_t
    {
        public static byte GetR(ushort value)
        {
            return (byte)(((value >> 11) & 0x1F) * 0x08);
        }

        public static byte GetG(ushort value)
        {
            return (byte)(((value >> 5) & 0x3F) * 0x04);
        }

        public static byte GetB(ushort value)
        {
            return (byte)((value & 0x1F) * 0x08);
        }

        public static System.Drawing.Color GetColor(ushort value)
        {
            var color = System.Drawing.Color.FromArgb(0xFF, GetR(value), GetG(value), GetB(value));
            return color;
        }
    }
}