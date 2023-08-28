using HarmonyLib;
using Overload;
using System.Reflection;
using UnityEngine;

namespace GameMod
{
    public class Nova : SecondaryWeapon
    {
        static float[] bomblet_angle1 = new float[2] { 60f, 130f };
        static float[] bomblet_angle2a = new float[3] { 50f, 100f, 150f };
        static float[] bomblet_angle2b = new float[3] { 20f, 40f, 60f };

        public Nova()
        {
            displayName = "NOVA";
            displayNamePlural = "NOVAS";
            Tag2A = "XB";
            Tag2B = "CL";
            icon_idx = (int)AtlasIndex0.MISSILE_NOVA1;
            projprefab = ProjPrefabExt.missile_smart;
            subproj = ProjPrefabExt.missile_smart_mini;
            ammo = 6;
            ammoUp = 10;
            ammoSuper = 3;
            firingMode = FiringMode.SEMI_AUTO;
            ExplodeSync = true;
        }

        public override void Fire(float refire_multiplier)
        {
            //Vector3 c_right = ship.c_right;
            //Vector3 c_up = ship.c_up;
            //Vector3 c_forward = ship.c_forward;
            Quaternion localRotation = ps.c_transform.localRotation;

            WeaponUnlock level = player.m_missile_level[(int)player.m_missile_type];

            MPSniperPackets.MaybePlayerFire(player, (ProjPrefab)projprefab, ps.m_muzzle_center2.position, localRotation, 0f, level);
            ps.m_refire_missile_time += 0.4f;
            player.PlayCameraShake(CameraShakeType.FIRE_NOVA, 1f, 1f);
        }

        public override void DrawHUDReticle(Vector2 pos, float m_alpha)
        {
            Vector2 temp_pos = new Vector2();

            Color c = Color.Lerp(UIManager.m_col_ui6, UIManager.m_col_ui7, UnityEngine.Random.value * UIElement.FLICKER);
            Color c2 = UIManager.m_col_ui2;

            temp_pos.x = pos.x;
            temp_pos.y = pos.y + 48f;
            UIManager.DrawQuadUIRotated(temp_pos, 10.8f, 10.8f, (float)System.Math.PI / 4f, c2, m_alpha, 35);
            if (GameManager.m_local_player.CanFireMissileAmmo())
            {
                UIManager.DrawQuadUIRotated(temp_pos, 6f, 6f, (float)System.Math.PI / 4f, c, m_alpha, 4);
            }
        }

        public override bool ProjectileFire(Projectile proj, Vector3 pos, Quaternion rot, ref int m_bounces, ref float m_damage, ref FXWeaponExplosion m_death_particle_override, ref float m_init_speed, ref float m_lifetime, ref float m_homing_cur_strength, ref float m_push_force, ref float m_push_torque, ref WeaponUnlock m_upgrade, bool save_pos, ref float m_strength, ref float m_vel_inherit)
        {
            if (proj.m_type == (ProjPrefab)projprefab)
            {
                proj.c_rigidbody.angularDrag = 0.01f;
                proj.c_rigidbody.AddRelativeTorque(0f, 0f, 1200f);
                if (m_upgrade >= WeaponUnlock.LEVEL_1)
                {
                    if (!GameplayManager.IsMultiplayerActive)
                    {
                        m_init_speed *= 1.3f;
                    }
                    m_damage *= 1.3f;
                }
                if (m_upgrade == WeaponUnlock.LEVEL_2B)
                {
                    m_lifetime = UnityEngine.Random.Range(0.28f, 0.3f);
                    m_vel_inherit += 0.4f;
                }
            }
            else // subproj
            {
                //Debug.Log("CCF ProjectileFire called on subprojectile for Nova");
                if (proj.m_team == ProjTeam.ENEMY)
                {
                    proj.m_lockon_sound = false;
                    proj.m_firing_sfx = SFXCue.weapon_reflex_lvl2B;
                }
                if (m_upgrade == WeaponUnlock.LEVEL_2B)
                {
                    proj.m_homing_strength *= 1.8f;
                    m_init_speed *= 1.5f;
                    m_damage *= 1.25f;
                }
                else if (m_upgrade >= WeaponUnlock.LEVEL_1)
                {
                    if (m_upgrade == WeaponUnlock.LEVEL_2A)
                    {
                        m_damage *= 1.25f;
                    }
                    proj.m_homing_strength *= 1.25f;
                    m_init_speed *= 1.2f;
                }
            }

            return false;
        }

