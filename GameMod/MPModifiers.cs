using HarmonyLib;
using Overload;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Networking.NetworkSystem;
using System.Collections;

namespace GameMod
{
    class MPModifiers
    {

        public static bool[] mms_modifier_filter = new bool[8] { true, true, true, true, true, true, true, true };
        public static bool PlayerModifiersValid(int mod1, int mod2)
        {
            bool flag = true;
            bool[] serverFilters = new bool[8];
            RUtility.BitmaskToBoolArray(MPModPrivateData.ModifierFilterMask, ref serverFilters);

            for (int i = 0; i < 4; i++)
            {
                // Only verify modifier 1 if there's an enabled lobby selection in left column
                if (serverFilters[i])
                {
                    flag &= serverFilters[mod1];
                    break;
                }
            }

            for (int j = 4; j < 8; j++)
            {
                // Only verify modifier 2 if there's an enabled lobby selection in right column
                if (serverFilters[j])
                {
                    flag &= serverFilters[mod2 + 4];
                    break;
                }
            }
                
            return flag;
        }
        public static void UpdateLobbyStatus()
        {
            if (!MPModifiers.PlayerModifiersValid(Player.Mp_modifier1, Player.Mp_modifier2))
            {
                if (Array.IndexOf(MenuManager.m_mp_status_id, 20) == -1)
                {
                    MenuManager.AddMpStatus("MODIFIER(S) DISALLOWED, CHECK CUSTOMIZE MENU", 1f, 20);
                }
            }
            else
            {
                var idx = Array.IndexOf(MenuManager.m_mp_status_id, 20);
                if (idx >= 0)
                {
                    MenuManager.m_mp_status_details[idx] = String.Empty;
                    MenuManager.m_mp_status_flash[idx] = 0f;
                    MenuManager.m_mp_status_id[idx] = -1;
                }
            }
        }
        public static string GetDisabledModifiers()
        {
            string result = String.Empty;
            List<int> disabled = new List<int>();
            bool[] serverFilters = new bool[8];
            RUtility.BitmaskToBoolArray(MPModPrivateData.ModifierFilterMask, ref serverFilters);

            for (int i = 0; i < 8; i++)
            {
                if (!serverFilters[i])
                    disabled.Add(i);
            }

            result = String.Join(", ", disabled.Select(x => Player.GetMpModifierName(x < 4 ? x : x - 4, x < 4 ? true : false)).ToArray());
            return result;
        }

        // UI for new "Allowed Modifiers" section in MP Create
        [HarmonyPatch(typeof(UIElement), "DrawMpMatchSetup")]
        class MPModifiers_UIElement_DrawMpMatchSetup
        {

            static void ModifierSettings(UIElement uie, ref Vector2 position)
            {
                position.y += 62f;
                uie.SelectAndDrawItem("ALLOWED MODIFIERS", position, 8, false, 1f, 0.75f);
            }

            [HarmonyPriority(Priority.Normal - 2)] // set global order of transpilers for this function
            private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
            {
                int state = 0;

                foreach (var code in codes)
                {
                    if (state == 0 && code.opcode == OpCodes.Ldstr && (string)code.operand == "CUSTOM MODIFIER 2")
                        state = 1;

                    if (state == 1 && code.opcode == OpCodes.Call && code.operand == AccessTools.Method(typeof(UIElement), "SelectAndDrawStringOptionItem"))
                    {
                        state = 2;
                        yield return code;
                        yield return new CodeInstruction(OpCodes.Ldarg_0);
                        yield return new CodeInstruction(OpCodes.Ldloca, 0);
                        yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(MPModifiers_UIElement_DrawMpMatchSetup), "ModifierSettings"));
                        continue;
                    }

                    yield return code;
                }
            }

            private static void Postfix(UIElement __instance)
            {
                switch (MenuManager.m_menu_micro_state)
                {
                    case 9:
                        Player local_player = GameManager.m_local_player;
                        Vector2 position = __instance.m_position;
                        __instance.DrawLabelSmall(Vector2.up * (UIManager.UI_TOP + 70f), Loc.LS("ALLOWED MODIFIERS"), 250f, 24f, 1f);
                        position.y = -133f;
                        __instance.DrawMenuSeparator(position - Vector2.up * 40f);
                        position.y += 40f;

                        position.x = -310f;
                        position.y = -90f;
                        for (int i = 0; i < 4; i++)
                        {
                            DrawMpModifier(__instance, position, i, true, local_player);
                            position.y += 95f;
                        }
                        position.x = 310f;
                        position.y = -90f;
                        for (int j = 0; j < 4; j++)
                        {
                            DrawMpModifier(__instance, position, j, false, local_player);
                            position.y += 95f;
                        }
                        break;
                    default:
                        break;
                }
            }

