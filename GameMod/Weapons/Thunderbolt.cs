using HarmonyLib;
using Overload;
using System.Reflection;
using UnityEngine;


namespace GameMod
{
    public class Thunderbolt : Weapon
    {
        FieldInfo m_thunder_sound_timer_Field = AccessTools.Field(typeof(PlayerShip), "m_thunder_sound_timer"); // float

        private int m_charge_loop_index = -1;
        private float m_tb_overchargedamage_multiplier = 4f; // 4.0dps self-damage instead of stock 1.0dps)
                                                                    //public static float m_muzzle_adjust = 0.2f; // Projectile exit point offsets -- now handled by the definitions in MPShips using local object scaling

        public Thunderbolt(Ship s)
        {
            displayName = "THUNDERBOLT";
            ship = s;
        }

        public override void Fire(Player player, float refire_multiplier)
        {
            ProjPrefab type = ProjPrefab.proj_thunderbolt;
            Quaternion localRotation = player.c_player_ship.c_transform.localRotation;
            player.c_player_ship.m_thunder_power = Mathf.Min((player.m_weapon_level[(int)player.m_weapon_type] != WeaponUnlock.LEVEL_2A) ? 1f : 1.15f, player.c_player_ship.m_thunder_power);
            // originally ProjectileManager.PlayerFire()
            MPSniperPackets.MaybePlayerFire(player, type, player.c_player_ship.m_muzzle_right.position, localRotation, player.c_player_ship.m_thunder_power, player.m_weapon_level[(int)player.m_weapon_type], true, 0);
            MPSniperPackets.MaybePlayerFire(player, type, player.c_player_ship.m_muzzle_left.position, localRotation, player.c_player_ship.m_thunder_power, player.m_weapon_level[(int)player.m_weapon_type], false, 1);
            if (GameplayManager.IsMultiplayerActive)
            {
                player.c_player_ship.m_refire_time += 0.5f * refire_multiplier;
            }
            else
            {
                player.c_player_ship.m_refire_time += ((player.m_weapon_level[(int)player.m_weapon_type] != WeaponUnlock.LEVEL_2A) ? 0.45f : 0.5f) * refire_multiplier;
            }
            if (!GameplayManager.IsMultiplayer)
            {
                player.c_player_ship.c_rigidbody.AddForce(player.c_player_ship.c_forward * (UnityEngine.Random.Range(-300f, -350f) * (0.5f + player.c_player_ship.m_thunder_power * 1.2f) * player.c_player_ship.c_rigidbody.mass * RUtility.FIXED_FT_INVERTED));
                Vector3 vector4 = RUtility.RandomUnitVector();
                player.c_player_ship.c_rigidbody.AddTorque((vector4 + UnityEngine.Random.onUnitSphere * 0.2f) * (UnityEngine.Random.Range(1000f, 1500f) * (0.5f + player.c_player_ship.m_thunder_power * 1.2f) * RUtility.FIXED_FT_INVERTED));
            }
            player.PlayCameraShake(CameraShakeType.FIRE_THUNDER, 1f + player.c_player_ship.m_thunder_power * 2f, 1f + player.c_player_ship.m_thunder_power);
            if (MPSniperPackets.AlwaysUseEnergy())
            {
                player.UseEnergy(2f + player.c_player_ship.m_thunder_power * 3f);
            }
            player.c_player_ship.m_thunder_power = 0f;
            m_thunder_sound_timer_Field.SetValue(player.c_player_ship, 0f);

            MPWeaponBehavior.Thunderbolt.StopThunderboltSelfDamageLoop();
            /*
            if (m_charge_loop_index != -1)
            {
                GameManager.m_audio.StopSound(m_charge_loop_index);
                m_charge_loop_index = -1;
            }
            */
        }
    }
}