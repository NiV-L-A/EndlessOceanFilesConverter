using System.Diagnostics;

namespace EndlessOceanFilesConverter
{
    public static class Utils
    {
        // Hardcoded lookup table for s01fNNMM.mdl files (eo1 main stage). Used to calculate the rod translation
        public static List<List<ushort>> LookupTableS01f = new()
        {
            new(){ 0x118, 0x218, 0x318, 0x418, 0x518, 0x618, 0x718, 0x117, 0x217, 0x317, 0x417, 0x517, 0x617, 0x717, 0x116, 0x216, 0x316, 0x416, 0x516, 0x616, 0x716, 0x816, 0x115, 0x215, 0x315, 0x415, 0x515, 0x615, 0x715, 0x815, 0x114, 0x214, 0x314, 0x414, 0x514, 0x614, 0x714, 0x814, 0x113, 0x213, 0x313, 0x413, 0x513, 0x613, 0x713, 0x112, 0x212, 0x312, 0x412, 0x512, 0x612, 0x712, 0x812, 0x311, 0x411, 0x511, 0x611, 0x711, 0x410, 0x510, 0x610, 0x710, 0x810 },
            new() { 0x917, 0xA17, 0xB17, 0x916, 0xA16, 0xB16, 0x915, 0xA15, 0xB15, 0xC15, 0xD15, 0xE15, 0xF15, 0x914, 0xA14, 0xB14, 0xC14, 0xD14, 0xE14, 0xF14, 0x913, 0xA13, 0xB13, 0xC13, 0xD13, 0xE13, 0xF13, 0x1113, 0x1213, 0x912, 0xA12, 0xB12, 0xC12, 0xD12, 0xE12, 0xF12, 0x1112, 0x1212, 0x911, 0xA11, 0xB11, 0xC11, 0xD11, 0x1011, 0x1111, 0x910, 0xA10, 0xB10, 0xC10, 0xD10, 0xE10, 0xF10, 0x1010, 0x1110 },
            new() { 0x20F, 0x30F, 0x40F, 0x50F, 0x60F, 0x70F, 0x80F, 0x10E, 0x20E, 0x30E, 0x40E, 0x50E, 0x60E, 0x70E, 0x80E, 0x10D, 0x20D, 0x30D, 0x40D, 0x50D, 0x60D, 0x70D, 0x80D, 0x20C, 0x30C, 0x40C, 0x50C, 0x60C, 0x70C, 0x80C, 0x10B, 0x20B, 0x30B, 0x60B, 0x70B, 0x80B, 0x10A, 0x20A, 0x30A, 0x50A, 0x60A, 0x70A, 0x80A, 0x109, 0x209, 0x309, 0x409, 0x509, 0x609, 0x709, 0x809, 0x108, 0x208, 0x308, 0x408, 0x508, 0x608, 0x708, 0x808, 0x608, 0x708, 0xFFFF, 0xFFFF, 0xFFFF, 0xFFFF, 0xFFFF, 0xFFFF, 0xFFFF, 0xFFFF, 0xFFFF, 0x808 },
            new() { 0x90F, 0xA0F, 0xB0F, 0xC0F, 0xD0F, 0xE0F, 0xF0F, 0x100F, 0x110F, 0x90E, 0xA0E, 0xB0E, 0xC0E, 0xD0E, 0xE0E, 0xF0E, 0x100E, 0x90D, 0xA0D, 0xB0D, 0xC0D, 0xD0D, 0xE0D, 0xF0D, 0x100D, 0x110D, 0x90C, 0xA0C, 0xB0C, 0xC0C, 0xD0C, 0xE0C, 0xF0C, 0x100C, 0x110C, 0x90B, 0xA0B, 0xB0B, 0xC0B, 0xD0B, 0xE0B, 0xF0B, 0x100B, 0x90A, 0xA0A, 0xB0A, 0xC0A, 0xD0A, 0xE0A, 0x909, 0xA09, 0xB09, 0xC09, 0xD09, 0xE09, 0xF09, 0x1009, 0x1109, 0x1209, 0x1309, 0x908, 0xA08, 0xB08, 0xC08, 0xD08, 0xE08, 0xF08, 0x1008, 0x1108, 0x1208, 0x1308 },
            new() { 0x107, 0x207, 0x307, 0x407, 0x507, 0x607, 0x707, 0x807, 0x6, 0x106, 0x206, 0x306, 0x406, 0x506, 0x606, 0x706, 0x806, 0x5, 0x105, 0x205, 0x305, 0x405, 0x505, 0x605, 0x705, 0x805, 0x4, 0x104, 0x204, 0x304, 0x404, 0x504, 0x604, 0x704, 0x804, 0x303, 0x403, 0x503, 0x603, 0x703, 0x803, 0x502, 0x602, 0x702, 0x802 },
            new() { 0x907, 0xA07, 0xB07, 0xC07, 0xD07, 0xE07, 0xF07, 0x1007, 0x1107, 0x906, 0xA06, 0xB06, 0xC06, 0xF06, 0x1006, 0x1106, 0x905, 0xA05, 0xB05, 0xC05, 0xF05, 0x1005, 0x1105, 0x904, 0xA04, 0xB04, 0xC04, 0xD04, 0xF04, 0x1004, 0x1104, 0x903, 0xA03, 0xB03, 0xC03, 0xD03, 0xE03, 0xF03, 0x1003, 0x1103, 0x902, 0xA02, 0xB02, 0xC02, 0xD02, 0xE02, 0xF02, 0x1002, 0xA01, 0xB01, 0xC01, 0xD01, 0xE01, 0xF01, 0x1001, 0xD00, 0xE00, 0xF00, 0x1000 }
        };

