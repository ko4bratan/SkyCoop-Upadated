#if !DEDICATED
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using GameServer;
using Il2Cpp;
using MelonLoader;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SkyCoop
{
    // Opt-in transport/transform proof in two real Survival clients. It deliberately does
    // not run the legacy world/save/gear patches or pretend that a primitive is a rig.
    public static class MinimalSurvival
    {
        public static bool Enabled { get; private set; }
        private static bool Host, Started, Failed, PeerReady;
        private static string Address = "127.0.0.1", Token = "", Build = "", Game = "", Scene = "";
        private static int Port = 26950, Seed, LocalSlot;
        private static long Sequence, LastSequence = -1, ReceivedStates;
        private static float NextSend, NextReport, LastReceived, StartedAt;
        private static readonly RemoteCharacter Character = new();
        private static readonly RemoteMotion Motion = new();
        private static long LastPoseSequence = -1;
        private static bool RemoteCrouch, RemoteRun;
        private static MinimalMessage Remote;

        public static void Configure(string[] args)
        {
            Enabled = args.Contains("-skycoop-minimal");
            if (!Enabled) return;
            Character.MarkerOnly = args.Contains("-skycoop-marker-only");
            try
            {
                Host = args.Contains("-skycoop-test-host");
                string join = args.FirstOrDefault(a => a.StartsWith("-skycoop-test-join="));
                if (Host == (join != null)) throw new ArgumentException("Specify exactly one test role: host or join=IPv4");
                if (join != null)
                {
                    Address = join.Substring("-skycoop-test-join=".Length);
                    if (!IPAddress.TryParse(Address, out var ip) || ip.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
                        throw new ArgumentException("Minimal test requires a literal IPv4 address");
                }
                string port = args.FirstOrDefault(a => a.StartsWith("-skycoop-test-port="));
                if (port != null) Port = int.Parse(port.Substring("-skycoop-test-port=".Length));
                if (Port < 1024 || Port > 65535 || Port == 26951) throw new ArgumentException("Invalid test port (26951 reserved for guest socket)");
                LocalSlot = Host ? 0 : 1;
            }
            catch (Exception error) { Failed = true; DiagnosticLog.Error("minimal.options", error); }
            Application.runInBackground = true;
            MelonLogger.Warning("[SkyCoop] MINIMAL TEST: vanilla Survival only, transform probe, no shared world/items/saves/PVP/voice. Open a disposable test save on each seat.");
        }

        public static void Update()
        {
            if (Failed) return;
            try
            {
                ThreadManager.UpdateMain();
                Scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
                bool ready = Scene != "MainMenu" && Scene != "Boot" && Scene != "Empty"
                    && SaveGameSystem.m_CurrentGameMode == SaveSlotType.SANDBOX
                    && GameManager.GetPlayerTransform() != null
                    && (GameCompat.Panel<Panel_Loading>() == null || !GameCompat.Panel<Panel_Loading>().IsEnabled());
                if (!ready) { Character.Show(false); return; }
                if (!Started) Start();
                if (Time.realtimeSinceStartup >= NextSend)
                {
                    NextSend = Time.realtimeSinceStartup + 0.1f;
                    if (PeerReady)
                    {
                        var state = Message(MinimalKind.State); Send(state);
                        var manager = GameManager.GetPlayerManagerComponent();
                        SendPose(new MinimalPose { Token = Token, Sequence = state.Sequence,
                            Crouch = manager != null && manager.PlayerIsCrouched(), Run = manager != null && manager.PlayerIsSprinting() });
                    }
                    else if (!Host && Token != "") Send(Message(MinimalKind.Hello));
                    else if (!Host)
                    {
                        NextSend = Time.realtimeSinceStartup + 1;
                        using var retry = new Packet(); retry.Write(0); ClientUser.udp.SendData(retry);
                    }
                }
                if (PeerReady && Time.realtimeSinceStartup - LastReceived > 10)
                {
                    DiagnosticLog.Failure("minimal.peer-timeout", "No validated peer state for 10 seconds; test is not passing");
                    Character.Show(false);
                }
                else if (Remote != null && Remote.Scene == Scene)
                {
                    var sample = Motion.At(Time.realtimeSinceStartup);
                    Character.Render(sample, RemoteCrouch, RemoteRun || (LastPoseSequence < 0 && sample.Speed > 3.5f));
                }
                else Character.Show(false);
                if (Time.realtimeSinceStartup >= NextReport)
                {
                    NextReport = Time.realtimeSinceStartup + 5;
                    DiagnosticLog.Report("minimal-state.json", new { mode = "transform-probe-only", role = Host ? "host" : "guest",
                        peerReady = PeerReady, validatedPeerStates = ReceivedStates, localScene = Scene,
                        remoteScene = Remote?.Scene, secondsSincePeerState = PeerReady ? Time.realtimeSinceStartup - LastReceived : -1,
                        character = Character.Status, crouch = RemoteCrouch, run = RemoteRun, unity = Application.unityVersion,
                        sharedSurvivalTested = false, realTwoClientTestComplete = false });
                    if (!PeerReady && Time.realtimeSinceStartup - StartedAt > 20)
                        DiagnosticLog.Failure("minimal.handshake-timeout", "No accepted peer after 20 seconds; check role, seed, DLL hash, scene and UDP ports");
                }
            }
            catch (Exception error) { Failed = true; DiagnosticLog.Error("minimal.initialization-or-update", error); Stop(); }
        }

        private static void Start()
        {
            using var stream = File.OpenRead("Mods/SkyCoop.dll");
            using var hash = SHA256.Create();
            Build = Convert.ToHexString(hash.ComputeHash(stream)).ToLowerInvariant();
            Game = Application.version;
            Seed = GameManager.m_SceneTransitionData.m_GameRandomSeed;
            MyMod.MaxPlayers = 2;
            EnsureSlots();
            StartedAt = Time.realtimeSinceStartup;
            if (Host)
            {
                Server.clients.Clear(); Server.UsingSteamWorks = false;
                Server.Start(1, Port); // One remote slot plus local host slot0.
                MyMod.MaxPlayers = 2;
                Server.packetHandlers = new Dictionary<int, Server.PacketHandler> { { MinimalProtocol.PacketId, ReceiveFromClient }, { MinimalPoseProtocol.PacketId, ReceivePoseFromClient } };
                MyMod.iAmHost = true;
            }
            else
            {
                ClientUser.ip = Address; ClientUser.ConnectPort = Port; ClientUser.LocalPort = 26951;
                ClientUser.ConnectToServer();
                ClientUser.packetHandlers = new Dictionary<int, ClientUser.PacketHandler> { { MinimalProtocol.PacketId, ReceiveFromServer }, { MinimalPoseProtocol.PacketId, ReceivePoseFromServer } };
            }
            Started = true;
            DiagnosticLog.Event("minimal.started", "role=" + (Host ? "host" : "guest") + ";game=" + Game + ";scene=" + Scene + ";worldSeed=" + Seed);
        }

        public static void EnsureSlots()
        {
            while (MyMod.playersData.Count < 2) MyMod.playersData.Add(new DataStr.MultiPlayerClientData());
            while (MyMod.players.Count < 2) MyMod.players.Add(null);
            // Markers are kept out of players[]: existing clothing/voice code expects a humanoid rig.
        }

        public static void SendChallenge(int client)
        {
            ThreadManager.ExecuteOnMainThread(() =>
            {
                Token = Guid.NewGuid().ToString("N"); PeerReady = false; ResetRemote();
                using var packet = new Packet(MinimalProtocol.PacketId);
                packet.Write(MinimalProtocol.Encode(Message(MinimalKind.Challenge)));
                ServerSend.SendUDPData(client, packet, true);
            });
        }

        private static MinimalMessage Message(MinimalKind kind)
        {
            Transform player = GameManager.GetPlayerTransform();
            return new MinimalMessage { Kind = kind, Token = Token, Build = Build, Game = Game, Seed = Seed,
                Scene = Scene, Slot = LocalSlot, Sequence = ++Sequence,
                X = player != null ? player.position.x : 0, Y = player != null ? player.position.y : 0,
                Z = player != null ? player.position.z : 0, Yaw = player != null ? player.eulerAngles.y : 0 };
        }

        private static void Send(MinimalMessage message)
        {
            using var packet = new Packet(MinimalProtocol.PacketId);
            packet.Write(MinimalProtocol.Encode(message));
            if (Host) ServerSend.SendUDPData(1, packet, true); else ClientUser.udp.SendData(packet);
        }

        private static void ReceiveFromClient(int slot, Packet packet) => Receive(packet, true);
        private static void ReceiveFromServer(Packet packet) => Receive(packet, false);

        private static void Receive(Packet packet, bool onHost)
        {
            try
            {
                var message = MinimalProtocol.Decode(packet.ReadBytes(packet.UnreadLength()));
                if (message.Kind == MinimalKind.Challenge && !onHost)
                {
                    if (message.Slot != 0) throw new InvalidDataException("Challenge is not from host");
                    if (Token != message.Token) { PeerReady = false; ResetRemote(); }
                    Token = message.Token;
                    if (!MinimalProtocol.SameIdentity(Message(MinimalKind.Hello), message)) throw new InvalidDataException("DLL/game/world seed mismatch; use two disposable copies of one test Survival world");
                    ClientUser.myId = 1; Send(Message(MinimalKind.Hello)); return;
                }
                if (message.Slot != (Host ? 1 : 0) || !MinimalProtocol.SameIdentity(Message(MinimalKind.Hello), message))
                    throw new InvalidDataException("Minimal session identity mismatch");
                if (message.Kind == MinimalKind.Hello && onHost)
                {
                    PeerReady = true; Server.clients[1].Ready = true;
                    LastReceived = Time.realtimeSinceStartup; Send(Message(MinimalKind.Accepted));
                    DiagnosticLog.Event("minimal.peer-accepted", "actualSeats=2;scope=transform-only"); return;
                }
                if (message.Kind == MinimalKind.Accepted && !onHost)
                {
                    PeerReady = true; LastReceived = Time.realtimeSinceStartup;
                    DiagnosticLog.Event("minimal.peer-accepted", "actualSeats=2;scope=transform-only"); return;
                }
                if (message.Kind != MinimalKind.State || !PeerReady || message.Sequence <= LastSequence) return;
                LastSequence = message.Sequence; LastReceived = Time.realtimeSinceStartup; ReceivedStates++;
                if (Remote == null || Remote.Scene != message.Scene) Motion.Reset();
                Motion.Push(message, Time.realtimeSinceStartup);
                Remote = message;
                DiagnosticLog.Event("minimal.peer-state", "sequence=" + message.Sequence + ";scene=" + message.Scene);
            }
            catch (Exception error) { DiagnosticLog.Error("minimal.protocol", error); }
        }

        private static void ResetRemote()
        {
            Remote = null; LastSequence = -1; LastPoseSequence = -1;
            RemoteCrouch = false; RemoteRun = false; Motion.Reset(); Character.Show(false);
        }

        private static void SendPose(MinimalPose pose)
        {
            using var packet = new Packet(MinimalPoseProtocol.PacketId);
            packet.Write(MinimalPoseProtocol.Encode(pose));
            if (Host) ServerSend.SendUDPData(1, packet, true); else ClientUser.udp.SendData(packet);
        }
        private static void ReceivePoseFromClient(int slot, Packet packet) => ReceivePose(packet);
        private static void ReceivePoseFromServer(Packet packet) => ReceivePose(packet);
        private static void ReceivePose(Packet packet)
        {
            try
            {
                var pose = MinimalPoseProtocol.Decode(packet.ReadBytes(packet.UnreadLength()));
                if (!PeerReady || pose.Token != Token || pose.Sequence <= LastPoseSequence) return;
                LastPoseSequence = pose.Sequence; RemoteCrouch = pose.Crouch; RemoteRun = pose.Run;
            }
            catch (Exception error) { DiagnosticLog.Error("minimal.pose", error); }
        }

        public static void Stop()
        {
            try { if (Host) Server.udpListener?.Close(); else ClientUser.udp?.socket?.Close(); }
            catch (Exception error) { DiagnosticLog.Error("minimal.stop", error); }
            try { Character.Dispose(); } catch (Exception error) { DiagnosticLog.Error("character.dispose", error); }
            ResetRemote(); PeerReady = false; Failed = true;
            DiagnosticLog.Event("minimal.stopped", "no save/world mutation performed by probe");
        }

        public static void DrawStatus()
        {
            GUI.Label(new Rect(15, 15, 950, 80), "SkyCoop MINIMAL: " + (Host ? "host" : "guest")
                + " | " + (Failed ? "STOPPED — inspect logs" : !Started ? "load disposable Survival" : PeerReady ? "peer accepted, received states=" + ReceivedStates : "waiting for peer")
                + " | " + Character.Status + "\nTransform probe only. NO shared items/world/saves. Real-game result remains unverified.");
        }
    }
}
#endif
