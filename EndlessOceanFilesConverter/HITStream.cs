using SharpGLTF.Geometry;
using SharpGLTF.Geometry.VertexTypes;
using SharpGLTF.Scenes;
using SharpGLTF.Schema2;
using System.Numerics;
using static EndlessOceanFilesConverter.Utils;

namespace EndlessOceanFilesConverter
{
    using SharpGLTFRigidMesh = MeshBuilder<VertexPosition, VertexEmpty, VertexEmpty>;
    public class HITStream
    {
        public string FilePath;
        public string FileName;

        public Header_t Header;
        public List<Collision_t> Collision;

        /// <summary>
        /// Additional translation for eo1 main stage (eo2 rods are already in their correct position)
        /// </summary>
        public Vector3 RodTranslation { get; set; }
        public ERROR Error;

        public bool IsEO1Rod { get; set; }
        
        public HITStream(EndianBinaryReader br, string filePath)
        {
            FilePath = filePath;
            FileName = Path.GetFileName(filePath);
            if (System.Text.RegularExpressions.Regex.IsMatch(FileName, s01fRegexPattern))
            {
                IsEO1Rod = true;
            }

            br.SetEndianness(EndianBinaryReader.Endianness.Big);
            Header = new(br);

            List<string> CollisionNames = new();
            for (int i = 0; i < Header.CollisionCount; i++)
            {
                CollisionNames.Add(br.ReadString((uint)(Header.Version == 1 ? 0x10 : 0x20)));
            }

            //br.Seek(Header.CollisionInfoOffset, SeekOrigin.Begin);
            List<CollisionInfo_t> CollisionInfo = new();
            for (int i = 0; i < Header.CollisionCount; i++)
            {
                CollisionInfo.Add(new(br));
            }

            //br.Seek(Header.PolygonInfoOffset, SeekOrigin.Begin);
            List<PolygonInfo_t> PolygonInfo = new();
            for (int i = 0; i < Header.CollisionCount; i++)
            {
                var polygonCount = CollisionInfo[i].PolygonCount;
                for (int j = 0; j < polygonCount; j++)
                {
                    PolygonInfo.Add(new(br));
                }
            }

            //br.Seek(Header.VertexPositionOffset, SeekOrigin.Begin);
            Collision = new();
            for (int i = 0; i < Header.CollisionCount; i++)
            {
                var collision = new Collision_t(CollisionNames[i]);
                var polygonCount = CollisionInfo[i].PolygonCount;
                for (int j = 0; j < polygonCount; j++)
                {
                    Polygon_t polygon = new();
                    PolygonInfo_t polygonInfo = PolygonInfo[CollisionInfo[i].PolygonOffset + j];
                    var vertexPositionCount = polygonInfo.VertexPositionCount;
                    for (int k = 0; k < vertexPositionCount; k++)
                    {
                        polygon.VertexPosition.Add(new(br.ReadSingle(), br.ReadSingle(), br.ReadSingle()));
                        br.Skip(4);
                    }
                    collision.Polygon.Add(polygon);
                }

                Collision.Add(collision);
            }

            FBH_t FBH = new(br);
            for (int i = 0; i < FBH.Header.TransformationCount; i++)
            {
                ushort id = FBH.Transformation[i].Id;
                byte numCollisions = FBH.Transformation[i].CollisionCount;
                for (int j = 0; j < numCollisions; j++)
                {
                    var mat = Matrix4x4.CreateFromQuaternion(FBH.Transformation[i].Rotation);
                    mat.Translation = FBH.Transformation[i].Translation;
                    Collision[id + j].WorldTransformation = mat;
                }
            }
        }

        public void SaveAsGLB()
        {
            if (FilePath == null || FilePath == "")
            {
                return;
            }

            SaveAsGLB(FilePath);
        }

        public void SaveAsGLB(string filePath)
        {
            var scene = new SceneBuilder();
            var mainNode = new NodeBuilder($"{FileName}");
            scene.AddNode(mainNode);

            Console.Write($"\t\tWith ");
            (int Left, int Top) cursorPositionCollisionCount = Console.GetCursorPosition();
            Console.Write($"0/{Header.CollisionCount} collision(s)");
            for (int i = 0; i < Header.CollisionCount; i++)
            {
                Collision[i].OutputName = $"{i}_{Collision[i].Name}";

                var meshWorldTransformation = Matrix4x4.Identity;
                meshWorldTransformation = meshWorldTransformation * Collision[i].WorldTransformation;
                if (RodTranslation != Vector3.Zero)
                {
                    meshWorldTransformation.Translation += RodTranslation;
                }

                var mesh = CollisionToSharpGLTFRigidMeshBuilder(Collision[i]);
                var node = new NodeBuilder(Collision[i].OutputName);
                node.SetLocalTransform(meshWorldTransformation, false);
                mainNode.AddNode(node); // Add all collisions to main node
                scene.AddRigidMesh(mesh, node);

                Console.SetCursorPosition(cursorPositionCollisionCount.Left, cursorPositionCollisionCount.Top);
                Console.Write($"{i + 1}/{Header.CollisionCount} collision(s)");
            }

            ModelRoot model = scene.ToGltf2();
            model.SaveGLB($"{filePath}.glb");
        }

