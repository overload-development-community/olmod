using Harmony;
using Overload;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;

// by terminal
namespace GameMod
{
    //Increases mass/drag on Monsterball, enables collision with wind tunnels and other potentially useful layers
    [HarmonyPatch(typeof(MonsterBall), "Awake")]
    class MonsterballEnableWeaponCollision
    {
        private static void Postfix(MonsterBall __instance)
        {
            Physics.IgnoreLayerCollision(31, 13, false);
            Physics.IgnoreLayerCollision(31, 22, false);
            Physics.IgnoreLayerCollision(31, 23, false);
            Physics.IgnoreLayerCollision(31, 24, false);
            Physics.IgnoreLayerCollision(31, 31, false);
            __instance.c_rigidbody.drag = 1f;
            __instance.c_rigidbody.mass *= 20f;
        }
    }

    //On collision, play sound and balance weapons
    [HarmonyPatch(typeof(MonsterBall), "OnCollisionEnter")]
    class MonsterballPlayBounceSound
    {
        private static void Prefix(Collision collision, MonsterBall __instance)
        {
            float d = 1f;
            SFXCueManager.PlayRawSoundEffectPos(SoundEffect.imp_force_field1, collision.contacts[0].point, 0.4f, UnityEngine.Random.Range(-0.3f, 0f), 0f);
            GameObject gameObject = collision.collider.gameObject;
            Projectile proj = gameObject.GetComponent<Projectile>();
            if (proj == null || proj.m_type == ProjPrefab.none)
                return;
            var mb = __instance;
            Vector3 forward = proj.c_transform.forward;
            if (proj.m_type == ProjPrefab.proj_impulse)
            {
                mb.c_rigidbody.AddForce(forward.normalized * 50f * d, ForceMode.Impulse);
            }
            if (proj.m_type == ProjPrefab.proj_driller)
            {
                Vector3 vector = ((mb.transform.position - collision.transform.position).normalized + forward.normalized) / 2f;
                mb.c_rigidbody.AddForce(vector.normalized * 35f * d, ForceMode.Impulse);
            }
            if (proj.m_type == ProjPrefab.proj_shotgun)
            {
                Vector3 vector2 = ((mb.transform.position - collision.transform.position).normalized + forward.normalized) / 2f;
                mb.c_rigidbody.AddForce(vector2.normalized * 8f * d, ForceMode.Impulse);
            }
            if (proj.m_type == ProjPrefab.proj_flak_cannon)
            {
                mb.c_rigidbody.AddForce(forward.normalized * 20f * d, ForceMode.Impulse);
            }
            if (proj.m_type == ProjPrefab.proj_reflex)
            {
                Vector3 vector3 = ((mb.transform.position - collision.transform.position).normalized + forward.normalized) / 2f;
                mb.c_rigidbody.AddForce(vector3.normalized * 40f * d, ForceMode.Impulse);
            }
            if (proj.m_type == ProjPrefab.proj_beam)
            {
                Vector3 vector4 = ((mb.transform.position - collision.transform.position).normalized + forward.normalized) / 2f;
                mb.c_rigidbody.AddForce(vector4.normalized * 120f * d, ForceMode.Impulse);
            }
            if (proj.m_type == ProjPrefab.missile_smart || proj.m_type == ProjPrefab.missile_smart_mini || proj.m_type == ProjPrefab.missile_timebomb || proj.m_type == ProjPrefab.missile_vortex)
            {
                Vector3 vector5 = ((mb.transform.position - collision.transform.position).normalized + forward.normalized) / 2f;
                mb.c_rigidbody.AddForce(vector5.normalized * 500f * d, ForceMode.Impulse);
            }
            if (proj.m_type == ProjPrefab.missile_devastator)
            {
                Vector3 vector6 = ((mb.transform.position - collision.transform.position).normalized + forward.normalized) / 2f;
                mb.c_rigidbody.AddForce(vector6.normalized * 2200f * d, ForceMode.Impulse);
            }
            if (proj.m_type == ProjPrefab.missile_devastator_mini)
            {
                Vector3 normalized = (mb.transform.position - collision.transform.position).normalized;
                mb.c_rigidbody.AddForce(normalized * 1000f * d, ForceMode.Impulse);
            }
            if (proj.m_type == ProjPrefab.missile_falcon)
            {
                mb.c_rigidbody.AddForce(forward.normalized * 200f * d, ForceMode.Impulse);
            }
            if (proj.m_type == ProjPrefab.missile_hunter)
            {
                mb.c_rigidbody.AddForce(forward.normalized * 100f * d, ForceMode.Impulse);
            }
            if (proj.m_type == ProjPrefab.missile_pod)
            {
                mb.c_rigidbody.AddForce(forward.normalized * 50f * d, ForceMode.Impulse);
            }
            if (proj.m_type == ProjPrefab.missile_creeper)
            {
                mb.c_rigidbody.AddForce(forward.normalized * 100f * d, ForceMode.Impulse);
            }
        }
    }

