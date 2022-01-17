using HarmonyLib;
using Overload;
using UnityEngine;

namespace GameMod
{

    /// <summary>
    /// Issue 200
    /// In multiplayer, non-CM item spawns cause unexpected behavior due to being unsync'd (e.g. Ascent health orbs).  Destroy these items and respawn as single-use sync'd spew.
    /// </summary>
    [HarmonyPatch(typeof(UpdateDynamicManager), "AddItem")]
    public class UpdateDynamicManager_AddItem
    {
        static bool Prefix(Item item)
        {
            if (GameplayManager.IsMultiplayerActive)
            {
                if (item.netId.Value <= 0)
                {
                    UnityEngine.Object.Destroy(item.c_go);
                    if (GameplayManager.IsDedicatedServer())
                    {
                        Item.Spew(item.c_go, item.c_transform.position, default(Vector3), -1, item.m_super);
                    }
                    return false;
                }
            }

            return true;
        }
    }

}
