using System;
using System.Collections.Generic;
using Overload;
using UnityEngine;
using UnityEngine.Networking;

namespace GameMod.Replay
{
    public struct ShipState
    {
        public bool present;
        public bool alive;
        public Vector3 pos;
        public Quaternion rot;
        public byte weapon;
        public byte missile;
        public byte health;
        public Vector3 vel;
        public Vector3 vrot;
        public byte energy;
        public ushort ammo;
        public uint weaponLevels;
        public uint missileLevels;
        public ulong missileAmmo;
        public byte statusFlags;

        public const byte FULL_HEALTH_BYTE = 255;
        private const int MISSILE_COUNT_BITS = 8;

        private const int WEAPON_LEVEL_BITS = 3;
        private const uint WEAPON_LEVEL_MASK = 7;

        public const byte CLOAKED_BIT = 1;
        public const byte OVERDRIVE_BIT = 2;
        public const byte INVULNERABLE_BIT = 4;
        private const int AMMO_CAPACITY_LEVEL_SHIFT = 3;
        private const byte AMMO_CAPACITY_LEVEL_MASK = 3;

        public int MissileCount(int missileType)
        {
            return (int)((missileAmmo >> (missileType * MISSILE_COUNT_BITS)) & 0xFF);
        }

        public void SetMissileCount(int missileType, int count)
        {
            int shift = missileType * MISSILE_COUNT_BITS;
            ulong clamped = (ulong)Mathf.Clamp(count, 0, 255);
            missileAmmo = (missileAmmo & ~(0xFFUL << shift)) | (clamped << shift);
        }

        public float Hitpoints(float maxHitpoints)
        {
            return health / (float)FULL_HEALTH_BYTE * maxHitpoints;
        }

        public float HealthFraction { get { return health / (float)FULL_HEALTH_BYTE; } }

        public WeaponUnlock WeaponLevel(int weaponType)
        {
            return (WeaponUnlock)((weaponLevels >> (weaponType * WEAPON_LEVEL_BITS)) & WEAPON_LEVEL_MASK);
        }

        public WeaponUnlock MissileLevel(int missileType)
        {
            return (WeaponUnlock)((missileLevels >> (missileType * WEAPON_LEVEL_BITS)) & WEAPON_LEVEL_MASK);
        }

        public static uint PackWeaponLevel(uint packed, int slot, WeaponUnlock level)
        {
            return packed | ((uint)level & WEAPON_LEVEL_MASK) << (slot * WEAPON_LEVEL_BITS);
        }

        public bool IsCloaked { get { return (statusFlags & CLOAKED_BIT) != 0; } }
        public bool HasOverdrive { get { return (statusFlags & OVERDRIVE_BIT) != 0; } }
        public bool IsInvulnerable { get { return (statusFlags & INVULNERABLE_BIT) != 0; } }

        public int AmmoCapacityLevel
        {
            get { return (statusFlags >> AMMO_CAPACITY_LEVEL_SHIFT) & AMMO_CAPACITY_LEVEL_MASK; }
        }

        public static byte PackStatusFlags(bool cloaked, bool overdrive, bool invulnerable,
            int ammoCapacityLevel)
        {
            return (byte)((cloaked ? CLOAKED_BIT : 0) |
                          (overdrive ? OVERDRIVE_BIT : 0) |
                          (invulnerable ? INVULNERABLE_BIT : 0) |
                          ((ammoCapacityLevel & AMMO_CAPACITY_LEVEL_MASK) << AMMO_CAPACITY_LEVEL_SHIFT));
        }
    }

    public struct ItemSample
    {
        public ushort id;
        public Vector3 pos;
    }

    public struct FlagSnapshot
    {
        public bool known;
        public FlagState state;
        public int carrierIndex;
        public float transitionTime;

        public float returnTimerSeconds;

        public float ReturnSecondsLeftAt(float replayTime)
        {
            if (returnTimerSeconds <= 0f)
                return 0f;
            return Mathf.Max(0f, returnTimerSeconds - (replayTime - transitionTime));
        }
    }

