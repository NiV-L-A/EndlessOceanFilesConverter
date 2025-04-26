using SharpGLTF.Geometry;
using SharpGLTF.Geometry.VertexTypes;
using SharpGLTF.Materials;
using SharpGLTF.Scenes;
using SharpGLTF.Schema2;
using System.Diagnostics;
using System.Numerics;
using System.Text.RegularExpressions;
using static EndlessOceanFilesConverter.Utils;

namespace EndlessOceanFilesConverter
{
    using SharpGLTFRigidMesh = MeshBuilder<VertexPositionNormal, VertexColor1Texture2, VertexEmpty>;
    using SharpGLTFSkinnedMesh = MeshBuilder<VertexPositionNormal, VertexColor1Texture2, VertexJoints4>;
    using VertexIndexDatatypes = (MDLStream.VERTEX_INDEX_DATATYPE Position,
                                 MDLStream.VERTEX_INDEX_DATATYPE Normal,
                                 MDLStream.VERTEX_INDEX_DATATYPE Light,
                                 MDLStream.VERTEX_INDEX_DATATYPE Texture,
                                 MDLStream.VERTEX_INDEX_DATATYPE Texture2);

    [DebuggerDisplay("{FileName} | MeshCount: {Header.MeshCount} | TDLCount: {TDL.Count}")]
    public class MDLStream
    {
        public string FilePath;
        public string FileName;
        public string FileNameWithoutExtension;
        public string DirectoryName;
        public string FolderName;

        public Header_t Header;
        public uint VDLOffset;
        public uint MOLOffset;
        public List<HiObject_t> HiList;
        public List<Mesh_t>? Mesh;
        public List<TDLStream>? TDL;
        public MOLStream? MOL;

        public ERROR Error;

        /// <summary>
        /// Additional translation for eo1 main stage and eo2 rods
        /// </summary>
        public Vector3 RodTranslation { get; set; }
        bool _ispmset { get; set; }
        bool _isPlayer { get; set; }
        public bool IsEO1Rod { get; set; } // main map
        public bool IsEO2Rod { get; set; } // b01, b02, b03 (gatama, ciceros, zahhab)
        public bool IsDumped { get; set; } // Used to detect if an external mdl was used (if b10obj00.mdl is passed but not b10stage.mdl (which uses b10obj00.mdl), then dump b10obj00.mdl)

        private (int Left, int Top) _cursorPositionMeshCount;
        private (int Left, int Top) _cursorPositionTDLCount;
        private (int Left, int Top) _cursorPositionLastLine;

        private Dictionary<HiObject_t, NodeBuilder> _gltfNodeMap;
        private (List<(SharpGLTFRigidMesh Mesh, NodeBuilder Node)> RigidMesh,
                 List<(SharpGLTFSkinnedMesh Mesh, NodeBuilder Node, ushort MeshIndex)> SkinnedMesh) _gltfMeshes;

        public MDLStream(EndianBinaryReader br, string filePath)
        {
            FilePath = filePath;
            Init(br, Path.GetFileName(filePath));
        }

        private void Init(EndianBinaryReader br, string fileName)
        {
            FileName = fileName;
            FileNameWithoutExtension = Path.GetFileNameWithoutExtension(FileName);
            DirectoryName = Path.GetDirectoryName(FilePath).Replace('\\', '/');
            FolderName = Path.GetFileName(DirectoryName);
            if (FileNameWithoutExtension.EndsWith("pmset"))
            {
                _ispmset = true;
            }
            else if (FileNameWithoutExtension == "p00"
                  || FileNameWithoutExtension == "p01"
                  || FileNameWithoutExtension == "p02"
                  || FileNameWithoutExtension == "p10"
                  || FileNameWithoutExtension == "p11"
                  || FileNameWithoutExtension == "p12")
            {
                _isPlayer = true;
            }

            if (Regex.IsMatch(FileNameWithoutExtension, bNNrodMMRegexPattern))
            {
                IsEO2Rod = true;
            }
            else if (Regex.IsMatch(FileNameWithoutExtension, s01fRegexPattern))
            {
                IsEO1Rod = true;
            }

            Header = new(br, FileNameWithoutExtension);
            TDL = new();
            if (Header.MeshCount == 0 && !_isPlayer)
            {
                Error = ERROR.MDL_MESH_COUNT_ZERO;
                return;
            }

            ushort i = 0;
            ushort j = 0;
            for (i = 0; i < Header.RFHeader.FileCount; i++)
            {
                switch (Header.RFHeader.Files[i].Type)
                {
                    case FILETYPE.VDL:
                        VDLOffset = Header.RFHeader.Files[i].Offset;
                        break;
                    case FILETYPE.TDL:
                        if (Header.RFHeader.Files[i].IsInFile)
                        {
                            br.Seek(Header.RFHeader.Files[i].Offset, SeekOrigin.Begin);
                            string filepath = Path.Combine(DirectoryName,
                                $"{FileNameWithoutExtension}_{Header.RFHeader.Files[i].Name}");
                            TDL.Add(new(br, filepath));
                        }
                        break;
                    case FILETYPE.TXS:
                        if (Header.RFHeader.Files[i].IsInFile)
                        {
                            br.Seek(Header.RFHeader.Files[i].Offset, SeekOrigin.Begin);
                            var txsOffset = br.BaseStream.Position;
                            RFHeader_t TXSRFHeader = new(br);
                            for (j = 0; j < TXSRFHeader.FileCount; j++)
                            {
                                // check if the tdl in the txs is actually present (eo1 s00stage.mdl)
                                if (TXSRFHeader.Files[j].Type == FILETYPE.TDL && TXSRFHeader.Files[j].IsInFile)
                                {
                                    br.Seek(Header.RFHeader.Files[i].Offset + TXSRFHeader.Files[j].Offset, SeekOrigin.Begin);
                                    string filepath = Path.Combine(DirectoryName,
                                $"{FileNameWithoutExtension}_{TXSRFHeader.Files[j].Name}");
                                    TDLStream TDLFile = new(br, filepath);
                                    //TDLStream TDLFile = new(TXSRFHeader.Files[j].Read(br, txsOffset), TXSRFHeader.Files[j].Name);
                                    TDL.Add(TDLFile);
                                }
                            }
                        }
                        break;
                    case FILETYPE.MOL:
                        MOLOffset = Header.RFHeader.Files[i].Offset;
                        break;
                }
            }

            if (VDLOffset == 0)
            {
                // No VDL file found
                Error = ERROR.MDL_NO_VDL_FILE;
                return;
            }

            Console.Write($"\t\tWith ");
            _cursorPositionMeshCount = Console.GetCursorPosition();
            Console.Write($"0/{Header.MeshCount} mesh(es)");
            _cursorPositionLastLine = Console.GetCursorPosition();
            if (TDL.Count > 0)
            {
                Console.Write($"\n\t\tWith ");
                _cursorPositionTDLCount = Console.GetCursorPosition();
                Console.Write($"0/{TDL.Count} texture(s)");
                _cursorPositionLastLine = Console.GetCursorPosition();
            }

            for (i = 0; i < Header.MaterialCount; i++)
            {
                for (j = 0; j < Header.Material[i].Texture.Count; j++)
                {
                    Header.Material[i].Texture[j].FilePath = $"{Path.Combine(DirectoryName, Header.Material[i].Texture[j].FileName)}";
                }
            }

            br.Seek(VDLOffset, SeekOrigin.Begin);
            HiList = GetHiList(br, Header.ObjectCount);

            Mesh = new();
            for (i = 0; i < Header.MeshCount; i++)
            {
                Mesh.Add(new(br, Header.MeshInfo[i], Header.MeshVersion, VDLOffset));
            }

            // Assign Mesh.HiObject
            j = 0;
            for (i = 0; i < Header.ObjectCount; i++)
            {
                if (j >= Header.MeshCount)
                {
                    // Stop if all meshes are processed
                    break;
                }

                if (HiList[i].Type == HI_OBJECT_TYPE.Mesh)
                {
                    var meshIndex = HiList[i].Index;
                    if (meshIndex == 0xffff)
                    {
                        continue;
                    }

                    Mesh[meshIndex].HiObject = HiList[i];
                    j++; // Move to the next mesh
                }
            }

            for (i = 0; i < Header.MeshCount; i++)
            {
                for (j = 0; j < Mesh[i].Primitive.Count; j++)
                {
                    Mesh[i].Primitive[j].Material = Header.Material[Header.MeshInfo[i].PrimitiveInfo[j].MaterialIndex];
                }
            }

            if (MOLOffset != 0)
            {
                br.Seek(MOLOffset, SeekOrigin.Begin);
                MOL = new(br);

                // Some models (all of the ones with magic RFP?) have the mot files listed both in the header of the mdl and in the header of the mol (eo1 d093, eo2 d001l).
                // The mot files listed in the header of the mdl have the offset and size fields non-zero, and are relative to the start of the mdl,
                // while the mot files listed in the header of the mol have the offset and size fields set to zero.
                // So, let's go through the files of the mdl (again) and check if there's a mot that needs to be manually added.
                if (MOL.Animation.Count != Header.AnimationCount)
                {
                    for (j = 0; j < Header.RFHeader.FileCount; j++)
                    {
                        switch (Header.RFHeader.Files[j].Type)
                        {
                            case FILETYPE.MOT:
                                MOL.AddMOT(br, Header.RFHeader.Files[j]);
                                break;
                        }
                    }
                }
            }
        }

        public void SaveTDLs()
        {
            for (int i = 0; i < TDL.Count; i++)
            {
                TDL[i].SaveAsPNG();
                Console.SetCursorPosition(_cursorPositionTDLCount.Left, _cursorPositionTDLCount.Top);
                Console.Write($"{i + 1}/{TDL.Count} texture(s)");
            }
        }