    //enforce speed cap on ball (disabled: causes linux problems, no longer needed with increased ball mass)
    [HarmonyPatch(typeof(MonsterBall), "Update")]
    class MonsterballSpeedCap
    {
        private static void Postfix(MonsterBall __instance)
        {
            //__instance.c_rigidbody.velocity = Vector3.ClampMagnitude(__instance.c_rigidbody.velocity, 25f);
        }
    }

    //disable various monsterball-layer collisions we enabled at the start of the match
    [HarmonyPatch(typeof(NetworkManager), "OnSceneUnloaded")]
    class MonsterballDisableWeaponCollision
    {
        private static void Postfix()
        {
            Physics.IgnoreLayerCollision(31, 13, true);
            Physics.IgnoreLayerCollision(31, 22, true);
            Physics.IgnoreLayerCollision(31, 23, true);
            Physics.IgnoreLayerCollision(31, 24, true);
            Physics.IgnoreLayerCollision(31, 31, true);
        }
    }

    //collide thunderbolt with monsterball, Thunderbolt pushes ball farther if charged
    [HarmonyPatch(typeof(Projectile), "OnTriggerEnter")]
    class MonsterballCollideThunderbolt
    {
        private static bool Prefix(Projectile __instance, Collider other, float ___m_strength)
        {
            if (!other || other.isTrigger)
            {
                return true;
            }
            var proj = __instance;
            if (NetworkMatch.GetMode() == MatchMode.MONSTERBALL && proj.m_type == ProjPrefab.proj_thunderbolt && other.gameObject.layer == 31)
            {
                Vector3 vector = other.transform.position - proj.transform.position;
                other.attachedRigidbody.AddForce(vector.normalized * 340f * ___m_strength + vector.normalized * 60f, ForceMode.Impulse);
                ParticleElement particleElement = ParticleManager.psm[3].StartParticle((int)proj.m_death_particle_default, proj.c_transform.localPosition, proj.c_transform.localRotation, null, null, false);
                particleElement.SetExplosionOwner(proj.m_owner);
                particleElement.SetParticleScaleAndSimSpeed(1f + ___m_strength * 0.25f, 1f - ___m_strength * 0.15f);
                GameManager.m_audio.PlayCuePos(255, proj.c_transform.localPosition, 0.7f, UnityEngine.Random.Range(-0.15f, 0.15f), 0f, 1f);
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(Player), "OnKilledByPlayer")]
    class MonsterballDisableKillScore
    {
        public static void MaybeAddPointForTeam(MpTeam team)
        {
            if (NetworkMatch.GetMode() != MatchMode.MONSTERBALL)
                NetworkMatch.AddPointForTeam(team);
        }

        public static void MaybeSubtractPointForTeam(MpTeam team)
        {
            if (NetworkMatch.GetMode() != MatchMode.MONSTERBALL)
                NetworkMatch.SubtractPointForTeam(team);
        }

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
        {
            foreach (var code in codes)
            {
                if (code.opcode == OpCodes.Call && ((MethodInfo)code.operand).Name == "AddPointForTeam")
                    code.operand = AccessTools.Method(typeof(MonsterballDisableKillScore), "MaybeAddPointForTeam");
                if (code.opcode == OpCodes.Call && ((MethodInfo)code.operand).Name == "SubtractPointForTeam")
                    code.operand = AccessTools.Method(typeof(MonsterballDisableKillScore), "MaybeSubtractPointForTeam");
                yield return code;
            }
        }
    }

    //Disable scoring during Postgame state
    [HarmonyPatch(typeof(NetworkMatch), "MaybeEndDeathPause")]
    internal class MonsterballDisablePostgameScore
    {
        private static void Prefix()
        {
            if (NetworkMatch.GetMode() == MatchMode.MONSTERBALL)
            {
                UnityEngine.Object.DestroyObject(NetworkMatch.m_monsterball);
            }
        }
    }

    //boost ball while it's in wind tunnel
    [HarmonyPatch(typeof(TriggerWindTunnel), "OnTriggerStay")]
    internal class MonsterballWind
    {
        private static void Prefix(Collider other, TriggerWindTunnel __instance)
        {
            MonsterBall componentInParent = other.GetComponentInParent<MonsterBall>();
            if (componentInParent)
            {
                componentInParent.c_rigidbody.AddForce(__instance.transform.forward * (componentInParent.c_rigidbody.mass * 80f));
            }
        }
    }
}