        public override void Explode(Projectile proj, bool damaged_something, FXWeaponExplosion m_death_particle_override, float strength, WeaponUnlock m_upgrade)
        {
            base.Explode(proj, damaged_something, m_death_particle_override, strength, m_upgrade);

            if (proj.m_type == (ProjPrefab)projprefab)
            {
                SFXCueManager.PlayCuePos(SFXCue.exp_nova_wash, proj.c_transform.localPosition, UnityEngine.Random.Range(0.9f, 1f), UnityEngine.Random.Range(-0.05f, 0.05f), false, UnityEngine.Random.Range(0.5f, 0.6f));
                Quaternion rotation = proj.c_transform.rotation;

                if (m_upgrade == WeaponUnlock.LEVEL_2B)
                {
                    for (int k = 0; k < 6; k++)
                    {
                        float num3 = 90f + (float)k / 6f * 360f;
                        for (int l = 0; l < 3; l++)
                        {
                            float num4 = bomblet_angle2b[l];
                            Quaternion rot = RUtility.AngleSpreadXZ(rotation, num4 + UnityEngine.Random.Range(-10f, 10f), num3 + UnityEngine.Random.Range(-15f, 15f));
                            rot = proj.RotationTowardsNearbyVisibleEnemy(proj.c_transform.localPosition, rot, 25f, 0.75f, -1f, 0.1f);
                            if (Server.IsActive() && (bool)proj.m_owner_player)
                            {
                                //Debug.Log("CCF Calling PlayerFire for Nova subprojectile");
                                ProjectileManager.PlayerFire(proj.m_owner_player, (ProjPrefab)subproj, proj.c_transform.localPosition, rot, 0f, m_upgrade);
                            }
                        }
                    }
                }
                else
                {
                    float[] array = ((m_upgrade != WeaponUnlock.LEVEL_2A) ? bomblet_angle1 : bomblet_angle2a);
                    int num5 = array.Length;
                    for (int m = 0; m < 5; m++)
                    {
                        float num6 = 90f + (float)m / 5f * 360f;
                        for (int n = 0; n < num5; n++)
                        {
                            float num7 = array[n];
                            Quaternion rot = RUtility.AngleSpreadXZ(rotation, num7 + UnityEngine.Random.Range(-15f, 15f), num6 + UnityEngine.Random.Range(-25f, 25f));
                            rot = proj.RotationTowardsNearbyVisibleEnemy(proj.c_transform.localPosition, rot, 30f, 0.88f);
                            if (Server.IsActive() && (bool)proj.m_owner_player)
                            {
                                ProjectileManager.PlayerFire(proj.m_owner_player, (ProjPrefab)subproj, proj.c_transform.localPosition, rot, 0f, m_upgrade);
                            }
                        }
                    }
                }
            }
        }

        public override void ProcessCollision(Projectile proj, GameObject collider, Vector3 collision_normal, int layer, ref bool m_bounce_allow, ref int m_bounces, ref Transform m_cur_target, ref Player m_cur_target_player, ref Robot m_cur_target_robot, ref float m_damage, ref float m_lifetime, ref float m_target_timer, ParticleElement m_trail_effect_pe)
        {
            if ((int)proj.m_type == (int)projprefab)
            {
                base.ProcessCollision(proj, collider, collision_normal, layer, ref m_bounce_allow, ref m_bounces, ref m_cur_target, ref m_cur_target_player, ref m_cur_target_robot, ref m_damage, ref m_lifetime, ref m_target_timer, m_trail_effect_pe);
            }
            else // subproj
            {
                if (proj.ShouldPlayDamageEffect(layer) || m_lifetime < 3.3f || m_bounces == proj.m_bounce_max_count)
                {
                    if (GameManager.m_audio.CanPlaySoundWithCooldown(SoundEffect.wep_missile_nova_orb_imp1, 0.02f))
                    {
                        SFXCueManager.PlayCuePos(SFXCue.impact_nova_orb, proj.c_transform.localPosition, UnityEngine.Random.Range(0.9f, 1f), UnityEngine.Random.Range(-0.05f, 0.05f));
                    }
                    proj.Explode(m_bounces != proj.m_bounce_max_count);
                }
            }
        }
    }
}
