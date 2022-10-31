﻿namespace GameMod.Metadata {
    internal enum Mods {
        /// <summary>
        /// Author: roncli
        /// Disables updating of Gamelift pings.
        /// </summary>
        DisableGamelift,

        /// <summary>
        /// Authors: luponix, Tobias
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
        /// Authors: luponix, roncli
        /// Sends multiplayer game data to a game tracker.
        /// </summary>
        Tracker,

        /// <summary>
        /// Author: derhass
        /// Removes rendering of the UI collision mesh.
        /// </summary>
        UIMeshCollider,

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
        /// Author: Tobias
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
