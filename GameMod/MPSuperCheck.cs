using HarmonyLib;
using Overload;
using System.Linq;

// only start super spawn timer if super spawn actually exists in the level
namespace GameMod
{
    [HarmonyPatch(typeof(NetworkMatch), "SetSpawnSuperTimer")]
    class MPSuperCheck
    {
        static bool Prefix(ref float ___m_spawn_super_timer)
        {
            if (GameManager.m_level_data.m_item_spawn_points.Any(x => x.multiplayer_team_association_mask == 1) && // 1 -> is super
                RobotManager.m_multiplayer_spawnable_supers.Count != 0)
                return true;
            ___m_spawn_super_timer = -1f;
            return false;
        }
    }
}
