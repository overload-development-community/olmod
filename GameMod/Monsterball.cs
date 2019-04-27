using Harmony;
using Overload;
using System.Collections.Generic;
using System.Reflection.Emit;
using UnityEngine;

// by terminal
namespace GameMod
{
    //postfix weapon collision to Monsterball Awake()
    //also add more drag to the ball
    [HarmonyPatch(typeof(MonsterBall), "Awake")]
    class MonsterballEnableWeaponCollision
    {
        static void Postfix(MonsterBall __instance)
        {
            Physics.IgnoreLayerCollision(31, 13, false);
            __instance.c_rigidbody.drag = 0.5f;
        }
    }

    //play sound when ball bounces
    [HarmonyPatch(typeof(MonsterBall), "OnCollisionEnter")]
    class MonsterballPlayBounceSound
    {
        static void Prefix(Collision collision)
        {
            SFXCueManager.PlayRawSoundEffectPos(SoundEffect.imp_force_field1, collision.contacts[0].point, 0.4f, UnityEngine.Random.Range(-0.3f, 0f), 0f);
        }
    }

    //enforce speed cap on ball
    [HarmonyPatch(typeof(MonsterBall), "Update")]
    class MonsterballSpeedCap
    {
        static void Postfix(MonsterBall __instance)
        {
            __instance.c_rigidbody.velocity = Vector3.ClampMagnitude(__instance.c_rigidbody.velocity, 25f);
        }
    }

    //reset the monsterball weapon collision once match is over
    [HarmonyPatch(typeof(NetworkManager), "OnSceneUnloaded")]
    class MonsterballDisableWeaponCollision
    {
        static void Postfix()
        {
            Physics.IgnoreLayerCollision(31, 13, true);
        }
    }

    [HarmonyPatch(typeof(NetworkMatch), "SubtractPointForTeam")]
    class MonsterballDisableSuicidePenalty
    {
        //make suicides not count as -1 score in Monsterball
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> codes = new List<CodeInstruction>(instructions);
            object operand = codes[1].operand;
            codes[1] = new CodeInstruction(OpCodes.Ldc_I4_1, null);
            CodeInstruction insertedCode = new CodeInstruction(OpCodes.Bne_Un, operand);
            codes.Insert(2, insertedCode);
            return codes;
        }
    }

    // collide thunderbolt with monsterball
    [HarmonyPatch(typeof(Projectile), "OnTriggerEnter")]
    class MonsterballCollideThunderbolt
    {
        static bool Prefix(Projectile __instance, Collider other, float ___m_strength)
        {
            if (!other || other.isTrigger)
            {
                return true;
            }
            var proj = __instance;
            if (NetworkMatch.GetMode() == MatchMode.MONSTERBALL && proj.m_type == ProjPrefab.proj_thunderbolt && other.gameObject.layer == 31)
            {
                Vector3 a = other.transform.position - proj.transform.position;
                other.attachedRigidbody.AddForce(a * 10f, ForceMode.Impulse);
                ParticleElement particleElement2 = ParticleManager.psm[3].StartParticle((int)proj.m_death_particle_default, proj.c_transform.localPosition, proj.c_transform.localRotation, null, null, false);
                particleElement2.SetExplosionOwner(proj.m_owner);
                particleElement2.SetParticleScaleAndSimSpeed(1f + ___m_strength * 0.25f, 1f - ___m_strength * 0.15f);
                GameManager.m_audio.PlayCuePos(255, proj.c_transform.localPosition, 0.7f, UnityEngine.Random.Range(-0.15f, 0.15f), 0f, 1f);
                return false;
            }
            return true;
        }
    }
}