            private static void DrawMpModifier(UIElement uie, Vector2 pos, int idx, bool mod1, Player player)
            {
                float num = 535f;
                float middle_h = 55f;
                Color col_ub = UIManager.m_col_ub0;
                col_ub.a = uie.m_alpha;
                UIManager.DrawFrameEmptyCenter(pos, 17f, 17f, num, middle_h, col_ub, 7);
                bool flag = mms_modifier_filter[idx + ((!mod1) ? 4 : 0)];
                uie.SelectAndDrawCheckboxItem(Player.GetMpModifierName(idx, mod1), pos - Vector2.up * 10f, idx + ((!mod1) ? 4 : 0), flag, false, 1f, Player.GetMpModifierIcon(idx, mod1));
                pos.x -= num * 0.5f + 10f;
                pos.y += 30f;
                uie.DrawStringSmall(Player.GetMpModifierDesc(idx, mod1), pos, 0.4f, StringOffset.LEFT, (!flag) ? UIManager.m_col_ub1 : UIManager.m_col_ui1, 1f, num - 40f);
            }

        }

        // Disable checking modifiers which aren't allowed
        [HarmonyPatch(typeof(UIElement), "DrawMpModifier")]
        class MPModifiers_UIElement_DrawMpModifier
        {
            static bool Prefix(UIElement __instance, Vector2 pos, int idx, bool mod1, Player player)
            {
                float num = 535f;
                float middle_h = 55f;
                Color col_ub = UIManager.m_col_ub0;
                col_ub.a = __instance.m_alpha;
                UIManager.DrawFrameEmptyCenter(pos, 17f, 17f, num, middle_h, col_ub, 7);
                bool flag = (!mod1) ? (Player.Mp_modifier2 == idx) : (Player.Mp_modifier1 == idx);
                int modIndex = idx + ((!mod1) ? 4 : 0);
                bool enabled = NetworkMatch.InLobby() ? !((MPModPrivateData.ModifierFilterMask & 1 << modIndex) == 1 << modIndex) : false;
                __instance.SelectAndDrawCheckboxItem(Player.GetMpModifierName(idx, mod1), pos - Vector2.up * 10f, idx + ((!mod1) ? 4 : 0), flag, enabled, 1f, Player.GetMpModifierIcon(idx, mod1));
                pos.x -= num * 0.5f + 10f;
                pos.y += 30f;
                __instance.DrawStringSmall(Player.GetMpModifierDesc(idx, mod1), pos, 0.4f, StringOffset.LEFT, (!flag) ? UIManager.m_col_ub1 : UIManager.m_col_ui1, 1f, num - 40f);

                return false;
            }
        }

        // Handle new "Allowed Modifiers" UI
        [HarmonyPatch(typeof(MenuManager), "MpMatchSetup")]
        class MPModifiers_MenuManager_MpMatchSetup
        {
            static void Postfix()
            {
                if (MenuManager.m_menu_sub_state == MenuSubState.ACTIVE)
                {

                    // Add status if your modifiers aren't allowed
                    if (MenuManager.m_menu_micro_state != 2)
                    {
                        MPModifiers.UpdateLobbyStatus();
                    }


                    if (!UIManager.PushedSelect(100) && (!MenuManager.option_dir || !UIManager.PushedDir()))
                        return;

                    switch (MenuManager.m_menu_micro_state)
                    {
                        // Allowed modifiers button
                        case 6:
                            switch (UIManager.m_menu_selection)
                            {
                                case 8:
                                    MenuManager.m_menu_micro_state = 9;
                                    MenuManager.UIPulse(2f);
                                    MenuManager.PlaySelectSound(1f);
                                    return;
                                case 100:
                                    MenuManager.m_menu_micro_state = 6;
                                    MenuManager.UIPulse(2f);
                                    MenuManager.PlaySelectSound(1f);
                                    return;
                                default:
                                    return;
                            }
                        // Allowed modifiers window
                        case 9:
                            switch (UIManager.m_menu_selection)
                            {
                                case 0:
                                case 1:
                                case 2:
                                case 3:
                                case 4:
                                case 5:
                                case 6:
                                case 7:
                                    MPModifiers.mms_modifier_filter[UIManager.m_menu_selection] = !MPModifiers.mms_modifier_filter[UIManager.m_menu_selection];
                                    MenuManager.PlaySelectSound(1f);
                                    return;
                                case 100:
                                    MenuManager.m_menu_micro_state = 6;
                                    MenuManager.UIPulse(2f);
                                    MenuManager.PlaySelectSound(1f);
                                    return;
                                default:
                                    return;
                            }
                    }
                }
            }
        }

        // Update lobby status display
        [HarmonyPatch(typeof(NetworkMatch), "OnAcceptedToLobby")]
        class MPModifiers_NetworkMatch_OnAcceptedToLobby
        {
            static void Postfix()
            {
                MPModifiers.UpdateLobbyStatus();
            }
        }

        // Update lobby status display
        [HarmonyPatch(typeof(UIElement), "DrawMpPreMatchMenu")]
        class MPModifiers_UIElement_DrawMpPreMatchMenu
        {
            static void Prefix()
            {
                MPModifiers.UpdateLobbyStatus();
            }
        }

        // Display "x" for missing/no modifier
        [HarmonyPatch(typeof(Player), "GetMpModifierIcon")]
        class MPModifiers_Player_GetMpModifierIcon
        {
            static bool Prefix(int idx, bool mod1, ref int __result)
            {
                if (idx == -1)
                {
                    __result = 212;
                    return false;
                }

                return true;
            }
        }

        // Disconnect player if lobby countdown starts and they have an invalid modifier set
        [HarmonyPatch(typeof(NetworkMatch), "ProcessLobbyCountdown")]
        class MPModifiers_NetworkMatch_ProcessLobbyCountdown
        {

            private static IEnumerator DisconnectCoroutine(int connectionId)
            {
                yield return new WaitForSecondsRealtime(5);
                var conn = NetworkServer.connections[connectionId];
                if (conn != null)
                {
                    conn.Disconnect();
                }
            }

            static void Prefix()
            {
                if (!Overload.NetworkManager.IsServer())
                    return;

                bool[] serverFilters = new bool[8];
                RUtility.BitmaskToBoolArray(MPModPrivateData.ModifierFilterMask, ref serverFilters);

                foreach (var player in NetworkMatch.m_player_loadout_data.Where(x => x.Key > 0))
                {
                    if (!MPModifiers.PlayerModifiersValid(player.Value.m_mp_modifier1, player.Value.m_mp_modifier2))
                    {
                        var conn = NetworkServer.connections[player.Key];
                        if (conn != null)
                        {
                            NetworkServer.SendToClient(conn.connectionId, 86, new StringMessage("This match has disabled modifiers: " + GetDisabledModifiers()));
                            NetworkMatch.RemoveConnectionId(conn.connectionId);
                            GameManager.m_gm.StartCoroutine(DisconnectCoroutine(conn.connectionId));
                        }            
                    }
                }
            }
        }

        // Update server loadout data if clients joined through with a modifier selected when none for that column are allowed
        [HarmonyPatch(typeof(Server),  "SendLoadoutDataToClients")]
        class MPModifiers_Server_SendLoadoutDataToClients
        {
            static void Prefix()
            {
                bool[] serverFilters = new bool[8];
                RUtility.BitmaskToBoolArray(MPModPrivateData.ModifierFilterMask, ref serverFilters);

                foreach (KeyValuePair<int, LoadoutDataMessage> keyValuePair in NetworkMatch.m_player_loadout_data.Where(x => x.Key > 0))
                {
                    if (!serverFilters.Take(4).Any(x => x))
                        keyValuePair.Value.m_mp_modifier1 = -1;

                    if (!serverFilters.Skip(4).Take(4).Any(x => x))
                        keyValuePair.Value.m_mp_modifier2 = -1;
                }
            }
        }
    }
}
