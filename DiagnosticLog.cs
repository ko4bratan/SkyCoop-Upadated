using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace SkyCoop
{
    // No game API calls here: safe for socket callbacks and last-chance exception hooks.
    public static class DiagnosticLog
    {
        private static readonly object Gate = new object();
        private static readonly Dictionary<string, (int Count, DateTime Last)> Events = new();
        private static string FileName;
        private static bool Hooked;
        private static bool ReportedIoFailure;
        private static readonly string Session = Guid.NewGuid().ToString("N");
        private const long MaxBytes = 4 * 1024 * 1024;

        public static void Initialize(string directory)
        {
            lock (Gate)
            {
                try
                {
                    Directory.CreateDirectory(directory);
                    FileName = Path.Combine(directory, "session-" + Session + ".jsonl");
                }
                catch (Exception error) { ReportIoFailure(error); }
                if (!Hooked)
                {
                    Hooked = true;
                    AppDomain.CurrentDomain.UnhandledException += (_, e) =>
                        Error("crash.unhandled", e.ExceptionObject as Exception, "terminating=" + e.IsTerminating);
                    TaskScheduler.UnobservedTaskException += (_, e) => Error("crash.unobserved-task", e.Exception);
                }
            }
        }

        public static void Error(string name, Exception error, string context = "") =>
            Write(name, "error", context, error?.ToString());

        public static void Event(string name, string context = "") => Write(name, "info", context, null);

        public static void Failure(string name, string message, string trace = "") => Write(name, "error", message, trace);

        public static void Report(string name, object data)
        {
            lock (Gate)
            {
                try
                {
                    if (FileName == null || name != Path.GetFileName(name)) return;
                    File.WriteAllText(Path.Combine(Path.GetDirectoryName(FileName), name), JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));
                }
                catch (Exception error) { ReportIoFailure(error); }
            }
        }

        public static void Guard(string name, Action action, string context = "")
        {
            try { action(); }
            catch (Exception error) { Error(name, error, context); throw; }
        }

        private static void Write(string name, string level, string context, string exception)
        {
            lock (Gate)
            {
                try
                {
                    if (FileName == null) return;
                    if (Events.Count >= 256 && !Events.ContainsKey(name)) name = "diagnostic.overflow";
                    Events.TryGetValue(name, out var state);
                    DateTime now = DateTime.UtcNow;
                    int count = state.Count + 1;
                    bool emit = state.Count == 0 || (now - state.Last).TotalSeconds >= 5;
                    Events[name] = (count, emit ? now : state.Last);
                    if (!emit) return;
                    if (File.Exists(FileName) && new FileInfo(FileName).Length > MaxBytes)
                    {
                        string previous = FileName + ".previous";
                        if (File.Exists(previous)) File.Delete(previous);
                        File.Move(FileName, previous);
                    }
                    string json = JsonSerializer.Serialize(new
                    {
                        utc = now.ToString("O"), session = Session, name, level, count,
                        context = Trim(context, 2048), exception = Trim(exception, 12000)
                    });
                    File.AppendAllText(FileName, json + Environment.NewLine);
                }
                catch (Exception error) { ReportIoFailure(error); }
            }
        }

        private static string Trim(string text, int limit) => text == null || text.Length <= limit ? text : text.Substring(0, limit);

        private static void ReportIoFailure(Exception error)
        {
            if (ReportedIoFailure) return;
            ReportedIoFailure = true;
            try { Console.Error.WriteLine("[SkyCoop] Diagnostic file unavailable: " + error.GetType().Name); }
            catch { } // Diagnostics must never take down a game or hide the original error.
        }
    }
}
