using System.Diagnostics;
using System.Numerics;
using System.Text.RegularExpressions;
using static EndlessOceanFilesConverter.Utils;

namespace EndlessOceanFilesConverter
{
    internal class Program
    {
        public const string Version = "2.0.0";
        public const string DefaultConsoleTitle = $"Endless Ocean Files Converter v{Version}";
        public const string SupportedFilesMessage = @"To use this tool, drag and drop a file, multiple files, a folder, or multiple folders,
containing one or more of the following supported file formats:
- .mdl -> .glb + .png (Model)
- .hit -> .glb (Hitbox)
- .tdl -> .png (Texture)
- .pak -> Converts the files found inside (Archive of files, usually textures except for ""md112mot.pak"" which contains exclusively .mot files)
- .txs -> Converts the files found inside (Archive of textures)
- .rtl -> To be passed along with the Endless Ocean 2 bNNrodMM.mdl files (Tranformation data used to correctly place each ""rod"" model)
- .mot -> To be passed along with the .mdl files that require external animation data
- GAME.DAT + INFO.DAT -> Extracts the files inside GAME.DAT and outputs them in a folder named 'out' (created in the same folder where GAME.DAT is).
  - To use this function, only pass to the exe the GAME.DAT and INFO.DAT files.

Press any key to exit...";

        static void Main(string[] args)
        {
            Console.Title = DefaultConsoleTitle;
            Thread.CurrentThread.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
            PrintCenter($"Endless Ocean Files Converter (v{Version})\n");
            PrintCenter("Author: NiV\n");
            PrintCenter("Special thanks to MDB & Hiroshi\n");
            if (args.Length == 0)
            {
                PrintError($"No arguments passed.");
                Console.WriteLine($"{SupportedFilesMessage}");
                Console.Read();
                return;
            }

            List<string> FilesToParse = new();
            for (int i = 0; i < args.Length; i++)
            {
                if (Directory.Exists(args[i]))
                {
                    // folder
                    FilesToParse.AddRange(Directory.GetFiles(args[i], "*.*", SearchOption.AllDirectories).ToArray());
                }
                else
                {
                    FilesToParse.Add(args[i]);
                }
            }

            Stopwatch stopwatch = new();
            if (FilesToParse.Count == 2)
            {
                string gameFilePath = "";
                string infoFilePath = "";
                bool gameAndInfoFound = false;
                if (FilesToParse[0].EndsWith("GAME.DAT") && FilesToParse[1].EndsWith("INFO.DAT"))
                {
                    gameFilePath = FilesToParse[0];
                    infoFilePath = FilesToParse[1];
                    gameAndInfoFound = true;
                }
                else if (FilesToParse[0].EndsWith("INFO.DAT") && FilesToParse[1].EndsWith("GAME.DAT"))
                {
                    infoFilePath = FilesToParse[0];
                    gameFilePath = FilesToParse[1];
                    gameAndInfoFound = true;
                }

                if (gameAndInfoFound)
                {
                    Console.WriteLine("Detected GAME.DAT and INFO.DAT");
                    stopwatch.Start();
                    ExtractMainArchive(gameFilePath, infoFilePath);
                    stopwatch.Stop();
                    Console.WriteLine("Done!");
                    PrintElapsedTime(stopwatch.Elapsed);
                    return;
                }
            }

            for (int i = 0; i < FilesToParse.Count; i++)
            {
                FilesToParse[i] = FilesToParse[i].Replace('\\', '/');
            }

            FilesToParse = SortFiles(FilesToParse);

            if (FilesToParse.Count == 0)
            {
                PrintError("No supported files found.");
                Console.WriteLine($"{SupportedFilesMessage}");
                Console.Read();
                return;
            }

            stopwatch.Start();
            ConvertFiles(FilesToParse);
            stopwatch.Stop();
            PrintElapsedTime(stopwatch.Elapsed);
        }

