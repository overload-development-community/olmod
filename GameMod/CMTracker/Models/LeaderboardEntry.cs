namespace GameMod.CMTracker.Models
{
    public class LeaderboardEntry
    {
        public int FavoriteWeaponId { get; set; }
        public float AliveTime { get; set; }
        public int RobotsDestroyed { get; set; }
        public string PilotName { get; set; }
        public int Score { get; set; }
    }
}