        private List<HiObject_t> GetHiList(EndianBinaryReader br, ushort objectCount)
        {
            if (Header.HiListVersion == 2)
            {
                br.Skip(2);
                Header.ExternalNodeCount = br.ReadUInt16();
                br.Skip(8);
            }

            List<HiObject_t> HiList = new(objectCount);
            for (ushort i = 0; i < objectCount; i++)
            {
                HiList.Add(new(br, Header.HiListVersion));
                HiList[i].Id = i;
            }

            // Set parent object
            for (ushort i = 0; i < objectCount; i++)
            {
                if (HiList[i].Level > 0)
                {
                    byte level = HiList[i].Level;

                    // Go backwards to find the parent object
                    for (ushort j = (ushort)(i - 1); j >= 0; j--)
                    {
                        if (HiList[j].Level == level - 1)
                        {
                            HiList[i].ParentObject = HiList[j];
                            break;
                        }
                    }
                }
            }

            if (Header.ExternalNodeCount > 0)
            {
                Header.ExternalNodeName = new(Header.ExternalNodeCount);
                for (ushort i = 0; i < Header.ExternalNodeCount; i++)
                {
                    br.Skip(4); // This field is a pointer filled by the game
                    Header.ExternalNodeName.Add(br.ReadString(0x10).ToLower());
                }
            }

            return HiList;
        }

        public void TryAddingExternalMOTFiles(EndianBinaryReader br, Dictionary<string, byte[]> externalMOTFiles)
        {
            // If we still haven't detected any animations (the animations are 100% not in this file),
            // let's check if they were "loose" files (passed through the exe)
            if (MOL.Animation.Count != Header.AnimationCount)
            {
                for (int j = 0; j < MOL.Header.RFHeader.FileCount; j++)
                {
                    if (MOL.Header.RFHeader.Files[j].IsInFile)
                    {
                        continue;
                    }

                    string motName = MOL.Header.RFHeader.Files[j].Name;
                    if (externalMOTFiles.TryGetValue(motName, out byte[]? motData))
                    {
                        MOL.AddMOT(br, motName, motData);
                    }
                    else
                    {
                        Console.WriteLine();
                        PrintWarning($"Could not find animation data with name \"{motName}\"");
                    }
                }
            }
            _cursorPositionLastLine = Console.GetCursorPosition();
        }

        public void PrepareForGltfExport()
        {
            SaveTDLs();
            _gltfNodeMap = HiListToNodeBuilder(HiList);
            _gltfMeshes = GetRigidAndSkinnedMeshes(_gltfNodeMap);
            Console.SetCursorPosition(_cursorPositionLastLine.Left, _cursorPositionLastLine.Top);
        }

        public void SaveAsGLB(Dictionary<string, MDLStream> externalMDLFiles)
        {
            if (FilePath == null || FilePath == "")
            {
                return;
            }

            SaveAsGLB(FilePath, externalMDLFiles);
        }

        public void SaveAsGLB(string filePath, Dictionary<string, MDLStream> externalMDLFiles)
        {
            var scene = ToSceneBuilder(externalMDLFiles);
            ModelRoot model = scene.ToGltf2();

            WriteSettings ws = new()
            {
                ImageWriting = ResourceWriteMode.SatelliteFile,
                ImageWriteCallback = ImageWriteCallback
            };
            //model.SaveGLTF($"{filePath}.gltf", ws);
            model.SaveGLB($"{filePath}.glb", ws);
            IsDumped = true;
            Console.SetCursorPosition(_cursorPositionLastLine.Left, _cursorPositionLastLine.Top);
        }

        private SceneBuilder ToSceneBuilder(Dictionary<string, MDLStream> externalMDLFiles)
        {
            SceneBuilder scene = new();
            var meshes = _gltfMeshes;
            var nodeMap = _gltfNodeMap;

            Console.SetCursorPosition(_cursorPositionLastLine.Left, _cursorPositionLastLine.Top);
            Console.Write("\n\t\tSaving...");
            _cursorPositionLastLine = Console.GetCursorPosition();

            scene = AddMeshesToScene(scene, nodeMap, meshes);

            if (externalMDLFiles.Count > 0)
            {
                // If an external mdl was detected,
                // convert its hierarchy list to a nodebuilder,
                // add the first node to this scene,
                // get the meshes of the external mdl and add them to the scene
                foreach (var key in ExternalMDLTable.Keys)
                {
                    if (Regex.IsMatch(FileName, key) || Regex.IsMatch($"{FolderName}/{FileName}", key))
                    {
                        AddExternalMDLFilesToScene(scene, nodeMap, externalMDLFiles, key);
                        break;
                    }
                }
            }

            if (RodTranslation != Vector3.Zero)
            {
                scene.ApplyBasisTransform(Matrix4x4.CreateTranslation(RodTranslation));
            }

            if (MOL != null && MOL.Animation.Count > 0)
            {
                scene = AddAnimationsToScene(scene, nodeMap.Values.ToList());
            }

            return scene;
        }

        private string ImageWriteCallback(WriteContext ctx, string uri, SharpGLTF.Memory.MemoryImage memoryImage)
        {
            var directory = Path.GetDirectoryName(memoryImage.SourcePath);
            string parentFolder = Path.GetFileName(directory);

            var directory2 = ctx.CurrentDirectory.FullName;
            string parentFolder2 = Path.GetFileName(directory2);

            string result = Path.GetFileName(memoryImage.SourcePath);
            if (parentFolder != parentFolder2)
            {
                result = $"{parentFolder}/{result}";
            }

            return result;
        }

        public Dictionary<HiObject_t, NodeBuilder> HiListToNodeBuilder(List<HiObject_t> hiList)
        {
            // Go through the HiList objects:
            // Convert the HiList object to NodeBuilder and add it to nodeMap
            Dictionary<HiObject_t, NodeBuilder> nodeMap = new();
            var mainNode = new NodeBuilder($"{FileName}"); // root node ("b09stage.mdl")
            for (int i = 0; i < hiList.Count; i++)
            {
                // b10stage.mdl
                if (/*i == 0x3D &&*/ hiList[i].Name == "insmoto")
                {
                    i = 0x4C;
                    continue;
                }

                var node = hiList[i].ToNodeBuilder($"{i}_{hiList[i].Name}");
                nodeMap[hiList[i]] = node;

                // Add the node to the parent node (mainNode if it doesn't have a parent like b11stage.mdl)
                var parentNode = hiList[i].ParentObject == null ? mainNode : nodeMap[hiList[i].ParentObject];
                parentNode.AddNode(node);

                // Hardcoded object name "trans" (comparison done with string at 0x531e70 in eo2 main.dol).
                // If it's present, everything else is a children of this node
                // Same thing for eo1 but it's "p00 "
                if (hiList[i].Name == "trans" && hiList[i + 1].Name == "trans0" ||
                    hiList[i].Name == "p00 " && hiList[i + 1].Name == "p00 0")
                {
                    mainNode = node;
                }
            }

            return nodeMap;
        }

        (List<(SharpGLTFRigidMesh Mesh, NodeBuilder Node)> RigidMesh,
        List<(SharpGLTFSkinnedMesh Mesh, NodeBuilder Node, ushort MeshIndex)> SkinnedMesh)
        GetRigidAndSkinnedMeshes(Dictionary<HiObject_t, NodeBuilder> nodeMap)
        {
            List<(SharpGLTFRigidMesh Mesh, NodeBuilder Node)> RigidMesh = new();
            List<(SharpGLTFSkinnedMesh Mesh, NodeBuilder Node, ushort MeshIndex)> SkinnedMesh = new();

            // The following dictionary is used to store "instances".
            // An instance is a clone of a mesh, the only difference is transformation and name.
            // So, if we already converted a mesh to a MeshBuilder, we don't need to convert it again.
            // This speeds up things, especially if the model has many instances of the same mesh (b29stage.mdl).
            // So we store the mesh index and the MeshBuilder associated with it in this dictionary.
            Dictionary<ushort, SharpGLTFRigidMesh> meshInstances = new();

            // - If the object is a mesh
            //     - Save it in the RigidMesh or SkinnedMesh list
            // - If the object is an instance:
            //     - We jump to the object referenced by the 0x30 object, and if we encounter a mesh, we add it to RigidMesh or SkinnedMesh.
            //     - We interrupt if the level is less or equal than the level of the object referenced by the 0x30 object

            // Save the count in another variable.
            // This is done because, in the case of mesh instances, the nodeMap.Count gets updated,
            // But we don't want to parse these added nodes, because we already parsed them!
            int originalNodeMapCount = nodeMap.Count;
            for (int i = 0; i < originalNodeMapCount; i++)
            {
                HiObject_t hiObject = nodeMap.Keys.ElementAt(i);
                if (hiObject.Type == HI_OBJECT_TYPE.Mesh)
                {
                    NodeBuilder node = nodeMap[hiObject];
                    ushort meshIndex = hiObject.Index;
                    if (meshIndex == 0xffff)
                    {
                        // p00.mdl
                        continue;
                    }

                    Mesh[meshIndex].OutputName = $"{hiObject.Id}_{Mesh[meshIndex].HiObject.Name}";
                    if (Mesh[meshIndex].SkinData != null)
                    {
                        SharpGLTFSkinnedMesh mesh = MeshToSharpGLTFSkinnedMeshBuilder(Mesh[meshIndex]);
                        SkinnedMesh.Add((mesh, node, meshIndex));
                    }
                    else
                    {
                        SharpGLTFRigidMesh mesh = MeshToSharpGLTFRigidMeshBuilder(Mesh[meshIndex]);
                        RigidMesh.Add((mesh, node));
                    }

                    Console.SetCursorPosition(_cursorPositionMeshCount.Left, _cursorPositionMeshCount.Top);
                    Console.Write($"{meshIndex + 1}/{Header.MeshCount} mesh(es)");
                }
                else if (hiObject.Type == HI_OBJECT_TYPE.Instance)
                {
                    ushort objectIndex = hiObject.Index;
                    byte level = HiList[objectIndex].Level;

                    for (int j = objectIndex + 1; j < Header.ObjectCount; j++)
                    {
                        if (HiList[j].Level <= level)
                        {
                            break;
                        }
                        else if (HiList[j].Type == HI_OBJECT_TYPE.Mesh)
                        {
                            var meshIndex = HiList[j].Index;
                            Mesh[meshIndex].OutputName = $"{HiList[j].Id}_{Mesh[meshIndex].HiObject.Name}";

                            // Get parent
                            // If it's the first mesh that we are duplicating for this instance, then the parent is the object with the 0x30 code (HiList[i])
                            // If it's it's not the first mesh, then it's the parent of this object
                            HiObject_t parentObject;
                            if (Header.HiListVersion == 1)
                            {
                                if (HiList[j].ParentObject.Byte2 == 0x10)
                                {
                                    parentObject = hiObject;
                                }
                                else
                                {
                                    parentObject = HiList[j].ParentObject;
                                }
                            }
                            else
                            {
                                if (HiList[j].ParentObject.Byte3 == 8)
                                {
                                    parentObject = hiObject;
                                }
                                else
                                {
                                    parentObject = HiList[j].ParentObject;
                                }
                            }

                            NodeBuilder meshNode = HiList[j].ToNodeBuilder(Mesh[meshIndex].OutputName);
                            nodeMap[parentObject].AddNode(meshNode);
                            nodeMap[HiList[j]] = meshNode;

                            if (meshInstances.ContainsKey(meshIndex))
                            {
                                SharpGLTFRigidMesh meshBuilder = meshInstances[meshIndex];
                                RigidMesh.Add((meshBuilder, meshNode));
                            }
                            else
                            {
                                SharpGLTFRigidMesh meshBuilder = MeshToSharpGLTFRigidMeshBuilder(Mesh[meshIndex]);
                                RigidMesh.Add((meshBuilder, meshNode));
                                meshInstances.Add(meshIndex, meshBuilder);
                            }
                        }
                    }
                }
            }

            return (RigidMesh, SkinnedMesh);
        }