        private SharpGLTFRigidMesh CollisionToSharpGLTFRigidMeshBuilder(Collision_t collision)
        {
            var MeshBuilder = new SharpGLTFRigidMesh(collision.OutputName);
            var material = new SharpGLTF.Materials.MaterialBuilder("").WithDoubleSide(true)
                                                            .WithMetallicRoughnessShader();
            var prim = MeshBuilder.UsePrimitive(material);
            for (int i = 0; i < collision.Polygon.Count; i++)
            {
                // Triangle fan
                // n+2 n+1 n
                // n+3 n+2 n
                // n+4 n+3 n
                // n+5 n+4 n
                // ...
                var trianglesCount = collision.Polygon[i].VertexPosition.Count - 2;
                for (int j = 0; j < trianglesCount; j++)
                {
                    var a = new VertexBuilder<VertexPosition, VertexEmpty, VertexEmpty>
                    (
                        collision.Polygon[i].VertexPosition[0]
                    );
                    var b = new VertexBuilder<VertexPosition, VertexEmpty, VertexEmpty>
                    (
                        collision.Polygon[i].VertexPosition[j + 1]
                    );
                    var c = new VertexBuilder<VertexPosition, VertexEmpty, VertexEmpty>
                    (
                        collision.Polygon[i].VertexPosition[j + 2]
                    );
                    prim.AddTriangle(a, b, c);
                }
            }

            return MeshBuilder;
        }

        public class Header_t
        {
            public ushort Version;
            public string Magic;
            public uint CollisionCount;
            public uint CollisionInfoOffset;
            public uint PolygonInfoOffset;
            public uint VertexPositionOffset;
            public uint FBHOffset;

            public Header_t(EndianBinaryReader br)
            {
                br.Skip(2);
                Version = br.ReadUInt16();
                // EO1 has version 0x1, EO2 has version 0x10, but let's simplify it to "2"
                if (Version == 0x10)
                {
                    Version = 2;
                }
                Magic = br.ReadString(4);
                CollisionCount = br.ReadUInt32();
                CollisionInfoOffset = br.ReadUInt32();
                PolygonInfoOffset = br.ReadUInt32();
                VertexPositionOffset = br.ReadUInt32();
                br.Skip(4);
                FBHOffset = br.ReadUInt32();
            }
        }

        public class Collision_t
        {
            public string Name;
            public string OutputName;
            public List<Polygon_t> Polygon;
            public Matrix4x4 WorldTransformation;

            public Collision_t(string name)
            {
                Name = name;
                Polygon = new();
                WorldTransformation = new();
            }
        }

        public class Polygon_t
        {
            public List<Vector3> VertexPosition;

            public Polygon_t()
            {
                VertexPosition = new();
            }
        }

        public class CollisionInfo_t
        {
            public int PolygonCount;
            public int PolygonOffset;
            public Vector3 Origin;
            public Vector3 OriginCopy;
            public Vector3 Scale; // Maybe? Read but not parsed for now

            public CollisionInfo_t(EndianBinaryReader br)
            {
                br.Skip(4);
                PolygonCount = br.ReadInt32();
                PolygonOffset = br.ReadInt32();
                br.Skip(4);
                Origin = new(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                br.Skip(4);
                OriginCopy = new(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                br.Skip(4);
                Scale = new(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                br.Skip(4);
            }
        }

        public class PolygonInfo_t
        {
            public uint VertexPositionCount;
            public uint VertexPositionOffset;

            public PolygonInfo_t(EndianBinaryReader br)
            {
                VertexPositionCount = br.ReadUInt32();
                VertexPositionOffset = br.ReadUInt32();
                br.Skip(0x18);
            }
        }

        public class FBH_t
        {
            public FBHHeader_t Header;
            public List<FBHTransformation_t> Transformation;

            public FBH_t(EndianBinaryReader br)
            {
                Header = new(br);
                Transformation = new();
                for (int i = 0; i < Header.TransformationCount; i++)
                {
                    Transformation.Add(new(br, Header.Version));
                }
            }
        }

        public class FBHHeader_t
        {
            public string Magic;
            public string Version;
            public ushort CollisionCount;
            public uint TransformationCount;
            public int FlagsOffset;
            public int TransformationOffset;

            public FBHHeader_t(EndianBinaryReader br)
            {
                Magic = br.ReadString(3);
                Version = br.ReadString(1);
                if (Version == "3")
                {
                    br.Skip(2);
                    CollisionCount = br.ReadUInt16();
                    TransformationCount = br.ReadUInt16();
                    br.Skip(6);
                    FlagsOffset = br.ReadInt32();
                    TransformationOffset = br.ReadInt32();
                }
                else
                {
                    TransformationCount = br.ReadUInt32();
                    br.Skip(8); // Skip offset to collision names (unused) and 4 bytes
                }
            }
        }

        public class FBHTransformation_t
        {
            public ushort Id;
            public byte CollisionCount;
            public byte Flag;
            public Vector3 Translation;
            public Quaternion Rotation;

            public FBHTransformation_t(EndianBinaryReader br, string version)
            {
                if (version == "2")
                {
                    Translation = new(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                    Id = br.ReadUInt16();
                    Flag = br.ReadByte();
                    CollisionCount = br.ReadByte();
                }
                else if (version == "3")
                {
                    Id = br.ReadUInt16();
                    CollisionCount = br.ReadByte();
                    Flag = br.ReadByte();
                    Translation = new(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                    Rotation = new(br.ReadSingle(), br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                }
            }
        }
    }
}
