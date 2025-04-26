using System.Diagnostics;
using System.Numerics;

namespace EndlessOceanFilesConverter
{
    [DebuggerDisplay("Animations: {Header.AnimationCount} | ")]
    public class MOLStream
    {
        public long AbsoluteOffset;

        public Header_t Header;
        public List<Animation_t> Animation;

        public MOLStream(EndianBinaryReader br)
        {
            AbsoluteOffset = br.BaseStream.Position;

            Header = new(br);
            Animation = new();
            for (int i = 0; i < Header.AnimationCount; i++)
            {
                if (!Header.RFHeader.Files[i].IsInFile)
                {
                    continue;
                }
                br.Seek(AbsoluteOffset + Header.RFHeader.Files[i].Offset, SeekOrigin.Begin);
                Animation.Add(new(br, Header.RFHeader.Files[i].Name));
                AddBonesToAnimation(br, Header.AnimationInfo[i], Animation.Last());
            }
        }

        // Used to add a mot which is present in the file BUT the mol says it is not
        public void AddMOT(EndianBinaryReader br, FileInfo_t rfFile)
        {
            br.Seek(rfFile.Offset, SeekOrigin.Begin);
            Animation.Add(new(br, rfFile.Name));
            for (int i = 0; i < Header.AnimationCount; i++)
            {
                if (Header.RFHeader.Files[i].Name == rfFile.Name)
                {
                    AddBonesToAnimation(br, Header.AnimationInfo[i], Animation[i]);
                    break;
                }
            }
        }

        // Used to add a mot from a "loose" file (a .mot passed through the exe)
        public void AddMOT(EndianBinaryReader br, string motName, byte[] motData)
        {
            EndianBinaryReader brMOT = new(motData);
            brMOT.SetEndianness(EndianBinaryReader.Endianness.Big);
            Animation.Add(new(brMOT, motName));
            for (int i = 0; i < Header.AnimationCount; i++)
            {
                if (Header.RFHeader.Files[i].Name == motName)
                {
                    AddBonesToAnimation(br, Header.AnimationInfo[i], Animation.Last());
                    break;
                }
            }
        }

        void AddBonesToAnimation(EndianBinaryReader br, AnimationInfo_t animationInfo, Animation_t animation)
        {
            br.Seek(AbsoluteOffset + animationInfo.BoneRemapOffset, SeekOrigin.Begin);

            for (int j = 0; j < animation.TranslationBoneCount; j++)
            {
                var boneRemapIndex = br.ReadUInt16();
                Bone_t bone = new();
                bone.Name = Header.BoneNames[boneRemapIndex];
                bone.RemapIndex = boneRemapIndex;
                bone.Translation = animation.Translation[j];
                animation.Bone.Add(bone);
            }

            for (int j = 0; j < animation.RotationBoneCount; j++)
            {
                var boneRemapIndex = br.ReadUInt16();
                Bone_t bone = new();

                if (animation.Bone.Exists(b => b.RemapIndex == boneRemapIndex))
                {
                    bone = animation.Bone.Find(b => b.RemapIndex == boneRemapIndex);
                    bone.Rotation = animation.Rotation[j];
                }
                else
                {
                    bone.Name = Header.BoneNames[boneRemapIndex];
                    bone.RemapIndex = boneRemapIndex;
                    bone.Rotation = animation.Rotation[j];
                    animation.Bone.Add(bone);
                }
            }

            for (int j = 0; j < animation.ScaleBoneCount; j++)
            {
                var boneRemapIndex = br.ReadUInt16();
                Bone_t bone = new();
                if (animation.Bone.Exists(b => b.RemapIndex == boneRemapIndex))
                {
                    bone = animation.Bone.Find(b => b.RemapIndex == boneRemapIndex);
                    bone.Scale = animation.Scale[j];
                }
                else
                {
                    bone.Name = Header.BoneNames[boneRemapIndex];
                    bone.RemapIndex = boneRemapIndex;
                    bone.Scale = animation.Scale[j];
                    animation.Bone.Add(bone);
                }
            }
        }
    }

    public class Header_t
    {
        public RFHeader_t RFHeader;
        public ushort AnimationCount;
        public ushort BoneCount;
        public uint BoneNameOffset;
        public uint AnimationInfoOffset;
        public uint AnimationFlagsOffset;
        public List<string> BoneNames;
        public List<AnimationInfo_t> AnimationInfo;
        public byte[] BoneFlags;

        public Header_t(EndianBinaryReader br)
        {
            br.SetEndianness(EndianBinaryReader.Endianness.Little);
            var AbsoluteOffset = br.BaseStream.Position;
            RFHeader = new(br);
            br.SetEndianness(EndianBinaryReader.Endianness.Big);
            AnimationCount = br.ReadUInt16();
            BoneCount = br.ReadUInt16();
            BoneNameOffset = br.ReadUInt32();
            AnimationInfoOffset = br.ReadUInt32();
            AnimationFlagsOffset = br.ReadUInt32();

            br.Seek(AbsoluteOffset + BoneNameOffset, SeekOrigin.Begin);
            BoneNames = new();
            for (int i = 0; i < BoneCount; i++)
            {
                BoneNames.Add(br.ReadString(0x10));
            }

            br.Seek(AbsoluteOffset + AnimationInfoOffset, SeekOrigin.Begin);
            AnimationInfo = new();
            for (int i = 0; i < AnimationCount; i++)
            {
                AnimationInfo.Add(new(br));
            }

            br.Seek(AbsoluteOffset + AnimationFlagsOffset, SeekOrigin.Begin);
            BoneFlags = br.ReadBytes(BoneCount);
        }
    }

