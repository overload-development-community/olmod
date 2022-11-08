using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using GameMod.Messages;
using GameMod.Metadata;
using GameMod.Objects;
using HarmonyLib;
using Overload;
using Overload_Vanilla = Overload; // Required to differentiate between Overload.NetworkManager and UnityEngine.Networking.NetworkManager, without Overload colliding with GameMod.Patches.Overload.
using UnityEngine.Networking;
using UnityEngine;

namespace GameMod.Patches.Overload {
    /// <summary>
    /// Instructs the server to tell all the clients that ammo has been added to a player.
    /// </summary>
    [Mod(Mods.SniperPackets)]
    [HarmonyPatch(typeof(Player), "AddAmmo")]
    public static class Player_AddAmmo {
        public static void Postfix(bool __result, Player __instance, int ammo) {
            if (!SniperPackets.enabled) return;

            if (__result && NetworkServer.active) {
                foreach (Player remotePlayer in Overload_Vanilla.NetworkManager.m_Players) {
                    if (Tweaks.ClientHasMod(remotePlayer.connectionToClient.connectionId)) {
                        NetworkServer.SendToClient(remotePlayer.connectionToClient.connectionId, MessageTypes.MsgPlayerAddResource, new PlayerAddResourceMessage {
                            m_player_id = __instance.netId,
                            m_type = PlayerAddResourceMessage.ValueType.AMMO,
                            m_value = ammo,
                            m_max_value = Player.MAX_AMMO[__instance.m_upgrade_level[2]],
                            m_default = false
                        });
                    }
                }
            }
        }
    }

    /// <summary>
    /// Instructs the server to tell all the clients that energy has been added to a player.
    /// </summary>
    [Mod(Mods.SniperPackets)]
    [HarmonyPatch(typeof(Player), "AddEnergy")]
    public static class Player_AddEnergy {
        public static bool Prepare() {
            return GameplayManager.IsDedicatedServer();
        }

        public static void Postfix(bool __result, Player __instance, float energy) {
            if (!SniperPackets.enabled) return;

            if (__result && NetworkServer.active) {
                foreach (Player remotePlayer in Overload_Vanilla.NetworkManager.m_Players) {
                    if (Tweaks.ClientHasMod(remotePlayer.connectionToClient.connectionId)) {
                        NetworkServer.SendToClient(remotePlayer.connectionToClient.connectionId, MessageTypes.MsgPlayerAddResource, new PlayerAddResourceMessage {
                            m_player_id = __instance.netId,
                            m_type = PlayerAddResourceMessage.ValueType.ENERGY,
                            m_value = energy,
                            m_max_value = Player.MAX_ENERGY,
                            m_default = false
                        });
                    }
                }
            }
        }
    }

    /// <summary>
    /// Increases the loop wait time for the energy center sounds. There is a noticeable performance hit when charging.
    /// 
    /// Instructs the server to tell all the clients that energy has been added to a player.  This is the function Overload calls when refueling happens.
    /// </summary>
    [Mod(new Mods[] { Mods.EnergyCenterPerformance, Mods.SniperPackets })]
    [HarmonyPatch(typeof(Player), "AddEnergyDefault")]
    public static class Player_AddEnergyDefault {
        public static bool Prepare() {
            return GameplayManager.IsDedicatedServer();
        }

        [Mod(Mods.EnergyCenterPerformance)]
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes) {
            bool state = false;
            foreach (var code in codes) {
                if (!state && code.opcode == OpCodes.Ldc_R4 && (float)code.operand == 0.1f) {
                    code.operand = 0.5f;
                    state = true;
                }
                yield return code;
            }
        }

