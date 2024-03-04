namespace GameMod.CMTracker.Models
{
    public class StatPlayer
    {
        public int WeaponTypeId { get; set; }
        public bool IsPrimary { get; set; }
        public float DamageDealt { get; set; }
        public int NumKilled { get; set; }
    }
}