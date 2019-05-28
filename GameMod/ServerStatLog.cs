using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using System.Linq;
using Harmony;
using Newtonsoft.Json;
using Overload;
using UnityEngine;

// by luponix

namespace GameMod
{
    public static class ServerStatLog
    {
        private struct PlayerPlayerWeaponDamage
        {
            public Player Attacker, Defender;
            public ProjPrefab Weapon;
        }

        private struct Kill
        {
            public DateTime Time;
            public string Attacker, Defender, Assisted;
            public ProjPrefab Weapon;
        }

        private static Dictionary<PlayerPlayerWeaponDamage, float> DamageTable = new Dictionary<PlayerPlayerWeaponDamage, float>();
        private static List<Kill> Kills = new List<Kill>();
        private static string Attacker, Defender, Assisted;
        public static DateTime StartTime, EndTime;

        public static void CleanUp()
        {
            DamageTable = new Dictionary<PlayerPlayerWeaponDamage, float>();
            Kills = new List<Kill>();
        }

        public static void WriteDamageTable(JsonWriter writer)
        {
            writer.WriteStartArray();
            foreach (var entry in DamageTable)
            {
                writer.WriteStartObject();
                writer.WritePropertyName("attacker");
                writer.WriteValue(entry.Key.Attacker.m_mp_name);
                writer.WritePropertyName("defender");
                writer.WriteValue(entry.Key.Defender.m_mp_name);
                writer.WritePropertyName("weapon");
                writer.WriteValue(entry.Key.Weapon.ToString());
                writer.WritePropertyName("damage");
                writer.WriteValue(entry.Value);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
        }

        public static void WriteKills(JsonWriter writer)
        {
            writer.WriteStartArray();
            foreach (var kill in Kills)
            {
                writer.WriteStartObject();
                writer.WritePropertyName("time");
                writer.WriteValue(kill.Time.ToString("o"));
                writer.WritePropertyName("attacker");
                writer.WriteValue(kill.Attacker);
                writer.WritePropertyName("defender");
                writer.WriteValue(kill.Defender);
                writer.WritePropertyName("assisted");
                writer.WriteValue(kill.Assisted);
                writer.WritePropertyName("weapon");
                writer.WriteValue(kill.Weapon.ToString());
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
        }

        public static void WriteGame(JsonWriter writer)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("Game");

            writer.WriteStartObject();
            writer.WritePropertyName("Start");
            writer.WriteValue(StartTime.ToString("o"));
            writer.WritePropertyName("End");
            writer.WriteValue(EndTime.ToString("o"));
            writer.WritePropertyName("MatchData");

            writer.WriteStartObject();
            writer.WritePropertyName("damage");
            WriteDamageTable(writer);
            writer.WritePropertyName("kills");
            WriteKills(writer);
            writer.WriteEndObject();

            writer.WriteEndObject();

            writer.WriteEndObject();
        }

        public static void PrintResultsAsJson()
        {
            if (!DamageTable.Any())
                return;

            string FullTimeJsonPath = FullTime();
            EndTime = DateTime.UtcNow;
            string JsonPath = Path.Combine(Application.persistentDataPath, "SSL_" + FullTimeJsonPath + ".json");

            using (StreamWriter streamWriter = new StreamWriter(JsonPath))
            using (JsonWriter writer = new JsonTextWriter(streamWriter))
            {
                writer.Formatting = Formatting.Indented;
                WriteGame(writer);
            }
        }

        public static void SetAttacker(string name)
        {
            Attacker = name;
        }

        public static void SetDefender(string name)
        {
            Defender = name;
        }

        public static void SetAssisted(string name)
        {
            Assisted = name;
        }

        public static void AddKill(DamageInfo di)
        {
            Kills.Add(new Kill {
                Time = DateTime.UtcNow,
                Attacker = Attacker, Defender = Defender, Assisted = Assisted,
                Weapon = di.weapon });

            Attacker = null;
            Defender = null;
            Assisted = null;
        }

        // used to create a unique file name
        public static string FullTime()
        {
            return DateTime.UtcNow.ToString("yyyy-MM-dd_HH-mm-ss");
        }

        public static void AddDamage(Player defender, Player attacker, ProjPrefab weapon, float damage)
        {
            var key = new PlayerPlayerWeaponDamage { Attacker = attacker, Defender = defender, Weapon = weapon };
            if (DamageTable.TryGetValue(key, out float totalDamage))
                DamageTable[key] = totalDamage + damage;
            else
                DamageTable[key] = damage;
        }
    }

