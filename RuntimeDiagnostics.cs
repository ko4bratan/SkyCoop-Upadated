#if !DEDICATED
using System;
using System.IO;
using System.Security.Cryptography;
using UnityEngine;

namespace SkyCoop
{
    public static class RuntimeDiagnostics
    {
        public static void Start()
        {
            DiagnosticLog.Initialize(Path.Combine("UserData", "SkyCoop", "Diagnostics"));
            DiagnosticLog.Event("startup.identity", "mod=" + MyMod.BuildInfo.Version
                + ";game=" + Application.version + ";unity=" + Application.unityVersion
                + ";loader=" + typeof(MelonLoader.MelonMod).Assembly.GetName().Version
                + ";runtime=" + Environment.Version + ";os=" + Environment.OSVersion);
            foreach (string file in new[] { "Mods/SkyCoop.dll", "UserLibs/Steamworks.NET.dll", "Mods/multiplayerstuff.unity3d", "Mods/SkyCoop.modcomponent" })
            {
                try
                {
                    if (!File.Exists(file)) { DiagnosticLog.Event("startup.missing." + Path.GetFileName(file)); continue; }
                    using FileStream stream = File.OpenRead(file);
                    using SHA256 hash = SHA256.Create();
                    DiagnosticLog.Event("startup.file." + Path.GetFileName(file), Convert.ToHexString(hash.ComputeHash(stream)).ToLowerInvariant());
                }
                catch (Exception error) { DiagnosticLog.Error("startup.file-check", error, Path.GetFileName(file)); }
            }
            // A managed hook cannot catch a native access violation or process termination.

        }

        private static void OnUnityLog(string message, string trace, LogType type)
        {
            if (type == LogType.Exception || type == LogType.Assert || type == LogType.Error)
                DiagnosticLog.Failure("unity." + type, message, trace);
        }

        [HarmonyLib.HarmonyPatch(typeof(Application), "CallLogCallback")]
        private static class UnityLogPatch
        {
            private static void Postfix(string __0, string __1, LogType __2) => OnUnityLog(__0, __1, __2);
        }

        public static void Connection(string stage) => DiagnosticLog.Event("connection." + stage,
            "host=" + MyMod.iAmHost + ";steam=" + ServerTransport() + ";scene=" + MyMod.level_name
            + ";slotCapacity=" + MyMod.MaxPlayers + ";client=" + GameServer.ClientUser.myId);

        private static bool ServerTransport() => GameServer.Server.UsingSteamWorks || MyMod.ConnectedSteamWorks;
    }
}
#endif
