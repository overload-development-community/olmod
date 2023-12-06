using HarmonyLib;
using Overload;
using UnityEngine;

namespace GameMod
{
    // The Kodachi.
    public class Kodachi : Ship
    {
        public Kodachi()
        {
            displayName = "Kodachi Gunship";
            name = "entity_special_player_ship";
            description = new string[3]
            {
                "A short-range enforcement gunship. Surprisingly well-armed and shielded.",
                "Its low mass and small size trade overall speed for maneuvering performance.",
                "Because of this, the Kodachi is able to out-turn any of its competitors."
            };

            meshName = null; // don't replace anything, we want the original
            colliderNames = new string[3] { "PlayershipCollider-100", "PlayershipCollider-105", "PlayershipCollider-110" };

            customizations = true;

            shipScale = 1f;

            FIRING_POINTS = new Vector3[9]
            {
                new Vector3(1.675f, -1.13f, 2.38f),      // Impulse
                new Vector3(0f, -2.08f, 2.08f),          // Cyclone
                new Vector3(1.675f, -1.06f, 2.27f),      // Reflex
                new Vector3(1.675f, -0.79f, 2.1f),       // Crusher
                new Vector3(0f, -1.88f, 2.48f),          // Driller
                new Vector3(1.675f, -0.79f, 2.18f),      // Flak
                //new Vector3(1.675f, -0.99f, 2.35f),    // Thunderbolt -- original spacing
                new Vector3(1.175f, -0.99f, 2.35f),      // Thunderbolt -- tighter spacing (0.2f world space adjustment, scaled at 0.4 for the ship local coordinates)
                new Vector3(1.675f, -0.93f, 2.6f),       // Lancer
                new Vector3(2.3f, -1.505f, 1.63f)        // Quad Impulse firepoint
            };

            ShieldMultiplier = 1f;

            boostMulti = 1.65f;
            boostMod = 1.85f;
        }

        // nothing needs to actually get created here, obviously, we handle creating the quad firepoints in base.ApplyParameters()
        public override void ApplyParameters(GameObject go)
        {
            ps = go.GetComponent<PlayerShip>();
            base.ApplyParameters(go);
        }
    }


    // ====================================================================
    //
    // ====================================================================
    // ====================================================================
    //
    // ====================================================================


    // The Kodachi, smaller.
    public class Kodachi85 : Ship
    {
        public Kodachi85()
        {
            displayName = "Kodachi Gunship (85%)";
            name = "entity_special_player_ship";
            description = new string[3]
            {
                "A short-range enforcement ship. Surprisingly well-armed and shielded.",
                "Its low mass and small size trade overall speed for maneuvering performance.",
                "Because of this, the Kodachi is able to out-turn any of its competitors."
            };

            meshName = null; // don't replace anything, we want the original
            colliderNames = new string[3] { "PlayershipCollider-100", "PlayershipCollider-105", "PlayershipCollider-110" };

            customizations = true;

            shipScale = 0.85f;

            FIRING_POINTS = new Vector3[9]
            {
                new Vector3(1.675f, -1.13f, 2.38f),      // Impulse
                new Vector3(0f, -2.08f, 2.08f),          // Cyclone
                new Vector3(1.675f, -1.06f, 2.27f),      // Reflex
                new Vector3(1.675f, -0.79f, 2.1f),       // Crusher
                new Vector3(0f, -1.88f, 2.48f),          // Driller
                new Vector3(1.675f, -0.79f, 2.18f),      // Flak
                //new Vector3(1.675f, -0.99f, 2.35f),    // Thunderbolt -- original spacing
                new Vector3(1.175f, -0.99f, 2.35f),      // Thunderbolt -- tighter spacing (0.2f world space adjustment, scaled at 0.4 for the ship local coordinates)
                new Vector3(1.675f, -0.93f, 2.6f),       // Lancer
                new Vector3(2.3f, -1.505f, 1.63f)        // Quad Impulse firepoint
            };

            ShieldMultiplier = 1f;

            //boostMulti = 1.7f;
            //boostMod = 1.9f;
            boostMulti = 1.8f;
            boostMod = 2f;

            boostBurst = 0.5f;
        }

        // nothing needs to actually get created here, obviously, we handle creating the quad firepoints in base.ApplyParameters()
        public override void ApplyParameters(GameObject go)
        {
            ps = go.GetComponent<PlayerShip>();
            base.ApplyParameters(go);
        }
    }
}