        // Get the translation of a rod in a eo1 main stage "s01fNNMM.mdl" file
        public static System.Numerics.Vector3 GetS01FRodTranslation(string fileName)
        {
            var Quadrant = Convert.ToUInt16(fileName.Substring(4, 2)) - 1;
            var RodIndex = Convert.ToUInt16(fileName.Substring(6, 2));
            var Modifier = LookupTableS01f[Quadrant][RodIndex];
            var TranslationColumn = 320f * (0.5f + (Modifier & 0xFF)) - 4000f;
            var TranslationRow = 320f * (0.5f + ((Modifier >> 0x8) & 0xFF)) - 3200f;
            System.Numerics.Vector3 trans = new(TranslationColumn, 0, TranslationRow);
            return trans;
        }

        public const string s01fRegexPattern = @"^s01f(\d{4}).*";
        public const string bNNrodMMRegexPattern = @"^b(\d{2})rod(\d{2}).*";

        public enum ERROR : int
        {
            OK,
            RF_WRONG_MAGIC,
            MDL_MESH_COUNT_ZERO,
            MDL_NO_VDL_FILE,
            RTL_WRONG_MAGIC,
            HIT_WRONG_MAGIC,
        }

        public static Dictionary<ERROR, string> ErrorMessages = new()
        {
            { ERROR.RF_WRONG_MAGIC, "Wrong magic: first bytes must be \"RFP\" or \"RF2\"." },
            { ERROR.MDL_MESH_COUNT_ZERO, "No meshes found." },
            { ERROR.MDL_NO_VDL_FILE, "No .vdl file found." },
            { ERROR.RTL_WRONG_MAGIC, "Wrong magic: first bytes must be \"RTL0\"." },
            { ERROR.HIT_WRONG_MAGIC, "Wrong magic: first bytes must be \"HIT \"." },
        };

        public static void PrintElapsedTime(TimeSpan ts)
        {
            Console.WriteLine(new string('-', Console.WindowWidth / 2));
            Console.WriteLine($"Elapsed time: {ts.Hours:00}:{ts.Minutes:00}:{ts.Seconds:00}.{ts.Milliseconds / 10:00}");
            // Fixes a bug in which you could press any key before the ReadKey()
            if (Console.KeyAvailable)
            {
                Console.ReadKey(true);
            }
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }

