using HarmonyLib;
using UnityEngine;

// by terminal
namespace GameMod
{
    // Prevent items from escaping through invisible ceilings on non-monsterball maps.
    [HarmonyPatch(typeof(NetworkMatch), "StartPlaying")]
    internal class MPItemCollideWithLayer31
    {
        private static void Postfix()
        {
            var isMonsterball = NetworkMatch.GetMode() == MatchMode.MONSTERBALL;
            Physics.IgnoreLayerCollision(31, (int)Overload.UnityObjectLayers.ITEMS, isMonsterball); 
            Physics.IgnoreLayerCollision(31, (int)Overload.UnityObjectLayers.ITEM_LEVEL, isMonsterball);
        }
    }
}