        private SharpGLTFSkinnedMesh MeshToSharpGLTFSkinnedMeshBuilder(Mesh_t mesh)
        {
            var MeshBuilder = new SharpGLTFSkinnedMesh(mesh.OutputName);
            List<MaterialBuilder> materials = GetMaterialsFromMesh(mesh);
            for (int i = 0; i < mesh.Primitive.Count; i++)
            {
                var prim = MeshBuilder.UsePrimitive(materials[i]);

                for (int j = 0; j < mesh.Primitive[i].VertexPositionTriangles.Count; j++)
                {
                    ushort vtx_pos1 = mesh.Primitive[i].VertexPositionTriangles[j][0];
                    ushort vtx_pos2 = mesh.Primitive[i].VertexPositionTriangles[j][1];
                    ushort vtx_pos3 = mesh.Primitive[i].VertexPositionTriangles[j][2];
                    ushort vtx_norm1 = mesh.Primitive[i].VertexNormalTriangles[j][0];
                    ushort vtx_norm2 = mesh.Primitive[i].VertexNormalTriangles[j][1];
                    ushort vtx_norm3 = mesh.Primitive[i].VertexNormalTriangles[j][2];
                    ushort vtx_uv1 = mesh.Primitive[i].VertexTextureTriangles[j][0];
                    ushort vtx_uv2 = mesh.Primitive[i].VertexTextureTriangles[j][1];
                    ushort vtx_uv3 = mesh.Primitive[i].VertexTextureTriangles[j][2];

                    var a = new VertexBuilder<VertexPositionNormal, VertexColor1Texture2, VertexJoints4>
                    (
                        /*new VertexPositionNormal*/(mesh.VertexPosition[vtx_pos1], mesh.VertexNormal[vtx_norm1])
                    );

                    var b = new VertexBuilder<VertexPositionNormal, VertexColor1Texture2, VertexJoints4>
                    (
                        /*new VertexPositionNormal*/(mesh.VertexPosition[vtx_pos2], mesh.VertexNormal[vtx_norm2])
                    );

                    var c = new VertexBuilder<VertexPositionNormal, VertexColor1Texture2, VertexJoints4>
                    (
                        /*new VertexPositionNormal*/(mesh.VertexPosition[vtx_pos3], mesh.VertexNormal[vtx_norm3])
                    );

                    a.Material.TexCoord0 = mesh.VertexTexture[vtx_uv1];
                    b.Material.TexCoord0 = mesh.VertexTexture[vtx_uv2];
                    c.Material.TexCoord0 = mesh.VertexTexture[vtx_uv3];

                    if (mesh.VertexIndexDatatype.Light != VERTEX_INDEX_DATATYPE.NONE)
                    {
                        var vtx_light1 = mesh.Primitive[i].VertexLightTriangles[j][0];
                        var vtx_light2 = mesh.Primitive[i].VertexLightTriangles[j][1];
                        var vtx_light3 = mesh.Primitive[i].VertexLightTriangles[j][2];
                        a.Material.Color = mesh.VertexLight[vtx_light1];
                        b.Material.Color = mesh.VertexLight[vtx_light2];
                        c.Material.Color = mesh.VertexLight[vtx_light3];
                    }
                    else
                    {
                        a.Material.Color = new(1, 1, 1, 1);
                        b.Material.Color = new(1, 1, 1, 1);
                        c.Material.Color = new(1, 1, 1, 1);
                    }

                    if (mesh.VertexIndexDatatype.Texture2 != VERTEX_INDEX_DATATYPE.NONE)
                    {
                        var vtx_uv21 = mesh.Primitive[i].VertexTexture2Triangles[j][0];
                        var vtx_uv22 = mesh.Primitive[i].VertexTexture2Triangles[j][1];
                        var vtx_uv23 = mesh.Primitive[i].VertexTexture2Triangles[j][2];
                        a.Material.TexCoord1 = mesh.VertexTexture2[vtx_uv21];
                        b.Material.TexCoord1 = mesh.VertexTexture2[vtx_uv22];
                        c.Material.TexCoord1 = mesh.VertexTexture2[vtx_uv23];
                    }

                    a.Skinning.SetBindings(new (int, float)[]
                    {
                        (mesh.SkinData.WeightIndex[vtx_pos1][0], mesh.SkinData.Weight[vtx_pos1].X),
                        (mesh.SkinData.WeightIndex[vtx_pos1][1], mesh.SkinData.Weight[vtx_pos1].Y),
                        (mesh.SkinData.WeightIndex[vtx_pos1][2], mesh.SkinData.Weight[vtx_pos1].Z),
                        (mesh.SkinData.WeightIndex[vtx_pos1][3], mesh.SkinData.Weight[vtx_pos1].W)
                    });

                    b.Skinning.SetBindings(new (int, float)[]
                    {
                        (mesh.SkinData.WeightIndex[vtx_pos2][0], mesh.SkinData.Weight[vtx_pos2].X),
                        (mesh.SkinData.WeightIndex[vtx_pos2][1], mesh.SkinData.Weight[vtx_pos2].Y),
                        (mesh.SkinData.WeightIndex[vtx_pos2][2], mesh.SkinData.Weight[vtx_pos2].Z),
                        (mesh.SkinData.WeightIndex[vtx_pos2][3], mesh.SkinData.Weight[vtx_pos2].W)
                    });

                    c.Skinning.SetBindings(new (int, float)[]
                    {
                        (mesh.SkinData.WeightIndex[vtx_pos3][0], mesh.SkinData.Weight[vtx_pos3].X),
                        (mesh.SkinData.WeightIndex[vtx_pos3][1], mesh.SkinData.Weight[vtx_pos3].Y),
                        (mesh.SkinData.WeightIndex[vtx_pos3][2], mesh.SkinData.Weight[vtx_pos3].Z),
                        (mesh.SkinData.WeightIndex[vtx_pos3][3], mesh.SkinData.Weight[vtx_pos3].W)
                    });

                    prim.AddTriangle(a, b, c);
                }
            }

            return MeshBuilder;
        }

        private SharpGLTFRigidMesh MeshToSharpGLTFRigidMeshBuilder(Mesh_t mesh)
        {
            var MeshBuilder = new SharpGLTFRigidMesh(mesh.OutputName);
            List<MaterialBuilder> materials = GetMaterialsFromMesh(mesh);

            for (int i = 0; i < mesh.Primitive.Count; i++)
            {
                var prim = MeshBuilder.UsePrimitive(materials[i]);
                for (int j = 0; j < mesh.Primitive[i].VertexPositionTriangles.Count; j++)
                {
                    var vtx_pos1 = mesh.Primitive[i].VertexPositionTriangles[j][0];
                    var vtx_pos2 = mesh.Primitive[i].VertexPositionTriangles[j][1];
                    var vtx_pos3 = mesh.Primitive[i].VertexPositionTriangles[j][2];
                    var vtx_norm1 = mesh.Primitive[i].VertexNormalTriangles[j][0];
                    var vtx_norm2 = mesh.Primitive[i].VertexNormalTriangles[j][1];
                    var vtx_norm3 = mesh.Primitive[i].VertexNormalTriangles[j][2];
                    var vtx_uv1 = mesh.Primitive[i].VertexTextureTriangles[j][0];
                    var vtx_uv2 = mesh.Primitive[i].VertexTextureTriangles[j][1];
                    var vtx_uv3 = mesh.Primitive[i].VertexTextureTriangles[j][2];

                    var a = new VertexBuilder<VertexPositionNormal, VertexColor1Texture2, VertexEmpty>
                    (
                        (mesh.VertexPosition[vtx_pos1], mesh.VertexNormal[vtx_norm1])
                    );

                    var b = new VertexBuilder<VertexPositionNormal, VertexColor1Texture2, VertexEmpty>
                    (
                        (mesh.VertexPosition[vtx_pos2], mesh.VertexNormal[vtx_norm2])
                    );

                    var c = new VertexBuilder<VertexPositionNormal, VertexColor1Texture2, VertexEmpty>
                    (
                        (mesh.VertexPosition[vtx_pos3], mesh.VertexNormal[vtx_norm3])
                    );

                    a.Material.TexCoord0 = mesh.VertexTexture[vtx_uv1];
                    b.Material.TexCoord0 = mesh.VertexTexture[vtx_uv2];
                    c.Material.TexCoord0 = mesh.VertexTexture[vtx_uv3];

                    if (mesh.VertexIndexDatatype.Light != VERTEX_INDEX_DATATYPE.NONE)
                    {
                        var vtx_light1 = mesh.Primitive[i].VertexLightTriangles[j][0];
                        var vtx_light2 = mesh.Primitive[i].VertexLightTriangles[j][1];
                        var vtx_light3 = mesh.Primitive[i].VertexLightTriangles[j][2];
                        a.Material.Color = mesh.VertexLight[vtx_light1];
                        b.Material.Color = mesh.VertexLight[vtx_light2];
                        c.Material.Color = mesh.VertexLight[vtx_light3];
                    }
                    else
                    {
                        a.Material.Color = new(1, 1, 1, 1);
                        b.Material.Color = new(1, 1, 1, 1);
                        c.Material.Color = new(1, 1, 1, 1);
                    }

                    if (mesh.VertexIndexDatatype.Texture2 != VERTEX_INDEX_DATATYPE.NONE)
                    {
                        var vtx_uv21 = mesh.Primitive[i].VertexTexture2Triangles[j][0];
                        var vtx_uv22 = mesh.Primitive[i].VertexTexture2Triangles[j][1];
                        var vtx_uv23 = mesh.Primitive[i].VertexTexture2Triangles[j][2];
                        a.Material.TexCoord1 = mesh.VertexTexture2[vtx_uv21];
                        b.Material.TexCoord1 = mesh.VertexTexture2[vtx_uv22];
                        c.Material.TexCoord1 = mesh.VertexTexture2[vtx_uv23];
                    }

                    prim.AddTriangle(a, b, c);
                }
            }

            return MeshBuilder;
        }

