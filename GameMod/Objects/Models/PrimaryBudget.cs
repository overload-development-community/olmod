using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GameMod.Metadata;
using Overload;

namespace GameMod.Objects.Models {
    /// <summary>
    /// Represents a budget for a single weapon type.
    /// </summary>
    [Mod(Mods.PrimarySpawns)]
    public class PrimaryBudget {
        /// <summary>
        /// The weapon type.
        /// </summary>
        public WeaponType Type { get; set; }

        /// <summary>
        /// The amount of max budget.  Defined by the map maker.
        /// </summary>
        public float Budget { get; set; }

        /// <summary>
        /// The remaining budget.
        /// </summary>
        public float Remaining { get; set; }

        /// <summary>
        /// The number of active weapons of this type currently in play.
        /// </summary>
        public int Active { get; set; }
    }
}
