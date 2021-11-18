using HarmonyLib;
using System.Collections.Generic;
using Overload;
using System.Reflection.Emit;
using System.Reflection;
using UnityEngine;
using UnityEngine.Networking;
using System;

namespace GameMod
{
    /// <summary>
    ///  Goal:    allowing to access the same max speed and the same speed gain that max turn ramping on high provides
    ///           
    ///  Author:  luponix 
    ///  Created: 2021-07-22
    /// </summary>
    class JoystickRotationFix
    {

        public static bool alt_turn_ramp_mode = false;                                      // defines which behaviour the local player uses, gets loaded/saved through MPSetup.cs in .xprefsmod
        public static Dictionary<uint, int> client_settings = new Dictionary<uint, int>();  // used to store the turn ramp mode setting of the active players on the server
        public static bool server_support = true;                                           // indicates wether the current server supports the changed behaviour, has to be true outside of games to make the ui option accessible


        [HarmonyPatch(typeof(PlayerShip), "FixedUpdateProcessControlsInternal")]
        internal class JoystickRotationFix_FixedUpdateProcessControlsInternal
        {
            static PlayerShip _inst;

            static void Prefix(PlayerShip __instance)
            {
                _inst = __instance;
            }
            // resets num20 to cc_turn_vec.y, num21 to cc_turn_vec.x and multiplies them with the selected PlayerShip.m_ramp_max[] if the linear behaviour is 
            // selected and supported on the server
            static IEnumerable<CodeInstruction> Transpiler(ILGenerator ilGen, IEnumerable<CodeInstruction> instructions)
            {
                var playerShip_c_player_Field = AccessTools.Field(typeof(PlayerShip), "c_player");
                var player_cc_turn_vec_Field = AccessTools.Field(typeof(Player), "cc_turn_vec");
                var joystickRotationFix_MaybeResetToInput_Method = AccessTools.Method(typeof(JoystickRotationFix_FixedUpdateProcessControlsInternal), "MaybeResetToInput");
                var joystickRotationFix_MaybeScaleUpRotation_Method = AccessTools.Method(typeof(JoystickRotationFix_FixedUpdateProcessControlsInternal), "MaybeScaleUpRotation");

                var codes = new List<CodeInstruction>(instructions);
                int state = 0;
                for (int i = 0; i < codes.Count; i++)
                {
                    if (codes[i].opcode == OpCodes.Ldfld && ((FieldInfo)codes[i].operand).Name == "m_ramp_turn")
                    {
                        state++;
                        if (state == 5)
                        {
                            var resetNum20 = new[] {
                                new CodeInstruction(OpCodes.Ldarg_0),
                                new CodeInstruction(OpCodes.Ldfld, playerShip_c_player_Field),
                                new CodeInstruction(OpCodes.Ldflda, player_cc_turn_vec_Field),
                                new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(UnityEngine.Vector3), "y")),
                                new CodeInstruction(OpCodes.Call, joystickRotationFix_MaybeResetToInput_Method)
                            };
                            var resetNum21 = new[] {
                                new CodeInstruction(OpCodes.Ldarg_0),
                                new CodeInstruction(OpCodes.Ldfld, playerShip_c_player_Field),
                                new CodeInstruction(OpCodes.Ldflda, player_cc_turn_vec_Field),
                                new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(UnityEngine.Vector3), "x")),
                                new CodeInstruction(OpCodes.Call, joystickRotationFix_MaybeResetToInput_Method)
                            };
                            var adjustScaling = new[] {
                                new CodeInstruction(OpCodes.Call, joystickRotationFix_MaybeScaleUpRotation_Method),
                                new CodeInstruction(OpCodes.Mul, null)
                            };
                            codes.InsertRange(i + 22, adjustScaling);
                            codes.InsertRange(i + 14, resetNum21);
                            codes.InsertRange(i + 12, adjustScaling);
                            codes.InsertRange(i + 4, resetNum20);
                            return codes;
                        }
                    }
                }
                return codes;

            }

            public static float MaybeResetToInput(float original, float changed)
            {
                return MaybeChangeRotation() ? changed : original;
            }

            public static float MaybeScaleUpRotation()
            {
                return MaybeChangeRotation() ? PlayerShip.m_ramp_max[_inst.c_player.m_player_control_options.opt_joy_ramp] : 1f;
            }

            public static bool MaybeChangeRotation()
            {
                // (Server) if this is the server lookup the current players setting with _inst
                if (GameplayManager.IsDedicatedServer())
                {
                    if (client_settings.ContainsKey(_inst.netId.Value))
                    {
                        client_settings.TryGetValue(_inst.netId.Value, out int val);
                        return val == 1;
                    }
                    return false;
                }
                // (Client)
                return alt_turn_ramp_mode && server_support;
            }
        }

        public class SetTurnRampModeMessage : MessageBase
        {
            public int mode;
            public uint netId;
            public override void Serialize(NetworkWriter writer)
            {
                writer.Write(mode);
                writer.Write(netId);
            }
            public override void Deserialize(NetworkReader reader)
            {
                mode = reader.ReadInt32();
                netId = reader.ReadUInt32();
            }
        }


        /// ///////////////////////////////////// SERVER ///////////////////////////////////// ///
        [HarmonyPatch(typeof(Server), "RegisterHandlers")]
        class JoystickRotationFix_Server_RegisterHandlers
        {
            private static void OnSetJoystickRampMode(NetworkMessage rawMsg)
            {
                var msg = rawMsg.ReadMessage<SetTurnRampModeMessage>();
                if (client_settings.ContainsKey(msg.netId)) client_settings.Remove(msg.netId);
                client_settings.Add(msg.netId, msg.mode);
            }

            static void Postfix()
            {
                if (GameplayManager.IsDedicatedServer())
                {
                    NetworkServer.RegisterHandler(MessageTypes.MsgSetTurnRampMode, OnSetJoystickRampMode);
                }
            }
        }

        [HarmonyPatch(typeof(Server), "SendPostgameToAllClients")]
        class JoystickRotationFix_Server_SendPostgameToAllClients
        {
            private static void Postfix()
            {
                client_settings.Clear();
            }
        }


        /// ///////////////////////////////////// CLIENT ///////////////////////////////////// ///
        [HarmonyPatch(typeof(Client), "OnMatchStart")] // m_local_player.netId will return 0 or an old netid if this gets called earlier than OnMatchStart
        class JoystickRotationFix_SendPlayerLoadoutToServer
        {
            private static void Postfix()
            {
                if (GameplayManager.IsDedicatedServer()) return;
                if (Client.GetClient() == null)
                {
                    Debug.Log("JoystickRamping_SendPlayerLoadoutToServer: no client?");
                    return;
                }
                Client.GetClient().Send(MessageTypes.MsgSetTurnRampMode,
                    new SetTurnRampModeMessage
                    {
                        mode = alt_turn_ramp_mode ? 1 : 0,
                        netId = GameManager.m_local_player.netId.Value
                    });
            }
        }

        [HarmonyPatch(typeof(NetworkMatch), "ExitMatch")]
        internal class JoystickRotationFix_NetworkMatch_ExitMatch
        {
            static void Postfix()
            {
                server_support = true;
            }
        }

        [HarmonyPatch(typeof(Client), "Disconnect")]
        class JoystickRotationFix_Client_Disconnect
        {
            private static void Postfix()
            {
                server_support = true;
            }
        }

        /// ///////////////////////////////////// CLIENT UI ///////////////////////////////////// ///
        [HarmonyPatch(typeof(UIElement), "DrawControlsMenu")]
        internal class JoystickRotationFix_DrawControlsMenu
        {
            private static void DrawTurnspeedModeOption(UIElement uie, ref Vector2 position)
            {
                if (!server_support && !GameplayManager.IsMultiplayerActive && NetworkMatch.GetMatchState() != MatchState.LOBBY)
                {
                    server_support = true;
                    Debug.Log("JoystickRamping.server_support didnt reset properly");
                }
                position.y += 55f;
                uie.SelectAndDrawStringOptionItem(Loc.LS("MAX TURN RAMP MODE"), position, 0, alt_turn_ramp_mode ? "LINEAR" : "DEFAULT", "LINEAR ADDS THE MAX TURN RAMPING SPEED LINEARLY ALONG THE INPUT [KB/JOYSTICK ONLY]", 1.5f, !server_support);
            }

            static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                var codes = new List<CodeInstruction>(instructions);
                for (int i = 0; i < codes.Count; i++)
                {
                    if (codes[i].opcode == OpCodes.Ldstr && (string)codes[i].operand == "MAX TURN RAMPING")
                    {
                        var newCodes = new[] {
                            new CodeInstruction(OpCodes.Ldarg_0),
                            new CodeInstruction(OpCodes.Ldloca, 0),
                            new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(JoystickRotationFix_DrawControlsMenu), "DrawTurnspeedModeOption"))
                        };
                        codes.InsertRange(i + 9, newCodes);
                        break;
                    }
                }
                return codes;
            }
        }



        [HarmonyPatch(typeof(MenuManager), "ControlsOptionsUpdate")]
        internal class JoystickRotationFix_ControlsOptionsUpdate
        {
            private static void ProcessTurnRampModeButtonPress()
            {
                alt_turn_ramp_mode = !alt_turn_ramp_mode;
                // also send the updated state to the server if the client is currently in a game
                if (GameplayManager.IsMultiplayerActive)
                {
                    if (Client.GetClient() == null)
                    {
                        Debug.Log("JoystickRamping_ControlsOptionsUpdate: no client?");
                        return;
                    }
                    Client.GetClient().Send(MessageTypes.MsgSetTurnRampMode, new SetTurnRampModeMessage { mode = alt_turn_ramp_mode ? 1 : 0, netId = GameManager.m_local_player.netId.Value });
                }
            }

            static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                var codes = new List<CodeInstruction>(instructions);
                for (int i = 0; i < codes.Count; i++)
                {
                    if (codes[i].opcode == OpCodes.Ldsfld && (codes[i].operand as FieldInfo).Name == "opt_primary_autoswitch")
                    {
                        // remove the button press handling of the 'PRIMARY AUTOSELECT' option
                        codes.RemoveRange(i + 1, 6);
                        // adds logic to handle button presses of the new option
                        codes.Insert(i + 2, new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(JoystickRotationFix_ControlsOptionsUpdate), "ProcessTurnRampModeButtonPress")));
                        break;
                    }
                }
                return codes;
            }
        }


    }
}


