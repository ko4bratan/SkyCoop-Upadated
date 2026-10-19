using System;
using System.IO;
using System.Text;

namespace SkyCoop
{
    // A bounded, separately versioned probe carried by SkyCoop's existing UDP framing.
    // No save/world/item RPC is enabled by this probe.
    public enum MinimalKind : byte { Challenge = 1, Hello = 2, Accepted = 3, State = 4 }

    public sealed class MinimalMessage
    {
        public MinimalKind Kind;
        public string Token = "";
        public string Build = "";
        public string Game = "";
        public string Scene = "";
        public int Seed;
        public int Slot;
        public long Sequence;
        public float X, Y, Z, Yaw;
    }

    public static class MinimalProtocol
    {
        public const int PacketId = 9055;
        public const int MaxBytes = 1024;

        public static byte[] Encode(MinimalMessage message)
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream, Encoding.UTF8, true);
            writer.Write(1);
            writer.Write((byte)message.Kind);
            WriteText(writer, message.Token, 64);
            WriteText(writer, message.Build, 64);
            WriteText(writer, message.Game, 64);
            WriteText(writer, message.Scene, 128);
            writer.Write(message.Seed);
            writer.Write(message.Slot);
            writer.Write(message.Sequence);
            writer.Write(message.X); writer.Write(message.Y); writer.Write(message.Z); writer.Write(message.Yaw);
            writer.Flush();
            return stream.ToArray();
        }

        public static MinimalMessage Decode(byte[] data)
        {
            if (data == null || data.Length > MaxBytes) throw new InvalidDataException("Minimal frame is oversized");
            using var stream = new MemoryStream(data, false);
            using var reader = new BinaryReader(stream, Encoding.UTF8);
            if (reader.ReadInt32() != 1) throw new InvalidDataException("Minimal protocol version mismatch");
            var message = new MinimalMessage { Kind = (MinimalKind)reader.ReadByte() };
            if (message.Kind < MinimalKind.Challenge || message.Kind > MinimalKind.State) throw new InvalidDataException("Unknown minimal message");
            message.Token = ReadText(reader, 64); message.Build = ReadText(reader, 64);
            message.Game = ReadText(reader, 64); message.Scene = ReadText(reader, 128);
            message.Seed = reader.ReadInt32(); message.Slot = reader.ReadInt32(); message.Sequence = reader.ReadInt64();
            message.X = reader.ReadSingle(); message.Y = reader.ReadSingle(); message.Z = reader.ReadSingle(); message.Yaw = reader.ReadSingle();
            if (stream.Position != stream.Length || message.Slot < 0 || message.Slot > 1 || message.Sequence < 0
                || !float.IsFinite(message.X) || !float.IsFinite(message.Y) || !float.IsFinite(message.Z) || !float.IsFinite(message.Yaw))
                throw new InvalidDataException("Invalid minimal state");
            return message;
        }

        public static bool SameIdentity(MinimalMessage local, MinimalMessage remote) =>
            local.Token == remote.Token && local.Build == remote.Build && local.Game == remote.Game && local.Seed == remote.Seed;

        private static void WriteText(BinaryWriter writer, string value, int maximum)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(value ?? "");
            if (bytes.Length > maximum) throw new InvalidDataException("Minimal text is oversized");
            writer.Write(bytes.Length); writer.Write(bytes);
        }

        private static string ReadText(BinaryReader reader, int maximum)
        {
            int length = reader.ReadInt32();
            if (length < 0 || length > maximum || length > reader.BaseStream.Length - reader.BaseStream.Position)
                throw new InvalidDataException("Invalid minimal text length");
            return new UTF8Encoding(false, true).GetString(reader.ReadBytes(length));
        }
    }
}
