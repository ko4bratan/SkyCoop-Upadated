using System;
using System.IO;
namespace SkyCoop
{
    public static class SteamEnvelope
    {
        private static bool Allowed(int id) => id == 0 || id == MinimalProtocol.PacketId || id == MinimalPoseProtocol.PacketId;
        public static byte[] Encode(int id, byte[] payload)
        {
            if (payload == null || !Allowed(id) || payload.Length > MinimalProtocol.MaxBytes || (id == 0 && payload.Length != 0)) throw new InvalidDataException("Недопустимый Steam probe пакет");
            byte[] frame = new byte[4 + payload.Length];
            Array.Copy(BitConverter.GetBytes(id), frame, 4); Array.Copy(payload, 0, frame, 4, payload.Length); return frame;
        }
        public static (int Id, byte[] Payload) Decode(byte[] frame)
        {
            if (frame == null || frame.Length < 4 || frame.Length > MinimalProtocol.MaxBytes + 4) throw new InvalidDataException("Размер Steam probe пакета");
            int id = BitConverter.ToInt32(frame, 0);
            if (!Allowed(id) || (id == 0 && frame.Length != 4)) throw new InvalidDataException("Неизвестный Steam probe пакет");
            byte[] payload = new byte[frame.Length - 4]; Array.Copy(frame, 4, payload, 0, payload.Length); return (id, payload);
        }
    }
}