    public class AnimationInfo_t
    {
        public uint BoneRemapOffset;
        public uint CorrectionIndexOffset;
        public List<ushort> BoneRemap = new();
        public AnimationInfo_t(EndianBinaryReader br)
        {
            br.Skip(4);
            BoneRemapOffset = br.ReadUInt32();
            CorrectionIndexOffset = br.ReadUInt32();
        }
    }

    public class Bone_t
    {
        public string Name;
        public ushort RemapIndex;
        public List<Vector3> Translation;
        public List<Quaternion> Rotation;
        public List<Vector3> Scale;

        public override string ToString()
        {
            string str = $"{(Translation == null ? " " : "T")}";
            str += $"{(Rotation == null ? " " : "R")}";
            str += $"{(Scale == null ? " " : "S")}";
            str += $" | {Name}";
            return str;
        }
    }

    [DebuggerDisplay("{Name} | Frames: {FrameCount} | Bones: {BoneCount}")]
    public class Animation_t
    {
        public long AbsoluteOffset;

        public string Name;
        public List<float> TranslationKeyFrames;
        public List<float> RotationKeyframes;
        public List<float> ScaleKeyframes;
        public List<Bone_t> Bone;

        public string Magic;
        public uint Size;
        public float Framerate;
        public ushort FrameCount;
        public ushort TRSPoseIndexCount;
        public ushort TRSPoseValuesCount;
        public ushort BoneCount;
        public uint TRSPoseIndexOffset;
        public uint TRSPoseValuesOffset;

        public ushort TranslationBoneCount;
        public ushort TranslationKeyframesCount;
        public uint TranslationOffset;
        public uint TranslationKeyframesOffset;
        public List<List<Vector3>> Translation;

        public ushort RotationBoneCount;
        public ushort RotationKeyframesCount;
        public uint RotationOffset;
        public uint RotationKeyframesOffset;
        public List<List<Quaternion>> Rotation;

        public ushort ScaleBoneCount;
        public ushort ScaleKeyframesCount;
        public uint ScaleOffset;
        public uint ScaleKeyframesOffset;
        public List<List<Vector3>> Scale;

        public Animation_t(EndianBinaryReader br, string name)
        {
            Name = name;
            AbsoluteOffset = br.BaseStream.Position;
            Magic = br.ReadString(4);
            Size = br.ReadUInt32();
            Framerate = br.ReadSingle();
            FrameCount = br.ReadUInt16();
            TRSPoseIndexCount = br.ReadUInt16();
            TRSPoseValuesCount = br.ReadUInt16();
            BoneCount = br.ReadUInt16();
            TRSPoseIndexOffset = br.ReadUInt32();
            TRSPoseValuesOffset = br.ReadUInt32();

            TranslationBoneCount = br.ReadUInt16();
            TranslationKeyframesCount = br.ReadUInt16();
            TranslationOffset = br.ReadUInt32();
            TranslationKeyframesOffset = br.ReadUInt32();

            RotationBoneCount = br.ReadUInt16();
            RotationKeyframesCount = br.ReadUInt16();
            RotationOffset = br.ReadUInt32();
            RotationKeyframesOffset = br.ReadUInt32();

            ScaleBoneCount = br.ReadUInt16();
            ScaleKeyframesCount = br.ReadUInt16();
            ScaleOffset = br.ReadUInt32();
            ScaleKeyframesOffset = br.ReadUInt32();

            Bone = new();

            if (TranslationBoneCount > 0)
            {
                Translation = new();
                br.Seek(AbsoluteOffset + TranslationOffset, SeekOrigin.Begin);
                for (int i = 0; i < TranslationBoneCount; i++)
                {
                    Translation.Add(new());
                    for (int j = 0; j < TranslationKeyframesCount; j++)
                    {
                        Translation[i].Add(new(br.ReadSingle(), br.ReadSingle(), br.ReadSingle()));
                        br.Skip(4);
                    }
                }

                TranslationKeyFrames = new();
                br.Seek(AbsoluteOffset + TranslationKeyframesOffset, SeekOrigin.Begin);
                for (int i = 0; i < TranslationKeyframesCount; i++)
                {
                    TranslationKeyFrames.Add(br.ReadSingle());
                }
            }

            if (RotationBoneCount > 0)
            {
                Rotation = new();
                br.Seek(AbsoluteOffset + RotationOffset, SeekOrigin.Begin);
                for (int i = 0; i < RotationBoneCount; i++)
                {
                    Rotation.Add(new());
                    for (int j = 0; j < RotationKeyframesCount; j++)
                    {
                        Rotation[i].Add(new(br.ReadSingle(), br.ReadSingle(), br.ReadSingle(), br.ReadSingle()));
                    }
                }

                RotationKeyframes = new();
                br.Seek(AbsoluteOffset + RotationKeyframesOffset, SeekOrigin.Begin);
                for (int i = 0; i < RotationKeyframesCount; i++)
                {
                    RotationKeyframes.Add(br.ReadSingle());
                }
            }

            if (ScaleBoneCount > 0)
            {
                Scale = new();
                br.Seek(AbsoluteOffset + ScaleOffset, SeekOrigin.Begin);
                for (int i = 0; i < ScaleBoneCount; i++)
                {
                    Scale.Add(new());
                    for (int j = 0; j < ScaleKeyframesCount; j++)
                    {
                        Scale[i].Add(new(br.ReadSingle(), br.ReadSingle(), br.ReadSingle()));
                        br.Skip(4);
                    }
                }

                ScaleKeyframes = new();
                br.Seek(AbsoluteOffset + ScaleKeyframesOffset, SeekOrigin.Begin);
                for (int i = 0; i < ScaleKeyframesCount; i++)
                {
                    ScaleKeyframes.Add(br.ReadSingle());
                }
            }
        }
    }
}