        List<MaterialBuilder> GetMaterialsFromMesh(Mesh_t mesh)
        {
            List<MaterialBuilder> materials = new(mesh.Primitive.Count);
            for (int i = 0; i < mesh.Primitive.Count; i++)
            {
                // If the mesh has more than 1 texture (which don't necessarily means more than 1 uv map)
                // The texture applied to a triangle depends on the vertex light data (b14stage ground)
                // As of now, GLTF materials don't support "internal texture blending", so we just set the first texture
                string MaterialName = mesh.Primitive[i].Material.Texture[0].FileName;
                string MaterialFilePath = mesh.Primitive[i].Material.Texture[0].FilePath;

                bool withDoubleSide = mesh.HiObject.RenderDoubleSide;
                if (mesh.Primitive[i].RenderDoubleSide == true)
                {
                    // The PrimitiveInfo RenderDoubleSide overrides the one in the HiObject (b11stage.mdl)
                    withDoubleSide = true;
                }

                var material = new MaterialBuilder(MaterialName).WithDoubleSide(withDoubleSide)
                                                                .WithMetallicRoughnessShader();
                if (File.Exists(MaterialFilePath))
                {
                    material.WithChannelImage(KnownChannel.BaseColor, MaterialFilePath);
                }

                if (mesh.VertexIndexDatatype.Light != VERTEX_INDEX_DATATYPE.NONE
                    /*|| (mesh.TransparencyFlagFromMeshInfo & 0x4) == 0x4*/
                    || mesh.TransparencyFlagFromMeshInfo != 0)
                {
                    material.WithAlpha(SharpGLTF.Materials.AlphaMode.MASK);

                    if (mesh.HiObject.HasAlphaBlend || mesh.Primitive[i].HasAlpha)
                    {
                        material.WithAlpha(SharpGLTF.Materials.AlphaMode.BLEND);
                    }
                }

                if (Header.HiListVersion == 2)
                {
                    // Fix for multitexturing, not yet supported in gltf
                    if (mesh.VertexIndexDatatype.Texture2 != VERTEX_INDEX_DATATYPE.NONE ||
                        mesh.Primitive[i].Material.Texture.Count > 1)
                    {
                        material.WithAlpha(SharpGLTF.Materials.AlphaMode.OPAQUE);
                    }

                    // Hardcoded, the last 3 meshes in b29stage have alpha, the game checks if the object name is the same as the one in main.dol at 0x5419D8
                    if (HiList[0].Name == "b29stage")
                    {
                        if (mesh.HiObject.ParentObject.Name == "glass")
                        {
                            material.WithAlpha(SharpGLTF.Materials.AlphaMode.BLEND);
                        }
                    }
                }

                materials.Add(material);
            }

            return materials;
        }

        private List<(NodeBuilder Joint, Matrix4x4 InverseBindMatrix)> GetJoints(Mesh_t mesh, List<NodeBuilder> nodeMap)
        {
            List<(NodeBuilder Joint, Matrix4x4 InverseBindMatrix)> Joints = new();
            for (int j = 0; j < mesh.SkinData.BoneNames.Count; j++)
            {
                NodeBuilder? node = nodeMap.Find(x => Regex.IsMatch(x.Name, $@"\d_{mesh.SkinData.BoneNames[j]}$"));
                if (node == null)
                {
                    continue;
                }
                Joints.Add((node, mesh.SkinData.InverseBoneMatrix[j]));
            }

            return Joints;
        }

        private SceneBuilder AddMeshesToScene(SceneBuilder scene, Dictionary<HiObject_t, NodeBuilder> nodeMap, (List<(SharpGLTFRigidMesh Mesh, NodeBuilder Node)> RigidMesh, List<(SharpGLTFSkinnedMesh Mesh, NodeBuilder Node, ushort MeshIndex)> SkinnedMesh) meshes)
        {
            // Add the meshes of this mdl to the scene
            for (int i = 0; i < meshes.RigidMesh.Count; i++)
            {
                scene.AddRigidMesh(meshes.RigidMesh[i].Mesh, meshes.RigidMesh[i].Node);
            }

            for (int i = 0; i < meshes.SkinnedMesh.Count; i++)
            {
                List<(NodeBuilder Joint, Matrix4x4 InverseBindMatrix)> Joints = new();
                Joints = GetJoints(Mesh[meshes.SkinnedMesh[i].MeshIndex], nodeMap.Values.ToList());
                if (NodeBuilder.IsValidArmature(Joints.Select(x => x.Joint)))
                {
                    scene.AddSkinnedMesh(meshes.SkinnedMesh[i].Mesh, Joints.ToArray());
                }
                else
                {
                    scene.AddRigidMesh(meshes.SkinnedMesh[i].Mesh, meshes.SkinnedMesh[i].Node);
                }
            }
            return scene;
        }

