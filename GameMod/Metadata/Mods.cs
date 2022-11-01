namespace GameMod.Metadata {
    internal enum Mods {
        /// <summary>
        /// Author: arbruijn (Arne)
        /// Adds custom music support.
        /// </summary>
        CustomMusic,

        /// <summary>
        /// Author: roncli
        /// Disables updating of Gamelift pings.
        /// </summary>
        DisableGamelift,

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
        /// Author: tobiasksu (Tobias)
        /// Balance changes made to the thunderbolt.
        /// </summary>
        ThunderboltBalance,

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