        static void ConvertFiles(List<string> filesToParse)
        {
            RTLStream? RTLFile = null;
            Dictionary<string, byte[]> ExternalMOTFiles = new();
            Dictionary<string, MDLStream> ExternalMDLFiles = new();

            for (int i = 0; i < filesToParse.Count; i++)
            {
                Console.Title = $"{DefaultConsoleTitle} ({i + 1}/{filesToParse.Count})";

                // While I would have liked to not have this big try block,
                // it makes it practical to have early returns ("continue"*) when there's an error,
                // and because we are using the "using" keyword, the filestream gets disposed and closed automatically
                try
                {
                    using FileStream fs = new(filesToParse[i], FileMode.Open);
                    using EndianBinaryReader br = new(fs, EndianBinaryReader.Endianness.Little);

                    string FileExtension = Path.GetExtension(filesToParse[i]);
                    if (FileExtension == ".mdl")
                    {
                        Console.WriteLine($"Converting\t\"{filesToParse[i]}\"\t({i + 1})");
                        MDLStream MDLFile = new(br, filesToParse[i]);

                        if (MDLFile.Error != ERROR.OK)
                        {
                            PrintWarning($"{ErrorMessages[MDLFile.Error]} File not converted.\n");
                            continue;
                        }

                        if (RTLFile != null && MDLFile.IsEO2Rod)
                        {
                            // eo2 3 main stage (b01 (gatama), b02 (ciceros), b03 (zahhab))

                            // b01rod00.mdl == b01stage.rtl
                            if (MDLFile.FileName.Substring(1, 2) == RTLFile.FileName.Substring(1, 2))
                            {
                                ushort FileNameRodIndex = Convert.ToUInt16(MDLFile.FileName.Substring(6, 2));
                                Vector3 trans = RTLFile.Rod.Find(item => item.RodIndex == FileNameRodIndex)!.Translation;
                                MDLFile.RodTranslation = trans;
                            }
                        }
                        else if (MDLFile.IsEO1Rod)
                        {
                            // eo1 main stage (s01f)
                            MDLFile.RodTranslation = GetS01FRodTranslation(MDLFile.FileName);
                        }

                        // If there's a mismatch between the animations that we have parsed and the ones that the mdl wants
                        if (MDLFile.MOL != null && MDLFile.MOL.Animation.Count != MDLFile.MOL.Header.AnimationCount)
                        {
                            MDLFile.TryAddingExternalMOTFiles(br, ExternalMOTFiles);
                        }

                        bool isExternalMdl = false;
                        for (int j = 0; j < ExternalMDLTable.Count; j++)
                        {
                            var key = ExternalMDLTable.ElementAt(j);
                            if (Regex.IsMatch(MDLFile.FileName, key.Key) ||
                                Regex.IsMatch($"{MDLFile.FolderName}/{MDLFile.FileName}", key.Key))
                            {
                                break;
                            }

                            for (int k = 0; k < key.Value.Count; k++)
                            {
                                string externalMdlFilePath = key.Value[k].MdlFilePathToImport;
                                if (Regex.IsMatch(MDLFile.FilePath, externalMdlFilePath))
                                {
                                    if (externalMdlFilePath.Contains('/'))
                                    {
                                        // If the file is in a sub folder (ps/lodc12.mdl is inside a folder of where b03rod52.mdl is)
                                        // Add folder+MDLFile.FileName ("ps/lodc12.mdl")
                                        string folderName = GetFolderName(filesToParse[i]); // ms or ps
                                        string result = Path.Combine(folderName, MDLFile.FileName);
                                        result = result.Replace('\\', '/');
                                        if (ExternalMDLFiles.ContainsKey(result))
                                        {
                                            break;
                                        }
                                        ExternalMDLFiles.Add(result, MDLFile);
                                    }
                                    else
                                    {
                                        // If the file is in the same folder (b03pmset.mdl is in the same folder as b03rod52.mdl)
                                        // Add MDLFile.FileName ("b03pmset.mdl")
                                        ExternalMDLFiles.Add(MDLFile.FileName, MDLFile);
                                    }

                                    isExternalMdl = true;
                                    break;
                                }
                            }

                            if (isExternalMdl)
                            {
                                break;
                            }
                        }

                        MDLFile.PrepareForGltfExport();
                        if (isExternalMdl)
                        {
                            Console.WriteLine($"\n\t\tDone!");
                        }
                        else
                        {
                            MDLFile.SaveAsGLB(ExternalMDLFiles);
                            Console.WriteLine($"Done!");
                        }
                    }
                    else if (FileExtension == ".mot")
                    {
                        Console.WriteLine($"Parsing\t\t\"{filesToParse[i]}\"\t({i + 1})");

                        string motName = Path.GetFileName(filesToParse[i]);
                        // Only consider the first .mot added
                        if (ExternalMOTFiles.ContainsKey(motName))
                        {
                            PrintWarning($"A .mot file with the same name ({motName}) was already parsed. Skipping...\n");
                            continue;
                        }

                        byte[] motData = new byte[fs.Length];
                        fs.ReadExactly(motData, 0, (int)fs.Length);
                        ExternalMOTFiles.Add(Path.GetFileName(filesToParse[i]), motData);
                        Console.WriteLine($"\t\tDone!");
                    }
                    else if (FileExtension == ".rtl")
                    {
                        Console.WriteLine($"Parsing\t\t\"{filesToParse[i]}\"\t({i + 1})");

                        RTLFile = new(br, filesToParse[i]);
                        if (RTLFile.Error != ERROR.OK)
                        {
                            PrintWarning($"{ErrorMessages[RTLFile.Error]} File not parsed.");
                            RTLFile = null;
                            continue;
                        }
                        Console.WriteLine($"\t\tDone!");
                    }
                    else if (FileExtension == ".tdl")
                    {
                        Console.WriteLine($"Converting\t\"{filesToParse[i]}\"\t({i + 1})");
                        TDLStream TDLFile = new(br, filesToParse[i]);
                        TDLFile.SaveAsPNG();
                        Console.WriteLine($"\t\tDone!");
                    }
                    else if (FileExtension == ".hit")
                    {
                        Console.WriteLine($"Converting\t\"{filesToParse[i]}\"\t({i + 1})");
                        HITStream HITFile = new(br, filesToParse[i]);
                        if (HITFile.Header.CollisionCount == 0)
                        {
                            PrintWarning($"No collision found. File not converted.\n");
                            continue;
                        }

                        // Only eo1 s01fNNMM.mdl files have a rod translation
                        // (the 3 main stages from eo2 are already in their correct position)
                        if (HITFile.IsEO1Rod)
                        {
                            HITFile.RodTranslation = GetS01FRodTranslation(HITFile.FileName);
                        }

                        HITFile.SaveAsGLB();
                        Console.WriteLine($"\n\t\tDone!");
                    }
                    else if (FileExtension == ".pak" || FileExtension == ".txs")
                    {
                        RFHeader_t RFHeader = new(br);
                        if (RFHeader.Files.Any(x => x.Type == FILETYPE.TDL))
                        {
                            // txs or pak made of tdl files
                            Console.WriteLine($"Converting\t\"{filesToParse[i]}\"\t({i + 1})");

                            Console.Write($"\t\tWith ");
                            (int Left, int Top) cursorPositionTDLCount = Console.GetCursorPosition();
                            Console.Write($"0/{RFHeader.FileCount} texture(s)");

                            for (int j = 0; j < RFHeader.FileCount; j++)
                            {
                                if (RFHeader.Files[j].Type == FILETYPE.TDL)
                                {
                                    TDLStream TDLFile = new(RFHeader.Files[j].Read(br), RFHeader.Files[j].Name);
                                    TDLFile.SaveAsPNG(Path.Combine(Path.GetDirectoryName(filesToParse[i]), $"{RFHeader.Files[j].Name}"));

                                    Console.SetCursorPosition(cursorPositionTDLCount.Left, cursorPositionTDLCount.Top);
                                    Console.Write($"{j + 1}/{RFHeader.FileCount} texture(s)");
                                }
                            }
                            Console.WriteLine($"\n\t\tDone!");
                        }
                        else
                        {
                            // pak made of mot files
                            Console.WriteLine($"Parsing\t\t\"{filesToParse[i]}\"\t({i + 1})");
                            for (int j = 0; j < RFHeader.FileCount; j++)
                            {
                                if (RFHeader.Files[j].Type == FILETYPE.MOT)
                                {
                                    br.Seek(RFHeader.Files[j].Offset, SeekOrigin.Begin);
                                    byte[] data = new byte[RFHeader.Files[j].Size];
                                    fs.ReadExactly(data, 0, (int)RFHeader.Files[j].Size);
                                    ExternalMOTFiles.Add(RFHeader.Files[j].Name, data);
                                }
                            }
                            Console.WriteLine($"\t\tDone!");
                        }
                    }
                }
                catch (IOException ex)
                {
                    PrintError($"{ex.Message}");
                    Console.WriteLine($"{SupportedFilesMessage}");
                    Console.Read();
                    return;
                }
                catch (UnauthorizedAccessException uax)
                {
                    if (Directory.Exists(filesToParse[i]))
                    {
                        // folder
                        PrintError($"{uax.Message}\nTried to open the FileStream on a folder, but expecting a file.\nIf you have used QuickBMS to extract the contents of the game,\nmake sure the flag 'AUTO_PARSING_RF2' in the .bms script is set to 0.\n");
                    }
                    else
                    {
                        // file
                        PrintError($"{uax.Message}\nConsider moving the file in another folder!\n");
                    }
                    Console.WriteLine($"{SupportedFilesMessage}");
                    Console.Read();
                    return;
                }
            }

            // Save the external mdl files that were passed but were not used
            // (for example b10obj00.mdl was passed but b10stage.mdl was not)
            (int Left, int Top) consoleCursorPosition = Console.GetCursorPosition();
            for (int i = 0; i < ExternalMDLFiles.Count; i++)
            {
                MDLStream MDLFile = ExternalMDLFiles.ElementAt(i).Value;
                if (!MDLFile.IsDumped)
                {
                    MDLFile.SaveAsGLB(new());
                    Console.WriteLine("Done!");
                }
            }

            Console.SetCursorPosition(consoleCursorPosition.Left, consoleCursorPosition.Top);
            Console.Title = $"{DefaultConsoleTitle}";
        }