        public static void PrintCenter(string text)
        {
            Console.WriteLine(string.Format("{0," + ((Console.WindowWidth / 2) + (text.Length / 2)) + "}", text));
        }

        public static void PrintError(string text)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("ERROR: " + text);
            Console.ForegroundColor = ConsoleColor.Gray;
        }

        public static void PrintWarning(string text)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write("\t\tWARNING: " + text);
            Console.ForegroundColor = ConsoleColor.Gray;
        }

        public static string GetFolderName(string filePath)
        {
            var directory = Path.GetDirectoryName(filePath);
            string parentFolder = Path.GetFileName(directory);
            return parentFolder;
        }

        [DebuggerDisplay("MdlFilePathToImport: {MdlFilePathToImport} | NodeNameInMainFile: {NodeNameInMainFile}")]
        public class ExternalMDL
        {
            public string MdlFilePathToImport;
            public string NodeNameInMainFile;
            public ExternalMDL(string targetMDL, string nodeNameInMainFile)
            {
                MdlFilePathToImport = targetMDL;
                NodeNameInMainFile = nodeNameInMainFile;
            }
        }

        public static Dictionary<string, List<ExternalMDL>> ExternalMDLTable = new()
        {
            {
                "00/p00.mdl",
                new List<ExternalMDL>
                {
                    new(@"body/p(\d{2})j00.mdl", "p00j00"),
                    new(@"boot/p(\d{2})h(\d{2}).mdl", "p00h00"),
                    new(@"face/p(\d{2})b00.mdl", "p00b00"),
                    new(@"fin/p00f(\d{2}).mdl", "p00f00"),
                    new(@"glove/p(\d{2})g(\d{2}).mdl", "p00g00"),
                    new(@"goggle/p00d00.mdl", "p00d00"),
                    new(@"hair/p00a(\d{2}).mdl", "p00a00"),
                    new(@"jacket/p00e(\d{2}).mdl", "p00e00"),
                    new(@"suit/p00c(\d{2}).mdl", "p00c00"),
                    new(@"tank/p00i(\d{2}).mdl", "p00i00"),
                }
            },
            {
                "10/p10.mdl",
                new List<ExternalMDL>
                {
                    new(@"body/p(\d{2})j00.mdl", "p10j00"),
                    new(@"boot/p(\d{2})h(\d{2}).mdl", "p10h00"),
                    new(@"face/p(\d{2})b00.mdl", "p10b00"),
                    new(@"fin/p10f(\d{2}).mdl", "p10f00"),
                    new(@"glove/p(\d{2})g(\d{2}).mdl", "p10g00"),
                    new(@"goggle/p10d00.mdl", "p10d00"),
                    new(@"hair/p10a(\d{2}).mdl", "p10a00"),
                    new(@"jacket/p10e(\d{2}).mdl", "p10e00"),
                    new(@"suit/p10c(\d{2}).mdl", "p10c00"),
                    new(@"tank/p10i(\d{2}).mdl", "p10i00"),
                }
            },
            {
                "p00.mdl",
                new List<ExternalMDL>
                {
                    new(@"body/p00d0(\d{3}).mdl", "p00d0000"),
                    new(@"body/p00d1(\d{3}).mdl", "p00d1000"),
                    new(@"body/p00d2(\d{3}).mdl", "p00d2000"),
                    new(@"boot/p00i(\d{2}).mdl", "p00i00"),
                    new(@"face/p00b(\d{3}).mdl", "Head"),
                    new(@"fin/p00h(\d{2}).mdl", "p00h00"),
                    new(@"foot/p00m(\d{3}).mdl", "p00m000"),
                    new(@"glove/p00j(\d{2}).mdl", "p00j00"),
                    new(@"goggle/p00e(\d{2}).mdl", "p00e00"),
                    new(@"hair/p00a(\d{4}).mdl", "p00a00"),
                    new(@"hand/p00l(\d{3}).mdl", "p00l000"),
                    new(@"jacket/p00f(\d{2}).mdl", "p00f00"),
                    new(@"suit/p00c0(\d{2}).mdl", "p00c000"),
                    new(@"suit/p00c1(\d{2}).mdl", "p00c100"),
                    new(@"suit/p00c2(\d{2}).mdl", "p00c200"),
                    new(@"suit/p00c3(\d{2}).mdl", "p00c300"),
                    new(@"tank/p00g(\d{2}).mdl", "p00g00"),
                }
            },
            {
                "p01.mdl",
                new List<ExternalMDL>
                {
                    new(@"body/p00d0(\d{3}).mdl", "p00d0000"),
                    new(@"body/p00d1(\d{3}).mdl", "p00d1000"),
                    new(@"body/p00d2(\d{3}).mdl", "p00d2000"),
                    new(@"boot/p00i(\d{2}).mdl", "p00i00"),
                    new(@"face/p01b(\d{3}).mdl", "Head"), // p01?
                    new(@"fin/p00h(\d{2}).mdl", "p00h00"),
                    new(@"foot/p00m(\d{3}).mdl", "p00m000"),
                    new(@"glove/p00j(\d{2}).mdl", "p00j00"),
                    new(@"goggle/p00e(\d{2}).mdl", "p00e00"),
                    new(@"hair/p01a(\d{4}).mdl", "p00a00"), // p01?
                    new(@"hand/p00l(\d{3}).mdl", "p00l000"),
                    new(@"jacket/p00f(\d{2}).mdl", "p00f00"),
                    new(@"suit/p00c0(\d{2}).mdl", "p00c000"),
                    new(@"suit/p00c1(\d{2}).mdl", "p00c100"),
                    new(@"suit/p00c2(\d{2}).mdl", "p00c200"),
                    new(@"suit/p00c3(\d{2}).mdl", "p00c300"),
                    new(@"tank/p00g(\d{2}).mdl", "p00g00"),
                }
            },
            {
                "p02.mdl",
                new List<ExternalMDL>
                {
                    new(@"body/p00d0(\d{3}).mdl", "p00d0000"),
                    new(@"body/p00d1(\d{3}).mdl", "p00d1000"),
                    new(@"body/p00d2(\d{3}).mdl", "p00d2000"),
                    new(@"boot/p00i(\d{2}).mdl", "p00i00"),
                    new(@"face/p00b(\d{3}).mdl", "Head"), // p00?
                    new(@"fin/p00h(\d{2}).mdl", "p00h00"),
                    new(@"foot/p00m(\d{3}).mdl", "p00m000"),
                    new(@"glove/p00j(\d{2}).mdl", "p00j00"),
                    new(@"goggle/p00e(\d{2}).mdl", "p00e00"),
                    new(@"hair/p00a(\d{4}).mdl", "p00a00"), // p00?
                    new(@"hand/p00l(\d{3}).mdl", "p00l000"),
                    new(@"jacket/p00f(\d{2}).mdl", "p00f00"),
                    new(@"suit/p00c0(\d{2}).mdl", "p00c000"),
                    new(@"suit/p00c1(\d{2}).mdl", "p00c100"),
                    new(@"suit/p00c2(\d{2}).mdl", "p00c200"),
                    new(@"suit/p00c3(\d{2}).mdl", "p00c300"),
                    new(@"tank/p00g(\d{2}).mdl", "p00g00"),
                }
            },
            {
                "p10.mdl",
                new List<ExternalMDL>
                {
                    new(@"body/p10d0(\d{3}).mdl", "p10d0000"),
                    new(@"body/p10d1(\d{3}).mdl", "p10d1000"),
                    new(@"body/p10d2(\d{3}).mdl", "p10d2000"),
                    new(@"boot/p10i(\d{2}).mdl", "p10i00"),
                    new(@"face/p10b(\d{3}).mdl", "Head"),
                    new(@"fin/p10h(\d{2}).mdl", "p10h00"),
                    new(@"foot/p10m(\d{3}).mdl", "p10m000"),
                    new(@"glove/p10j(\d{2}).mdl", "p10j00"),
                    new(@"goggle/p10e(\d{2}).mdl", "p10e00"),
                    new(@"hair/p10a(\d{4}).mdl", "p10a00"),
                    new(@"hand/p10l(\d{3}).mdl", "p10l000"),
                    new(@"jacket/p10f(\d{2}).mdl", "p10f00"),
                    new(@"suit/p10c0(\d{2}).mdl", "p10c000"),
                    new(@"suit/p10c1(\d{2}).mdl", "p10c100"),
                    new(@"suit/p10c2(\d{2}).mdl", "p10c200"),
                    new(@"suit/p10c3(\d{2}).mdl", "p10c300"),
                    new(@"tank/p10g(\d{2}).mdl", "p10g00"),
                }
            },
            {
                "p11.mdl",
                new List<ExternalMDL>
                {
                    new(@"body/p10d0(\d{3}).mdl", "p10d0000"),
                    new(@"body/p10d1(\d{3}).mdl", "p10d1000"),
                    new(@"body/p10d2(\d{3}).mdl", "p10d2000"),
                    new(@"boot/p10i(\d{2}).mdl", "p10i00"),
                    new(@"face/p11b(\d{3}).mdl", "Head"), // p11?
                    new(@"fin/p10h(\d{2}).mdl", "p10h00"),
                    new(@"foot/p10m(\d{3}).mdl", "p10m000"),
                    new(@"glove/p10j(\d{2}).mdl", "p10j00"),
                    new(@"goggle/p10e(\d{2}).mdl", "p10e00"),
                    new(@"hair/p11a(\d{4}).mdl", "p10a00"), // p11?
                    new(@"hand/p10l(\d{3}).mdl", "p10l000"),
                    new(@"jacket/p10f(\d{2}).mdl", "p10f00"),
                    new(@"suit/p10c0(\d{2}).mdl", "p10c000"),
                    new(@"suit/p10c1(\d{2}).mdl", "p10c100"),
                    new(@"suit/p10c2(\d{2}).mdl", "p10c200"),
                    new(@"suit/p10c3(\d{2}).mdl", "p10c300"),
                    new(@"tank/p10g(\d{2}).mdl", "p10g00"),
                }
            },
            {
                "p12.mdl",
                new List<ExternalMDL>
                {
                    new(@"body/p10d0(\d{3}).mdl", "p10d0000"),
                    new(@"body/p10d1(\d{3}).mdl", "p10d1000"),
                    new(@"body/p10d2(\d{3}).mdl", "p10d2000"),
                    new(@"boot/p10i(\d{2}).mdl", "p10i00"),
                    new(@"face/p10b(\d{3}).mdl", "Head"), // p10?
                    new(@"fin/p10h(\d{2}).mdl", "p10h00"),
                    new(@"foot/p10m(\d{3}).mdl", "p10m000"),
                    new(@"glove/p10j(\d{2}).mdl", "p10j00"),
                    new(@"goggle/p10e(\d{2}).mdl", "p10e00"),
                    new(@"hair/p10a(\d{4}).mdl", "p10a00"), // p10?
                    new(@"hand/p10l(\d{3}).mdl", "p10l000"),
                    new(@"jacket/p10f(\d{2}).mdl", "p10f00"),
                    new(@"suit/p10c0(\d{2}).mdl", "p10c000"),
                    new(@"suit/p10c1(\d{2}).mdl", "p10c100"),
                    new(@"suit/p10c2(\d{2}).mdl", "p10c200"),
                    new(@"suit/p10c3(\d{2}).mdl", "p10c300"),
                    new(@"tank/p10g(\d{2}).mdl", "p10g00"),
                }
            },
            {
                "p20.mdl",
                new List<ExternalMDL>
                {
                    new("p20b000.mdl", "Head")
                }
            },
            {
                "p30.mdl",
                new List<ExternalMDL>
                {
                    new("p30b000.mdl", "Head")
                }
            },
            {
                "p32.mdl",
                new List<ExternalMDL>
                {
                    new("p30b000.mdl", "Head")
                }
            },
            {
                "p40.mdl",
                new List<ExternalMDL>
                {
                    new("p40b100.mdl", "Head")
                }
            },
            {
                "p42.mdl",
                new List<ExternalMDL>
                {
                    new("p40b100.mdl", "Head")
                }
            },
            {
                "p50.mdl",
                new List<ExternalMDL>
                {
                    new("p50b200.mdl", "Head")
                }
            },
            {
                "p52.mdl",
                new List<ExternalMDL>
                {
                    new("p50b200.mdl", "Head")
                }
            },
            {
                "p60.mdl",
                new List<ExternalMDL>
                {
                    new("p60b000.mdl", "Head")
                }
            },
            {
                @"(b01rod(\d{2}).mdl)|(b01land.mdl)",
                new List<ExternalMDL>
                {
                    new("b01pmset.mdl", ""),
                    new(@"ms/k.*$", ""),
                    new(@"ps/lod.*$", ""),
                }
            },
            {
                @"b02rod(\d{2}).mdl$",
                new List<ExternalMDL>
                {
                    new("b02pmset.mdl", ""),
                }
            },
            {
                @"b03rod(\d{2}).mdl$",
                new List<ExternalMDL>
                {
                    new("b03pmset.mdl", ""),
                    new(@"ms/k.*$", ""),
                    new(@"ps/lod.*$", ""),
                }
            },
            {
                "b07stage.mdl",
                new List<ExternalMDL>
                {
                    new("b07pmset.mdl", "")
                }
            },
            {
                "b08stage.mdl",
                new List<ExternalMDL>
                {
                    new("b08pmset.mdl", ""),
                }
            },
            {
                "b10stage.mdl",
                new List<ExternalMDL>
                {
                    new("b10obj00.mdl", "b10_obj00")
                }
            },
            {
                "b14stage.mdl",
                new List<ExternalMDL>
                {
                    new("b14obj00.mdl", "b14_obj00"),
                    new("b14obj01.mdl", "b14_obj01"),
                    new("b14obj02.mdl", "b14_obj02"),
                    new("b14obj03.mdl", "b14_obj03"),
                    new("b14obj04.mdl", "b14_obj04"),
                    new("b14obj05.mdl", "b14_obj05"),
                    new("b14obj06.mdl", "b14_obj06"),
                    new("b14obj07.mdl", "b14_obj07"),
                    new("b14obj08.mdl", "b14_obj08"),
                    new("b14obj11.mdl", "b14_obj110;b14_obj111;b14_obj112"),
                    new("b14obj12.mdl", "b14_obj12"),
                    new("b14obj13.mdl", "b14_obj13"),
                    new("b14obj14.mdl", "b14_obj14"),
                    new("b14obj15.mdl", "b14_obj15"),
                    new("b14obj20.mdl", "b14_obj20"),
                    new("palm00.mdl", "palm00;palm01;palm02;palm03"),
                    new("palm01.mdl", "palm10;palm12;palm13"),
                    new("palm02.mdl", "palm20;palm21;palm22;palm23;palm24"),
                    new("palm03.mdl", "palm30;palm31;palm32"),
                    new("palm04.mdl", "palm40;palm41;palm42;palm43"),
                    new("palm05.mdl", "palm50;palm51;palm52"),
                    new("palm06.mdl", "palm60;palm61;palm62;palm63;palm64;palm65"),
                }
            },
            {
                "b17stage.mdl",
                new List<ExternalMDL>
                {
                    new(@"ms/k.*$", ""),
                }
            }
        };
    }
}