        SceneBuilder AddExternalMDLFilesToScene(SceneBuilder scene, Dictionary<HiObject_t, NodeBuilder> nodeMap, Dictionary<string, MDLStream> externalMDLFiles, string externalMDLTableKey)
        {
            List<(NodeBuilder node, NodeBuilder requestedNode)> NodesToAdd = new();

            // For every external file that was passed through the exe
            for (int i = 0; i < externalMDLFiles.Count; i++)
            {
                // Is the current external file accepted by this mdl?
                MDLStream? externalMdl = GetExternalMdl(ExternalMDLTable[externalMDLTableKey], externalMDLFiles.ElementAt(i), out string nodeNameInMainFile);
                if (externalMdl == null)
                {
                    continue;
                }

                // Yes, this mdl needs this external file
                var externalMdlNodeMap = externalMdl._gltfNodeMap;
                var externalMdlMeshes = externalMdl._gltfMeshes;
                externalMdl.IsDumped = true;

                // If a nodeNameInMainFile isn't provided, then let's go through the hilist and detect the node(s) that are requesting it
                // For example for pmset.mdl we yet don't know the requested node
                // But for b10obj00.mdl it has an hardcoded node that it needs to be placed at in b10stage.mdl
                if (nodeNameInMainFile == "")
                {
                    for (int k = 0; k < HiList.Count; k++)
                    {
                        if (HiList[k].Type == HI_OBJECT_TYPE.ExternalNode)
                        {
                            string name = Header.ExternalNodeName[HiList[k].Index];
                            HiObject_t? externalHiObj = null;

                            if (externalMdl._ispmset)
                            {
                                // bNNpmset.mdl contains multiple nodes with multiple meshes,
                                // and we need to copy only the referenced node instead of the whole mdl
                                // example: b08stage.mdl checks b08pmset.mdl for the "n41 " node
                                externalHiObj = externalMdl.HiList.FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));
                            }
                            else
                            {
                                // The external mdl file has its filename the same as the current ExternalNodeName
                                // example: b17stage.mdl checks for ms/k008.mdl
                                if (externalMdl.FileNameWithoutExtension == name)
                                {
                                    externalHiObj = externalMdl.HiList.FirstOrDefault();
                                }
                            }

                            if (externalHiObj == null)
                            {
                                // if the requested node couldn't be found in the current externalMdl, then skip
                                continue;
                            }

                            NodeBuilder externalNode = externalMdlNodeMap[externalHiObj];
                            Dictionary<NodeBuilder, NodeBuilder> externalNodeDeepCloned = externalNode.DeepClone();

                            if (externalMdlMeshes.RigidMesh.Count > 0)
                            {
                                for (int l = 0; l < externalNodeDeepCloned.Count; l++)
                                {
                                    var targetKey = externalNodeDeepCloned.Keys.ElementAt(l);
                                    var meshToDump = externalMdlMeshes.RigidMesh.Find(x => x.Node == targetKey);
                                    if (meshToDump.Mesh == null)
                                    {
                                        continue;
                                    }
                                    scene.AddRigidMesh(meshToDump.Mesh, externalNodeDeepCloned[targetKey]);
                                }
                            }

                            for (int l = 0; l < externalMdlMeshes.SkinnedMesh.Count; l++)
                            {
                                List<(NodeBuilder Joint, Matrix4x4 InverseBindMatrix)> Joints;
                                Joints = GetJoints(externalMdl.Mesh[externalMdlMeshes.SkinnedMesh[l].MeshIndex], externalNodeDeepCloned.Values.ToList());

                                scene.AddSkinnedMesh(externalMdlMeshes.SkinnedMesh[l].Mesh, Joints.ToArray());
                            }

                            var externalMdlReferencedNode = externalNodeDeepCloned.FirstOrDefault().Value;
                            // The game ignores the transformation of the node referenced by the node that was requesting it
                            externalMdlReferencedNode.SetLocalTransform(new(Matrix4x4.Identity), false);
                            var thisNode = nodeMap[HiList[k]];
                            NodesToAdd.Add((thisNode, externalMdlReferencedNode));

                            if (externalMdl.MOL != null && externalMdl.MOL.Animation.Count > 0)
                            {
                                scene = externalMdl.AddAnimationsToScene(scene, externalNodeDeepCloned.Values.ToList());
                            }
                        }
                    }
                }
                else
                {
                    // One or more specific node names were specified
                    var nodeNamesInMainFile = nodeNameInMainFile.Split(';');
                    for (int k = 0; k < nodeNamesInMainFile.Length; k++)
                    {
                        // Get externalmdl file's root ("b10obj00.mdl") and deep clone it.
                        // In the dictionary created, the key is the original node and the value is the deep cloned node
                        Dictionary<NodeBuilder, NodeBuilder> externalNodeDeepCloned = externalMdlNodeMap.FirstOrDefault().Value.Root.DeepClone().Skip(1).ToDictionary();

                        int m = 0;
                        for (int l = 0; l < externalMdlMeshes.RigidMesh.Count; l++)
                        {
                            // Find the key
                            for (; m < externalNodeDeepCloned.Count; m++)
                            {
                                if (externalNodeDeepCloned.ElementAt(m).Key == externalMdlMeshes.RigidMesh[l].Node)
                                {
                                    scene.AddRigidMesh(externalMdlMeshes.RigidMesh[l].Mesh, externalNodeDeepCloned.ElementAt(m).Value);
                                    break;
                                }
                            }
                        }

                        for (int l = 0; l < externalMdlMeshes.SkinnedMesh.Count; l++)
                        {
                            List<(NodeBuilder Joint, Matrix4x4 InverseBindMatrix)> Joints;
                            if (_isPlayer && (externalMdl.FolderName != "face" || FolderName == "00" || FolderName == "10"))
                            {
                                // Use p00.mdl skeleton (nodeMap)
                                Joints = GetJoints(externalMdl.Mesh[externalMdlMeshes.SkinnedMesh[l].MeshIndex], nodeMap.Values.ToList());
                            }
                            else
                            {
                                Joints = GetJoints(externalMdl.Mesh[externalMdlMeshes.SkinnedMesh[l].MeshIndex], externalNodeDeepCloned.Values.ToList());
                            }

                            scene.AddSkinnedMesh(externalMdlMeshes.SkinnedMesh[l].Mesh, Joints.ToArray());
                        }

                        NodeBuilder externalMdlReferencedNode = externalNodeDeepCloned.FirstOrDefault().Value.Root;
                        NodeBuilder thisNode = nodeMap[HiList.FirstOrDefault(x => x.Name == nodeNamesInMainFile[k])];
                        NodesToAdd.Add((thisNode, externalMdlReferencedNode));

                        if (externalMdl.MOL != null && externalMdl.MOL.Animation.Count > 0)
                        {
                            scene = externalMdl.AddAnimationsToScene(scene, externalNodeDeepCloned.Values.ToList());
                        }
                    }
                }
            }

            // Add the node to the node that was requesting it
            for (int i = 0; i < NodesToAdd.Count; i++)
            {
                NodesToAdd[i].node.AddNode(NodesToAdd[i].requestedNode);
            }

