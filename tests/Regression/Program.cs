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
        int failures = 0;
        foreach (var test in cases)
        {
            Reset();
            try { test.Run(); Console.WriteLine("PASS: " + test.Name); }
            catch (Exception error) { failures++; Console.WriteLine("FAIL: " + test.Name + ": " + error.Message); }
        }
        Console.WriteLine($"RESULT: {cases.Length - failures}/{cases.Length} passed");
        Directory.Delete(logDirectory, true);
        return failures == 0 ? 0 : 1;
    }
}