    [HarmonyPatch(typeof(NetworkMatch), "StartPlaying")]
    internal class LogOnConnect
    {

        private static void Prefix()
        {
            if (!NetworkManager.IsHeadless())
                return;
            ServerStatLog.StartTime = DateTime.UtcNow;
            ServerStatLog.CleanUp();
        }
    }

    [HarmonyPatch(typeof(NetworkMatch), "ExitMatch")]
    internal class LogOnExit
    {
        private static void Prefix()
        {
            if (!NetworkManager.IsHeadless())
                return;
            ServerStatLog.PrintResultsAsJson();
        }
    }

    [HarmonyPatch(typeof(PlayerShip), "ApplyDamage")]
    internal class LogOnDamage
    {
        public static void Prefix(DamageInfo di, PlayerShip __instance)
        {
            if (!NetworkManager.IsHeadless() || di.damage == 0f || di.owner == null ||
                __instance.m_death_stats_recorded || __instance.m_cannot_die || __instance.c_player.m_invulnerable)
                return;
            var otherPlayer = di.owner.GetComponent<Player>();
            if (otherPlayer == null)
                return;

            string mp_name = __instance.c_player.m_mp_name;
            string mp_name2 = otherPlayer.m_mp_name;
            float hitpoints = __instance.c_player.m_hitpoints;
            ProjPrefab weapon = di.weapon;

            //bool killed = false;
            float damage = di.damage;
            if (hitpoints - di.damage <= 0f) {
                damage = hitpoints;
                //killed = true;
            }
            ServerStatLog.AddDamage(__instance.c_player, otherPlayer, weapon, damage);
            //ServerStatLog.damageTable.
            //ServerStatLog.damageTable[new PlayerPlayerWeaponDamage { From = __instance.c_player, To = otherPlayer, Weapon = weapon }] += damage;
            //ServerStatLog.AddLine("Event: " + ServerStatLog.ShortTime() + ":" + mp_name2 + ":" +mp_name + ":" + di.weapon + ":" + damage + ":" + killed);
        }
    }

    [HarmonyPatch(typeof(Overload.Player), "OnKilledByPlayer")]
    internal class LogOnKill
    {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            bool lastTryGetValue = false;
            object lastLocVar = null;
            int setCount = 0;
            string[] setMethods = new [] { "SetDefender", "SetAttacker", "SetAssisted" };

            foreach (var code in instructions)
            {
                if (code.opcode == OpCodes.Ret && setCount > 0)
                {
                    yield return new CodeInstruction(OpCodes.Ldarg_1); // damageInfo
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(ServerStatLog), "AddKill"));
                }
                yield return code;
                if (code.opcode == OpCodes.Brfalse && lastTryGetValue && setCount < setMethods.Length)
                {
                    yield return new CodeInstruction(OpCodes.Ldloc_S, lastLocVar);
                    yield return new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(PlayerLobbyData), "m_name"));
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(ServerStatLog),
                        setMethods[setCount]));
                    setCount++;
                }
                if (code.opcode == OpCodes.Ldloca || code.opcode == OpCodes.Ldloca_S)
                {
                    lastLocVar = code.operand;
                }
                lastTryGetValue = code.opcode == OpCodes.Callvirt && ((MemberInfo)code.operand).Name == "TryGetValue";
            }
        }
    }
}
