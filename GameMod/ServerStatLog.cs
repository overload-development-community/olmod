using System;
using System.IO;
using Harmony;
using Overload;
using UnityEngine;

// by luponix

namespace GameMod
{
    public class ServerStatLog
    {
        public static StreamWriter OpenLog()
        {
            string path = Path.Combine(Application.persistentDataPath, "ServerStatLog.txt");
            return new StreamWriter(path, true);
        }

        public static void AddLine(string line)
        {
            using (var streamWriter = OpenLog())
            {
                streamWriter.WriteLine(line);
            }
        }

        public static string FullTime()
        {
            return DateTime.Now.ToString("yyyy-MM-dd h:mm:ss tt");
        }

        public static string ShortTime()
        {
            return DateTime.Now.ToString("h:mm:ss tt");
        }
    }

    [HarmonyPatch(typeof(NetworkMatch), "StartPlaying")]
    internal class LogOnConnect
    {

        private static void Prefix()
        {
            if (!Server.IsActive())
                return;
            ServerStatLog.AddLine("START: " + ServerStatLog.FullTime());
        }
    }

    [HarmonyPatch(typeof(NetworkMatch), "ExitMatch")]
    internal class LogOnExit
    {
        private static void Prefix()
        {
            if (!Server.IsActive())
                return;
            using (StreamWriter streamWriter = ServerStatLog.OpenLog())
            {
                streamWriter.WriteLine("END: " + ServerStatLog.FullTime());
                streamWriter.WriteLine();
            }
        }
    }

    [HarmonyPatch(typeof(PlayerShip), "ApplyDamage")]
    internal class LogOnDamage
    {
        public static void Prefix(DamageInfo di, PlayerShip __instance)
        {
            if (!Server.IsActive() || di.damage == 0f ||
                __instance.m_death_stats_recorded || __instance.m_cannot_die || __instance.c_player.m_invulnerable)
                return;

            string mp_name = __instance.c_player.m_mp_name;
            string mp_name2 = di.owner.GetComponent<Player>().m_mp_name;
            float hitpoints = __instance.c_player.m_hitpoints;

            bool killed = false;
            float damage = di.damage;
            if (hitpoints - di.damage <= 0f) {
                damage = hitpoints;
                killed = true;
            }

            ServerStatLog.AddLine("Event: " + ServerStatLog.ShortTime() + ":" + mp_name2 + ":" +mp_name + ":" + di.weapon + ":" + damage + ":" + killed);
        }
    }

    [HarmonyPatch(typeof(Server), "SendMatchEndToAllClients")]
    internal class LogPostgameStats
    {
        private static void Prefix()
        {
            if (!Server.IsActive())
                return;
            using (StreamWriter streamWriter = ServerStatLog.OpenLog())
            {
                for (int i = 1; i < NetworkManager.m_Players.Count; i++)
                {
                    Player player = NetworkManager.m_Players[i];
                    streamWriter.WriteLine(player.m_mp_name + ":" + player.m_kills + ":" + player.m_assists + ":" + player.m_deaths);
                }
            }
        }
    }

}