        static void ExtractMainArchive(string gameFilePath, string infoFilePath)
        {
            try
            {
                INFOStream InfoFile = new(File.ReadAllBytes(infoFilePath));

                var outFolder = Path.Combine(Path.GetDirectoryName(gameFilePath), "out");
                Directory.CreateDirectory(outFolder);
                Console.WriteLine($"Created output folder: \"{outFolder}\"");

                using FileStream fsGame = new(gameFilePath, FileMode.Open);
                byte[] buffer = new byte[InfoFile.Files.Max(f => f.Size)];

                for (int i = 0; i < InfoFile.Header.FileCount; i++)
                {
                    Console.Write($"Extracting...{i + 1:D4}/{InfoFile.Header.FileCount} file(s)\r");

                    var fileInfo = InfoFile.Files[i];
                    string outFilePath = Path.Combine(outFolder, fileInfo.Name);
                    string directoryPath = Path.GetDirectoryName(outFilePath);
                    if (!Directory.Exists(directoryPath))
                    {
                        Directory.CreateDirectory(directoryPath);
                    }

                    fsGame.Seek(fileInfo.Offset * InfoFile.Header.Alignment, SeekOrigin.Begin);
                    int size = fileInfo.Size;
                    fsGame.ReadExactly(buffer.AsSpan(0, size));

                    File.WriteAllBytes(outFilePath, buffer.AsSpan(0, size).ToArray());
                }

                Console.WriteLine();
            }
            catch (IOException ex)
            {
                PrintError($"{ex.Message}");
                Console.WriteLine($"{SupportedFilesMessage}");
                Console.Read();
                return;
            }
            catch (UnauthorizedAccessException uax)
            {
                PrintError($"{uax.Message}\nConsider moving the file in another folder!\n");
                Console.WriteLine($"{SupportedFilesMessage}");
                Console.Read();
                return;
            }
        }

