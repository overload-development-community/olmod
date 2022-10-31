﻿namespace GameMod.Metadata {
    internal enum Mods {
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
