namespace GameMod.Metadata {
    internal enum Mods {
        /// <summary>
        /// Author: tobiasksu (Tobias)
        /// Allows map makers to specify how basic powerups spawn in multiplayer games.
        /// </summary>
        BasicPowerupSpawns,

        /// <summary>
        /// Author: arbruijn (Arne)
        /// Adds custom music support.
        /// </summary>
        CustomMusic,

        /// <summary>
        /// Author: tobiasksu (Tobias)
        /// Allows for custom team colors in two team games.
        /// </summary>
        CustomTeamColors,

        /// <summary>
        /// Author: roncli
        /// Disables updating of Gamelift pings.
        /// </summary>
        DisableGamelift,

        /// <summary>
        /// Author: arbruijn (Arne)
        /// Disables the super spawn in maps that don't have a super spawn.
        /// </summary>
        DisableSuperSpawn,

        /// <summary>
        /// Author: arbruijn (Arne)
        /// Adds a red arrow under enemy players in team games.
        /// </summary>
        EnemyPlayerArrows,

        /// <summary>
        /// Author: roncli
        /// Extends the launch countdown to 60 seconds for players with poor-performing systems.
        /// </summary>
        LaunchCountdown,

        /// <summary>
        /// Author: tobiasksu (Tobias)
        /// Allows creation of games that disable modifiers in multiplayer.
        /// </summary>
        Modifiers,

        /// <summary>
        /// Author: roncli
        /// Provides common functionality for custom messages and message handlers for olmod.
        /// </summary>
        MessageHandlers,

        /// <summary>
        /// Authors: arbruijn (Arne), tobiasksu (Tobias)
        /// Allows for custom projdata and robotdata.
        /// </summary>
        PresetData,

        /// <summary>
        /// Authors: luponix, tobiasksu (Tobias)
        /// Fixes the previous weapon keybind.
        /// </summary>
        PreviousWeaponFix,

        /// <summary>
        /// Author: arbruijn (Arne)
        /// Adds a picture-in-picture rear view.
        /// </summary>
        RearView,

        /// <summary>
        /// Author: roncli
        /// Reduces the quantity of missiles spewed from destroyed ships, and removes the lancer from a ship's spew.
        /// </summary>
        ReduceSpewedMissiles,

        /// <summary>
        /// Author: tobiasksu (Tobias)
        /// Scales respawn time based on the number of players in the game.
        /// </summary>
        ScaleRespawnTime,

        /// <summary>
        /// Authors: derhass, roncli
        /// Disables unnecessary game elements when running as a server.
        /// </summary>
        ServerCleanup,

        /// <summary>
        /// Author: arbruijn (Arne)
        /// Allows pinging of Internet servers.
        /// </summary>
        ServerPing,

        /// <summary>
        /// Author: arbruijn (Arne)
        /// Allows specifying a server port.
        /// </summary>
        ServerPort,

        /// <summary>
        /// Author: roncli
        /// Allows for sudden death overtime for CTF and monsterball.
        /// </summary>
        SuddenDeath,

        /// <summary>
        /// Author: arbruijn (Arne)
        /// Allows for more than 2 teams in a team anarchy game.
        /// </summary>
        Teams,

        /// <summary>
        /// Author: tobiasksu (Tobias)
        /// Balance changes made to the thunderbolt.
        /// </summary>
        ThunderboltBalance,

        /// <summary>
        /// Author: tobiasksu (Tobias)
        /// Adds an match creation option to allow thunderbolt passthrough.
        /// </summary>
        ThunderboltPassthrough,

        /// <summary>
        /// Authors: luponix, roncli
        /// Sends multiplayer game data to a game tracker.
        /// </summary>
        Tracker,

        /// <summary>
        /// Author: arbruijn (Arne)
        /// Prevents teleporters from disappearing in multiplayer.
        /// </summary>
        Triggers,

        /// <summary>
        /// Author: arbruijn (Arne)
        /// Provides a mechanism to communicate what tweaks are available for a multiplayer game.
        /// </summary>
        Tweaks,

        /// <summary>
        /// Author: derhass
        /// Removes rendering of the UI collision mesh.
        /// </summary>
        UIMeshCollider,

        /// <summary>
        /// Author: terminal
        /// Unlocks all modifiers, regardless of player XP.
        /// </summary>
        UnlockModifiers,

        /// <summary>
        /// Author: roncli
        /// Fixes annoying flashing in VR.
        /// </summary>
        VRFlashingFix,

        /// <summary>
        /// Author: roncli
        /// Adds VRScale option.  Based on https://github.com/Raicuparta/unity-scale-adjuster.
        /// </summary>
        VRScale,

        /// <summary>
        /// Author: tobiasksu (Tobias)
        /// Fixes VSync implementation.
        /// </summary>
        VSync,

        /// <summary>
        /// Author: klmcdorm (kevin)
        /// Fixes object orientation coming out of a warper.
        /// </summary>
        WarperOrientation
    }
}
