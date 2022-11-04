using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using GameMod.Messages;
using GameMod.Metadata;
using GameMod.Objects;
using HarmonyLib;
using Overload;
using Overload_Vanilla = Overload; // Required to differentiate between Overload.NetworkManager and UnityEngine.Networking.NetworkManager, without Overload colliding with GameMod.Patches.Overload.
using UnityEngine;
using UnityEngine.Networking;

namespace GameMod.Patches.Overload {
    /// <summary>
    /// Reports damage to the tracker.
    /// </summary>
    [Mod(Mods.Tracker)]
    [HarmonyPatch(typeof(PlayerShip), "ApplyDamage")]
    public static class PlayerShip_ApplyDamage {
        public static bool Prepare() {
            return Config.Settings.Value<bool>("isServer") && !string.IsNullOrEmpty(Config.Settings.Value<string>("trackerBaseUrl"));
        }

        public static void Prefix(DamageInfo di, PlayerShip __instance) {
            if (!Overload_Vanilla.NetworkManager.IsHeadless() || di.damage == 0f || __instance.m_death_stats_recorded || __instance.m_cannot_die || __instance.c_player.m_invulnerable)
                return;

            var otherPlayer = di.owner?.GetComponent<Player>();

            float hitpoints = __instance.c_player.m_hitpoints;

            // Increase hitpoints by ratio of damage reduction so that we report the correct value.
            float reduction = Player.ARMOR_DAMAGE[__instance.c_player.m_upgrade_level[0]];
            if (di.type == DamageType.EXPLOSIVE && __instance.c_player.m_unlock_blast_damage) {
                reduction *= 0.8f;
            }
            hitpoints /= reduction;

            ProjPrefab weapon = di.weapon;

            float damage = di.damage;
            if (hitpoints - di.damage <= 0f)
                damage = hitpoints;
            Tracker.AddDamage(__instance.c_player, otherPlayer, weapon, damage);
        }
    }

    /// <summary>
    /// Sets the VR scale upon entering the game.
    /// </summary>
    [Mod(Mods.VRScale)]
    [HarmonyPatch(typeof(PlayerShip), "Awake")]
    public static class PlayerShip_Awake {
        public static bool Prepare() {
            return Switches.VREnabled;
        }

        public static void Postfix(PlayerShip __instance) {
            __instance.c_camera_transform.localScale = Vector3.one * VRScale.VR_Scale;
        }
    }

    /// <summary>
    /// This disables the ability of the server to control detonation of a devastator.
    /// </summary>
    [Mod(Mods.SniperPackets)]
    [HarmonyPatch(typeof(PlayerShip), "DetonatorInFlight")]
    public static class PlayerShip_DetonatorInFlight {
        public static bool Prepare() {
            return GameplayManager.IsDedicatedServer();
        }

        public static bool Prefix(PlayerShip __instance) {
            if (!SniperPackets.enabled) return true;
            if (!(GameplayManager.IsMultiplayerActive && NetworkServer.active)) return true;
            if (NetworkServer.active && !Tweaks.ClientHasMod(__instance.c_player.connectionToClient.connectionId)) return true;

            return false;
        }
    }