        public static List<string> SortFiles(List<string> filesToParse)
        {
            // Sort files based on the ExternalMDLTable dependencies
            Dictionary<string, int> priorityMap = filesToParse.ToDictionary(f => f, f => 0);

            for (int i = 0; i < ExternalMDLTable.Count; i++)
            {
                var mainFilePath = ExternalMDLTable.Keys.ElementAt(i);
                var externalFiles = ExternalMDLTable.Values.ElementAt(i).Select(e => e.MdlFilePathToImport).ToList();

                for (int j = 0; j < filesToParse.Count; j++)
                {
                    if (Regex.IsMatch(filesToParse[j], mainFilePath))
                    {
                        for (int k = 0; k < externalFiles.Count; k++)
                        {
                            for (int l = 0; l < filesToParse.Count; l++)
                            {
                                if (Regex.IsMatch(filesToParse[l], externalFiles[k]))
                                {
                                    priorityMap[filesToParse[l]]--;
                                }
                            }
                        }
                    }
                }
            }

            var result = filesToParse.OrderBy(f => priorityMap[f]).ToList();

            string[] extensionPriority = { ".txs", ".pak", ".tdl", ".rtl", ".mot", ".mdl", ".hit" };
            result = [.. result
                    .Where(file => extensionPriority.Contains(Path.GetExtension(file)))
                    .OrderBy(file => Array.IndexOf(extensionPriority, Path.GetExtension(file)))];

            return result;
        }
    }
}