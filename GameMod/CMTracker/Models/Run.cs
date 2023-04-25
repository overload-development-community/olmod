using System.Collections.Generic;

namespace GameMod.CMTracker.Models
{
    public class Run
    {
        public string PlayerId { get; set; }
        public string PilotName { get; set; }
        public bool DisplayOnTracker { get; set; }
        public string LevelName { get; set; }
        public string LevelHash { get; set; }
        public int KillerId { get; set; }
        public int FavoriteWeaponId { get; set; }
        public int DifficultyLevelId { get; set; }
        public int ModeId { get; set; }
        public int RobotsDestroyed { get; set; }
        public float AliveTime { get; set; }
        public int Score { get; set; }
        public float SmashDamage { get; set; }
        public int SmashKills { get; set; }
        public float AutoOpDamage { get; set; }
        public int AutoOpKills { get; set; }
        public float SelfDamage { get; set; }
        public List<Models.StatRobot> StatsRobot { get; set; }
        public List<Models.StatPlayer> StatsPlayer { get; set; }
    }
}