    /// <summary>
    /// If ScaleRespawnTime is set, automatically set respawn timer = player's team count.
    /// </summary>
    [Mod(Mods.ScaleRespawnTime)]
    [HarmonyPatch(typeof(PlayerShip), "DyingUpdate")]
    public static class PlayerShip_DyingUpdate {
        public static void MaybeUpdateDeadTimer(PlayerShip playerShip) {
            if (MPModPrivateData.ScaleRespawnTime) {
                playerShip.m_dead_timer = Overload_Vanilla.NetworkManager.m_Players.Count(x => x.m_mp_team == playerShip.c_player.m_mp_team && !x.m_spectator);
            }
        }

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes) {
            var playerShip_m_dead_timer_Field = AccessTools.Field(typeof(PlayerShip), "m_dead_timer");
            var mpTeams_PlayerShip_DyingUpdate_Method = AccessTools.Method(typeof(PlayerShip_DyingUpdate), "MaybeUpdateDeadTimer");

            int state = 0;
            foreach (var code in codes) {
                if (state == 0 && code.opcode == OpCodes.Stfld && code.operand == playerShip_m_dead_timer_Field) {
                    state = 1;
                    yield return code;
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Call, mpTeams_PlayerShip_DyingUpdate_Method);
                    continue;
                }

                yield return code;
            }
        }
    }

    /// <summary>
    /// Similar to MaybeFireWeapon, we redirect Projectile.PlayerFire to SniperPackets.MaybePlayerFire in order for the client to control where the flare gets fired from.
    /// </summary>
    [Mod(Mods.SniperPackets)]
    [HarmonyPatch(typeof(PlayerShip), "FireFlare")]
    public static class PlayerShip_FireFlare {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes) {
            foreach (var code in codes) {
                if (code.opcode == OpCodes.Call && ((MethodInfo)code.operand).Name == "PlayerFire") {
                    code.operand = AccessTools.Method(typeof(SniperPackets), "MaybePlayerFire");
                }

                yield return code;
            }
        }
    }

    /// <summary>
    /// Similar to MaybeFireWeapon, we redirect Server.IsActive to SniperPackets.AlwaysUseEnergy to instruct the clients to deduct energy for boosting, and not wait for the server to synchronize the energy count.
    /// </summary>
    [Mod(Mods.SniperPackets)]
    [HarmonyPatch(typeof(PlayerShip), "FixedUpdateProcessControlsInternal")]
    public static class PlayerShip_FixedUpdateProcessControlsInternal {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes) {
            foreach (var code in codes) {
                if (code.opcode == OpCodes.Call && code.operand is MethodInfo method && method.Name == "IsActive") {
                    code.operand = AccessTools.Method(typeof(SniperPackets), "AlwaysUseEnergy");
                }

                yield return code;
            }
        }
    }

    /// <summary>
    /// Similar to MaybeFireWeapon, we redirect Projectile.PlayerFire to SniperPackets.MaybePlayerFire in order for the client to control where the missile gets fired from.
    /// 
    /// We also want to try to switch to a new secondary on the client no matter what at the end of MaybeFireMissile.
    /// </summary>
    [Mod(Mods.SniperPackets)]
    [HarmonyPatch(typeof(PlayerShip), "MaybeFireMissile")]
    public static class PlayerShip_MaybeFireMissile {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes) {
            foreach (var code in codes) {
                if (code.opcode == OpCodes.Call && ((MethodInfo)code.operand).Name == "PlayerFire") {
                    code.operand = AccessTools.Method(typeof(SniperPackets), "MaybePlayerFire");
                }

                yield return code;
            }
        }
    }

    /// <summary>
    /// Updates where thunderbolt appears from when fired from a ship.
    /// 
    /// In base Overload, energy is only deducted from the player's total on the server, and then it synchronizes that energy amount to the client.  Instead, we are going to keep track of the energy on the client and sync it to the server.  Since everywhere where energy is used in this function check Server.IsActive, we instead redirect to our own function SniperPackets.AlwaysUseEnergy, which always returns true, and thus always deducts energy regardless as to whether it's on the server or the client.
    /// 
    /// In base Overload, the server simulates the position/rotation of each player's weapon fire.  Instead, we are going to let players decide the position/rotation of the weapon fire.  We replace the call to ProjectileManager.PlayerFire with our own call to SniperPackets.MaybePlayerFire that ensures that this simulation does not happen server side, and that when the client fires a weapon that it is synced to the server as a sniper packet.
    /// </summary>
    [Mod(new Mods[] { Mods.SniperPackets, Mods.ThunderboltBalance })]
    [HarmonyPatch(typeof(PlayerShip), "MaybeFireWeapon")]
    public static class PlayerShip_MaybeFireWeapon {

        [Mod(Mods.ThunderboltBalance)]
        public static Vector3 AdjustLeftPos(Vector3 muzzle_pos, Vector3 c_right) {
            return GameplayManager.IsMultiplayerActive ? muzzle_pos + c_right * ThunderboltBalance.m_muzzle_adjust : muzzle_pos;
        }

        [Mod(Mods.ThunderboltBalance)]
        public static Vector3 AdjustRightPos(Vector3 muzzle_pos, Vector3 c_right) {
            return GameplayManager.IsMultiplayerActive ? muzzle_pos + c_right * -ThunderboltBalance.m_muzzle_adjust : muzzle_pos;
        }

        [Mod(new Mods[] { Mods.SniperPackets, Mods.ThunderboltBalance })]
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes) {
            int state = 0;
            foreach (var code in codes) {
                // case WeaponType.THUNDERBOLT:
                if (state == 0 && code.opcode == OpCodes.Ldc_I4_S && (sbyte)code.operand == 27)
                    state++;

                // ProjectileManager.PlayerFire(this.c_player, type, this.m_muzzle_right.position, rot, this.m_thunder_power, this.c_player.m_weapon_level[(int)this.c_player.m_weapon_type], true, 0, -1);
                if (state == 1 && code.opcode == OpCodes.Callvirt && code.operand == AccessTools.Method(typeof(Transform), "get_position")) {
                    state++;
                    yield return code;
                    yield return new CodeInstruction(OpCodes.Ldloc_2);
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(PlayerShip_MaybeFireWeapon), "AdjustRightPos"));
                    continue;
                }

                // ProjectileManager.PlayerFire(this.c_player, type, this.m_muzzle_left.position, rot, this.m_thunder_power, this.c_player.m_weapon_level[(int)this.c_player.m_weapon_type], false, 1, -1);
                if (state == 2 && code.opcode == OpCodes.Callvirt && code.operand == AccessTools.Method(typeof(Transform), "get_position")) {
                    state++;
                    yield return code;
                    yield return new CodeInstruction(OpCodes.Ldloc_2);
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(PlayerShip_MaybeFireWeapon), "AdjustLeftPos"));
                    continue;
                }

                // this.m_thunder_sound_timer = 0f;
                if (code.opcode == OpCodes.Stfld && code.operand == AccessTools.Field(typeof(PlayerShip), "m_thunder_sound_timer")) {
                    yield return code;
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(ThunderboltBalance), "StopThunderboltSelfDamageLoop"));
                    continue;
                }

                // Always use energy on the client for sniper packets.
                if (code.opcode == OpCodes.Call && ((MethodInfo)code.operand).Name == "IsActive") {
                    code.operand = AccessTools.Method(typeof(SniperPackets), "AlwaysUseEnergy");
                }
                
                // Use the player's firing position/rotation for sniper packets.
                if (code.opcode == OpCodes.Call && ((MethodInfo)code.operand).Name == "PlayerFire") {
                    code.operand = AccessTools.Method(typeof(SniperPackets), "MaybePlayerFire");
                }

                yield return code;
            }
        }
    }

    /// <summary>
    /// Stops the overcharge sound when the player ship is destroyed.
    /// </summary>
    [Mod(Mods.ThunderboltBalance)]
    [HarmonyPatch(typeof(PlayerShip), "OnDestroy")]
    public static class PlayerShip_OnDestroy {
        public static void Postfix() {
            ThunderboltBalance.StopThunderboltSelfDamageLoop();
        }
    }

    /// <summary>
    /// Stops the overcharge sound when the player ship is disabled.
    /// </summary>
    [Mod(Mods.ThunderboltBalance)]
    [HarmonyPatch(typeof(PlayerShip), "OnDisable")]
    public static class PlayerShip_OnDisable {
        public static void Postfix() {
            ThunderboltBalance.StopThunderboltSelfDamageLoop();
        }
    }

    /// <summary>
    /// Here, we are attaching to the end of PlayerShip.ProcessFiringControls to synchronize a player's resource when they release the primary fire key and the boost key, two sources of frequent resource use.
    /// 
    /// We also use this method to mark when we have released the secondary fire key so that devastators don't explode in the player's face unless they purposely triggered them to.
    /// </summary>
    [Mod(Mods.SniperPackets)]
    [HarmonyPatch(typeof(PlayerShip), "ProcessFiringControls")]
    public static class PlayerShip_ProcessFiringControls {
        public static bool Prepare() {
            return !GameplayManager.IsDedicatedServer();
        }

        public static void Postfix(PlayerShip __instance) {
            if (!SniperPackets.enabled) return;

            if (__instance.c_player.isLocalPlayer) {
                if (__instance.c_player.JustReleased(CCInput.FIRE_WEAPON)) {
                    switch (__instance.c_player.m_weapon_type) {
                        case WeaponType.IMPULSE:
                        case WeaponType.CYCLONE:
                        case WeaponType.REFLEX:
                        case WeaponType.THUNDERBOLT:
                        case WeaponType.LANCER:
                            Client.GetClient().Send(MessageTypes.MsgPlayerSyncResource, new PlayerSyncResourceMessage {
                                m_player_id = __instance.c_player.netId,
                                m_type = PlayerSyncResourceMessage.ValueType.ENERGY,
                                m_value = __instance.c_player.m_energy
                            });
                            break;
                        case WeaponType.CRUSHER:
                        case WeaponType.DRILLER:
                        case WeaponType.FLAK:
                            Client.GetClient().Send(MessageTypes.MsgPlayerSyncResource, new PlayerSyncResourceMessage {
                                m_player_id = __instance.c_player.netId,
                                m_type = PlayerSyncResourceMessage.ValueType.AMMO,
                                m_value = __instance.c_player.m_ammo
                            });
                            break;
                    }
                }

                if (__instance.c_player.JustReleased(CCInput.USE_BOOST)) {
                    Client.GetClient().Send(MessageTypes.MsgPlayerSyncResource, new PlayerSyncResourceMessage {
                        m_player_id = __instance.c_player.netId,
                        m_type = PlayerSyncResourceMessage.ValueType.ENERGY,
                        m_value = __instance.c_player.m_energy
                    });
                }

                if (__instance.c_player.isLocalPlayer && __instance.c_player.JustReleased(CCInput.FIRE_MISSILE)) {
                    SniperPackets.justFiredDev = false;
                }
            }
        }
    }

    /// <summary>
    /// Similar to MaybeFireWeapon, we redirect Server.IsActive to SniperPackets.AlwaysUseEnergy to instruct the clients to deduct energy for a flare, and not wait for the server to synchronize the energy count.
    /// </summary>
    [Mod(Mods.SniperPackets)]
    [HarmonyPatch(typeof(PlayerShip), "ProcessFlareAndHeadlightControls")]
    public static class PlayerShip_ProcessFlareAndHeadlightControls {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes) {
            foreach (var code in codes) {
                if (code.opcode == OpCodes.Call && ((MethodInfo)code.operand).Name == "IsActive") {
                    code.operand = AccessTools.Method(typeof(SniperPackets), "AlwaysUseEnergy");
                }

                yield return code;
            }
        }
    }

    /// <summary>
    /// Do not spew the lancer.
    /// </summary>
    [Mod(Mods.ReduceSpewedMissiles)]
    [HarmonyPatch(typeof(PlayerShip), "SpewItemsOnDeath")]
    public static class PlayerShip_SpewItemsOnDeath {
        public static bool Prepare() {
            return GameplayManager.IsDedicatedServer();
        }

        public static void Prefix(PlayerShip __instance) {
            if (!Overload_Vanilla.NetworkManager.IsServer()) {
                return;
            }

            __instance.c_player.m_weapon_picked_up[(int)WeaponType.LANCER] = false;
        }
    }

    /// <summary>
    /// When the player dies, we need to sync our missile inventory with the server so it knows what to spew.
    /// </summary>
    [Mod(Mods.SniperPackets)]
    [HarmonyPatch(typeof(PlayerShip), "StartDying")]
    public static class PlayerShip_StartDying {
        public static bool Prepare() {
            return !GameplayManager.IsDedicatedServer();
        }

        public static void Postfix(PlayerShip __instance) {
            if (!SniperPackets.enabled) return;

            if (Overload_Vanilla.NetworkManager.IsMultiplayerSceneLoaded() && !NetworkMatch.InGameplay()) {
                return;
            }

            if (__instance.c_player.isLocalPlayer) {
                var missiles = __instance.c_player.m_missile_ammo.Select(a => (int)a).ToArray();
                Client.GetClient().Send(MessageTypes.MsgPlayerSyncAllMissiles, new PlayerSyncAllMissilesMessage {
                    m_player_id = __instance.c_player.netId,
                    m_missile_ammo = missiles
                });
            }
        }
    }

    /// <summary>
    /// Self damage and charge time
    /// </summary>
    [Mod(Mods.ThunderboltBalance)]
    [HarmonyPatch(typeof(PlayerShip), "ThunderCharge")]
    public static class PlayerShip_ThunderCharge {
        public static void Prefix(PlayerShip __instance) {
            if (__instance.m_refire_time <= 0f && __instance.m_thunder_power == 0f)
                ThunderboltBalance.StopThunderboltSelfDamageLoop();
        }

        // Audio cue once charged
        public static void Postfix(PlayerShip __instance) {
            if (__instance.isLocalPlayer && __instance.m_thunder_power >= 2f && ThunderboltBalance.m_charge_loop_index == -1) {
                ThunderboltBalance.m_charge_loop_index = GameManager.m_audio.PlayCue2DLoop((int)SoundEffect.cine_sfx_warning_loop, 1f, 0f, 0f, true);
            }
        }

        // Override self-damage ramping
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes) {
            foreach (var code in codes) {
                // di.damage = ((this.m_thunder_power <= 3f) ? (this.m_thunder_power - 2f) : 1f) * num;
                if (code.opcode == OpCodes.Stfld && code.operand == AccessTools.Field(typeof(DamageInfo), "damage")) {
                    yield return code;
                    yield return new CodeInstruction(OpCodes.Ldloca_S, 2);
                    yield return new CodeInstruction(OpCodes.Ldloc_0);
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(ThunderboltBalance), "GetSelfChargeDamage"));
                    yield return new CodeInstruction(OpCodes.Stfld, AccessTools.Field(typeof(DamageInfo), "damage"));
                    continue;
                }

                yield return code;
            }
        }
    }

    /// <summary>
    /// Stops the overcharge sound if it no longer applies, and shows edge glow in the correct color.
    /// </summary>
    [Mod(new Mods[] { Mods.Teams, Mods.ThunderboltBalance })]
    [HarmonyPatch(typeof(PlayerShip), "Update")]
    public static class PlayerShip_Update {
        [Mod(Mods.Teams)]
        public static Material LoadDamageMaterial(PlayerShip player_ship) {
            if (GameplayManager.IsMultiplayerActive && !GameplayManager.IsDedicatedServer() && NetworkMatch.IsTeamMode(NetworkMatch.GetMode())) {
                Material m = new Material(UIManager.gm.m_damage_material);
                var teamcolor = UIManager.ChooseMpColor(player_ship.c_player.m_mp_team);
                m.SetColor("_EdgeColor", teamcolor);
                return m;
            } else {
                return UIManager.gm.m_damage_material;
            }
        }

        /// <summary>
        /// Damage glow in team color
        /// </summary>
        /// <param name="__instance"></param>
        [Mod(new Mods[] { Mods.Teams, Mods.ThunderboltBalance })]
        public static void Postfix(PlayerShip __instance) {
            if (GameplayManager.IsMultiplayerActive && !GameplayManager.IsDedicatedServer() && NetworkMatch.IsTeamMode(NetworkMatch.GetMode())) {
                if ((__instance.m_boosting || __instance.m_dead || __instance.m_dying) && GameplayManager.IsMultiplayerActive && __instance.isLocalPlayer) {
                    ThunderboltBalance.StopThunderboltSelfDamageLoop();
                }

                var teamcolor = UIManager.ChooseMpColor(__instance.c_player.m_mp_team);
                foreach (var mat in __instance.m_materials) {
                    // Main damage color
                    if (mat.shader != null) {
                        if ((Color)mat.GetVector("_color_burn") == teamcolor) {
                            return;
                        }
                        mat.SetVector("_color_burn", teamcolor);
                    }

                    // Light color (e.g. TB overcharge)
                    __instance.c_lights[4].color = teamcolor;
                }
            }
        }

        /// <summary>
        /// UIManager.gm.m_damage_material is a global client field for heavy incurred damage/TB overcharge, patch to call our LoadDamageMaterial() instead
        /// </summary>
        /// <param name="codes"></param>
        /// <returns></returns>
        [Mod(Mods.Teams)]
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes) {
            foreach (var code in codes) {
                if (code.opcode == OpCodes.Ldfld && code.operand == AccessTools.Field(typeof(GameManager), "m_damage_material")) {
                    yield return new CodeInstruction(OpCodes.Pop); // Remove previous ldsfld    class Overload.GameManager Overload.UIManager::gm, cheap enough to keep transpiler simpler
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(PlayerShip_Update), "LoadDamageMaterial"));
                    continue;
                }

                yield return code;
            }
        }
    }

    /// <summary>
    /// Enables the keybind for the previous weapon.
    /// </summary>
    [Mod(Mods.PreviousWeaponFix)]
    [HarmonyPatch(typeof(PlayerShip), "UpdateReadImmediateControls")]
    public static class PlayerShip_UpdateReadImmediateControls {
        public static bool Prepare() {
            return !GameplayManager.IsDedicatedServer();
        }

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes) {
            foreach (var code in codes) {
                // Before if (Controls.IsPressed(CCInput.SWITCH_MISSILE))
                if (code.opcode == OpCodes.Ldc_I4_S && (sbyte)code.operand == 17) {
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(PlayerShip_UpdateReadImmediateControls), "PrevWeaponUpdate"));
                }

                yield return code;
            }
        }

        public static void PrevWeaponUpdate(PlayerShip player) {
            player.c_player.CallCmdSetCurrentWeapon(player.c_player.m_weapon_type);
        }
    }

    /// <summary>
    /// Updates the ship rim color, which is Team0/Team1/Anarchy dependent.
    /// </summary>
    [Mod(Mods.Teams)]
    [HarmonyPatch(typeof(PlayerShip), "UpdateRimColor")]
    public static class PlayerShip_UpdateRimColor {
        public static bool Prepare() {
            return !GameplayManager.IsDedicatedServer();
        }

        public static bool Prefix(PlayerShip __instance) {
            Color c = UIManager.ChooseMpColor(__instance.c_player.m_mp_team);
            __instance.m_mp_rim_color.r = c.r * 0.25f;
            __instance.m_mp_rim_color.g = c.g * 0.25f;
            __instance.m_mp_rim_color.b = c.b * 0.25f;
            __instance.m_update_mp_color = true;
            return false;
        }
    }

    /// <summary>
    /// Updates the ship colors.
    /// </summary>
    [Mod(Mods.Teams)]
    [HarmonyPatch(typeof(PlayerShip), "UpdateShipColors")]
    public static class PlayerShip_UpdateShipColors {
        public static bool Prepare() {
            return !GameplayManager.IsDedicatedServer();
        }

        public static void Prefix(ref MpTeam team, ref int glow_color, ref int decal_color) {
            if (team == MpTeam.ANARCHY)
                return;

            glow_color = decal_color = Teams.TeamColorIdx(team);
            team = MpTeam.ANARCHY; // prevent original team color assignment
        }
    }

}
