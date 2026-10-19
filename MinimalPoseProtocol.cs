using System;
using System.IO;
using System.Text;

namespace SkyCoop
{
    // Optional extension. Packet9055 remains byte-for-byte version1; old state decoders are unchanged.
    public sealed class MinimalPose
    {
        public string Token = "";
        public long Sequence;
        public bool Crouch, Run;
    }
    public static class MinimalPoseProtocol
    {
        public const int PacketId = 9056;
        public static byte[] Encode(MinimalPose pose)
        {
            if (pose.Token == null || pose.Token.Length != 32 || pose.Sequence < 0) throw new InvalidDataException("Invalid pose identity");
            using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream);
            writer.Write((byte)1); writer.Write(Encoding.ASCII.GetBytes(pose.Token)); writer.Write(pose.Sequence);
            writer.Write((byte)((pose.Crouch ? 1 : 0) | (pose.Run ? 2 : 0)));
            return stream.ToArray();
        }
        public static MinimalPose Decode(byte[] data)
        {
            if (data == null || data.Length != 42 || data[0] != 1 || data[41] > 3) throw new InvalidDataException("Invalid pose extension");
            using var reader = new BinaryReader(new MemoryStream(data)); reader.ReadByte();
            var pose = new MinimalPose { Token = Encoding.ASCII.GetString(reader.ReadBytes(32)), Sequence = reader.ReadInt64() };
            byte flags = reader.ReadByte(); pose.Crouch = (flags & 1) != 0; pose.Run = (flags & 2) != 0;
            if (pose.Sequence < 0) throw new InvalidDataException("Negative pose sequence");
            return pose;
        }
    }
}