        [Mod(Mods.SniperPackets)]
        public static void Postfix(bool __result, Player __instance, float energy) {
            if (!SniperPackets.enabled) return;

            if (__result && NetworkServer.active) {
                foreach (Player remotePlayer in Overload_Vanilla.NetworkManager.m_Players) {
                    if (Tweaks.ClientHasMod(remotePlayer.connectionToClient.connectionId)) {
                        NetworkServer.SendToClient(remotePlayer.connectionToClient.connectionId, MessageTypes.MsgPlayerAddResource, new PlayerAddResourceMessage {
                            m_player_id = __instance.netId,
                            m_type = PlayerAddResourceMessage.ValueType.ENERGY,
                            m_value = energy,
                            m_max_value = 100,
                            m_default = true
                        });
                    }
                }
            }
        }
    }

    /// <summary>
    /// This will send the amount of ammo added to the player to all of the clients.
    /// </summary>
    [Mod(Mods.SniperPackets)]
    [HarmonyPatch(typeof(Player), "AddMissileAmmo")]
    public static class Player_AddMissileAmmo {
        public static void Prefix(Player __instance, int amt, MissileType mt, bool super = false) {
            if (!SniperPackets.enabled) return;

            if (GameplayManager.IsMultiplayerActive && NetworkServer.active && __instance.CanAddMissileAmmo(mt, super)) {
                int max = 999;
                if (super) {
                    if (GameplayManager.IsMultiplayerActive) {
                        amt = Player.SUPER_MISSILE_AMMO_MP[(int)mt];
                    } else {
                        amt = __instance.GetMaxMissileAmmo(mt);
                    }
                } else {
                    max = __instance.GetMaxMissileAmmo(mt);
                }

                foreach (Player remotePlayer in Overload_Vanilla.NetworkManager.m_Players) {
                    if (Tweaks.ClientHasMod(remotePlayer.connectionToClient.connectionId)) {
                        NetworkServer.SendToClient(remotePlayer.connectionToClient.connectionId, MessageTypes.MsgPlayerAddResource, new PlayerAddResourceMessage {
                            m_player_id = __instance.netId,
                            m_type = (PlayerAddResourceMessage.ValueType)((int)PlayerAddResourceMessage.ValueType.FALCON + (int)mt),
                            m_value = amt,
                            m_max_value = max,
                            m_default = false
                        });
                    }
                }
            }
        }
    }

    /// <summary>
    /// Instructs the server to tell all the clients that energy has been added to a player.
    /// </summary>
    [Mod(Mods.SniperPackets)]
    [HarmonyPatch(typeof(Player), "AddWeakEnergy")]
    public static class Player_AddWeakEnergy {
        public static bool Prepare() {
            return GameplayManager.IsDedicatedServer();
        }

        public static void Postfix(Player __instance, float energy, float max_energy) {
            if (!SniperPackets.enabled) return;

            if (NetworkServer.active) {
                foreach (Player remotePlayer in Overload_Vanilla.NetworkManager.m_Players) {
                    if (Tweaks.ClientHasMod(remotePlayer.connectionToClient.connectionId)) {
                        NetworkServer.SendToClient(remotePlayer.connectionToClient.connectionId, MessageTypes.MsgPlayerAddResource, new PlayerAddResourceMessage {
                            m_player_id = __instance.netId,
                            m_type = PlayerAddResourceMessage.ValueType.ENERGY,
                            m_value = energy,
                            m_max_value = max_energy,
                            m_default = false
                        });
                    }
                }
            }
        }
    }

    /// <summary>
    /// Sets the minimium XP required for modifiers to 0.
    /// </summary>
    [Mod(Mods.UnlockModifiers)]
    [HarmonyPatch(typeof(Player), "GetModifierMinXP")]
    public static class MPUnlockAllModifiers {
        public static bool Prepare() {
            return !GameplayManager.IsDedicatedServer();
        }

        public static bool Prefix(ref int __result) {
            __result = 0;
            return false;
        }
    }

    /// <summary>
    /// This prevents the server from synchronizing the missile type being used to the client whose missile type it wants to set.  Eliminates the use of the Unity SyncVar setup.
    /// </summary>
    [Mod(Mods.SniperPackets)]
    [HarmonyPatch(typeof(Player), "Networkm_missile_type", MethodType.Setter)]
    public static class Player_Networkm_missile_type {
        public static bool Prefix(Player __instance, MissileType value) {
            if (!SniperPackets.enabled) return true;
            if (!GameplayManager.IsMultiplayerActive) return true;
            if (NetworkServer.active && !Tweaks.ClientHasMod(__instance.connectionToClient.connectionId)) return true;

            if (!NetworkServer.active) {
                __instance.m_missile_type = value;

                if (__instance.isLocalPlayer && __instance.m_missile_ammo[(int)value] > 0) {
                    Client.GetClient().Send(MessageTypes.MsgPlayerWeaponSynchronization, new PlayerWeaponSynchronizationMessage {
                        m_player_id = __instance.netId,
                        m_type = PlayerWeaponSynchronizationMessage.ValueType.MISSILE,
                        m_value = (int)value
                    });
                }
            }
            return false;
        }
    }

    /// <summary>
    /// This prevents the server from synchronizing the previous missile type being used to the client whose missile type it wants to set.  Eliminates the use of the Unity SyncVar setup.
    /// </summary>
    [Mod(Mods.SniperPackets)]
    [HarmonyPatch(typeof(Player), "Networkm_missile_type_prev", MethodType.Setter)]
    public static class Player_Networkm_missile_type_prev {
        public static bool Prefix(Player __instance, MissileType value) {
            if (!SniperPackets.enabled) return true;
            if (!GameplayManager.IsMultiplayerActive) return true;
            if (NetworkServer.active && !Tweaks.ClientHasMod(__instance.connectionToClient.connectionId)) return true;

            if (__instance.m_missile_type_prev != value) {
                if (!NetworkServer.active) {
                    __instance.m_missile_type_prev = value;

                    if (__instance.isLocalPlayer) {
                        Client.GetClient().Send(MessageTypes.MsgPlayerWeaponSynchronization, new PlayerWeaponSynchronizationMessage {
                            m_player_id = __instance.netId,
                            m_type = PlayerWeaponSynchronizationMessage.ValueType.MISSILE_PREV,
                            m_value = (int)value
                        });
                    }
                }
            }
            return false;
        }
    }

    /// <summary>
    /// This prevents the server from synchronizing the weapon type being used to the client whose weapon type it wants to set.  Eliminates the use of the Unity SyncVar setup.
    /// </summary>
    [Mod(Mods.SniperPackets)]
    [HarmonyPatch(typeof(Player), "Networkm_weapon_type", MethodType.Setter)]
    public static class Player_Networkm_weapon_type {
        public static bool Prefix(Player __instance, WeaponType value) {
            if (!SniperPackets.enabled) return true;
            if (!GameplayManager.IsMultiplayerActive) return true;
            if (NetworkServer.active && !Tweaks.ClientHasMod(__instance.connectionToClient.connectionId)) return true;

            if (__instance.m_weapon_type != value) {
                if (!NetworkServer.active) {
                    __instance.m_weapon_type = value;

                    if (__instance.isLocalPlayer) {
                        Client.GetClient().Send(MessageTypes.MsgPlayerWeaponSynchronization, new PlayerWeaponSynchronizationMessage {
                            m_player_id = __instance.netId,
                            m_type = PlayerWeaponSynchronizationMessage.ValueType.WEAPON,
                            m_value = (int)value
                        });
                    }
                }
            }
            return false;
        }
    }

    /// <summary>
    /// This prevents the server from synchronizing the previous weapon type being used to the client whose weapon type it wants to set.  Eliminates the use of the Unity SyncVar setup.
    /// </summary>
    [Mod(Mods.SniperPackets)]
    [HarmonyPatch(typeof(Player), "Networkm_weapon_type_prev", MethodType.Setter)]
    public static class Player_Networkm_weapon_type_prev {
        public static bool Prefix(Player __instance, WeaponType value) {
            if (!SniperPackets.enabled) return true;
            if (!GameplayManager.IsMultiplayerActive) return true;
            if (NetworkServer.active && !Tweaks.ClientHasMod(__instance.connectionToClient.connectionId)) return true;

            if (__instance.m_weapon_type_prev != value) {
                if (!NetworkServer.active) {
                    __instance.m_weapon_type_prev = value;

                    if (__instance.isLocalPlayer) {
                        Client.GetClient().Send(MessageTypes.MsgPlayerWeaponSynchronization, new PlayerWeaponSynchronizationMessage {
                            m_player_id = __instance.netId,
                            m_type = PlayerWeaponSynchronizationMessage.ValueType.WEAPON_PREV,
                            m_value = (int)value
                        });
                    }
                }
            }
            return false;
        }
    }

    /// <summary>
    /// Allows us to keep track of who killed/assisted/died for reporting to the tracker.
    /// </summary>
    [Mod(Mods.Tracker)]
    [HarmonyPatch(typeof(Player), "OnKilledByPlayer")]
    public static class Player_OnKilledByPlayer {
        public static bool Prepare() {
            return Config.Settings.Value<bool>("isServer") && !string.IsNullOrEmpty(Config.Settings.Value<string>("trackerBaseUrl"));
        }

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) {
            bool lastTryGetValue = false;
            object lastLocVar = null;
            int setCount = 0;
            string[] setMethods = new[] { "SetDefender", "SetAttacker", "SetAssisted" };

            foreach (var code in instructions) {
                if (code.opcode == OpCodes.Ret && setCount > 0) {
                    yield return new CodeInstruction(OpCodes.Ldarg_1); // damageInfo
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Tracker), "AddKill"));
                }
                yield return code;
                if (code.opcode == OpCodes.Brfalse && lastTryGetValue && setCount < setMethods.Length) {
                    yield return new CodeInstruction(OpCodes.Ldloc_S, lastLocVar);
                    yield return new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(PlayerLobbyData), "m_name"));
                    yield return new CodeInstruction(OpCodes.Ldloc_S, lastLocVar);
                    yield return new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(PlayerLobbyData), "m_team"));
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Tracker), setMethods[setCount]));
                    setCount++;
                }
                if (code.opcode == OpCodes.Ldloca || code.opcode == OpCodes.Ldloca_S) {
                    lastLocVar = code.operand;
                }
                lastTryGetValue = code.opcode == OpCodes.Callvirt && ((MemberInfo)code.operand).Name == "TryGetValue";
            }
        }
    }

    /// <summary>
    /// Disable rendering other ships' cockpit after restoring ship data on respawn.
    /// Does a better job of initializing playership state at spawn, resetting the flak/cyclone fire counter, the thunderbolt power level, and clearing the boost overheat.
    /// </summary>
    [Mod(new Mods[] { Mods.DisableOpponentCockpits, Mods.SpawnInitialization })]
    [HarmonyPatch(typeof(Player), "RestorePlayerShipDataAfterRespawn")]
    public static class Player_RestorePlayerShipDataAfterRespawn {
        private static readonly FieldInfo _PlayerShip_flak_fire_count_Field = typeof(PlayerShip).GetField("flak_fire_count", BindingFlags.NonPublic | BindingFlags.Instance);

        [Mod(Mods.SpawnInitialization)]
        public static void Prefix(Player __instance) {
            _PlayerShip_flak_fire_count_Field.SetValue(__instance.c_player_ship, 0);
            __instance.c_player_ship.m_thunder_power = 0;
            __instance.c_player_ship.m_boost_heat = 0;
            __instance.c_player_ship.m_boost_overheat_timer = 0f;
        }

        [Mod(Mods.DisableOpponentCockpits)]
        public static void Postfix(Player __instance) {
            DisableOpponentCockpits.SetOpponentCockpitVisibility(__instance, false);
        }
    }

    /// <summary>
    /// Only track assists if assist scoring is enabled for this game.
    /// </summary>
    [Mod(Mods.AssistScoring)]
    [HarmonyPatch(typeof(Player), "RpcAddAssist")]
    public static class Player_RpcAddAssist {
        public static bool Prepare() {
            return !GameplayManager.IsDedicatedServer();
        }

        public static bool Prefix() {
            return MPModPrivateData.AssistScoring;
        }
    }

    /// <summary>
    /// Prevents the server from setting a client's ammo.
    /// </summary>
    [Mod(Mods.SniperPackets)]
    [HarmonyPatch(typeof(Player), "RpcSetAmmo")]
    public static class Player_RpcSetAmmo {
        public static bool Prefix(Player __instance) {
            if (!SniperPackets.enabled) return true;
            if (!GameplayManager.IsMultiplayerActive) return true;
            if (NetworkServer.active && !Tweaks.ClientHasMod(__instance.connectionToClient.connectionId)) return true;

            return false;
        }
    }

    /// <summary>
    /// Prevents the server from setting a client's energy.
    /// </summary>
    [Mod(Mods.SniperPackets)]
    [HarmonyPatch(typeof(Player), "RpcSetEnergy")]
    public static class Player_RpcSetEnergy {
        public static bool Prefix(Player __instance) {
            if (!SniperPackets.enabled) return true;
            if (!GameplayManager.IsMultiplayerActive) return true;
            if (NetworkServer.active && !Tweaks.ClientHasMod(__instance.connectionToClient.connectionId)) return true;

            return false;
        }
    }

    /// <summary>
    /// This prevents the server from telling the client how many missiles it has.
    /// </summary>
    [Mod(Mods.SniperPackets)]
    [HarmonyPatch(typeof(Player), "RpcSetMissileAmmo")]
    public static class Player_RpcSetMissileAmmo {
        public static bool Prefix(Player __instance) {
            if (!SniperPackets.enabled) return true;
            if (!GameplayManager.IsMultiplayerActive) return true;
            if (NetworkServer.active && !Tweaks.ClientHasMod(__instance.connectionToClient.connectionId)) return true;

            return false;
        }
    }

    /// <summary>
    /// This prevents the server from telling the client what missile to use.
    /// </summary>
    [Mod(Mods.SniperPackets)]
    [HarmonyPatch(typeof(Player), "RpcSetMissileType")]
    public static class Player_RpcSetMissileType {
        public static bool Prefix(Player __instance) {
            if (!SniperPackets.enabled) return true;
            if (!GameplayManager.IsMultiplayerActive) return true;
            if (NetworkServer.active && !Tweaks.ClientHasMod(__instance.connectionToClient.connectionId)) return true;

            return false;
        }
    }

    /// <summary>
    /// We want the client to control what weapon they are using, so if this function is called by the server, we return the result that is expected, but otherwise ignore the call.
    /// </summary>
    [Mod(Mods.SniperPackets)]
    [HarmonyPatch(typeof(Player), "SwitchToAmmoWeapon")]
    public static class Player_SwitchToAmmoWeapon {
        public static bool Prepare() {
            return GameplayManager.IsDedicatedServer();
        }

        public static bool Prefix(Player __instance, ref bool __result) {
            if (!SniperPackets.enabled) return true;
            if (NetworkServer.active && !Tweaks.ClientHasMod(__instance.connectionToClient.connectionId)) return true;

            __result = __instance.m_weapon_level[4] != WeaponUnlock.LOCKED || __instance.m_weapon_level[3] != WeaponUnlock.LOCKED || __instance.m_weapon_level[5] != WeaponUnlock.LOCKED;

            return false;
        }
    }

    /// <summary>
    /// We want the client to control what weapon they are using, so if this function is called by the server, we ignore the call.
    /// </summary>
    [Mod(Mods.SniperPackets)]
    [HarmonyPatch(typeof(Player), "SwitchToEnergyWeapon")]
    public static class Player_SwitchToEnergyWeapon {
        public static bool Prepare() {
            return GameplayManager.IsDedicatedServer();
        }

        public static bool Prefix(Player __instance) {
            if (!SniperPackets.enabled) return true;
            if (NetworkServer.active && !Tweaks.ClientHasMod(__instance.connectionToClient.connectionId)) return true;

            return false;
        }
    }

    /// <summary>
    /// We want the client to control what missile they are using, so if this function is called by the server, we ignore the call.
    /// </summary>
    [Mod(Mods.SniperPackets)]
    [HarmonyPatch(typeof(Player), "SwitchToNextMissileWithAmmo")]
    public static class Player_SwitchToNextMissileWithAmmo {
        public static bool Prefix(Player __instance) {
            if (!SniperPackets.enabled) return true;
            if (!(GameplayManager.IsMultiplayerActive && NetworkServer.active)) return true;
            if (NetworkServer.active && !Tweaks.ClientHasMod(__instance.connectionToClient.connectionId)) return true;

            return false;
        }
    }

    /// <summary>
    /// Prevents the server from sending a client a HUD message when ammo is increased.
    /// </summary>
    [Mod(Mods.SniperPackets)]
    [HarmonyPatch(typeof(Player), "TargetAddHUDMessageAmmoIncreased")]
    public static class Player_TargetAddHUDMessageAmmoIncreased {
        public static bool Prefix(Player __instance) {
            if (!SniperPackets.enabled) return true;
            if (!GameplayManager.IsMultiplayerActive) return true;
            if (NetworkServer.active && !Tweaks.ClientHasMod(__instance.connectionToClient.connectionId)) return true;

            return false;
        }
    }

    /// <summary>
    /// Prevents the server from sending a client a HUD message when energy is increased.
    /// </summary>
    [Mod(Mods.SniperPackets)]
    [HarmonyPatch(typeof(Player), "TargetAddHUDMessageEnergyIncreased")]
    public static class Player_TargetAddHUDMessageEnergyIncreased {
        public static bool Prefix(Player __instance) {
            if (!SniperPackets.enabled) return true;
            if (!GameplayManager.IsMultiplayerActive) return true;
            if (NetworkServer.active && !Tweaks.ClientHasMod(__instance.connectionToClient.connectionId)) return true;

            return false;
        }
    }

    /// <summary>
    /// Skip the portion in Player.UpdateInvul() where ship movement reduces your invuln time. 
    /// </summary>
    [Mod(Mods.SpawnInvulnerability)]
    [HarmonyPatch(typeof(Player), "UpdateInvul")]
    public static class Player_UpdateInvul {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes) {
            int state = 0;
            foreach (var code in codes) {
                if (code.opcode == OpCodes.Stloc_1) {
                    state++;
                    if (state == 2)
                        code.opcode = OpCodes.Pop;
                }

                yield return code;
            }
        }
    }

    /// <summary>
    /// Allows UseAmmo to be called on both server and client.
    /// </summary>
    [Mod(Mods.SniperPackets)]
    [HarmonyPatch(typeof(Player), "UseAmmo")]
    public static class Player_UseAmmo {
        public static bool Prefix(Player __instance, int amount) {
            if (!SniperPackets.enabled) return true;
            if (!GameplayManager.IsMultiplayerActive) return true;
            if (NetworkServer.active && !Tweaks.ClientHasMod(__instance.connectionToClient.connectionId)) return true;

            if (__instance.m_overdrive || Player.CheatUnlimited) {
                return false;
            }

            __instance.m_ammo = Mathf.Max(0, __instance.m_ammo - amount);
            return false;
        }
    }

    /// <summary>
    /// Allows UseEnergy to be called on both server and client.
    /// </summary>
    [Mod(Mods.SniperPackets)]
    [HarmonyPatch(typeof(Player), "UseEnergy")]
    public static class Player_UseEnergy {
        public static bool Prefix(Player __instance, float amount) {
            if (!SniperPackets.enabled) return true;
            if (!GameplayManager.IsMultiplayerActive) return true;
            if (NetworkServer.active && !Tweaks.ClientHasMod(__instance.connectionToClient.connectionId)) return true;

            if (__instance.m_overdrive || Player.CheatUnlimited) {
                return false;
            }

            __instance.m_energy = Mathf.Max(0f, __instance.m_energy - amount * Player.ENERGY_USAGE[__instance.m_upgrade_level[1]]);
            return false;
        }
    }
}
