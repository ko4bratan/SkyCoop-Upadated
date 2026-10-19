#if !DEDICATED
using System;
using HarmonyLib;
using Il2Cpp;
using UnityEngine;

namespace SkyCoop
{
    // Uses the game's real BasicMenu and confirmation/input panel, with native navigation.
    public static class MultiplayerMenu
    {
        private static BasicMenu original, menu;
        private static bool awaitingIp;
        private static void Add(BasicMenu target, string id, string text, int order, Action action) =>
            target.AddItem(id, 19000 + order, order, text, "", "", new Action(() => { try { action(); } catch (Exception error) { DiagnosticLog.Error("ui.action", error); } }), Color.white, Color.white);
        private static void Attach(Panel_MainMenu panel)
        {
            if (panel == null || panel.m_BasicMenu == null) return;
            foreach (BasicMenu.BasicMenuItemModel model in panel.m_BasicMenu.m_ItemModelList)
                if (model != null && model.m_Id == "skycoop.multiplayer") return;
            Add(panel.m_BasicMenu, "skycoop.multiplayer", "МУЛЬТИПЛЕЕР / MULTIPLAYER", panel.m_BasicMenu.m_ItemModelList.Count, () => Open(panel));
            DiagnosticLog.Event("ui.multiplayer-attached", "Штатный BasicMenu; нативный игровой тест не выполнен");
        }
        private static void Open(Panel_MainMenu panel)
        {
            try
            {
                original = panel.m_BasicMenu;
                if (menu == null)
                {
                    GameObject clone = GameObject.Instantiate(original.gameObject, original.transform.parent);
                    menu = clone.GetComponent<BasicMenu>();
                }
                menu.Reset(); menu.UpdateTitle("МУЛЬТИПЛЕЕР");
                Add(menu, "steam.host", "Создать сервер Steam", 0, () => { SteamSession.Host(); });
                Add(menu, "steam.join", "Подключиться через Steam: приглашение друга", 8, SteamSession.Friends);
                Add(menu, "steam.invite", "Пригласить друга Steam", 1, SteamSession.Invite);
                Add(menu, "steam.friends", "Друзья Steam / принять приглашение в оверлее", 2, SteamSession.Friends);
                Add(menu, "udp.host", "Создать сервер UDP / Radmin", 3, () => { MinimalSurvival.SelectSession(true, null, false); Back(); });
                Add(menu, "udp.join", "Подключиться по IPv4", 4, AskIp);
                Add(menu, "status", "Состояние соединения", 5, () => Notice(MinimalSurvival.Status + "\n" + SteamSession.Status));
                Add(menu, "disconnect", "Отключиться / сбросить сеанс", 6, () => { MinimalSurvival.ResetSession(); SteamSession.Leave(); });
                Add(menu, "back", "Назад", 7, Back);
                menu.SetBackAction(new Action(Back)); menu.EnableConfirm(false, "GAMEPLAY_Select");
                original.gameObject.SetActive(false); menu.gameObject.SetActive(true); menu.SetItemSelected(0);
            }
            catch (Exception error) { DiagnosticLog.Error("ui.multiplayer-open", error); Back(); }
        }
        private static void Back()
        {
            if (menu != null) menu.gameObject.SetActive(false);
            if (original != null) original.gameObject.SetActive(true);
        }
        public static void Notice(string message) => GameCompat.Panel<Panel_Confirmation>()?.AddConfirmation(
            Panel_Confirmation.ConfirmationType.ErrorMessage, message, Panel_Confirmation.ButtonLayout.Button_1,
            Panel_Confirmation.Background.Transperent, null);
        private static void AskIp()
        {
            awaitingIp = true;
            GameCompat.Panel<Panel_Confirmation>()?.AddConfirmation(Panel_Confirmation.ConfirmationType.Rename,
                "IPv4 хоста Radmin VPN", "26.0.0.1", Panel_Confirmation.ButtonLayout.Button_2,
                "Подключиться", "GAMEPLAY_Cancel", Panel_Confirmation.Background.Transperent, null, null);
        }
        [HarmonyPatch(typeof(Panel_MainMenu), "Initialize")]
        private static class Initialized { private static void Postfix(Panel_MainMenu __instance) { if (MinimalSurvival.Enabled) { try { Attach(__instance); } catch (Exception error) { DiagnosticLog.Error("ui.attach", error); } } } }
        [HarmonyPatch(typeof(Panel_MainMenu), "Enable")]
        private static class Enabled { private static void Postfix(Panel_MainMenu __instance) { if (MinimalSurvival.Enabled) { try { Attach(__instance); } catch (Exception error) { DiagnosticLog.Error("ui.attach", error); } } } }
        [HarmonyPatch(typeof(Panel_Confirmation), "OnCancel")]
        private static class Cancelled { private static void Prefix() { awaitingIp = false; } }
        [HarmonyPatch(typeof(Panel_Confirmation), "OnConfirm")]
        private static class Confirmed
        {
            private static void Prefix(Panel_Confirmation __instance)
            {
                if (!awaitingIp) return;
                awaitingIp = false;
                string address = __instance.m_CurrentGroup?.m_InputField?.GetText();
                try { MinimalSurvival.SelectSession(false, address, false); Back(); }
                catch (Exception error) { DiagnosticLog.Error("ui.ip-invalid", error); }
            }
        }
    }
}
#endif