    public struct ObjectiveState
    {
        public bool ballPresent;
        public Vector3 ballPos;
        public Quaternion ballRot;
        public Vector3 ballVel;
    }

    public class Frame
    {
        public float t;
        public ShipState[] ships;
        public ItemSample[] items;
        public ObjectiveState objective;
    }

    public enum MatchEventType : byte
    {
        Fire = 0,
        Damage = 2,
        Kill = 3,
        Death = 4,
        Respawn = 5,
        ItemSpawn = 7,
        ItemPickup = 8,
        ProjDespawn = 10,
        TeamChange = 11,
        Assist = 12,
        SuperAlert = 13,
        DamageImpact = 14,

        TeamScore = 15,
        MonsterballGoal = 16,
        FlagState = 17,
    }

    public class MatchEvent
    {
        public float t;
        public MatchEventType type;
        public int iA;
        public int iB;
        public int iC;
        public float fA;
        public Vector3 pos;
        public Quaternion rot;

        public string nameA = "";
        public string nameB = "";

        public int GoalScoringTeam { get { return iA; } set { iA = value; } }
        public int PickedUpByIndex { get { return iB; } set { iB = value; } }
    }

    public class ChatLine
    {
        public float t;
        public int sender;
        public int team;
        public string senderName = "";
        public string message = "";
    }

    public class RosterEntry
    {
        public string name;
        public int team;
        public Color color;

        public NetworkInstanceId netId;
    }

    public class Recording
    {
        public string sceneName = "";
        public string levelIdHash = "";
        public int matchMode;
        public float maxHitpoints = 120f;
        public float matchTimeLimit;
        public float matchStartOffset;
        public int recorderSlot = -1;
        public long recordedUtcBinary;
        public List<RosterEntry> players = new List<RosterEntry>();
        public List<Frame> frames = new List<Frame>();
        public List<MatchEvent> events = new List<MatchEvent>();
        public List<ChatLine> chat = new List<ChatLine>();

        public float Duration
        {
            get
            {
                float lastFrameTime = frames.Count > 0 ? frames[frames.Count - 1].t : 0f;
                float lastChatTime = chat.Count > 0 ? chat[chat.Count - 1].t : 0f;
                return Mathf.Max(lastFrameTime, lastChatTime);
            }
        }

        public int IndexForName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return -1;
            for (int i = 0; i < players.Count; i++)
                if (players[i].name == name)
                    return i;
            string trimmedName = name.Trim();
            for (int i = 0; i < players.Count; i++)
                if (!string.IsNullOrEmpty(players[i].name) &&
                    string.Equals(players[i].name.Trim(), trimmedName, StringComparison.OrdinalIgnoreCase))
                    return i;
            return -1;
        }

        private struct ScoreEvent { public float t; public MatchEventType type; public int killer, victim; }
        private ScoreEvent[] scoreEvents;
        private struct TeamScoreEvent { public float t; public int team, score; }
        private TeamScoreEvent[] teamScoreEvents;
        private struct FlagStateEvent { public float t; public int flag, state, carrier; public float returnTimer; }
        private FlagStateEvent[] flagStateEvents;

        private static bool IsScoringEvent(MatchEventType type)
        {
            return type == MatchEventType.Kill || type == MatchEventType.Death ||
                   type == MatchEventType.Assist;
        }

        private void BuildScoreEvents()
        {
            int scoringCount = 0;
            for (int i = 0; i < events.Count; i++)
                if (IsScoringEvent(events[i].type))
                    scoringCount++;
            scoreEvents = new ScoreEvent[scoringCount];
            int next = 0;
            for (int i = 0; i < events.Count; i++)
            {
                var ev = events[i];
                if (!IsScoringEvent(ev.type))
                    continue;
                scoreEvents[next++] = new ScoreEvent
                {
                    t = ev.t, type = ev.type, killer = ev.iA, victim = ev.iB
                };
            }
        }