            return scene;
        }

        private MDLStream? GetExternalMdl(List<ExternalMDL> externalMdlList, KeyValuePair<string, MDLStream> externalMDLFile, out string nodeNameInMainFile)
        {
            MDLStream? externalMdl = null;
            nodeNameInMainFile = "";
            for (int j = 0; j < externalMdlList.Count; j++)
            {
                string key = externalMdlList[j].MdlFilePathToImport;
                if (Regex.IsMatch(externalMDLFile.Key, key))
                {
                    externalMdl = externalMDLFile.Value;
                    nodeNameInMainFile = externalMdlList[j].NodeNameInMainFile;
                    break;
                }
            }

            return externalMdl;
        }

        private SceneBuilder AddAnimationsToScene(SceneBuilder scene, List<NodeBuilder> nodes)
        {
            for (int i = 0; i < MOL.Animation.Count; i++)
            {
                var Anim = MOL.Animation[i];

                for (int j = 0; j < Anim.Bone.Count; j++)
                {
                    NodeBuilder? node = nodes.Find(x => Regex.IsMatch(x.Name, $@"\d_{Anim.Bone[j].Name}$"));

                    // Sometimes the MOL has bone names that are not present in the hierarchy list
                    if (node == null)
                    {
                        continue;
                    }

                    if (Anim.Bone[j].Translation != null)
                    {
                        Dictionary<float, Vector3> TranslationKeyFrames = new();
                        for (int k = 0; k < Anim.TranslationKeyframesCount; k++)
                        {
                            TranslationKeyFrames.Add(Anim.TranslationKeyFrames[k], Anim.Bone[j].Translation[k]);
                        }

                        node.WithLocalTranslation(Anim.Name, TranslationKeyFrames);
                    }

                    if (Anim.Bone[j].Rotation != null)
                    {
                        Dictionary<float, Quaternion> RotationKeyFrames = new();
                        for (int k = 0; k < Anim.RotationKeyframesCount; k++)
                        {
                            RotationKeyFrames.Add(Anim.RotationKeyframes[k], Anim.Bone[j].Rotation[k]);
                        }

                        node.WithLocalRotation(Anim.Name, RotationKeyFrames);
                    }

                    if (Anim.Bone[j].Scale != null)
                    {
                        Dictionary<float, Vector3> ScaleKeyFrames = new();
                        for (int k = 0; k < Anim.ScaleKeyframesCount; k++)
                        {
                            ScaleKeyFrames.Add(Anim.ScaleKeyframes[k], Anim.Bone[j].Scale[k]);
                        }

                        node.WithLocalScale(Anim.Name, ScaleKeyFrames);
                    }
                }
            }

            return scene;
        }

        public class Header_t
        {
            public RFHeader_t RFHeader;
            public byte HiListVersion;
            public byte MeshVersion;
            public ushort ObjectCount;
            public ushort TDLFileCount;
            public ushort MaterialTextureIndexCount;
            public ushort MeshCount;
            public ushort MeshWithBonesCount;
            public ushort AnimationCount;
            public uint MaterialOffset;

            public ushort UnkVDLSectionCount;
            public ushort MaterialCount;
            public uint UnkVDLSectionInfoOff;
            public uint MaterialInfoOffset;
            public uint MaterialIndexOffset;
            public List<uint> MeshInfoOffset;

            public List<Material_t> Material;
            public List<MeshInfo_t> MeshInfo;

            // These 2 fields are at the beginning and end respectively of the hierarchy list for RF2MD3
            public ushort ExternalNodeCount;
            public List<string> ExternalNodeName;

            public Header_t(EndianBinaryReader br, string fileNameWithoutExtension)
            {
                RFHeader = new(br);
                if (RFHeader.Type == "MD2")
                {
                    br.Skip(4);
                    ObjectCount = br.ReadUInt16();
                    TDLFileCount = br.ReadUInt16();
                    MaterialCount = br.ReadUInt16();
                    MeshCount = br.ReadUInt16();
                    MeshWithBonesCount = br.ReadUInt16();
                    AnimationCount = br.ReadUInt16();
                    MaterialOffset = br.ReadUInt32();
                    HiListVersion = 1;
                    MeshVersion = 1;
                }
                else if (RFHeader.Type == "MD3")
                {
                    br.Skip(2);
                    var VDLVersion = br.ReadUInt16();

                    if (VDLVersion == 0x12D)
                    {
                        // d075c.mdl
                        HiListVersion = 1;
                        MeshVersion = 1;
                    }
                    else if (VDLVersion == 0x12E)
                    {
                        // d441a00.mdl
                        HiListVersion = 1;
                        MeshVersion = 2;
                    }
                    else if (VDLVersion >= 0x12F)
                    {
                        // 0x12F: d003.mdl
                        // 0x130: d002.mdl (VDLVersion of 0x130 has VertexIndexDataTypeFlags not 0)
                        HiListVersion = 2;
                        MeshVersion = 2;
                    }

                    ObjectCount = br.ReadUInt16();
                    TDLFileCount = br.ReadUInt16();
                    UnkVDLSectionCount = br.ReadUInt16();
                    MaterialCount = br.ReadUInt16();
                    MaterialTextureIndexCount = br.ReadUInt16();
                    MeshCount = br.ReadUInt16();
                    MeshWithBonesCount = br.ReadUInt16();
                    AnimationCount = br.ReadUInt16();
                    UnkVDLSectionInfoOff = br.ReadUInt32();
                    MaterialInfoOffset = br.ReadUInt32();
                    MaterialIndexOffset = br.ReadUInt32();
                }

                MeshInfoOffset = new();
                for (int i = 0; i < MeshCount; i++)
                {
                    MeshInfoOffset.Add(br.ReadUInt32());
                }

                // Switch to big endian here
                br.SetEndianness(EndianBinaryReader.Endianness.Big);

                Material = new();
                if (RFHeader.Type == "MD2")
                {
                    for (ushort i = 0; i < MaterialCount; i++)
                    {
                        br.Seek(MaterialOffset + i * 0xC, SeekOrigin.Begin);
                        ushort TDLIndex = br.ReadUInt16();
                        if (TDLIndex == 0xFFFF)
                        {
                            // dummy material in eo2 b00stage.mdl and eo1 s00stage.mdl
                            Material.Add(new() { Index = i });
                            continue;
                        }

                        string textureFileName = GetTextureFileNameFromIndex(br, TDLIndex, out bool isInFile);
                        if (isInFile)
                        {
                            textureFileName = $"{fileNameWithoutExtension}_{textureFileName}";
                        }

                        Material_t material = new();
                        material.Index = i;
                        material.Texture.Add(new(textureFileName));
                        Material.Add(material);
                    }

                    br.Skip(0xA);
                }
                else
                {
                    for (ushort i = 0; i < MaterialCount; i++)
                    {
                        br.Seek(MaterialInfoOffset + i * 0xC, SeekOrigin.Begin);

                        Material_t material = new();
                        var Offset = br.ReadUInt32();
                        byte TextureCount = br.ReadByte();
                        byte Type = br.ReadByte();

                        br.Seek(Offset, SeekOrigin.Begin);
                        for (int j = 0; j < TextureCount; j++)
                        {
                            ushort TDLIndex = br.ReadUInt16();
                            string textureFileName = GetTextureFileNameFromIndex(br, TDLIndex, out bool isInFile);
                            if (isInFile)
                            {
                                textureFileName = $"{fileNameWithoutExtension}_{textureFileName}";
                            }

                            material.Texture.Add(new(textureFileName));
                            br.Skip(0x2);
                        }

                        material.Index = i;
                        material.Type = Type;
                        Material.Add(material);
                    }
                }

                MeshInfo = new();
                for (int i = 0; i < MeshCount; i++)
                {
                    //br.Seek(MeshInfoOffset[i], SeekOrigin.Begin);
                    MeshInfo.Add(new MeshInfo_t(br));
                }
            }

            string GetTextureFileNameFromIndex(EndianBinaryReader br, ushort index, out bool isInFile)
            {
                isInFile = false;
                string fileName = "notexture";
                FileInfo_t? File = RFHeader.GetTextureFileInfoAtIndex(index);
                if (File.Type == FILETYPE.TXS && File.IsInFile)
                {
                    var originalPosition = br.BaseStream.Position;
                    br.Seek(File.Offset, SeekOrigin.Begin);
                    RFHeader_t TXSHeader = new(br);
                    br.Seek(originalPosition, SeekOrigin.Begin);

                    if (TXSHeader.Files.Count > 0)
                    {
                        isInFile = TXSHeader.Files[0].IsInFile;
                        fileName = TXSHeader.Files[0].Name;
                    }
                }
                else if (File.Type == FILETYPE.TDL)
                {
                    fileName = File.Name;
                    isInFile = File.IsInFile;
                }

                return $"{fileName}.png";
            }
        }

        [DebuggerDisplay("{Index} | {Texture.Count} | {Texture[0].FileName}")]
        public class Material_t
        {
            public List<Texture_t> Texture;
            public ushort Index;
            public byte Type;

            public Material_t()
            {
                Texture = new();
            }
        }

        [DebuggerDisplay("{FileName}")]
        public class Texture_t
        {
            public string FileName;
            public string FilePath;
            public Texture_t(string fileName)
            {
                FileName = fileName;
            }
        }

        public class MeshInfo_t
        {
            public byte TransparencyFlag;
            public byte VertexType;
            public bool HasSkinData;
            public ushort PrimitiveCount;
            public Vector3 Origin;
            public Vector3 BoundingVolumeMin;
            public Vector3 BoundingVolumeMax;
            public uint HeaderOffset;
            public uint Size;
            public ushort BoneCount;
            public uint SkeletonOffset;
            public List<PrimitiveInfo_t> PrimitiveInfo = new();

            public MeshInfo_t(EndianBinaryReader br)
            {
                br.Skip(1);
                byte Type = br.ReadByte();
                if ((Type & 0b01000000) == 0b01000000)
                {
                    HasSkinData = true;
                }
                TransparencyFlag = br.ReadByte();
                // 0b0000_0001 = Has VertexPosition
                // 0b0000_0010 = Has VertexNormal
                // 0b0000_0100 = Has VertexLight
                // 0b0000_1000 = Has VertexTexture
                // 0b1111_0000 = first 4 bit is uv map count?
                VertexType = (byte)(br.ReadByte() & 0xF);
                br.Skip(2);
                PrimitiveCount = br.ReadUInt16();
                // Skip "always 0", "always 0xFFFFFFFF" and a float.
                // "Always 0" and "always 0xFFFFFFFF" are someway related to models that use an archive of textures (txs) (d037.mdl)
                br.Skip(0xC);
                Origin = new(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                BoundingVolumeMin = new(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                BoundingVolumeMax = new(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                HeaderOffset = br.ReadUInt32();
                Size = br.ReadUInt32();

                if (HasSkinData)
                {
                    BoneCount = br.ReadUInt16();
                    br.Skip(0x2);
                    SkeletonOffset = br.ReadUInt32();
                }

                for (int i = 0; i < PrimitiveCount; i++)
                {
                    PrimitiveInfo.Add(new(br));
                }
            }
        }

        public class PrimitiveInfo_t
        {
            public ushort MaterialIndex;
            public bool RenderDoubleSide;
            public bool HasAlphaBlend;
            public PRIMITIVE_TYPE PrimitiveType;
            public uint Offset;

            public PrimitiveInfo_t(EndianBinaryReader br)
            {
                MaterialIndex = br.ReadUInt16();
                var unk01 = br.ReadByte();
                RenderDoubleSide = (unk01 & 0x4) == 0x4;
                HasAlphaBlend = (unk01 & 0x2) == 0x2;
                PrimitiveType = (PRIMITIVE_TYPE)br.ReadByte();
                br.Skip(4);
                Offset = br.ReadUInt32();
            }
        }

        [DebuggerDisplay("({Id}) | {Name} | {Type} | {Index}")]
        public class HiObject_t
        {
            public bool HasAlphaBlend;
            public byte Byte2;
            public bool RenderDoubleSide;
            public byte Byte3;
            public ushort Id;
            public HiObject_t? ParentObject;
            public HI_OBJECT_TYPE Type;
            public byte Level;
            //public byte TransparencyType;
            public ushort Index;
            public Vector3 Translation;
            public Quaternion Rotation;
            public Vector3 Scale;
            public string Name;

            public HiObject_t(EndianBinaryReader br, byte hiListVersion)
            {
                byte Byte1 = br.ReadByte();
                if (hiListVersion == 2)
                {
                    HasAlphaBlend = (Byte1 & 0x4) == 0x4;
                }
                Byte2 = br.ReadByte();
                RenderDoubleSide = (Byte2 & 0x4) == 0x4;
                Byte3 = br.ReadByte();
                var type = (byte)(br.ReadByte() & 0xF0);
                Type = (HI_OBJECT_TYPE)type;

                Level = br.ReadByte();
                byte Byte4 = br.ReadByte();
                if (hiListVersion == 1)
                {
                    HasAlphaBlend = (Byte4 & 0x1) == 0x1;
                }
                Index = br.ReadUInt16();

                Translation = new(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                Rotation = new(br.ReadSingle(), br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                Scale = new(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                // Name = br.ReadString(0x10);
                // fix for eo1/stage/zoom/s03/s03z0121.mdl
                Name = System.Text.Encoding.GetEncoding("ISO-8859-1").GetString(br.ReadBytes(0x10)).Split('\0')[0];

                if (Rotation.Length() == 0)
                {
                    Rotation = Quaternion.Identity;
                }
            }

            public NodeBuilder ToNodeBuilder(string name)
            {
                var node = new NodeBuilder(name);
                node.WithLocalTranslation(Translation);
                node.WithLocalRotation(Rotation);
                node.WithLocalScale(Scale);
                return node;
            }
        }

        [DebuggerDisplay("{HiObject.Name}")]
        public class Mesh_t
        {
            public long AbsoluteOffset;
            public HiObject_t HiObject;
            public string OutputName;

            public SkinData_t SkinData;

            public uint VertexPositionOffset;
            public uint VertexNormalOffset;
            public uint VertexLightOffset;
            public uint VertexTextureOffset;
            public uint VertexTexture2Offset;

            public ushort VertexPositionCount;
            public ushort VertexNormalCount;
            public ushort VertexLightCount;
            public ushort VertexTextureCount;
            public ushort VertexTexture2Count;

            public ushort VertexIndexDataTypeFlags;
            public byte VertexIndexStride;
            public bool IsVertexPositionAndNormalInterleaved; // Always 1 if the mesh has bones
            public byte UvMapsCount;
            public byte VertexPositionStride;
            public byte VertexNormalStride;
            public byte VertexLightStride;
            public byte VertexTextureStride;

            public List<Vector3> VertexPosition;
            public List<Vector3> VertexNormal;
            public List<Vector4> VertexLight;
            public List<Vector2> VertexTexture;
            public List<Vector2> VertexTexture2;

            public VertexIndexDatatypes VertexIndexDatatype;
            public List<Primitive_t> Primitive;

            public byte TransparencyFlagFromMeshInfo;
            public Mesh_t(EndianBinaryReader br, MeshInfo_t meshInfo, ushort meshVersion, long vdlOffset)
            {
                if (meshInfo.HasSkinData)
                {
                    br.Seek(vdlOffset + meshInfo.SkeletonOffset, SeekOrigin.Begin);
                    SkinData = new(br, meshInfo.BoneCount);
                }

                br.Seek(vdlOffset + meshInfo.HeaderOffset, SeekOrigin.Begin);
                AbsoluteOffset = br.BaseStream.Position;
                VertexPositionOffset = br.ReadUInt32();
                VertexNormalOffset = br.ReadUInt32();
                VertexLightOffset = br.ReadUInt32();
                VertexTextureOffset = br.ReadUInt32();
                if (meshVersion == 2)
                {
                    VertexTexture2Offset = br.ReadUInt32();
                    br.Skip(12);
                }
                else
                {
                    br.Skip(4);
                }

                VertexPositionCount = br.ReadUInt16();
                VertexNormalCount = br.ReadUInt16();
                VertexLightCount = br.ReadUInt16();
                VertexTextureCount = br.ReadUInt16();

                if (meshVersion == 2)
                {
                    VertexTexture2Count = br.ReadUInt16();
                    br.Skip(8);
                    VertexIndexDataTypeFlags = br.ReadUInt16();
                    br.Skip(5);
                    VertexIndexStride = br.ReadByte();
                    IsVertexPositionAndNormalInterleaved = br.ReadBoolean();
                    UvMapsCount = br.ReadByte();
                    VertexPositionStride = br.ReadByte();
                    VertexNormalStride = br.ReadByte();
                    VertexLightStride = br.ReadByte();
                    VertexTextureStride = br.ReadByte();
                }
                else
                {
                    br.Skip(3);
                    IsVertexPositionAndNormalInterleaved = br.ReadBoolean();
                }

                // If the VDLVersion was not 0x130:
                // the meshes have the fields VertexIndexDataTypeFlags and VertexIndexStride set to 0
                // To calculate them, the game uses the VertexType field from the MeshInfo_t struct
                // This might come from a tool they used to convert MD2 format to MD3 format (because in MD2 there's no optimization for the index buffer, everything is a ushort)
                // So let's consider everything as a ushort too.
                // This is a hardcoded table in main.dol. Here we actually just check for the odd values, because we assume that all models have VertexPosition information
                // MeshInfo.VertexType: 0x4 or 0x5, VertexIndexDataTypeFlags: 0x33 (0011_0011), stride: 2 * 2
                // MeshInfo.VertexType: 0x8 or 0x9, VertexIndexDataTypeFlags: 0xC3 (1100_0011), stride: 2 * 2
                // MeshInfo.VertexType: 0xA or 0xB, VertexIndexDataTypeFlags: 0xCF (1100_1111), stride: 3 * 2
                // MeshInfo.VertexType: 0xC or 0xD, VertexIndexDataTypeFlags: 0xF3 (1111_0011), stride: 3 * 2
                // MeshInfo.VertexType: 0xE or 0xF, VertexIndexDataTypeFlags: 0xFF (1111_1111), stride: 4 * 2
                if (VertexIndexDataTypeFlags == 0)
                {
                    // We put 0x1B and 0x1F first because they are the most common
                    // NOTE: There actually don't seem to be any mdl files with VertexType 0x5, 0x9 and 0xD
                    if (meshInfo.VertexType == 0xB)
                    {
                        VertexIndexDataTypeFlags = 0xCF;
                        VertexIndexStride = 0x6;
                    }
                    else if (meshInfo.VertexType == 0xF)
                    {
                        VertexIndexDataTypeFlags = 0xFF;
                        VertexIndexStride = 0x8;
                    }
                    else if (meshInfo.VertexType == 0x5)
                    {
                        VertexIndexDataTypeFlags = 0x33;
                        VertexIndexStride = 0x4;
                    }
                    else if (meshInfo.VertexType == 0x9)
                    {
                        VertexIndexDataTypeFlags = 0xC3;
                        VertexIndexStride = 0x4;
                    }
                    else if (meshInfo.VertexType == 0xD)
                    {
                        VertexIndexDataTypeFlags = 0xF3;
                        VertexIndexStride = 0x6;
                    }
                }
                CalculateVertexIndexDatatypes();

                VertexPosition = new(VertexPositionCount);
                VertexNormal = new(VertexNormalCount);
                VertexTexture = new(VertexTextureCount);
                br.BaseStream.Seek(AbsoluteOffset + VertexPositionOffset, SeekOrigin.Begin);
                if (IsVertexPositionAndNormalInterleaved)
                {
                    for (int i = 0; i < VertexPositionCount; i++)
                    {
                        VertexPosition.Add(new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle()));
                        VertexNormal.Add(new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle()));
                    }
                }
                else
                {
                    for (int i = 0; i < VertexPositionCount; i++)
                    {
                        VertexPosition.Add(new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle()));
                    }

                    br.BaseStream.Seek(AbsoluteOffset + VertexNormalOffset, SeekOrigin.Begin);
                    for (int i = 0; i < VertexNormalCount; i++)
                    {
                        VertexNormal.Add(new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle()));
                    }
                }

                if (VertexIndexDatatype.Light != VERTEX_INDEX_DATATYPE.NONE)
                {
                    VertexLight = new(VertexLightCount);
                    br.BaseStream.Seek(AbsoluteOffset + VertexLightOffset, SeekOrigin.Begin);
                    for (int i = 0; i < VertexLightCount; i++)
                    {
                        float r = (float)br.ReadByte() / 255;
                        float g = (float)br.ReadByte() / 255;
                        float b = (float)br.ReadByte() / 255;
                        float a = (float)br.ReadByte() / 255;
                        VertexLight.Add(new(r, g, b, a));
                    }
                }

                br.BaseStream.Seek(AbsoluteOffset + VertexTextureOffset, SeekOrigin.Begin);
                for (int i = 0; i < VertexTextureCount; i++)
                {
                    VertexTexture.Add(new Vector2(br.ReadSingle(), br.ReadSingle()));
                }

                if (VertexIndexDatatype.Texture2 != VERTEX_INDEX_DATATYPE.NONE)
                {
                    VertexTexture2 = new(VertexTexture2Count);
                    br.BaseStream.Seek(AbsoluteOffset + VertexTexture2Offset, SeekOrigin.Begin);
                    for (int i = 0; i < VertexTexture2Count; i++)
                    {
                        VertexTexture2.Add(new Vector2(br.ReadSingle(), br.ReadSingle()));
                    }
                }

                Primitive = new(meshInfo.PrimitiveCount);
                for (int i = 0; i < meshInfo.PrimitiveCount; i++)
                {
                    br.BaseStream.Seek(AbsoluteOffset + meshInfo.PrimitiveInfo[i].Offset, SeekOrigin.Begin);
                    Primitive.Add(new(br, meshInfo.PrimitiveInfo[i], VertexIndexStride, VertexIndexDatatype));
                }

                TransparencyFlagFromMeshInfo = meshInfo.TransparencyFlag;
            }

            public void CalculateVertexIndexDatatypes()
            {
                if ((VertexIndexDataTypeFlags & 0b00000011) == 0b00000011)
                {
                    VertexIndexDatatype.Position = VERTEX_INDEX_DATATYPE.USHORT;
                }
                else if ((VertexIndexDataTypeFlags & 0b00000010) == 0b00000010)
                {
                    VertexIndexDatatype.Position = VERTEX_INDEX_DATATYPE.BYTE;
                }

                if ((VertexIndexDataTypeFlags & 0b00001100) == 0b00001100)
                {
                    VertexIndexDatatype.Normal = VERTEX_INDEX_DATATYPE.USHORT;
                }
                else if ((VertexIndexDataTypeFlags & 0b00001000) == 0b00001000)
                {
                    VertexIndexDatatype.Normal = VERTEX_INDEX_DATATYPE.BYTE;
                }

                if ((VertexIndexDataTypeFlags & 0b00110000) == 0b00110000)
                {
                    VertexIndexDatatype.Light = VERTEX_INDEX_DATATYPE.USHORT;
                }
                else if ((VertexIndexDataTypeFlags & 0b00100000) == 0b00100000)
                {
                    VertexIndexDatatype.Light = VERTEX_INDEX_DATATYPE.BYTE;
                }

                if ((VertexIndexDataTypeFlags & 0b11000000) == 0b11000000)
                {
                    VertexIndexDatatype.Texture = VERTEX_INDEX_DATATYPE.USHORT;
                }
                else if ((VertexIndexDataTypeFlags & 0b10000000) == 0b10000000)
                {
                    VertexIndexDatatype.Texture = VERTEX_INDEX_DATATYPE.BYTE;
                }

                if ((VertexIndexDataTypeFlags & 0b1100000000) == 0b1100000000)
                {
                    VertexIndexDatatype.Texture2 = VERTEX_INDEX_DATATYPE.USHORT;
                }
                else if ((VertexIndexDataTypeFlags & 0b1000000000) == 0b1000000000)
                {
                    VertexIndexDatatype.Texture2 = VERTEX_INDEX_DATATYPE.BYTE;
                }
            }
        }

        public class SkinData_t
        {
            public uint Size;
            public uint WeightIndexOffset;
            public uint WeightOffset;
            public uint VertexCount;
            public List<string> BoneNames = new();
            public List<Matrix4x4> InverseBoneMatrix = new();
            public List<Vector4> Weight = new();
            public List<byte[]> WeightIndex = new();

            public SkinData_t(EndianBinaryReader br, ushort boneCount)
            {
                long AbsoluteOffset = br.BaseStream.Position;
                Size = br.ReadUInt32();
                WeightIndexOffset = br.ReadUInt32();
                WeightOffset = br.ReadUInt32();
                VertexCount = br.ReadUInt32();
                BoneNames = new();
                for (int i = 0; i < boneCount; i++)
                {
                    BoneNames.Add(br.ReadString(0x10));
                }
                InverseBoneMatrix = new();
                for (int i = 0; i < boneCount; i++)
                {
                    InverseBoneMatrix.Add(new(
                        br.ReadSingle(), br.ReadSingle(), br.ReadSingle(), br.ReadSingle(),
                        br.ReadSingle(), br.ReadSingle(), br.ReadSingle(), br.ReadSingle(),
                        br.ReadSingle(), br.ReadSingle(), br.ReadSingle(), br.ReadSingle(),
                        br.ReadSingle(), br.ReadSingle(), br.ReadSingle(), br.ReadSingle()));
                }

                br.BaseStream.Seek(AbsoluteOffset + WeightOffset, SeekOrigin.Begin);
                for (int i = 0; i < VertexCount; i++)
                {
                    float x = float.Round(br.ReadSingle(), 3);
                    float y = float.Round(br.ReadSingle(), 3);
                    float z = float.Round(br.ReadSingle(), 3);
                    float w = float.Round(br.ReadSingle(), 3);

                    if (x < 0)
                    {
                        x = x * -1;
                    }

                    Weight.Add(new(x, y, z, w));
                }

                br.BaseStream.Seek(AbsoluteOffset + WeightIndexOffset, SeekOrigin.Begin);
                for (int i = 0; i < VertexCount; i++)
                {
                    WeightIndex.Add(new byte[] { br.ReadByte(), br.ReadByte(), br.ReadByte(), br.ReadByte() });
                }
            }
        }

        public class Primitive_t
        {
            public List<ushort[]> VertexPositionTriangles = new();
            public List<ushort[]> VertexNormalTriangles = new();
            public List<ushort[]> VertexLightTriangles = new();
            public List<ushort[]> VertexTextureTriangles = new();
            public List<ushort[]> VertexTexture2Triangles = new();
            public bool RenderDoubleSide;
            public bool HasAlpha;
            public Material_t Material;

            public Primitive_t(EndianBinaryReader br, PrimitiveInfo_t primitiveInfo, byte vertexIndexStride, VertexIndexDatatypes vertexIndexDatatype)
            {
                RenderDoubleSide = primitiveInfo.RenderDoubleSide;
                HasAlpha = primitiveInfo.HasAlphaBlend;
                ushort primitiveTerminatorFlag;

                if (primitiveInfo.PrimitiveType == PRIMITIVE_TYPE.TRIANGLE)
                {
                    List<ushort> VertexPositionIndex = new();
                    List<ushort> VertexNormalIndex = new();
                    List<ushort> VertexLightIndex = new();
                    List<ushort> VertexTextureIndex = new();
                    List<ushort> VertexTexture2Index = new();

                    // Unused primitiveTerminatorFlag, if the PrimitiveType is triangles, there's always 1 draw call
                    _ = br.ReadUInt16();
                    ushort vertexCount = br.ReadUInt16();
                    if (vertexCount < 3)
                    {
                        br.BaseStream.Seek(vertexCount * vertexIndexStride, SeekOrigin.Current);
                        return;
                    }

                    for (int i = 0; i < vertexCount; i++)
                    {
                        VertexPositionIndex.Add(ReadVertexIndex(br, vertexIndexDatatype.Position));
                        VertexNormalIndex.Add(ReadVertexIndex(br, vertexIndexDatatype.Normal));
                        if (vertexIndexDatatype.Light != VERTEX_INDEX_DATATYPE.NONE)
                        {
                            VertexLightIndex.Add(ReadVertexIndex(br, vertexIndexDatatype.Light));
                        }
                        VertexTextureIndex.Add(ReadVertexIndex(br, vertexIndexDatatype.Texture));
                        if (vertexIndexDatatype.Texture2 != VERTEX_INDEX_DATATYPE.NONE)
                        {
                            VertexTexture2Index.Add(ReadVertexIndex(br, vertexIndexDatatype.Texture2));
                        }
                    }

                    for (int i = 0; i < vertexCount; i = i + 3)
                    {
                        VertexPositionTriangles.Add(new ushort[]
                        {
                            VertexPositionIndex[i + 2], VertexPositionIndex[i + 1], VertexPositionIndex[i]
                        });
                        VertexNormalTriangles.Add(new ushort[]
                        {
                            VertexNormalIndex[i + 2], VertexNormalIndex[i + 1], VertexNormalIndex[i]
                        });

                        if (vertexIndexDatatype.Light != VERTEX_INDEX_DATATYPE.NONE)
                        {
                            VertexLightTriangles.Add(new ushort[]
                            {
                                VertexLightIndex[i + 2], VertexLightIndex[i + 1], VertexLightIndex[i]
                            });
                        }

                        VertexTextureTriangles.Add(new ushort[]
                        {
                            VertexTextureIndex[i + 2], VertexTextureIndex[i + 1], VertexTextureIndex[i]
                        });

                        if (vertexIndexDatatype.Texture2 != VERTEX_INDEX_DATATYPE.NONE)
                        {
                            VertexTexture2Triangles.Add(new ushort[]
                            {
                                VertexTexture2Index[i + 2], VertexTexture2Index[i + 1], VertexTexture2Index[i]
                            });
                        }
                    }
                }
                else if (primitiveInfo.PrimitiveType == PRIMITIVE_TYPE.TRISTRIP)
                {
                    // Tristrip, multiple draw calls,
                    // the last draw call is when primitiveTerminatorFlag has the last bit set to 0
                    do
                    {
                        List<ushort> VertexPositionIndex = new();
                        List<ushort> VertexNormalIndex = new();
                        List<ushort> VertexLightIndex = new();
                        List<ushort> VertexTextureIndex = new();
                        List<ushort> VertexTexture2Index = new();

                        primitiveTerminatorFlag = br.ReadUInt16();
                        ushort vertexCount = br.ReadUInt16();
                        if (vertexCount < 3)
                        {
                            br.BaseStream.Seek(vertexCount * vertexIndexStride, SeekOrigin.Current);
                            continue;
                        }

                        for (int i = 0; i < vertexCount; i++)
                        {
                            VertexPositionIndex.Add(ReadVertexIndex(br, vertexIndexDatatype.Position));
                            VertexNormalIndex.Add(ReadVertexIndex(br, vertexIndexDatatype.Normal));
                            if (vertexIndexDatatype.Light != VERTEX_INDEX_DATATYPE.NONE)
                            {
                                VertexLightIndex.Add(ReadVertexIndex(br, vertexIndexDatatype.Light));
                            }
                            VertexTextureIndex.Add(ReadVertexIndex(br, vertexIndexDatatype.Texture));
                            if (vertexIndexDatatype.Texture2 != VERTEX_INDEX_DATATYPE.NONE)
                            {
                                VertexTexture2Index.Add(ReadVertexIndex(br, vertexIndexDatatype.Texture2));
                            }
                        }

                        // vertexCount = 3 -> 1 triangle
                        // vertexCount = 4 -> 2 triangles
                        // vertexCount = 5 -> 3 triangles
                        // vertexCount = 6 -> 4 triangles
                        // vertexCount = 7 -> 5 triangles
                        // ...
                        ushort trianglesCount = (ushort)(vertexCount - 2);
                        for (int i = 0; i < trianglesCount; i++)
                        {
                            VertexPositionTriangles.Add(new ushort[]
                            {
                                VertexPositionIndex[i + 2], VertexPositionIndex[i + 1], VertexPositionIndex[i]
                            });
                            VertexNormalTriangles.Add(new ushort[]
                            {
                                VertexNormalIndex[i + 2], VertexNormalIndex[i + 1], VertexNormalIndex[i]
                            });

                            if (vertexIndexDatatype.Light != VERTEX_INDEX_DATATYPE.NONE)
                            {
                                VertexLightTriangles.Add(new ushort[]
                                {
                                    VertexLightIndex[i + 2], VertexLightIndex[i + 1], VertexLightIndex[i]
                                });
                            }

                            VertexTextureTriangles.Add(new ushort[]
                            {
                                VertexTextureIndex[i + 2], VertexTextureIndex[i + 1], VertexTextureIndex[i]
                            });

                            if (vertexIndexDatatype.Texture2 != VERTEX_INDEX_DATATYPE.NONE)
                            {
                                VertexTexture2Triangles.Add(new ushort[]
                                {
                                    VertexTexture2Index[i + 2], VertexTexture2Index[i + 1], VertexTexture2Index[i]
                                });
                            }

                            if (i + 1 == trianglesCount)
                            {
                                break;
                            }

                            VertexPositionTriangles.Add(new ushort[]
                            {
                                VertexPositionIndex[i + 1], VertexPositionIndex[i + 2], VertexPositionIndex[i + 3]
                            });
                            VertexNormalTriangles.Add(new ushort[]
                            {
                                VertexNormalIndex[i + 1], VertexNormalIndex[i + 2], VertexNormalIndex[i + 3]
                            });

                            if (vertexIndexDatatype.Light != VERTEX_INDEX_DATATYPE.NONE)
                            {
                                VertexLightTriangles.Add(new ushort[]
                                {
                                    VertexLightIndex[i + 1], VertexLightIndex[i + 2], VertexLightIndex[i + 3]
                                });
                            }

                            VertexTextureTriangles.Add(new ushort[]
                            {
                                VertexTextureIndex[i + 1], VertexTextureIndex[i + 2], VertexTextureIndex[i + 3]
                            });

                            if (vertexIndexDatatype.Texture2 != VERTEX_INDEX_DATATYPE.NONE)
                            {
                                VertexTexture2Triangles.Add(new ushort[]
                                {
                                    VertexTexture2Index[i + 1], VertexTexture2Index[i + 2], VertexTexture2Index[i + 3]
                                });
                            }

                            i++;
                        }
                    } while ((primitiveTerminatorFlag & 1) != 0);
                }
            }

            public static ushort ReadVertexIndex(EndianBinaryReader br, VERTEX_INDEX_DATATYPE VertexIndexDatatype)
            {
                if (VertexIndexDatatype == VERTEX_INDEX_DATATYPE.BYTE)
                {
                    return br.ReadByte();
                }
                else if (VertexIndexDatatype == VERTEX_INDEX_DATATYPE.USHORT)
                {
                    return br.ReadUInt16();
                }
                else
                {
                    return 0xFFFF;
                }
            }
        }

        public enum HI_OBJECT_TYPE
        {
            Group = 0x10, // "transfogroup"
            Mesh = 0x20,
            Instance = 0x30, // "instance" where the node is in this mdl file
            LOD = 0x40, // Has 2 children/meshes, one for high detail and one for low detail
            ExternalNode = 0x50 // "instance" where the node is from another mdl file
        }

        public enum VERTEX_INDEX_DATATYPE
        {
            NONE = 0,
            BYTE = 2,
            USHORT = 3
        }

        public enum PRIMITIVE_TYPE
        {
            // POINT = 1, // Unused but accepted by the game
            // LINE = 2, // Unused but accepted by the game
            TRIANGLE = 3,
            TRISTRIP = 4
        }
    }
}
