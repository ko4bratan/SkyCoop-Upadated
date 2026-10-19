#if !DEDICATED
using System;
using System.IO;
using System.Security.Cryptography;
using Steamworks;
using UnityEngine;

namespace SkyCoop
{
    // Reuses the original Steamworks.NET20.2 legacy lobby/P2P API, on an isolated channel.
    // Never dispatch probe packets into legacy gameplay/save RPC handlers.
    public static class SteamSession
    {
        private const int Channel = 17;
        private static CSteamID lobby, peer;
        private static bool hosting, attempted;
        private static Callback<GameLobbyJoinRequested_t> invite;
        private static Callback<LobbyEnter_t> entered;
        private static Callback<P2PSessionRequest_t> session;
        private static Callback<P2PSessionConnectFail_t> failure;
        private static CallResult<LobbyCreated_t> created;
        public static string Status { get; private set; } = "Steam ещё не проверен";
        public static bool Ready => lobby.IsValid() && (hosting || peer.IsValid());
        private static string BuildHash()
        {
            using var file = File.OpenRead("Mods/SkyCoop.dll"); using var hash = SHA256.Create();
            return Convert.ToHexString(hash.ComputeHash(file)).ToLowerInvariant();
        }
        public static void Ensure()
        {
            if (attempted) return;
            attempted = true; SteamConnect.Init();
            if (!SteamConnect.CanUseSteam) Status = "Steam недоступен: запустите игру через Steam; проверьте steam.init и native steam_api64 в логах";
        }
        public static void Initialize()
        {
            invite = Callback<GameLobbyJoinRequested_t>.Create(data => Join(data.m_steamIDLobby));
            entered = Callback<LobbyEnter_t>.Create(OnEntered);
            session = Callback<P2PSessionRequest_t>.Create(data => {
                if (Member(data.m_steamIDRemote)) SteamNetworking.AcceptP2PSessionWithUser(data.m_steamIDRemote);
            });
            failure = Callback<P2PSessionConnectFail_t>.Create(data => {
                Status = "Ошибка Steam P2P: " + data.m_eP2PSessionError;
                DiagnosticLog.Failure("steam.p2p-failure", Status);
            });
            created = CallResult<LobbyCreated_t>.Create((data, io) => {
                if (io || data.m_eResult != EResult.k_EResultOK) { Status = "Не удалось создать Steam lobby: " + data.m_eResult; return; }
                lobby = new CSteamID(data.m_ulSteamIDLobby); hosting = true;
                SteamMatchmaking.SetLobbyData(lobby, "skycoop-probe", "1");
                SteamMatchmaking.SetLobbyData(lobby, "skycoop-build", BuildHash());
                SteamMatchmaking.SetLobbyData(lobby, "skycoop-target", "2.55");
                MinimalSurvival.SelectSession(true, null, true);
                Status = "Лобби создано. Пригласите друга; затем оба загрузите тестовое Выживание";
            });
            Status = "Steam обнаружен; игровой тест ещё не выполнен";
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "+connect_lobby");
            if (index >= 0 && index + 1 < args.Length && ulong.TryParse(args[index + 1], out ulong id)) Join(new CSteamID(id));
        }
        private static bool Member(CSteamID id)
        {
            if (!lobby.IsValid() || !id.IsValid() || id == SteamUser.GetSteamID()) return false;
            for (int i = 0; i < SteamMatchmaking.GetNumLobbyMembers(lobby); i++)
                if (SteamMatchmaking.GetLobbyMemberByIndex(lobby, i) == id) return true;
            return false;
        }
        public static void Host()
        {
            Ensure(); if (!SteamConnect.CanUseSteam) return;
            Leave(); hosting = true;
            created.Set(SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypeFriendsOnly, 2));
            Status = "Создаём Steam lobby…";
        }
        public static void Join(CSteamID id)
        {
            if (!id.IsValid()) return;
            Leave(); hosting = false;
            SteamMatchmaking.JoinLobby(id); Status = "Подключаемся к Steam lobby…";
        }
        private static void OnEntered(LobbyEnter_t data)
        {
            if ((EChatRoomEnterResponse)data.m_EChatRoomEnterResponse != EChatRoomEnterResponse.k_EChatRoomEnterResponseSuccess)
            { Status = "Вход в lobby отклонён: " + data.m_EChatRoomEnterResponse; return; }
            lobby = new CSteamID(data.m_ulSteamIDLobby);
            hosting = SteamMatchmaking.GetLobbyOwner(lobby) == SteamUser.GetSteamID();
            if (hosting) return; // Created callback publishes metadata.
            if (SteamMatchmaking.GetLobbyData(lobby, "skycoop-probe") != "1" || SteamMatchmaking.GetLobbyData(lobby, "skycoop-target") != "2.55"
                || SteamMatchmaking.GetLobbyData(lobby, "skycoop-build") != BuildHash())
            { Leave(); Status = "Лобби несовместимо: нужны одинаковые experimental DLL для 2.55"; return; }
            peer = SteamMatchmaking.GetLobbyOwner(lobby);
            MinimalSurvival.SelectSession(false, null, true);
            Status = "Лобби принято. Загрузите копию того же тестового мира и ту же сцену";
        }
        public static void Invite()
        {
            if (!Ready) { Status = "Сначала создайте Steam lobby"; return; }
            if (!SteamUtils.IsOverlayEnabled()) { Status = "Оверлей Steam отключён. Включите его в Steam для приглашений"; return; }
            SteamFriends.ActivateGameOverlayInviteDialog(lobby);
        }
        public static void Friends()
        {
            Ensure(); if (SteamConnect.CanUseSteam) SteamFriends.ActivateGameOverlay("Friends");
        }
        public static void Send(int id, byte[] payload)
        {
            if (!peer.IsValid()) return;
            byte[] bytes = SteamEnvelope.Encode(id, payload);
            if (!SteamNetworking.SendP2PPacket(peer, bytes, (uint)bytes.Length, EP2PSend.k_EP2PSendReliable, Channel))
                DiagnosticLog.Failure("steam.send", "Steam отказал в отправке пакета");
        }
        public static void Tick()
        {
            // Callback lifetime is owned by this class; game owns native Steam shutdown.
            SteamAPI.RunCallbacks();
            if (peer.IsValid() && !Member(peer))
            {
                SteamNetworking.CloseP2PSessionWithUser(peer); peer = default;
                MinimalSurvival.PeerDisconnected();
                if (!hosting) { Leave(); Status = "Хост вышел из lobby. Выберите новое приглашение"; return; }
            }
            for (int count = 0; count < 64 && SteamNetworking.IsP2PPacketAvailable(out uint size, Channel); count++)
            {
                if (size > 1048576) { Leave(); Status = "Steam-пакет превышает безопасный лимит"; return; }
                if (size > 2048) { byte[] discard = new byte[size]; SteamNetworking.ReadP2PPacket(discard, (uint)discard.Length, out _, out _, Channel); continue; }
                byte[] bytes = new byte[size];
                if (!SteamNetworking.ReadP2PPacket(bytes, size, out uint read, out CSteamID remote, Channel)) continue;
                if (!Member(remote) || read != size || size < 4) continue;
                if (peer.IsValid() && peer != remote) continue;
                peer = remote;
                try { var frame = SteamEnvelope.Decode(bytes); MinimalSurvival.ReceiveSteam(frame.Id, frame.Payload); }
                catch (InvalidDataException error) { DiagnosticLog.Error("steam.frame-invalid", error); }
            }
        }
        public static void Leave()
        {
            MinimalSurvival.ResetSession();
            if (peer.IsValid()) SteamNetworking.CloseP2PSessionWithUser(peer);
            if (lobby.IsValid()) SteamMatchmaking.LeaveLobby(lobby);
            peer = default; lobby = default; Status = "Steam-сеанс завершён";
        }
    }
}
#endif