        public void TallyScores(float upToTime, int[] kills, int[] deaths, int[] assists)
        {
            Array.Clear(kills, 0, kills.Length);
            Array.Clear(deaths, 0, deaths.Length);
            Array.Clear(assists, 0, assists.Length);
            if (scoreEvents == null)
                BuildScoreEvents();

            for (int i = 0; i < scoreEvents.Length && scoreEvents[i].t <= upToTime; i++)
            {
                int slot = scoreEvents[i].killer;
                if (slot < 0 || slot >= players.Count)
                    continue;
                bool suicide = slot == scoreEvents[i].victim;
                switch (scoreEvents[i].type)
                {
                    case MatchEventType.Kill:
                        if (suicide) kills[slot]--; else kills[slot]++;
                        break;
                    case MatchEventType.Death:
                        deaths[slot]++;
                        break;
                    case MatchEventType.Assist:
                        assists[slot]++;
                        break;
                }
            }
        }

        private void BuildObjectiveEvents()
        {
            int teamScoreCount = 0, flagStateCount = 0;
            for (int i = 0; i < events.Count; i++)
            {
                if (events[i].type == MatchEventType.TeamScore)
                    teamScoreCount++;
                else if (events[i].type == MatchEventType.FlagState)
                    flagStateCount++;
            }
            teamScoreEvents = new TeamScoreEvent[teamScoreCount];
            flagStateEvents = new FlagStateEvent[flagStateCount];
            int nextTeamScore = 0, nextFlagState = 0;
            for (int i = 0; i < events.Count; i++)
            {
                var ev = events[i];
                if (ev.type == MatchEventType.TeamScore)
                    teamScoreEvents[nextTeamScore++] = new TeamScoreEvent
                    {
                        t = ev.t, team = ev.iA, score = ev.iB
                    };
                else if (ev.type == MatchEventType.FlagState)
                    flagStateEvents[nextFlagState++] = new FlagStateEvent
                    {
                        t = ev.t, flag = ev.iA, state = ev.iB, carrier = ev.iC, returnTimer = ev.fA
                    };
            }
        }

        private void EnsureObjectiveEvents()
        {
            if (teamScoreEvents == null || flagStateEvents == null)
                BuildObjectiveEvents();
        }

        public void TeamScoresAt(float upToTime, int[] scores)
        {
            Array.Clear(scores, 0, scores.Length);
            EnsureObjectiveEvents();
            for (int i = 0; i < teamScoreEvents.Length && teamScoreEvents[i].t <= upToTime; i++)
            {
                int team = teamScoreEvents[i].team;
                if (team >= 0 && team < scores.Length)
                    scores[team] = teamScoreEvents[i].score;
            }
        }

        public bool HasFlagState
        {
            get { EnsureObjectiveEvents(); return flagStateEvents.Length > 0; }
        }

        public FlagSnapshot FlagStateAt(float upToTime, int flag)
        {
            var snapshot = new FlagSnapshot { state = FlagState.HOME, carrierIndex = -1 };
            EnsureObjectiveEvents();
            for (int i = 0; i < flagStateEvents.Length && flagStateEvents[i].t <= upToTime; i++)
            {
                if (flagStateEvents[i].flag != flag)
                    continue;
                snapshot.known = true;
                snapshot.state = (FlagState)flagStateEvents[i].state;
                snapshot.carrierIndex = flagStateEvents[i].carrier;
                snapshot.transitionTime = flagStateEvents[i].t;
                snapshot.returnTimerSeconds = flagStateEvents[i].returnTimer;
            }
            return snapshot;
        }
    }

    public struct OlrInfo
    {
        public string path;
        public string sceneName;
        public float durationSeconds;
        public int matchMode;
        public int playerCount;
        public string[] playerNames;
        public DateTime date;

        public byte formatVersion;
        public bool IsPlayable { get { return formatVersion == OlrFile.VERSION; } }
    }
}
