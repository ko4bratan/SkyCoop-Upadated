using System;
using System.Collections.Generic;

namespace SkyCoop
{
    // Arrival-time snapshot interpolation, bounded at latest state (no extrapolation).
    // Uses a local monotonic clock; sender clocks need not agree.
    public sealed class RemoteMotion
    {
        public readonly struct Sample
        {
            public readonly float X, Y, Z, Yaw, Speed, VX, VZ;
            public Sample(float x, float y, float z, float yaw, float speed = 0, float vx = 0, float vz = 0)
            { X = x; Y = y; Z = z; Yaw = yaw; Speed = speed; VX = vx; VZ = vz; }
        }
        private readonly List<(double Time, Sample Value)> snapshots = new();
        private long sequence = -1;
        public void Reset() { snapshots.Clear(); sequence = -1; }
        public bool Push(MinimalMessage message, double time)
        {
            if (!double.IsFinite(time) || message.Sequence <= sequence) return false;
            if (snapshots.Count > 0 && time <= snapshots[^1].Time) return false;
            var current = new Sample(message.X, message.Y, message.Z, message.Yaw);
            if (snapshots.Count > 0)
            {
                var last = snapshots[^1];
                float dx = current.X - last.Value.X, dz = current.Z - last.Value.Z;
                float distance = (float)Math.Sqrt(dx * dx + dz * dz);
                if (distance > 25 || Math.Abs(current.Y - last.Value.Y) > 10) snapshots.Clear();
                else
                {
                    float dt = (float)Math.Max(0.02, time - last.Time);
                    current = new Sample(current.X, current.Y, current.Z, current.Yaw, Math.Min(distance / dt, 12), dx / dt, dz / dt);
                }
            }
            sequence = message.Sequence; snapshots.Add((time, current));
            if (snapshots.Count > 16) snapshots.RemoveAt(0);
            return true;
        }
        public Sample At(double now)
        {
            if (snapshots.Count == 0) return default;
            double target = now - 0.1;
            if (target <= snapshots[0].Time) return snapshots[0].Value;
            for (int i = 1; i < snapshots.Count; i++)
            {
                var a = snapshots[i - 1]; var b = snapshots[i];
                if (target > b.Time) continue;
                float t = (float)((target - a.Time) / (b.Time - a.Time));
                float yaw = ((b.Value.Yaw - a.Value.Yaw) % 360 + 540) % 360 - 180;
                return new Sample(Lerp(a.Value.X, b.Value.X, t), Lerp(a.Value.Y, b.Value.Y, t), Lerp(a.Value.Z, b.Value.Z, t),
                    a.Value.Yaw + yaw * t, b.Value.Speed, b.Value.VX, b.Value.VZ);
            }
            // No new position: stop locomotion after 300ms instead of walking into thin air.
            var last = snapshots[^1];
            return now - last.Time > 0.3 ? new Sample(last.Value.X, last.Value.Y, last.Value.Z, last.Value.Yaw) : last.Value;
        }
        private static float Lerp(float a, float b, float t) => a + (b - a) * t;
    }
}
