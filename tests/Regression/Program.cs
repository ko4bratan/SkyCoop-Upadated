using GameServer;
using SkyCoop;
using System.Text.Json;

public class Il2CppObjectBase { }
namespace UnityEngine { public class Object : Il2CppObjectBase { } }
public class Asset : UnityEngine.Object { }

public class FakeBundle
{
    public readonly Dictionary<string, Il2CppObjectBase> Assets = new();
    public int Lookups;
    public T LoadAsset<T>(string name) where T : UnityEngine.Object
    {
        Lookups++;
        return Assets.TryGetValue(name, out var value) ? value as T : null;
    }
}

public static class MelonLogger
{
    public static readonly List<string> Warnings = new();
    public static void Warning(string message) => Warnings.Add(message);
}

public sealed class SkippedTest : Exception { public SkippedTest(string message) : base(message) {} }

public static class Program
{
    static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    static void Reset()
    {
        MyMod.LoadedBundle = new FakeBundle();
        MyMod.ReportedMissingAssets.Clear();
        MelonLogger.Warnings.Clear();
        ThreadManager.executeOnMainThread.Clear();
        ThreadManager.executeCopiedOnMainThread.Clear();
        ThreadManager.actionToExecuteOnMainThread = false;
    }

    public static int Main()
    {
        string logDirectory = Path.Combine(Path.GetTempPath(), "skycoop-diagnostics-test-" + Guid.NewGuid());
        DiagnosticLog.Initialize(logDirectory);
        var cases = new (string Name, Action Run)[] {
            ("minimal protocol roundtrip and identity reject mismatched builds/worlds", () => {
                var sent = new MinimalMessage { Kind = MinimalKind.State, Token = "nonce", Build = "hash", Game = "2.55", Scene = "Lake", Seed = 19, Slot = 1, Sequence = 42, X = 1.5f, Y = -2, Z = 3, Yaw = 90 };
                var received = MinimalProtocol.Decode(MinimalProtocol.Encode(sent));
                Check(received.Sequence == 42 && received.X == 1.5f && received.Scene == "Lake", "state changed");
                Check(MinimalProtocol.SameIdentity(sent, received), "identity changed");
                received.Build = "different"; Check(!MinimalProtocol.SameIdentity(sent, received), "different DLL accepted");
                received.Build = sent.Build; received.Seed++; Check(!MinimalProtocol.SameIdentity(sent, received), "different world accepted");
                received.Seed = sent.Seed; received.Token = "other"; Check(!MinimalProtocol.SameIdentity(sent, received), "different session accepted");
            }),
            ("minimal protocol rejects corrupt, truncated and non-finite states", () => {
                var valid = MinimalProtocol.Encode(new MinimalMessage { Kind = MinimalKind.State });
                var golden = new byte[53]; golden[0] = 1; golden[4] = 4;
                Check(valid.SequenceEqual(golden), "9055/version1 wire layout changed");
                var invalid = new List<byte[]> { Array.Empty<byte>(), valid[..^1], valid.Concat(new byte[] {0}).ToArray(), new byte[1025] };
                var wrongVersion = (byte[])valid.Clone(); wrongVersion[0] = 2; invalid.Add(wrongVersion);
                invalid.Add(MinimalProtocol.Encode(new MinimalMessage { Kind = MinimalKind.State, X = float.NaN }));
                invalid.Add(MinimalProtocol.Encode(new MinimalMessage { Kind = MinimalKind.State, Slot = 2 }));
                foreach (var bytes in invalid) { bool rejected = false; try { MinimalProtocol.Decode(bytes); } catch (Exception error) when (error is IOException || error is InvalidDataException) { rejected = true; } Check(rejected, "bad state accepted"); }
                bool oversized = false; try { MinimalProtocol.Encode(new MinimalMessage { Token = new string('x', 65) }); } catch (InvalidDataException) { oversized = true; } Check(oversized, "oversized text encoded");
            }),
            ("minimal protocol crosses real UDP loopback without Unity", () => {
                System.Net.Sockets.UdpClient socket;
                try { socket = new System.Net.Sockets.UdpClient(new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, 0)); }
                catch (System.Net.Sockets.SocketException error) when (error.SocketErrorCode == System.Net.Sockets.SocketError.AccessDenied && Environment.GetEnvironmentVariable("SKYCOOP_REQUIRE_UDP_TEST") != "1")
                { throw new SkippedTest("actual UDP loopback forbidden by sandbox; CI requires this test"); }
                using var host = socket;
                using var guest = new System.Net.Sockets.UdpClient(new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, 0));
                host.Client.ReceiveTimeout = 2000; guest.Client.ReceiveTimeout = 2000;
                var data = MinimalProtocol.Encode(new MinimalMessage { Kind = MinimalKind.Hello, Token = "test" });
                guest.Send(data, data.Length, (System.Net.IPEndPoint)host.Client.LocalEndPoint);
                var endpoint = new System.Net.IPEndPoint(System.Net.IPAddress.Any, 0);
                var request = MinimalProtocol.Decode(host.Receive(ref endpoint)); Check(request.Kind == MinimalKind.Hello, "hello lost");
                request.Kind = MinimalKind.Accepted; var answer = MinimalProtocol.Encode(request); host.Send(answer, answer.Length, endpoint);
                Check(MinimalProtocol.Decode(guest.Receive(ref endpoint)).Kind == MinimalKind.Accepted, "accept lost");
            }),
            ("optional pose preserves crouch/run and rejects malformed flags", () => {
                var pose = new MinimalPose { Token = new string('a', 32), Sequence = 73, Crouch = true, Run = false };
                byte[] bytes = MinimalPoseProtocol.Encode(pose);
                var decoded = MinimalPoseProtocol.Decode(bytes);
                Check(decoded.Crouch && !decoded.Run && decoded.Sequence == 73 && decoded.Token == pose.Token, "pose changed");
                bytes[41] = 4; bool rejected = false;
                try { MinimalPoseProtocol.Decode(bytes); } catch (InvalidDataException) { rejected = true; }
                Check(rejected, "unknown flag accepted");
                rejected = false; try { MinimalPoseProtocol.Decode(bytes[..^1]); } catch (InvalidDataException) { rejected = true; }
                Check(rejected, "truncated pose accepted");
            }),
            ("remote snapshots interpolate position and shortest yaw, stop stale animation", () => {
                var motion = new RemoteMotion();
                Check(motion.Push(new MinimalMessage { Sequence = 1, Yaw = 350 }, 10), "first state rejected");
                Check(motion.Push(new MinimalMessage { Sequence = 2, X = 2, Yaw = 10 }, 10.1), "second state rejected");
                var halfway = motion.At(10.15);
                Check(Math.Abs(halfway.X - 1) < 0.001 && Math.Abs(halfway.Yaw - 360) < 0.001, "wrong interpolation or long yaw turn");
                Check(!motion.Push(new MinimalMessage { Sequence = 1, X = 200 }, 10.2), "reordered state accepted");
                Check(motion.At(11).X == 2 && motion.At(11).Speed == 0, "stale extrapolation/animation");
                motion.Push(new MinimalMessage { Sequence = 3, X = 100 }, 12);
                Check(motion.At(12).X == 100 && motion.At(12).Speed == 0, "teleport did not reset history");
                motion.Reset(); Check(motion.Push(new MinimalMessage { Sequence = 1 }, 1), "scene reset kept sequence");
            }),
            ("Steam envelope preserves base protocol and refuses legacy gameplay RPC", () => {
                var state = new MinimalMessage { Kind = MinimalKind.State, Sequence = 12, X = 3 };
                var bytes = MinimalProtocol.Encode(state);
                var decoded = SteamEnvelope.Decode(SteamEnvelope.Encode(MinimalProtocol.PacketId, bytes));
                Check(decoded.Id == MinimalProtocol.PacketId && decoded.Payload.SequenceEqual(bytes), "Steam changed base wire payload");
                Check(MinimalProtocol.Decode(decoded.Payload).Sequence == 12, "state lost");
                foreach (byte[] invalid in new[] { new byte[3], new byte[1029], BitConverter.GetBytes(77), new byte[5] }) {
                    bool rejected = false; try { SteamEnvelope.Decode(invalid); } catch (InvalidDataException) { rejected = true; }
                    Check(rejected, "bad/legacy RPC accepted");
                }
                Check(SteamEnvelope.Decode(SteamEnvelope.Encode(0, Array.Empty<byte>())).Id == 0, "handshake lost");
            }),
            ("Steam ID validity rejects default and accepts well-formed individual ID", () => {
                Check(!new Steamworks.CSteamID(0).IsValid(), "default Steam ID accepted");
                Check(new Steamworks.CSteamID(76561197960265729UL).IsValid(), "well-formed Steam ID rejected");
            }),
            ("SHA256 text remains wire-compatible", () => {
                Check(TextChecksumUnderTest.SHA256CheckSum("abc") == "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", "text checksum changed");
                Check(TextChecksumUnderTest.SHA256CheckSum(null) == "null", "null sentinel changed");
            }),
            ("SHA256 file remains compatible and disposes stream", () => {
                string path = Path.Combine(logDirectory, "checksum.txt");
                File.WriteAllText(path, "abc");
                Check(FileChecksumUnderTest.SHA256CheckSum(path) == "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", "file checksum changed");
                File.Delete(path);
                Check(FileChecksumUnderTest.SHA256CheckSum(path) == "null", "missing file sentinel changed");
            }),
            ("diagnostics retain packet failure and rethrow original", () => {
                var failure = new InvalidOperationException("injected packet handler failure");
                try { DiagnosticLog.Guard("test.handler", () => throw failure, "packet=42;bytes=12"); }
                catch (Exception caught) { Check(ReferenceEquals(caught, failure), "original exception was replaced"); }
                var line = File.ReadAllLines(Directory.GetFiles(logDirectory, "*.jsonl").Single()).Select(l => JsonDocument.Parse(l)).Single(d => d.RootElement.GetProperty("name").GetString() == "test.handler");
                Check(line.RootElement.GetProperty("level").GetString() == "error", "lost severity");
                Check(line.RootElement.GetProperty("context").GetString() == "packet=42;bytes=12", "lost packet context");
                Check(line.RootElement.GetProperty("exception").GetString().Contains("injected packet handler failure"), "lost exception");
            }),
            ("diagnostics concurrent writes remain valid JSON and bounded repeats", () => {
                Parallel.For(0, 1000, _ => DiagnosticLog.Event("test.repeat", "no payload"));
                var entries = File.ReadAllLines(Directory.GetFiles(logDirectory, "*.jsonl").Single()).Select(l => JsonDocument.Parse(l)).ToArray();
                Check(entries.Count(d => d.RootElement.GetProperty("name").GetString() == "test.repeat") == 1, "repeat flood not limited");
                DiagnosticLog.Report("test-report.json", new { slots = new[] { 1, 2 }, outcome = "pending" });
                using var report = JsonDocument.Parse(File.ReadAllText(Path.Combine(logDirectory, "test-report.json")));
                Check(report.RootElement.GetProperty("slots").GetArrayLength() == 2, "report truncated");
            }),
            ("absent bundle returns null without warnings", () => {
                MyMod.LoadedBundle = null;
                Check(MyMod.BundleAsset<Asset>("player") == null, "expected null");
                Check(MelonLogger.Warnings.Count == 0, "unexpected warning");
            }),
            ("valid bundle resolves asset exactly once", () => {
                var asset = new Asset();
                MyMod.LoadedBundle.Assets["player"] = asset;
                Check(ReferenceEquals(asset, MyMod.BundleAsset<Asset>("player")), "wrong asset");
                Check(MyMod.LoadedBundle.Lookups == 1, "lookup count");
                Check(MelonLogger.Warnings.Count == 0, "unexpected warning");
            }),
            ("missing asset logs once per name", () => {
                Check(MyMod.BundleAsset<Asset>("missing") == null, "expected null");
                Check(MyMod.BundleAsset<Asset>("missing") == null, "expected null again");
                Check(MyMod.BundleAsset<Asset>("other") == null, "expected other null");
                Check(MelonLogger.Warnings.Count == 2, "missing warnings must be deduplicated by name");
            }),
            ("main thread queue preserves order", () => {
                var result = new List<int>();
                ThreadManager.ExecuteOnMainThread(() => result.Add(1));
                ThreadManager.ExecuteOnMainThread(() => result.Add(2));
                Check(result.Count == 0, "executed before main thread tick");
                ThreadManager.UpdateMain();
                ThreadManager.UpdateMain();
                Check(result.SequenceEqual(new[] { 1, 2 }), "wrong order or duplicate execution");
            }),
            ("nested queue action waits for next tick", () => {
                int result = 0;
                ThreadManager.ExecuteOnMainThread(() => {
                    result++;
                    ThreadManager.ExecuteOnMainThread(() => result++);
                });
                ThreadManager.UpdateMain();
                Check(result == 1, "nested action ran in same tick");
                ThreadManager.UpdateMain();
                Check(result == 2, "nested action was lost");
            }),
            ("concurrent producers retain all queued work", () => {
                int result = 0;
                Parallel.For(0, 1000, _ => ThreadManager.ExecuteOnMainThread(() => result++));
                ThreadManager.UpdateMain();
                Check(result == 1000, "queued actions were lost");
            })
        };
        int failures = 0, skipped = 0;
        foreach (var test in cases)
        {
            Reset();
            try { test.Run(); Console.WriteLine("PASS: " + test.Name); }
            catch (SkippedTest error) { skipped++; Console.WriteLine("SKIP: " + test.Name + ": " + error.Message); }
            catch (Exception error) { failures++; Console.WriteLine("FAIL: " + test.Name + ": " + error.Message); }
        }
        Console.WriteLine($"RESULT: {cases.Length - failures - skipped}/{cases.Length} passed; {skipped} skipped");
        Directory.Delete(logDirectory, true);
        return failures == 0 ? 0 : 1;
    }
}
